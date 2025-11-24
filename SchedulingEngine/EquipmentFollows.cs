/*
USAGE / OVERVIEW
----------------

What this does
- Lets one piece of Equipment "follow" another by placing a special line in its SourcePath script.
- A follow line looks like:    !follow SomeEquipmentFullName
- At runtime, all these follow relationships are resolved into a directed acyclic graph (DAG).
- Each Equipment.SourcePath is then rebuilt so that followed equipment scripts are inlined in order.

How to use in code
1. Ensure each Equipment has:
   - bool Active
   - string FullName
   - string SourcePath      // this is the script text, not a file path

2. In your engine setup code, AFTER equipment is loaded but BEFORE you run scheduling:

   var engine = new SchedulingEngine(caseObj);
   // any other initialisation here...
   EqFollow.SetupEquipmentPaths(engine);
   // from here on, engine.Case.Equipment.AllEquipment[x].SourcePath is fully expanded

3. Write SourcePath scripts using follow directives:
   - Valid follow tokens at the start of a line:
       "!follow", "!f", "'!follow", "'!fol", "'!follows"
   - Everything after the token is treated as an equipment name and matched to Equipment.FullName
   - Example:
       !follow Loader_01

4. Region wrapping for inlined sections:
   - When A follows B, this code injects B’s expanded SourcePath wrapped with:
       #region B.FullName
       ... (B's expanded script here)
       #endregion
   - Inside the original scripts, it also expects these regions to be well-formed
     when it scans followed sections.

5. Failure / edge cases:
   - If a follow references an unknown equipment name:
       - The line is kept as plain text; no inlining is performed.
   - If a circular follow is detected anywhere:
       - SetupEquipmentPaths throws ArgumentException("Circular reference detected in equipment follows").

Summary
- Call EqFollow.SetupEquipmentPaths(engine) once.
- Use !follow <EquipmentFullName> in SourcePath to reuse other equipment scripts.
- The code guarantees a follow order (DAG), inlines scripts, and rejects circular graphs.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Scheduling;



//Example Setup
public partial class ItFollows
{    
	public static void SetupEngine(SchedulingEngine engine)
    {
			Console.WriteLine("SETUPENGINE");
			EqFollow.SetupEquipmentPaths(engine);
    }
}

/// <summary>
/// Helper that reads a string line-by-line, caches lines, and allows
/// moving the "current line index" backwards or peeking ahead.
/// Intended for parsing Equipment.SourcePath text.
/// </summary>
public class CachedLineReader : IDisposable
{

    #region Declarations

    private readonly StringReader m_reader;      // Underlying reader for the string content
    private readonly List<string> m_cache;       // Cache of all lines read so far
    private int m_currentIndex;                  // Logical index of current line in the cache
    private bool m_endOfFileReached;             // True once we hit end of the underlying string

    #endregion

    #region Constructors

    /// <summary>
    /// Construct a line reader around the given string content.
    /// </summary>
    public CachedLineReader(string content)
    {
        m_reader = new StringReader(content);
        m_cache = new List<string>();
        m_currentIndex = -1;          // Start before the first line
        m_endOfFileReached = false;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Zero-based line number (index in the cache) of the current line.
    /// Can be adjusted manually to move the logical position backwards.
    /// </summary>
    public int LineNumber { get { return m_currentIndex; } set { m_currentIndex = value; } }

    #endregion

    #region Methods

    public void Dispose()
    {
        m_reader.Dispose();
    }

    /// <summary>
    /// Reads the next line, advancing the current position.
    /// Returns null at end-of-content.
    /// </summary>
    public string ReadLine()
    {
        return GetLine(true);
    }

    /// <summary>
    /// Peeks at the next line without advancing the current position.
    /// Returns null at end-of-content.
    /// </summary>
    public string PeekNextLine()
    {
        return GetLine(false);
    }

    /// <summary>
    /// Move to a specific line index (zero-based), reading ahead
    /// as needed. Throws if the requested index is beyond the end.
    /// </summary>
    public void SetLineNumber(int lineNumber)
    {
        if (lineNumber < 0)
            throw new ArgumentOutOfRangeException("Line number must be 0 or greater.");

        var targetIndex = lineNumber;

        // If we already cached that far, just move the pointer.
        if (targetIndex < m_cache.Count)
        {
            m_currentIndex = targetIndex;
            return;
        }

        // Otherwise read lines until we either reach that index or hit EOF.
        while (m_cache.Count <= targetIndex)
        {
            var line = ReadLine();
            if (line == null)
                throw new ArgumentOutOfRangeException("Line number exceeds total lines in the content.");
        }
    }

    /// <summary>
    /// Core method that returns the next line either advancing the position
    /// or not (depending on 'advance').
    /// </summary>
    private string GetLine(bool advance)
    {
        var targetIndex = m_currentIndex + 1;

        // If we already have this line in the cache, return from cache.
        if (targetIndex < m_cache.Count)
        {
            if (advance)
                m_currentIndex++;
            return m_cache[targetIndex];
        }

        // If we've previously hit EOF, nothing more to read.
        if (m_endOfFileReached)
            return null;

        // Read a new line from the underlying StringReader.
        var line = m_reader.ReadLine();
        if (line != null)
        {
            m_cache.Add(line);
            if (advance)
                m_currentIndex++;
        }
        else
            m_endOfFileReached = true;

        return line;
    }

    #endregion

}

public class EqFollow
{

    #region Classes

    /// <summary>
    /// Represents a single Equipment and the parsed view of its SourcePath:
    /// - The list of other Equipment it follows.
    /// - The ordered list of "parts" (chunks of text), each optionally tied
    ///   to a referenced Equipment (follow block) or plain text (null).
    /// </summary>
    internal class EquipmentFollow
    {

        #region Constants

        // Tokens that mark a "follow" directive at the start of a line.
        // Lines starting with any of these are treated as "!follow <EquipmentFullName>".
        private static readonly string[] FollowTokens = new[] { "'!follows", "'!follow", "'!follow", "'!fol", "'!f" , "'!justfuckingwork"};

        #endregion

        #region Declarations

        private readonly Equipment m_equipment;                        // Underlying equipment
        private readonly List<Tuple<Equipment, int>> m_followReferences; // (followed equipment, lineNumber)
        private readonly List<Tuple<string, Equipment>> m_parts;       // (text chunk, referenced equipment or null)
        private readonly List<Equipment> m_equipments;                 // All equipment in the case (for lookups)

        #endregion

        #region Constructors

        /// <summary>
        /// Parse the given equipment's SourcePath and extract:
        /// - follow references
        /// - ordered parts that mix plain text and follow blocks.
        /// </summary>
        public EquipmentFollow(Equipment equipment, List<Equipment> sourcePathEquipment)
        {
            m_equipment = equipment;
            m_parts = new List<Tuple<string, Equipment>>();
            m_equipments = sourcePathEquipment;
            m_followReferences = new List<Tuple<Equipment, int>>();
            DetermineFollowReferences(m_equipment.SourcePath);
        }

        #endregion

        #region Properties

        public Equipment Equipment { get { return m_equipment; } }

        /// <summary>
        /// List of equipment followed by this one, with the line number of each follow.
        /// </summary>
        public List<Tuple<Equipment, int>> Follows { get { return m_followReferences; } }

        public string Name { get { return m_equipment.FullName; } }

        /// <summary>
        /// Ordered list of chunks of the original SourcePath:
        /// - item1: text for the chunk
        /// - item2: the referenced Equipment if this chunk is a follow-block, otherwise null.
        /// </summary>
        public List<Tuple<string, Equipment>> Parts { get { return m_parts; } }

        #endregion

        #region Methods

        /// <summary>
        /// Reads forward from the current line to find the matching #endregion for the
        /// region starting with "#region eqfName". Uses a stack to match nested regions.
        /// If not in a proper region, rewinds one line and returns true (end of follow region).
        /// </summary>
        private bool ReachedEndOfFollowRegion(CachedLineReader lineReader, string eqfName)
        {
            var line = lineReader.ReadLine();
            if (line == null)
                return true;

            // Expect the next line to be "#region <equipmentFullName>".
            if (!line.Contains(string.Format("#region {0}", eqfName)))
            {
                // If it isn't, step back one line and treat this as end of follow region.
                lineReader.LineNumber -= 1;
                return true;
            }

            // Track nested #region/#endregion using a stack of line indexes.
            var regionStack = new Stack<int>();
            regionStack.Push(lineReader.LineNumber);

            while ((line = lineReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("#region"))
                    regionStack.Push(lineReader.LineNumber);
                else if (line.StartsWith("#endregion"))
                {
                    if (regionStack.Count <= 0)
                        continue;
                    regionStack.Pop();
                    // When stack is empty, we've closed the outermost region.
                    if (regionStack.Count != 0)
                        continue;
                    return true;
                }
            }

            // EOF without closing region. Treat as end-of-region.
            return false;
        }

        /// <summary>
        /// Parse the SourcePath string for this equipment:
        /// - Identify follow lines based on FollowTokens.
        /// - Build Follows list.
        /// - Build Parts list (interleave plain text and follow chunks).
        /// </summary>
        private void DetermineFollowReferences(string equipmentSourcePath)
        {
            using (var lineReader = new CachedLineReader(equipmentSourcePath))
            {
                for (string line; (line = lineReader.ReadLine()) != null;)
                {
                    line = line.Trim();
					Console.WriteLine(line);
                    // Check if the line starts with any of the possible follow tokens.
                    var matchedToken = FollowTokens.FirstOrDefault(t => line.StartsWith(t.ToLower()));
                    var isFollowLine = matchedToken != null;

                    if (isFollowLine)
                    {
                        // Extract equipment name after the token.
                        var equipmentNamePart = line.Substring(matchedToken.Length).Trim();
						
						
                        var equipment = m_equipments.FirstOrDefault(x => x.FullName.Equals(equipmentNamePart));
                        if (equipment == null)
                        {
                            // Referenced equipment not found: keep the line as plain text and mark no equipment.
                            m_parts.Add(Tuple.Create(line, (Equipment)null));
                            continue;
                        }

                        // Record follow reference with its line number.
                        m_followReferences.Add(Tuple.Create(equipment, lineReader.LineNumber));

                        // Build a follow-block part starting with this line.
                        var currentLineWriter = new StringBuilder();
                        currentLineWriter.AppendLine(line);
                        m_parts.Add(Tuple.Create(currentLineWriter.ToString(), equipment));

                        // Skip following empty/whitespace-only lines in the follow block.
                        do
                        {
                            line = lineReader.ReadLine();
                            if (line == null)
                                break;
                        } while (string.IsNullOrWhiteSpace(line));

                        // If we stopped on a non-empty line, move the pointer back so the outer loop re-processes it.
                        if (line != null)
                            lineReader.LineNumber -= 1;

                        // Now advance through the region of the followed equipment.
                        if (!ReachedEndOfFollowRegion(lineReader, equipment.FullName))
                            throw new Exception("Could not find end of region for " + Name);
                    }
                    else
                    {
                        // Build a text chunk until we hit a follow line or EOF.
                        var appended = false;
                        var currentLineWriter = new StringBuilder();
                        while (!isFollowLine)
                        {
                            currentLineWriter.AppendLine(line);
                            appended = true;
                            line = lineReader.ReadLine();
                            if (line == null)
                                break;

                            // Re-evaluate whether this new line is the start of a follow block.
                            matchedToken = FollowTokens.FirstOrDefault(t => line.StartsWith(t));
                            isFollowLine = matchedToken != null;
                            if (isFollowLine)
                                lineReader.LineNumber -= 1; // Put the follow line back for the outer loop.
                        }
                        var part = currentLineWriter.ToString();

                        // Strip the trailing newline if we appended at least one line.
                        m_parts.Add(Tuple.Create(appended ? part.Substring(0, part.Length - 1) : part, (Equipment)null));
                    }
                }
            }
        }

        #endregion

    }

    /// <summary>
    /// Directed acyclic graph of EquipmentFollow nodes, representing
    /// "equipment follow dependencies". Provides topological ordering
    /// and cycle detection.
    /// </summary>
    internal class EquipmentFollowDAG
    {

        #region Declarations

        // Maps a node to the set of its predecessors.
        private readonly Dictionary<EquipmentFollow, HashSet<EquipmentFollow>> m_predeccesorEdges;
        // Maps a node to the set of its successors.
        private readonly Dictionary<EquipmentFollow, HashSet<EquipmentFollow>> m_successorEdges;

        #endregion

        #region Constructors

        /// <summary>
        /// Build the DAG from a map of Equipment -> EquipmentFollow.
        /// For each follow reference, add edges from "followed equipment" to "follower".
        /// </summary>
        public EquipmentFollowDAG(Dictionary<Equipment, EquipmentFollow> equipmentFollows)
        {
            m_predeccesorEdges = new Dictionary<EquipmentFollow, HashSet<EquipmentFollow>>();
            m_successorEdges = new Dictionary<EquipmentFollow, HashSet<EquipmentFollow>>();

            foreach (var eq in equipmentFollows)
            {
                if (eq.Value.Follows.Any())
                {
                    foreach (var follow in eq.Value.Follows)
                    {
                        EquipmentFollow childFollow;
                        if (equipmentFollows.TryGetValue(follow.Item1, out childFollow))
                            Add(eq.Value, childFollow);
                    }
                }
                else
                    // Equipment with no follows still gets added as an isolated node.
                    Add(eq.Value, null);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Add an edge pred <- successor in the predecessor graph
        /// and successor -> pred in the successor graph.
        /// </summary>
        public void Add(EquipmentFollow pred, EquipmentFollow successor)
        {
            if (pred != null)
                AddToDictionary(m_predeccesorEdges, pred, successor);
            if (successor != null)
                AddToDictionary(m_successorEdges, successor, pred);
        }

        /// <summary>
        /// Returns the equipment in a "least reference" order:
        /// - Start with nodes that have no predecessors.
        /// - Breadth-first walk successors.
        /// - Remove duplicates keeping the last occurrence.
        /// This is used so that when building inlined source paths,
        /// dependencies are guaranteed to have been processed already.
        /// </summary>
        public Dictionary<Equipment, EquipmentFollow> GetLeastReferenceOrder()
        {
            // Start with nodes that have no predecessors.
            var orderedList = new List<EquipmentFollow>(m_predeccesorEdges.Where(x => x.Value.Count == 0).Select(x => x.Key));

            var list = GetOrderedListRecursive(orderedList, new Queue<EquipmentFollow>(orderedList));

            // Remove duplicates, keeping the last occurrence, then map to Equipment keys.
            return RemoveDuplicatesKeepLast(GetOrderedListRecursive(orderedList, new Queue<EquipmentFollow>(orderedList)))
                .ToDictionary(x => x.Equipment, x => x);
        }

        /// <summary>
        /// Detect whether there's any cycle in the successor graph using DFS.
        /// </summary>
        internal bool HasCircularReference()
        {
            var visited = new HashSet<EquipmentFollow>();
            var recursionStack = new HashSet<EquipmentFollow>();

            return m_successorEdges.Keys.Any(node => DetectCycle(node, visited, recursionStack));
        }

        /// <summary>
        /// Depth-first search for cycles: if we re-visit a node on the current recursion stack,
        /// then there's a cycle.
        /// </summary>
        private bool DetectCycle(EquipmentFollow node, HashSet<EquipmentFollow> visited, HashSet<EquipmentFollow> recursionStack)
        {
            if (recursionStack.Contains(node))
                return true; // Cycle detected

            if (visited.Contains(node))
                return false; // Already processed

            visited.Add(node);
            recursionStack.Add(node);

            HashSet<EquipmentFollow> successors;
            if (m_successorEdges.TryGetValue(node, out successors))
            {
                if (successors.Any(successor => DetectCycle(successor, visited, recursionStack)))
                    return true;
            }

            recursionStack.Remove(node);
            return false;
        }

        /// <summary>
        /// Helper to add a (key -> value) edge to the given adjacency dictionary.
        /// If value is null, create an empty set.
        /// </summary>
        private void AddToDictionary(Dictionary<EquipmentFollow, HashSet<EquipmentFollow>> dictionary, EquipmentFollow key, EquipmentFollow value)
        {
            HashSet<EquipmentFollow> set;
            if (dictionary.TryGetValue(key, out set))
                set.Add(value);
            else if (value != null)
                dictionary.Add(key, new HashSet<EquipmentFollow> { value });
            else
                dictionary.Add(key, new HashSet<EquipmentFollow>());
        }

        /// <summary>
        /// From a list with potential duplicates, keep only the last occurrence of each element,
        /// preserving relative order of those last occurrences.
        /// </summary>
        private List<T> RemoveDuplicatesKeepLast<T>(List<T> list)
        {
            var lastIndexMap = new Dictionary<T, int>();

            // Step 1: Store the last occurrence index of each element
            for (var i = 0; i < list.Count; i++)
                lastIndexMap[list[i]] = i;

            // Step 2: Build the result list efficiently
            var result = new List<T>(lastIndexMap.Count);
            for (var i = 0; i < list.Count; i++)
            {
                if (lastIndexMap[list[i]] == i)
                    result.Add(list[i]);
            }

            return result;
        }

        /// <summary>
        /// BFS-style layering across successors to compute a "distance" order
        /// from the initial nodes, appending each wave to the list.
        /// </summary>
        private List<EquipmentFollow> GetOrderedListRecursive(List<EquipmentFollow> currentList, Queue<EquipmentFollow> processingQueue)
        {
            var newList = new List<EquipmentFollow>();
            var nextProcessingQueue = new Queue<EquipmentFollow>();
            while (processingQueue.Count > 0)
            {
                var item = processingQueue.Dequeue();

                HashSet<EquipmentFollow> successors;
                if (m_successorEdges.TryGetValue(item, out successors))
                {
                    foreach (var successor in successors)
                    {
                        if (!currentList.Contains(successor))
                        {
                            newList.Add(successor);
                            nextProcessingQueue.Enqueue(successor);
                        }
                    }
                }
            }

            if (newList.Count == 0)
                return currentList;

            currentList.AddRange(newList);
            return GetOrderedListRecursive(currentList, nextProcessingQueue);
        }

        #endregion

    }

    #endregion

    #region Methods

    /// <summary>
    /// Main entry point:
    /// - Build EquipmentFollow objects for each active piece of equipment.
    /// - Build a DAG of follow relationships.
    /// - Detect circular references (throw if found).
    /// - Build fully expanded SourcePath strings with follows inlined.
    /// 
    /// Call this once after equipment is loaded and before scheduling logic
    /// that expects all SourcePaths to be fully expanded.
    /// </summary>
    internal static void SetupEquipmentPaths(SchedulingEngine engine)
    {
        // Work only with active equipment.
        var regularEquipment = engine.Case.Equipment.AllEquipment.Where(x => x.Active).ToList();
        var allEquipment = engine.Case.Equipment.AllEquipment.Where(x => x.Active).ToList();

        // Build EquipmentFollow wrappers for each equipment.
        var dag = new EquipmentFollowDAG(allEquipment.ToDictionary(x => x, x => new EquipmentFollow(x, allEquipment)));

        // Fail fast if there is any circular follow chain.
        if (dag.HasCircularReference())
            throw new ArgumentException("Circular reference detected in equipment follows");

        // Topological-ish order of equipment follows.
        var orderSourcePathEquipmentList = dag.GetLeastReferenceOrder();

        // Build a map: Equipment -> fully-expanded source path, resolving follows.
        var equipmentFollowSourcePaths = BuildEquipmentFollowSourcePaths(orderSourcePathEquipmentList);

        // Apply the computed, expanded source paths back to each regular equipment.
        foreach (var eq in regularEquipment)
            SetEquipmentSourcePathsFromEquipmentFollows(eq, orderSourcePathEquipmentList, equipmentFollowSourcePaths);
    }

    /// <summary>
    /// For a single EquipmentFollow, build the final SourcePath by:
    /// - concatenating plain text parts, and
    /// - when a part refers to another equipment, injecting that equipment's
    ///   expanded SourcePath inside a #region/#endregion block.
    /// </summary>
    private static string BuildSourcePathFromEquipmentFollowParts(Dictionary<Equipment, string> equipmentFollowSourcePaths, EquipmentFollow equipmentFollow)
    {
		
			
        var sb = new StringBuilder();
        foreach (var part in equipmentFollow.Parts)
        {
            var referenceEquipment = part.Item2;
            sb.Append(part.Item1);
			
			Console.WriteLine("REFERENCE CHECK");

            if (referenceEquipment == null)
                continue;
			
			Console.WriteLine("IT'S NOT NULL");
			
            string equipmentSourcePath;
            if (equipmentFollowSourcePaths.TryGetValue(referenceEquipment, out equipmentSourcePath))
            {
                sb.AppendLine(string.Format("#region {0}", referenceEquipment.FullName));
                sb.AppendLine(equipmentSourcePath);
                sb.AppendLine("#endregion");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// For a given Equipment, find its EquipmentFollow wrapper and overwrite
    /// eq.SourcePath with the fully composed source path.
    /// </summary>
    private static void SetEquipmentSourcePathsFromEquipmentFollows(Equipment eq, Dictionary<Equipment, EquipmentFollow> orderSourcePathEquipmentList, Dictionary<Equipment, string> equipmentFollowSourcePaths)
    {
        EquipmentFollow equipmentFollow;
        if (!orderSourcePathEquipmentList.TryGetValue(eq, out equipmentFollow))
            return;

        eq.SourcePath = BuildSourcePathFromEquipmentFollowParts(equipmentFollowSourcePaths, equipmentFollow);
    }

    /// <summary>
    /// Build the final mapping Equipment -> expanded SourcePath.
    /// For each equipment:
    /// - If it has no follow references, just use its original SourcePath.
    /// - Otherwise, recursively build by expanding its parts with references
    ///   to already-computed equipment follow paths.
    /// </summary>
    private static Dictionary<Equipment, string> BuildEquipmentFollowSourcePaths(Dictionary<Equipment, EquipmentFollow> ordersourcePathEquipmentList)
    {
        var result = new Dictionary<Equipment, string>();
        foreach (var eqPair in ordersourcePathEquipmentList)
            result.Add(
                eqPair.Key,
                eqPair.Value.Parts.All(x => x.Item2 == null)
                    ? eqPair.Key.SourcePath               // No follows: keep original.
                    : BuildSourcePathFromEquipmentFollowParts(result, eqPair.Value) // Expand via follows.
            );
        return result;
    }

    #endregion

}


