#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PrecisionMining.Common.Util;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Scenarios;

#endregion

public partial class FieldOffsetDependencyOptions
{
    #region Declarations

    public Range Range { get; private set; }
    public Range PredecessorRange { get; private set; }
    public List<Process> PredecessorProcesses { get; private set; }
    public bool PredecessorProcessesIncludeAll { get; private set; }
    public Range SuccessorRange { get; private set; }
    public List<Process> SuccessorProcesses { get; private set; }
    public bool SuccessorProcessesIncludeAll { get; private set; }
    public List<Field> Fields { get; private set; }
    public List<string> Offsets { get; private set; }
    public bool GapfillSearch { get; private set; }
    public bool UseRangeForTablePositions { get; private set; }
    
    #endregion

    public FieldOffsetDependencyOptions(Range range, Range predecessorRange, List<Process> predecessorProcesses, bool predecessorProcessesIncludeAll, Range successorRange, List<Process> successorProcesses, bool successorProcessesIncludeAll, List<Field> fields, List<string> offsets, bool gapfillSearch, bool useRangeForTablePositions)
    {
        Range = range;
        PredecessorRange = predecessorRange;
        PredecessorProcesses = predecessorProcesses;
        PredecessorProcessesIncludeAll = predecessorProcessesIncludeAll;
        SuccessorRange = successorRange;
        SuccessorProcesses = successorProcesses;
        SuccessorProcessesIncludeAll = successorProcessesIncludeAll;
        Fields = fields;
        Offsets = offsets;
        GapfillSearch = gapfillSearch;
        UseRangeForTablePositions = useRangeForTablePositions;
    }

    public string GetOffsetString()
    {
        return string.Join("\\", Offsets);
    }

    public static List<string> GetOffsets(string offsetString)
    {
        return offsetString.Split('\\').ToList();
    }
    
    public string ToOptions()
    {
        var predcessorProccesses = PredecessorProcessesIncludeAll ? "< All >" : PredecessorProcesses == null ? "< All >" : string.Join(", ", PredecessorProcesses);
        var successorProccesses = SuccessorProcessesIncludeAll ? "< All >" : SuccessorProcesses == null ? "< All >" : string.Join(", ", SuccessorProcesses);
        
        return string.Join("|",
            FieldOffsetDependency.DependencyName,
            FieldOffsetDependency.Version,
            FieldOffsetDependency.DefaultRegenerationFlag,
            Range == null ? "" : Range.FullName,
            PredecessorRange == null ? "" : PredecessorRange.FullName,
            predcessorProccesses,
            SuccessorRange == null ? "" : SuccessorRange.FullName,
            successorProccesses,
            string.Join(", ", Fields.Select(x => x.FullName)), 
            string.Join("\\", Offsets),
            GapfillSearch,
            UseRangeForTablePositions);
    }
    
}


public partial class FieldOffsetDependency
{

    #region Declarations

    public static int[] AttributeSizes = { 8, 10, 11, 12 };

    #endregion

    #region Methods

    public static void RegenerateFieldOffsetDependency(RangeDependencyRule rangeDependencyRule, Errors errors)
    {
        var entries = rangeDependencyRule.Entries.Where(x => !x.Active).ToList();
        var inputStrings = rangeDependencyRule.Description.Split('|');
        if (!AttributeSizes.Contains(inputStrings.Length))
        {
            errors.Add(new Error(string.Format("Description of Field Offset for {2} was expected to have either {0} inputs not {1}.", string.Join(" or ", AttributeSizes), inputStrings.Length, rangeDependencyRule.FullName)));
            return;
        }

        var table = rangeDependencyRule.Case.SourceTable;
        if (table == null)
        {
            errors.Add(new Error(string.Format("'{0}' Rule has no Table.", rangeDependencyRule.Name)));
            return;
        }

        var index = 0;
        var dependencyTypeName = inputStrings[index++].Trim();
        if (!dependencyTypeName.Equals(DependencyName))
        {
            errors.Add(new Error(string.Format("Dependency Type Name does not match {0}", DependencyName)));
            return;
        }

        var versionString = inputStrings[index].Trim();
        var version = -1;
        Int32.TryParse(versionString, out version);
        FieldOffsetDependencyOptions options = null;
        if (inputStrings.Length == 8)
            options = GetOptionValuesForDependencyFor8Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 10)
            options = GetOptionValuesForDependencyFor10Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 11)
            options = GetOptionValuesForDependencyFor11Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 12)
        {
            if (version == 1)
                options = GetOptionValuesForDependencyForV1(table, rangeDependencyRule, errors);
            else
                errors.Add(new Error(string.Format("Could not regenerate Field Offset Dependency '{0}' from description.", rangeDependencyRule.FullName)));
        }
        else
            errors.Add(new Error(string.Format("Could not regenerate Field Offset Dependency '{0}' from description.", rangeDependencyRule.FullName)));

        if (options == null || errors.Any())
            return;

        var rangeDependency = CreateFieldOffsetDependency(null, rangeDependencyRule.Case, rangeDependencyRule.FullName, options, errors);

        if (!entries.Any())
            return;

        foreach (var entry in entries)
        {
            var predecessorMatch = rangeDependency.Entries.FirstOrDefault(x => entry.PredecessorTextRange == x.PredecessorTextRange);
            if (predecessorMatch == null)
                continue;
            if (entry.SuccessorTextRange != predecessorMatch.SuccessorTextRange)
                continue;
            if (!entry.PredecessorProcesses.ToString().Equals(predecessorMatch.PredecessorProcesses.ToString()))
                continue;
            if (!entry.SuccessorProcesses.ToString().Equals(predecessorMatch.SuccessorProcesses.ToString()))
                continue;
            if (entry.Name != predecessorMatch.Name)
                continue;
            predecessorMatch.Active = false;
        }
    }

    public static RangeDependencyRule CreateFieldOffsetDependency(NodeFieldGroupingRoot root, Case scenario, string dependencyName, FieldOffsetDependencyOptions options, Errors errors, List<Tuple<Node, Node>> links = null)
    {
        var tryParseInts = options.Offsets.Select(s =>
        {
            int n;
            if (int.TryParse(s, out n))
                return n;
            return (int?) null;
        }).ToList();
        var table = scenario.SourceTable;
        var description = GetLatestDescription(options);
        RangeDependencyRule rangeDependency = null;
        if (dependencyName != null)
            rangeDependency = SetupRangeDependency(scenario, dependencyName, options.Range, options.PredecessorRange, options.SuccessorRange, description);
        if (tryParseInts.All(x => x.HasValue))
            return CreateFieldOffsetIntegerDependency(root, rangeDependency, table, options, tryParseInts.Select(x => x.Value).ToList(), errors, links);
        else
            return CreateFieldOffsetStringDependency(root, rangeDependency, table, options, errors, links);
    }

    public static RangeDependencyRule CreateFieldOffsetStringDependency(NodeFieldGroupingRoot root, RangeDependencyRule rangeDependency, Table table, FieldOffsetDependencyOptions options, Errors errors, List<Tuple<Node, Node>> links)
    {
        if (root == null)
        {
            var fieldLevels = new FieldLevels(table, options.UseRangeForTablePositions ? options.Range : null);
            foreach (var field in options.Fields)
                fieldLevels.Add(field);
            root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FIELDOFFSETDEBUG, options.UseRangeForTablePositions ? options.Range : null);
        }
        var leafGroups = root.GetLeafGroups();

        var parsingErrors = new Errors();
        var offsetConnections = OffsetConnection.ParseOffsetsForConnections(options.Offsets, parsingErrors);
        if (parsingErrors.Count > 0 || offsetConnections.Count != options.Offsets.Count)
        {
            MessageBox.Show(string.Join("\n", parsingErrors.Items.Distinct()) + "\n\nThe Dependency will not be generated.", "Creating Offset Dependency with Strings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        foreach (var leafGroup in leafGroups)
        {
            // var offsetGroup = continuousSearchBack ? root.GetGapfillOffset(leafGroup, offset) : root.GetOffset(leafGroup, offset); 
            List<NodeFieldGroupContainer> offsetGroups;
            List<NodeFieldGroupContainer> leafPredecessorGroups;
            root.GetManyOffsetWithOffsetConnections(leafGroup, offsetConnections, out offsetGroups, out leafPredecessorGroups, errors);
            if (errors.Any())
                return null;
            if (offsetGroups == null || !offsetGroups.Any() || offsetGroups.Any(x => x == null))
                continue;
            if (leafPredecessorGroups == null || !leafPredecessorGroups.Any() || leafPredecessorGroups.Any(x => x == null))
                continue;

            var predecessorNodes = options.PredecessorRange != null ? offsetGroups.SelectMany(x => x.GetLeavesAsNodes()).Where(x => options.PredecessorRange.IsInRange(x)).ToList() : offsetGroups.SelectMany(x => x.GetLeavesAsNodes()).ToList();
            var successorNodes = options.SuccessorRange != null ? leafPredecessorGroups.SelectMany(x => x.GetLeavesAsNodes()).Where(x => options.SuccessorRange.IsInRange(x)).ToList() : leafPredecessorGroups.SelectMany(x => x.GetLeavesAsNodes()).ToList();

            TryToCreateEntryForLeafGroup(rangeDependency, options.PredecessorProcesses, options.PredecessorProcessesIncludeAll, options.SuccessorProcesses, options.SuccessorProcessesIncludeAll, links, predecessorNodes, successorNodes);
        }

        return rangeDependency;
    }

    public static RangeDependencyRule CreateFieldOffsetIntegerDependency(NodeFieldGroupingRoot root, RangeDependencyRule rangeDependency, Table table, FieldOffsetDependencyOptions options, List<int> offsets, Errors errors, List<Tuple<Node, Node>> links)
    {
        if (root == null)
        {
            var fieldLevels = new FieldLevels(table, options.UseRangeForTablePositions ? options.Range : null);
            foreach (var field in options.Fields)
                fieldLevels.Add(field);
            root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FIELDOFFSETDEBUG, options.UseRangeForTablePositions ? options.Range : null);
        }
        var leafGroups = root.GetLeafGroups();
        foreach (var leafGroup in leafGroups)
        {
            var offsetGroup = options.GapfillSearch ? root.GetGapfillOffset(leafGroup, offsets, errors) : root.GetOffset(leafGroup, offsets, errors);
            if (errors.Any())
                return null;
            if (offsetGroup == null)
                continue;
            var predecessorNodes = options.PredecessorRange != null ? offsetGroup.GetLeavesAsNodes().Where(x => options.PredecessorRange.IsInRange(x)).ToList() : offsetGroup.GetLeavesAsNodes();
            var successorNodes = options.SuccessorRange != null ? leafGroup.GetLeavesAsNodes().Where(x => options.SuccessorRange.IsInRange(x)).ToList() : leafGroup.GetLeavesAsNodes();
            TryToCreateEntryForLeafGroup(rangeDependency, options.PredecessorProcesses, options.PredecessorProcessesIncludeAll, options.SuccessorProcesses, options.SuccessorProcessesIncludeAll, links, predecessorNodes, successorNodes);
        }
        return rangeDependency;
    }
    
    private static void TryToCreateEntryForLeafGroup(RangeDependencyRule rangeDependency, List<Process> predecessorProcesses, bool includeAllPredecessorProccesses, List<Process> successorProcesses, bool includeAllSuccessorProccesses, List<Tuple<Node, Node>> links, List<Node> predecessorNodes, List<Node> successorNodes)
    {
        if (!predecessorNodes.Any() || !successorNodes.Any())
            return;
        if (rangeDependency != null)
        {
            var entry = new RangeDependencyEntry(string.Format("Entry {0}", rangeDependency.Entries.Count + 1));
            SetupEntryProcesses(entry, predecessorProcesses, includeAllPredecessorProccesses, successorProcesses, includeAllSuccessorProccesses);
            entry.PredecessorTextRange = string.Join("\n", predecessorNodes.Select(x => x.FullName));
            entry.SuccessorTextRange = string.Join("\n", successorNodes.Select(x => x.FullName));
            rangeDependency.Entries.Add(entry);
        }
        if (links != null)
        {
            foreach (var pred in predecessorNodes)
                links.AddRange(successorNodes.Select(succ => new Tuple<Node, Node>(pred, succ)));
        }
    }
    
    private static string GetLatestDescription(FieldOffsetDependencyOptions options)
    {
        return options.ToOptions();
    }
    
    internal static FieldOffsetDependencyOptions GetOptionValuesForDependency(Table table, RangeDependencyRule rangeDependencyRule, Errors errors)
    {
        var inputStrings = rangeDependencyRule.Description.Split('|');
 		var versionString = inputStrings[1].Trim();
	    var version = -1;
		Int32.TryParse(versionString, out version);
		
        if (inputStrings.Length == 8)
            return GetOptionValuesForDependencyFor8Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 10)
            return GetOptionValuesForDependencyFor10Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 11)
            return GetOptionValuesForDependencyFor11Attributes(table, rangeDependencyRule, errors);
        else if (inputStrings.Length == 12)
		{
            if (version == 1)
                return GetOptionValuesForDependencyForV1(table, rangeDependencyRule, errors);
            else
                errors.Add(new Error(string.Format("Could not regenerate Field Offset Dependency '{0}' from description.", rangeDependencyRule.FullName)));
		}
		errors.Add("No Match for Arguments Count, Contains " + inputStrings.Length);

        return null;
    }
    
    private static FieldOffsetDependencyOptions GetOptionValuesForDependencyFor11Attributes(Table table, RangeDependencyRule rangeDependencyRule, Errors errors)
    { 
        var inputStrings = rangeDependencyRule.Description.Split('|');

        var index = 0;
        var dependencyTypeName = inputStrings[index++].Trim();
        if (!dependencyTypeName.Equals(DependencyName))
        {
            errors.Add(new Error(string.Format("Dependency Type Name does not match {0}", DependencyName)));
            return null;
        }

        var versionString = inputStrings[index].Trim();
        var version = -1;
        Int32.TryParse(versionString, out version);

        var gapFill = false;
        var useRangeForTableNodes = false;
        var useRangeForTablePositions = false;
        var @case = rangeDependencyRule.Case;
        var rangeName = inputStrings[index++].Trim();
        var predecessorRangeName = inputStrings[index++].Trim();
        var predecessorProcessesName = inputStrings[index++].Trim();
        var successorRangeName = inputStrings[index++].Trim();
        var successorProcessesName = inputStrings[index++].Trim();
        var groupingFieldsStrings = inputStrings[index++];
        var groupingFields = groupingFieldsStrings.Split(',');
        var continuousSearchBack = inputStrings[index++].Trim();
        var rangeForPositionString = inputStrings[index++].Trim();
        var rangeForTableString = inputStrings[index++].Trim();
        var offsetsStrings = inputStrings[index++];
        var offsets = offsetsStrings.Split('\\').ToList();
        var includeAllPredecessorProcesses = false;
        var includeAllSuccessorProcesses = false;

        var dependencyName = rangeDependencyRule.FullName;
        Range range = null;
        Range predecessorRange = null;
        Range sucessorRange = null;
        List<Process> predecessorProcesses = null;
        List<Process> successorProcesses = null;
		List<Field> fields = null;

        GetRanges(table, rangeName, predecessorRangeName, successorRangeName, ref range, ref predecessorRange, ref sucessorRange, errors);
        GetProcesses(@case, predecessorProcessesName, successorProcessesName, ref predecessorProcesses, ref includeAllPredecessorProcesses, ref successorProcesses, ref includeAllSuccessorProcesses, errors);
        GetOtherParameters(table, groupingFields, groupingFieldsStrings, offsets, offsetsStrings, continuousSearchBack, ref gapFill, ref fields, errors);

        if (!Boolean.TryParse(rangeForTableString, out useRangeForTableNodes))
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTableNodes Boolean ", rangeForTableString)));
        if (!Boolean.TryParse(rangeForPositionString, out useRangeForTablePositions))
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTablePositions Boolean ", rangeForPositionString)));
        
        
        if (errors.Any())
            return null;

        return new FieldOffsetDependencyOptions(range, predecessorRange, predecessorProcesses, includeAllPredecessorProcesses, sucessorRange, successorProcesses, includeAllSuccessorProcesses, fields, offsets, gapFill, useRangeForTablePositions);
    }
    
    private static FieldOffsetDependencyOptions GetOptionValuesForDependencyFor10Attributes(Table table, RangeDependencyRule rangeDependencyRule, Errors errors)
    { 
        var inputStrings = rangeDependencyRule.Description.Split('|');

        var index = 0;
        var dependencyTypeName = inputStrings[index++].Trim();
        if (!dependencyTypeName.Equals(DependencyName))
        {
            errors.Add(new Error(string.Format("Dependency Type Name does not match {0}", DependencyName)));
            return null;
        }

        var versionString = inputStrings[index].Trim();
        var version = -1;
        Int32.TryParse(versionString, out version);

        var gapFill = false;
        var useRangeForTableNodes = false;
        var useRangeForTablePositions = false;
        var @case = rangeDependencyRule.Case;
        var rangeName = inputStrings[index++].Trim();
        var predecessorRangeName = inputStrings[index++].Trim();
        var predecessorProcessesName = inputStrings[index++].Trim();
        var successorRangeName = inputStrings[index++].Trim();
        var successorProcessesName = inputStrings[index++].Trim();
        var groupingFieldsStrings = inputStrings[index++];
        var groupingFields = groupingFieldsStrings.Split(',');
        var continuousSearchBack = inputStrings[index++].Trim();
        var rangeForPositionString = inputStrings[index++].Trim();
        var offsetsStrings = inputStrings[index++];
        var offsets = offsetsStrings.Split('\\').ToList();
        var includeAllPredecessorProcesses = false;
        var includeAllSuccessorProcesses = false;

        var dependencyName = rangeDependencyRule.FullName;
        Range range = null;
        Range predecessorRange = null;
        Range sucessorRange = null;
        List<Process> predecessorProcesses = null;
        List<Process> successorProcesses = null;
		List<Field> fields = null;

        GetRanges(table, rangeName, predecessorRangeName, successorRangeName, ref range, ref predecessorRange, ref sucessorRange, errors);
        GetProcesses(@case, predecessorProcessesName, successorProcessesName, ref predecessorProcesses, ref includeAllPredecessorProcesses, ref successorProcesses, ref includeAllSuccessorProcesses, errors);
        GetOtherParameters(table, groupingFields, groupingFieldsStrings, offsets, offsetsStrings, continuousSearchBack, ref gapFill, ref fields, errors);
        
        if (!Boolean.TryParse(rangeForPositionString, out useRangeForTablePositions))
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTablePositions Boolean ", rangeForPositionString)));
        
        if (errors.Any())
            return null;
        
        return new FieldOffsetDependencyOptions(range, predecessorRange, predecessorProcesses, includeAllPredecessorProcesses, sucessorRange, successorProcesses, includeAllSuccessorProcesses, fields, offsets, gapFill, useRangeForTablePositions);
    }    
    
    private static FieldOffsetDependencyOptions GetOptionValuesForDependencyFor8Attributes(Table table, RangeDependencyRule rangeDependencyRule, Errors errors)
    { 
        var inputStrings = rangeDependencyRule.Description.Split('|');

        var index = 0;
        var dependencyTypeName = inputStrings[index++].Trim();
        if (!dependencyTypeName.Equals(DependencyName))
        {
            errors.Add(new Error(string.Format("Dependency Type Name does not match {0}", DependencyName)));
            return null;
        }

        var versionString = inputStrings[index].Trim();
        var version = -1;
        Int32.TryParse(versionString, out version);

        var gapFill = false;
        var useRangeForTableNodes = false;
        var useRangeForTablePositions = false;
        var @case = rangeDependencyRule.Case;
        var rangeName = inputStrings[index++].Trim();
        var predecessorRangeName = inputStrings[index++].Trim();
        var successorRangeName = inputStrings[index++].Trim();
        var groupingFieldsStrings = inputStrings[index++];
        var groupingFields = groupingFieldsStrings.Split(',');
        var continuousSearchBack = inputStrings[index++].Trim();
        var rangeForPositionString = inputStrings[index++].Trim();
        var offsetsStrings = inputStrings[index++];
        var offsets = offsetsStrings.Split('\\').ToList();
        var includeAllPredecessorProcesses = true;
        var includeAllSuccessorProcesses = true;

        var dependencyName = rangeDependencyRule.FullName;
        Range range = null;
        Range predecessorRange = null;
        Range sucessorRange = null;
        List<Process> predecessorProcesses = null;
        List<Process> successorProcesses = null;
		List<Field> fields = null;

        GetRanges(table, rangeName, predecessorRangeName, successorRangeName, ref range, ref predecessorRange, ref sucessorRange, errors);
        GetOtherParameters(table, groupingFields, groupingFieldsStrings, offsets, offsetsStrings, continuousSearchBack, ref gapFill, ref fields, errors);
        
        if (!Boolean.TryParse(rangeForPositionString, out useRangeForTablePositions))
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTablePositions Boolean ", rangeForPositionString)));
        
        if (errors.Any())
            return null;
        
        return new FieldOffsetDependencyOptions(range, predecessorRange, predecessorProcesses, includeAllPredecessorProcesses, sucessorRange, successorProcesses, includeAllSuccessorProcesses, fields, offsets, gapFill, useRangeForTablePositions);
    }    
    
	private static FieldOffsetDependencyOptions GetOptionValuesForDependencyForV1(Table table, RangeDependencyRule rangeDependencyRule, Errors errors)
    { 
        var inputStrings = rangeDependencyRule.Description.Split('|');

        var index = 0;
        var dependencyTypeName = inputStrings[index++].Trim();
        if (!dependencyTypeName.Equals(DependencyName))
        {
            errors.Add(new Error(string.Format("Dependency Type Name does not match {0}", DependencyName)));
            return null;
        }
		
        var @case = rangeDependencyRule.Case;
        var versionString = inputStrings[index++].Trim();
        var regenerateThisString = inputStrings[index++].Trim();
        var rangeName = inputStrings[index++].Trim();
        var predecessorRangeName = inputStrings[index++].Trim();
        var predecessorProcessesName = inputStrings[index++].Trim();
        var successorRangeName = inputStrings[index++].Trim();
        var successorProcessesName = inputStrings[index++].Trim();
        var groupingFieldsStrings = inputStrings[index++];
        var groupingFields = groupingFieldsStrings.Split(',');
        var offsetsStrings = inputStrings[index++];
        var offsets = offsetsStrings.Split('\\').ToList();
        var gapFillString = inputStrings[index++].Trim();
        var rangeForPositionString = inputStrings[index++].Trim();
        var includeAllPredecessorProcesses = false;
        var includeAllSuccessorProcesses = false;

        var dependencyName = rangeDependencyRule.FullName;
        Range range = null;
        Range predecessorRange = null;
        Range sucessorRange = null;
        List<Process> predecessorProcesses = null;
        List<Process> successorProcesses = null;
        var gapFill = false;
        var regenerateThis = false;
        var useRangeForTablePositions = false;
        List<Field> fields = null;

        if (!Boolean.TryParse(regenerateThisString, out regenerateThis))
        {
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTableNodes Boolean ", regenerateThisString)));
            return null;
        }

        if (!regenerateThis)
            return null;

        GetRanges(table, rangeName, predecessorRangeName, successorRangeName, ref range, ref predecessorRange, ref sucessorRange, errors);
        GetProcesses(@case, predecessorProcessesName, successorProcessesName, ref predecessorProcesses, ref includeAllPredecessorProcesses, ref successorProcesses, ref includeAllSuccessorProcesses, errors);
        GetOtherParameters(table,groupingFields, groupingFieldsStrings, offsets, offsetsStrings, gapFillString, ref gapFill, ref fields,  errors);

        if (!Boolean.TryParse(rangeForPositionString, out useRangeForTablePositions))
        {
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTableNodes Boolean ", rangeForPositionString)));
            return null;
        }

        if (errors.Any())
            return null;
        return new FieldOffsetDependencyOptions(range, predecessorRange, predecessorProcesses, includeAllPredecessorProcesses, sucessorRange, successorProcesses, includeAllSuccessorProcesses, fields, offsets, gapFill, useRangeForTablePositions);
    }

	
    private static void GetOtherParameters(Table table, string[] groupingFields, string groupingFieldsString, List<string> offsets, string offsetsStrings, string continuousSearchBack, ref bool gapFill, ref List<Field> fields, Errors errors)
    {
        if (groupingFields.Length != offsets.Count)
            errors.Add(new Error(string.Format("Grouping Fields Length '{0}' did not match offsets '{1}'", groupingFieldsString, offsetsStrings)));
        if (!Boolean.TryParse(continuousSearchBack, out gapFill))
            errors.Add(new Error(string.Format("Could not parse '{0}' for GapFill Boolean.", continuousSearchBack)));

        var tableFields = groupingFields.Select(x => new { FullName = x, Field = table.Schema.GetField(x) }).ToList();
        if (tableFields.Any(x => x.Field == null))
        {
            var nullFields = tableFields.Where(x => x.Field == null).Select(x => x.FullName);
            errors.Add(new Error(string.Format("Could not find these {0} fields in Table '{1}'.", string.Join(", ", nullFields), table.Name)));
        }
        
        fields = tableFields.Select(x=>x.Field).ToList();
    }

    private static void GetProcesses(Case @case, string predecessorProcessesName, string successorProcessesName, ref List<Process> predecessorProcesses, ref bool includeAllPredecessorProcesses, ref List<Process> successorProcesses, ref bool includeAllSuccessorProcesses, Errors errors)
    {
        if (!string.IsNullOrEmpty(predecessorProcessesName))
        {
            if (predecessorProcessesName.Equals("< All >"))
                includeAllPredecessorProcesses = true;
            else
            {
                var processes = predecessorProcessesName.Split(',').Select(x => @case.Processes.Get(x));
                if (processes.Any(x => x == null))
                    errors.Add(new Error(string.Format("Could not find all predecessor processes '{0}'", predecessorProcessesName)));
                else if (!processes.Any())
                    errors.Add(new Error(string.Format("Could not find any predecessor processes '{0}'", predecessorProcessesName)));

                predecessorProcesses = processes.ToList();
                var activeProcesses = @case.Processes.Where(x => x.Active && x.Productive).ToList();
                includeAllPredecessorProcesses = predecessorProcesses.Count == activeProcesses.Count;
            }
        }

        if (!string.IsNullOrEmpty(successorProcessesName))
        {
            if (successorProcessesName.Equals("< All >"))
                includeAllSuccessorProcesses = true;
            else
            {
                var processes = successorProcessesName.Split(',').Select(x => @case.Processes.Get(x));
                if (processes.Any(x => x == null))
                    errors.Add(new Error(string.Format("Could not find all successor processes '{0}'", successorProcessesName)));
                else if (!processes.Any())
                    errors.Add(new Error(string.Format("Could not find any successor processes '{0}'", successorProcessesName)));

                successorProcesses = processes.ToList();
                var activeProcesses = @case.Processes.Where(x => x.Active && x.Productive).ToList();
                includeAllSuccessorProcesses = successorProcesses.Count == activeProcesses.Count;
            }
        }
    }
    
    private static void GetRanges(Table table, string rangeName, string predecessorRangeName, string successorRangeName, ref Range range, ref Range predecessorRange, ref Range sucessorRange, Errors errors)
    {
        if (!string.IsNullOrEmpty(rangeName))
        {
            range = table.Ranges.GetRange(rangeName);
            if (range == null)
                errors.Add(new Error(string.Format("Could not find Range '{0}' in Table {1}", rangeName, table.Name)));
        }

        if (!string.IsNullOrEmpty(predecessorRangeName))
        {
            predecessorRange = table.Ranges.GetRange(predecessorRangeName);
            if (predecessorRange == null)
                errors.Add(new Error(string.Format("Could not find Predecessor Range '{0}' in Table {1}", predecessorRangeName, table.Name)));
        }

        if (!string.IsNullOrEmpty(successorRangeName))
        {
            sucessorRange = table.Ranges.GetRange(successorRangeName);
            if (sucessorRange == null)
                errors.Add(new Error(string.Format("Could not find Successor Range '{0}' in Table {1}", successorRangeName, table.Name)));
        }
    }

    private static void SetupEntryProcesses(RangeDependencyEntry entry, List<Process> predecessorProcesses, bool includeAllPredecessorProcesses, List<Process> successorProcesses, bool includeAllSuccessorProcesses)
    {
        entry.IncludeAllPredecessorProcesses = includeAllPredecessorProcesses;
        entry.IncludeAllSuccessorProcesses = includeAllSuccessorProcesses;

        if (predecessorProcesses != null)
        {
            entry.PredecessorProcesses.Clear();
            foreach (var process in predecessorProcesses)
                entry.PredecessorProcesses.Add(process);
        }
        if (successorProcesses != null)
        {
            entry.SuccessorProcesses.Clear();
            foreach (var process in successorProcesses)
                entry.SuccessorProcesses.Add(process);
        }
    }

    private static RangeDependencyRule SetupRangeDependency(Case scenario, string dependencyName, Range range, Range predecessorRange, Range successorRange, string description)
    {
        var rangeDependency = RangeDependencyUtil.GetDependency(scenario, dependencyName, true);

        while (rangeDependency.Entries.Count > 0)
            rangeDependency.Entries.RemoveAt(0);

        rangeDependency.Description = description;

        rangeDependency.SourcePredecessorCompositeRange.Set(RangeMode.Table, predecessorRange ?? range, "", false);
        rangeDependency.SourceSuccessorCompositeRange.Set(RangeMode.Table, successorRange ?? range, "", false);
        rangeDependency.UseRanges = predecessorRange != null && successorRange != null;
        rangeDependency.Priority = 2;
        return rangeDependency;
    }

    #endregion

}