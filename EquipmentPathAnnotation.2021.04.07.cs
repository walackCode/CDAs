// Version 2021.04.07
// Use of this script is not officially supported
// Documentation is available at https://staging.precisionmining.com/docs/equipment-path-annotation-script
// READ THE DOCUMENTATION. READ THE WARNINGS.

#region Version History
// Ideally this would be version controlled, but as most engineers cannot reliably use winzip, dvcs is a non-starter.
// 2015/06/13	bugfix constraint not being added to the engine	
// 2015/07/15	bugfix dependfree not working correctly	
// 2015/07/15	add ability to specify delay= arguments for !waitfree and !waiton
// 2015/07/22	bugfix case sensitivity bug in waiton etc
// 2015/08/21	bugfix disable script if source scheduling is disabled
// 2015/10/29	bugfix dependclear on=<date> was firing immediately and not on specified date
// 2015/12/02	bugfix dependclear with limited task arguments was not working (all predecessors cleared instead)
// 2016/01/21	adjust dependclears that run at schedule start to trigger immediately instead of a single tick after engine run; add !pathclear
// 2016/03/04	bugfix dependon delay= directives not being honoured. Minor adjustment to parsing framework to allow for comments to be added in a more structured manner
// 2016/10/18	added !remove, !deadhead/!delayafter and !timeconstrain directives
// 2016/12/21	bugfix !delayafter alias not working properly
// 2017/01/27	Add additional debugging information (use ISourceConstraintSnapshotInformation mechanism; improve dependency descriptions	
// 2017/05/18	bugfix !timeconstrain mode=allow not working (threw an error instead)
// 2018.02.23.prerelease1 - !forcecompletion and !reprioritise
// 2018.11.06.prerelease1   change timconstrain to allow multiple timeconstraints. allow now acts acts as a veto allow. This is a breaking change.
// 2019.04.05   release and refactor
// 2019.06.12.prerelease1 - add %selfrange% interpolation variable for task specifications to allow for better shorthanding of !waiton
// 2019.06.15.prerelease2 - more resilient task parsing to handle range expansions
// 2019.06.30.prerelease3 - more resilient task parsing to handle rate and partial completion (i.e. [] and ())
// 2020.01.09.prerelease4 - bugfix !forcecompletion causing errors if the task has already been completed. Thanks to Chris Grant-Saunders
// 2020.01.10.prerelease5 - bugfix !remove causing errors if 'Release Task at Every Completion' is not enabled
// 2021.01.07 - bugfix !forcecompletion causing errors with absurdly high percentage completion (instead of unit rate)
//            - added !injectschedulepath
//            - major refactor to command parsing and re-implementation of some commands
//            - rudimentary testing framework
//            - expose API for utilising path annotation command infrastructure
//            - require use of !equipmentpathannotation_enable
// 2021.02.27 - bugfix !injectschedulepath obey= arguments, instantaneous steps, collection modified exception if adding regular steps after calling !injectionschedulepath
// 2021.04.07 - bugfix !timeconstrain with mode=allow not working properly (again), update documentation url
#endregion

#region Using Directives

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Scheduling;
using System.Runtime.CompilerServices;

#endregion

// debugging
partial class EquipmentPathAnnotation
{
    bool DEBUG_PRINT_EQUIPMENT_PATH = false
        //|| true
    ;
    bool DEBUG_DUMP_WAIT_COUNTER_DESCRIPTION_TO_CONSOLE = false
        //|| true
    ;

    const string ENABLE_COMMAND = "'!equipmentpathannotation_enable This uses the equipment path annotation script. It is an unsupported utility that is documented here https://staging.precisionmining.com/docs/equipment-path-annotation-script";
    const string DOCUMENTATION_URL = "https://staging.precisionmining.com/docs/equipment-path-annotation-script";
}

// ISourceConstraint and ISourceConstraintSnapshotInformation implementation
public partial class EquipmentPathAnnotation : ISourceConstraint, ISourceConstraintSnapshotInformation
{
    private readonly Dictionary<EquipmentTaskPair, Waits> waits = new Dictionary<EquipmentTaskPair, Waits>();

    private Waits? Get(SchedulingEquipment eq, ITask task, bool create = false)
    {
        var pair = new EquipmentTaskPair(eq, task);
        Waits ret;
        if (!waits.TryGetValue(pair, out ret))
        {
            if (create)
                waits.Add(pair, ret = new Waits(0));
            else
                return null;
        }
        return ret;
    }

    public EquipmentPathAnnotation(SchedulingEngine engine)
    {
        Enabled = true;
        Engine = engine;
		
		Name = this.GetType().Name;
		FullName = this.GetType().FullName;
    }

    public SchedulingEngine Engine { get; private set; }

    public bool Enabled { get; private set; }
    public string Name { get; private set; }
    public string FullName { get; private set; }

    public void PrescheduleSetup(SchedulingEngine engine)
    {
        Enabled = true;

        engine.GenerateDependencies();
        foreach (var eq in engine.Equipment)
            ParsePath(eq);

        var evt = SetupCompleted;
        if (evt != null)
            evt(this, EventArgs.Empty);
    }

    public bool Available(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask task, DateTime date)
    {
        Waits? wcs = Get(equipment, task, false);
        if (wcs == null)
            return true; // no waits setup, all good from this end

        foreach (var wc in wcs.Value.WaitOns)
        {
            if (wc.Counter > 0)
                return false;
            if (date - wc.LastDecremented < wc.Delay)
                return false;
        }

        bool suppress = false;
        foreach (var w in wcs.Value.TimeConstraints)
        {
            if (w.Allowing && w.Active)
                return true; // an allow acts as a 'reverse veto', i.e. presence of ANY allow will make the task available
			else if (w.Allowing && !w.Active)
				suppress = true;
            else if (w.Suppressing && w.Active)
                suppress = true;
        }
        return !suppress;
    }

    public string GetSummary(SchedulingEngine engine, DateTime date)
    {
        return "Active; " + waits.Count.ToString("#,0") + " waits/timeconstraints applied";
    }

    public string GetDetail(SchedulingEngine engine, DateTime date)
    {
        return GetSummary(engine, date);
    }

    public string AvailableDescription(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask task, DateTime date)
    {
        var pair = new EquipmentTaskPair(equipment, task);
        Waits waitList;
        if (!waits.TryGetValue(pair, out waitList))
            return "No waits/timeconstraints applied";

        int count = waitList.WaitOns.Count + waitList.TimeConstraints.Count;

        var sb = new StringBuilder();
        for (var i = 0; i < waitList.WaitOns.Count; i++)
        {
            var wc = waitList.WaitOns[i];
            if (count > 1)
                sb.AppendLine();
            if (wc.Counter > 0)
            {
                sb.AppendFormat("Waiting on {0:#,0} {1} from line #{2} ({3}) - {4}", wc.Counter, wc.Counter > 1 ? "tasks" : "task", wc.LineNumber, wc.Directive.Trim(), wc.Line.Trim());
                sb.AppendLine();
                var firstPredecessor = wc.PredecessorTasks.FirstOrDefault(pt => pt.State != SchedulingTaskState.Complete);
                sb.AppendFormat("   first task: {0}", firstPredecessor);

            }
            else if (date - wc.LastDecremented < wc.Delay)
                sb.AppendFormat("Release delay ({0} remaining) on {1:#,0} completed {2} from line #{3} ({4}) - {5}", wc.Delay - (date - wc.LastDecremented), wc.PredecessorTasks.Count, wc.PredecessorTasks.Count > 1 ? "tasks" : "task", wc.LineNumber, wc.Directive.Trim(), wc.Line.Trim());
            else
                sb.AppendFormat("(Inactive) Released; previously waiting on {0:#,0} completed {1} from line #{2} ({3}) - {4}", wc.PredecessorTasks.Count, wc.PredecessorTasks.Count > 1 ? "tasks" : "task", wc.LineNumber, wc.Directive.Trim(), wc.Line.Trim());


            if (DEBUG_DUMP_WAIT_COUNTER_DESCRIPTION_TO_CONSOLE)
            {
                foreach (var t in wc.PredecessorTasks)
                {
                    Console.WriteLine(t);
                }
            }
        }

        for (var i = 0; i < waitList.TimeConstraints.Count; i++)
        {
            var tc = waitList.TimeConstraints[i];
            if (count > 1)
                sb.AppendLine();

            if (tc.Active)
            {
                sb.AppendFormat("Active Time Constraint");
            }
            else
            {
                sb.AppendFormat("(Inactive) Time Constraint");
            }

            if (tc.Suppressing)
            {
                sb.AppendFormat(" Suppressing ");
            }
            else
            {
                sb.AppendFormat(" Allowing ");
            }
            sb.AppendFormat("line #{0} ({1}) - {2}", tc.LineNumber, tc.Directive.Trim(), tc.Line.Trim());
        }
        return sb.ToString();
    }

    private struct EquipmentTaskPair
    {
        public readonly SchedulingEquipment Equipment;
        public readonly ITask Task;
        private readonly int hashCode;

        public EquipmentTaskPair(SchedulingEquipment equipment, ITask task)
        {
            Equipment = equipment;
            Task = task;
            unchecked
            {
                hashCode = equipment.GetHashCode() + Task.GetHashCode() * 397;
            }
        }

        public bool Equals(EquipmentTaskPair other)
        {
            return Equals(Equipment, other.Equipment) && Equals(Task, other.Task);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is EquipmentTaskPair && Equals((EquipmentTaskPair)obj);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public static EquipmentTaskPair Make(SchedulingEquipment equipment, ITask task)
        {
            return new EquipmentTaskPair(equipment, task);
        }
    }

    // used to track waits - used for !wait
    private class WaitCounter
    {
        public int Counter;
        public DateTime LastDecremented = DateTime.MinValue;
        public TimeSpan Delay = TimeSpan.Zero;

        public ICollection<ITask> PredecessorTasks;
        public int LineNumber;
        public string Line;
        public string Directive;
    }

    // used to track timeconstraints
    private class TimeConstraint
    {
        public bool Active;
        public bool Suppressing;
        public bool Allowing;
        public DateTime Start;
        public DateTime End;

        public int LineNumber;
        public string Line;
        public string Directive;
    }

    private struct Waits
    {
        public List<WaitCounter> WaitOns;
        public List<TimeConstraint> TimeConstraints;

        public Waits(int x)
        {
            WaitOns = new List<WaitCounter>();
            TimeConstraints = new List<TimeConstraint>();
        }
    }
}

// setup
public partial class EquipmentPathAnnotation
{
    private static readonly Action<SchedulingEngine> dummyAction = e => { };

    public static void SetupEngine(SchedulingEngine se)
    {
        if (!se.RunSourceScheduling)
            return;

        var epa = new EquipmentPathAnnotation(se);
        se.Constraints.Add(epa);
    }

    // rely on PrescheduleSetup call to perform equipment path parsing
    public void ParsePath(SchedulingEquipment se)
    {

        var lineReader = new System.IO.StringReader(se.BaseEquipment.SourcePath);
        int lineNumber = 0;

        bool? path_annotation_enabled = null;
        ISchedulingSourcePathElement nextElement = se.SourcePath.First;
        var pathEnumeratorCurrentLine = -1;
        ISchedulingSourcePathElement lastElement = null;
        for (string line; (line = lineReader.ReadLine()) != null;)
        {
            lineNumber += 1;
            try
            {
                List<ISchedulingSourcePathElement> pathElements = null;
                List<SchedulingSourcePathSteps> pathSteps = null; // new List<SchedulingSourcePathStep>();
                List<SchedulingSourcePathDelayTimeSpan> timeDelays = null; // new List<SchedulingSourcePathDelayTimeSpan>();
                List<SchedulingSourcePathDelayDate> dateDelays = null; // new List<SchedulingSourcePathDelayDate>();
                string comment = null;

                while (nextElement != null && pathEnumeratorCurrentLine <= lineNumber)
                {
                    var steps = nextElement as SchedulingSourcePathSteps;
                    if (steps != null)
                    {
                        pathEnumeratorCurrentLine = steps.First().PathLineNumber;
                        if (pathEnumeratorCurrentLine == lineNumber)
                        {
                            pathSteps = pathSteps ?? new List<SchedulingSourcePathSteps>();
                            pathSteps.Add(steps);
                            comment = comment ?? steps.First().PathLineComment;

                        }
                    }
                    var timeDelay = lastElement as SchedulingSourcePathDelayTimeSpan;
                    if (timeDelay != null)
                    {
                        pathEnumeratorCurrentLine = timeDelay.PathLineNumber;
                        if (pathEnumeratorCurrentLine == lineNumber)
                        {
                            timeDelays = timeDelays ?? new List<SchedulingSourcePathDelayTimeSpan>();
                            timeDelays.Add(timeDelay);
                            //comment = comment ?? timeDelay.PathLineComment; // not currently available
                        }
                    }
                    var dateDelay = lastElement as SchedulingSourcePathDelayDate;
                    if (dateDelay != null && dateDelay.PathLineNumber == lineNumber)
                    {
                        pathEnumeratorCurrentLine = dateDelay.PathLineNumber;
                        if (pathEnumeratorCurrentLine == lineNumber)
                        {
                            dateDelays = dateDelays ?? new List<SchedulingSourcePathDelayDate>();
                            dateDelays.Add(dateDelay);
                            //comment = comment ?? dateDelay.PathLineComment; // not currently available
                        }
                    }

                    if (pathEnumeratorCurrentLine == lineNumber)
                    {
                        pathElements = pathElements ?? new List<ISchedulingSourcePathElement>();
                        pathElements.Add(nextElement);
                    }

                    if (pathEnumeratorCurrentLine <= lineNumber)
                    {
                        nextElement = nextElement.Next;
                    }
                }
                if (comment == null)
                {
                    // might not have been set if timeDelay or dateDelay
                    var commentCharIdx = line.IndexOf('\'');
                    if (commentCharIdx != -1)
                        comment = line.Substring(commentCharIdx);
                }

                List<Command> cmds = null;
                if (!string.IsNullOrWhiteSpace(comment))
                    cmds = Command.Parse(comment).ToList();

                if (DEBUG_PRINT_EQUIPMENT_PATH)
                {
                    Console.WriteLine("Equipment Path Annotation Parsing for {0} (DEBUG_PRINT_EQUIPMENT_PATH = true)", se.BaseEquipment.FullName);
                    Console.WriteLine("Line {0}: {1}", lineNumber, string.IsNullOrWhiteSpace(line) ? "BLANK" : line);
                    if (pathSteps != null && pathSteps.Count > 0)
                        Console.WriteLine("\t has steps {0} ", pathSteps.Count);
                    if (timeDelays != null)
                        Console.WriteLine("\t has timeDelays {0}", timeDelays.Count);
                    if (dateDelays != null)
                        Console.WriteLine("\t has dateDelays {0}", dateDelays.Count);

                    if (cmds != null)
                    {
                        foreach (var cmd in cmds)
                        {
                            Console.WriteLine("\t has command {0}, default:", cmd.name, cmd.defaultarg);
                            foreach (var pair in cmd.namedargs)
                            {
                                Console.WriteLine("\t {0} = {1}", pair.Key, pair.Value);
                            }
                            Console.WriteLine("\t last element: " + lastElement);
                        }
                    }
                }

                if (cmds != null)
                {
                    foreach (var cmd in cmds)
                    {
                        if (EqualsIgnoreCase(cmd.name, "equipmentpathannotation_enable"))
                        {
                            if (cmd.defaultarg != null && cmd.defaultarg.Contains("https") && cmd.defaultarg.Contains("document") && cmd.defaultarg.Contains("unsupported"))
                                // cheap checks to try to ensure that ENABLE_COMMAND variable or something close to it is in 
                                path_annotation_enabled = true;

                            // this should fall into the ENABLE_COMMAND error below
                            continue;

                        }

                        if (EqualsIgnoreCase(cmd.name, "equipmentpathannotation_disable"))
                        {
                            path_annotation_enabled = false;
                            continue;
                        }

                        if (!path_annotation_enabled.HasValue)
                        {
                            var innerException = new Exception(ENABLE_COMMAND);
                            var exception = new Exception("Equipment Path Annotation commands appear to be present in equipment '" + se.BaseEquipment.FullName + "' path but have not been explicitly enabled (or disabled).\n" +
                                "Versions of this script from 2021 onwards require an explicit command to enable this in order to encourage users towards a higher level understanding of this utility instead of rote lever pulling.\n" +
                                "Also, the Equipment Path Annotation script is an unsupported utility and this has generally been poorly understood due to aforementioned issues.\n" +
                                "Add the following line to the top of your equipment source path, before any other commands (ideally at the top):"
                                + Environment.NewLine + Environment.NewLine +
                                "   " + ENABLE_COMMAND
                                + Environment.NewLine + Environment.NewLine +
                                "(this message can be copied from the EquipmentPathAnnotation script or from the inner exception message in the Exception pane at the bottom of the Spry window)",
                                innerException);
                            // exception.Data.Add("ENABLE_COMMAND", ENABLE_COMMAND); // would be better to just add to the exception .Data but this is not output in the Exception window in Spry
                            throw exception;
                        } 

                        if (!path_annotation_enabled.Value)
                            continue;

                        if (pathSteps != null && pathSteps.Count > 0)
                        {
                            if (EqualsIgnoreCase(cmd.name, "waitfree"))
                                ProcessWaitfree(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "timeconstrain") || EqualsIgnoreCase(cmd.name, "timeconstraint"))
                                ProcessTimeConstrain(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "deadhead") || EqualsIgnoreCase(cmd.name, "delayafter"))
                                ProcessDeadhead(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "waiton"))
                                ProcessWaiton(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "dependfree"))
                                ProcessDependfree(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "dependon"))
                                ProcessDependon(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "dependclear"))
                                ProcessDependclear(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "remove"))
                                ProcessRemove(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "forcecomplete") || EqualsIgnoreCase(cmd.name, "forcecompletion"))
                                ProcessForceComplete(se, cmd, lineNumber, line, pathSteps);
                            else if (EqualsIgnoreCase(cmd.name, "reprioritise") || EqualsIgnoreCase(cmd.name, "reprioritize"))
                                ProcessReprioritise(se, cmd, lineNumber, line, pathSteps);
                            
						}
                        
						if (EqualsIgnoreCase(cmd.name, "injectschedulepath"))
							ProcessInjectSchedulePath(se, cmd, lineNumber, line, pathSteps, ref lastElement);
                    }
                }

                var evt = ProcessLine;
                if (evt != null) {
                    evt(this, new ProcessLineEvent(se, pathElements, pathSteps, timeDelays, dateDelays, cmds, line, lineNumber));
                };
            } catch (Exception e)
            {
                // note that exceptions thrown in THIS frame (ie in the code above) will not have stack info preserved
                // would have to do something like https://weblogs.asp.net/fmarguerie/rethrowing-exceptions-and-preserving-the-full-call-stack-trace
                // but currently not possible
                throw new Exception(e.Message + Environment.NewLine + string.Format("   while parsing equipment path for {0} line {1:#,0}", se.BaseEquipment.FullName, lineNumber), e);
            }
        }
    }
	
	void ProcessWaitfree(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps) 
    {
        // !waitfree
        // !waitfree delay=
        var delay = ParseTimeSpan(cmd.namedargs.GetValueOrDefault("delay"), TimeSpan.Zero);

        var successorHash = new HashSet<ITask>(pathSteps.SelectMany(x => x).Select(x => x.Task));
        var predecessors = successorHash.SelectMany(x => x.Predecessors).Select(x => x.Predecessor).Where(x => !successorHash.Contains(x)).ToList();

        SetWait(se, successorHash, predecessors, delay, lineNumber, line, cmd.name);
    }

    void ProcessTimeConstrain(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        // !timeconstrain start=1/1/2020 end=1/2/2020
        // !timeconstrain start=1/1/2020 end=1/2/2020 mode=allow
        var start = ParseDate(cmd.namedargs.GetValueOrDefault("start"), se.Engine.StartDate);
        var end = ParseDate(cmd.namedargs.GetValueOrDefault("end"), se.Engine.EndDate);
        var mode = MatchEnum(cmd.namedargs.GetValueOrDefault("mode"),
                "suppress", "constrain", "constrained", null,
                "allow", "allowed", null
        ); // suppress or allow

        bool suppressing;
        if (mode == null || mode == "suppress")
            suppressing = true;
        else if (mode == "allow")
            suppressing = false;
        else throw new Exception("Unknown time constraint mode: " + mode);

        SetTimeConstraint(se, pathSteps.SelectMany(x => x).Select(x => x.Task), start, end, suppressing, lineNumber, line, cmd.name);
    }

    void ProcessDeadhead(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
		var delayProcess = ParseProcess(cmd.namedargs.GetValueOrDefault("process"), se.BaseEquipment.Case, null);
		if (delayProcess == null) {
			if (se.BaseEquipment.InactiveProcess == null)
				throw new Exception("Nonproductive delay process not specified (e.g. process=Deadhead) and equipment has no default inactive process to use as a fallback");
			else 
				delayProcess = se.BaseEquipment.InactiveProcess;
		}
		var delay = ParseTimeSpan(cmd.defaultarg, TimeSpan.Zero, "default");

        SetDeadhead(se, pathSteps.SelectMany(x => x).Select(x => x.Task), delay, delayProcess);
    }

    void ProcessWaiton(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        // !waiton A/1/2 <asdf>
        // !waiton s=A/1/2 <asdf>
        // !waiton d=A/1/2
        // !waiton A/1/2 <asdf> delay=
        // !waiton s=A/1/2 <asdf> delay=
        // !waiton d=A/1/2 delay=

        var tasks = ParseTasks(cmd.defaultarg, line);
        var delay = ParseTimeSpan(cmd.namedargs.GetValueOrDefault("delay"), TimeSpan.Zero, "delay");

        SetWaitOn(se, pathSteps.SelectMany(x => x).Select(x => x.Task), tasks, delay, lineNumber, line, cmd.name);
    }

    void ProcessDependfree(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        // !dependfree
        // !dependfree delay=

        var delay = ParseTimeSpan(cmd.namedargs.GetValueOrDefault("delay"), TimeSpan.Zero, "delay");

        SetDependFree(pathSteps.SelectMany(x => x).Select(x => x.Task), delay, GetType().Name + " !" + cmd.name + " on " + se.BaseEquipment.FullName + " Line " + lineNumber.ToString("#,0"));
    }
    void ProcessDependon(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        // !dependon A/1/2 <asdf>
        // !dependon s=A/1/2 <asdf>
        // !dependon d=A/1/2
        // !dependon A/1/2 <asdf> delay=
        // !dependon s=A/1/2 <asdf> delay=
        // !dependon d=A/1/2 delay=

        var tasks = ParseTasks(cmd.defaultarg, line);
        var delay = ParseTimeSpan(cmd.namedargs.GetValueOrDefault("delay"), TimeSpan.Zero, "delay");

        SetDependency(tasks, pathSteps.SelectMany(x => x).Select(x => x.Task), delay, GetType().Name + " !" + cmd.name + " on " + se.BaseEquipment.FullName + " Line " + lineNumber.ToString("#,0"));

    }
    void ProcessDependclear(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        // !dependclear A/1/2 <asdf>
        // !dependclear s=A/1/2 <asdf>
        // !dependclear d=A/1/2
        // !dependclear A/1/2 <asdf> on=
        // !dependclear s=A/1/2 <asdf> on=
        // !dependclear d=A/1/2 on=
        // !dependclear on=

        var date = ParseDate(cmd.namedargs.GetValueOrDefault("on"), Engine.StartDate, "on");

        IEnumerable<ITask> predecessorTasksToClear = null;
        if (cmd.defaultarg != null)
            predecessorTasksToClear = ParseTasks(cmd.defaultarg, line);

        SetDeferredDependencyClear(Engine, predecessorTasksToClear, pathSteps.SelectMany(x => x).Select(x => x.Task), date);
    }
    void ProcessRemove(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        var on_arg = cmd.namedargs.GetValueOrDefault("on");
        var event_arg = cmd.namedargs.GetValueOrDefault("event");

        if (on_arg != null && event_arg != null)
            throw new Exception("Both on= and event= arguments are specified. Should use one and only one");

        DateTime? date = null;
        if (on_arg != null)
            date = ParseDate(on_arg, Engine.StartDate, "on");

        var evnt = MatchEnum(event_arg,
            "completed", "complete", null,
            "available", "free", "waitfree", null
        );

        var delay = ParseTimeSpan(cmd.namedargs.GetValueOrDefault("delay"), TimeSpan.Zero, "delay");

        var tasks = ParseTasks(cmd.namedargs.GetValueOrDefault("tasks"), line);

        SetDeferredPathClear(se.Engine, se, pathSteps.SelectMany(x => x).Select(x => x.Task), pathSteps, tasks, evnt, date, delay);
    }
    void ProcessForceComplete(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        var date = ParseDate(cmd.namedargs.GetValueOrDefault("on"), Engine.StartDate, "on");
        if (date > Engine.EndDate)
            return; // don't bother setting completion after end of schedule

        var obey = MatchEnum(cmd.namedargs.GetValueOrDefault("obey"),
            "all", "both", null,
            "dep", "dependencies", null,
            "con", "constraints", null,
            "none", "no"
        );

        bool ignoreDependencies = true;
        bool ignoreConstraints = true;

        if (obey == null || obey == "none")
            ignoreDependencies = ignoreConstraints = true;
        else if (obey == "all")
            ignoreDependencies = ignoreConstraints = false;
        else if (obey == "dep")
            ignoreDependencies = false;
        else if (obey == "con")
            ignoreConstraints = false;
        else
            throw new Exception("Did not understand obey= arg. Value should be both/dep/con/none but was " + obey);

        SetForceCompletion(se, date, pathSteps, ignoreDependencies, ignoreConstraints);
    }
    void ProcessReprioritise(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps)
    {
        SetMovePath(se, pathSteps.SelectMany(x => x).Select(x => x.Task) , pathSteps);
    }

    private void SetForceCompletion(SchedulingEquipment se, DateTime completeDate, IList<SchedulingSourcePathSteps> steps, bool ignoreDependencies, bool ignoreConstraints)
    {
        se.Engine.TimeKeeper.Add(completeDate, se2 =>
        {
            SchedulingSourcePathSteps completeSteps = null;
            foreach (var pathSteps in steps)
            {
                foreach (var pathStep in pathSteps)
                {
                    if (pathStep.Task.Complete)
                        continue;

                    completeSteps = completeSteps ?? new SchedulingSourcePathSteps() { IgnoreConstraints = ignoreConstraints, IgnoreDependencies = ignoreDependencies, AllowAdvance = steps.First().AllowAdvance };
                    completeSteps.Add((SourceTask)pathStep.Task, PathValueType.Percentage, 1.0, RateModifierType.Absolute, 10E12, EquipmentCountModifierType.Factor, 1.0); // should equate to mining in 0.036 of a tick which should be instantaneous.
                }
            }
            if (completeSteps != null)
                se.SourcePath.AddFirst(completeSteps);
        });
    }

    public void SetMovePath(SchedulingEquipment se, IEnumerable<SourceTask> tasks, IList<SchedulingSourcePathSteps> steps)
    {
        // this is hackish - in an ideal world we'd move the path as soon as it is acquired, but we can't do this because of the exact same reason (it's been acquired). So instead when a task is acquired, we add a hook to when the task is released, and move it then
        var hashSteps = new HashSet<SchedulingSourcePathStep>(steps.SelectMany(x => x));

        EventHandler<SchedulingTaskSelectedEventArgs> selectTask = null;
        EventHandler<SchedulingTaskReleasedEventArgs> releaseTask = null;
        selectTask = (sender, args) =>
        {
            if (!hashSteps.Contains(se.ActiveSourcePathStep))
                return;

            se.TaskSelected -= selectTask;
            se.TaskReleased += releaseTask;
        };

        releaseTask = (sender, args) =>
        {
            se.TaskReleased -= releaseTask;

            foreach (var step in steps)
                se.SourcePath.Remove(step);
            for (int i = steps.Count - 1; i >= 0; i--)
                se.SourcePath.AddFirst(steps[i]);
        };

        se.TaskSelected += selectTask;
    }

    // should be used by SetWaitFree and SetWaitOn only
    private void SetWait(SchedulingEquipment se, IEnumerable<ITask> successorTasks, ICollection<ITask> predecessorTasks, TimeSpan delay, int lineNumber, string originalLine, string directive)
    {
        var waitCounter = new WaitCounter
        {
            Counter = predecessorTasks.Count,
            Delay = delay,
            PredecessorTasks = predecessorTasks,
            Line = originalLine,
            LineNumber = lineNumber,
            Directive = directive
        };

        foreach (var predecessor in predecessorTasks)
        {
            var st = predecessor as SourceTask;
            var dt = predecessor as DestinationTask;

            if (st != null)
                st.TaskCompleted += (sender, args) =>
                {
                    waitCounter.Counter--;
                    waitCounter.LastDecremented = args.Engine.CurrentDate;
                    if (waitCounter.Counter <= 0 && delay != TimeSpan.Zero)
                    {
                        args.Engine.TimeKeeper.Add(delay);
                    }
                };
            else if (dt != null)
                dt.TaskCompleted += (sender, args) =>
                {
                    waitCounter.Counter--;
                    waitCounter.LastDecremented = args.Engine.CurrentDate;
                    if (waitCounter.Counter <= 0 && delay != TimeSpan.Zero)
                    {
                        args.Engine.TimeKeeper.Add(delay);
                    }
                };
        }

        foreach (var task in successorTasks)
        {
            var waits = Get(se, task, true);
            waits.Value.WaitOns.Add(waitCounter);
        }
    }

    private void SetTimeConstraint(SchedulingEquipment se, IEnumerable<ITask> successorTasks, DateTime start, DateTime end, bool suppress, int lineNumber, string originalLine, string directive)
    {
        var tc = new TimeConstraint();
        tc.Start = start;
        tc.End = end;
        tc.LineNumber = lineNumber;
        tc.Line = originalLine;
        tc.Directive = directive;
        tc.Allowing = false;
        tc.Suppressing = false;

        foreach (var task in successorTasks)
        {
            var waits = Get(se, task, true);
            waits.Value.TimeConstraints.Add(tc);
        }

        if (suppress)
        {
            tc.Suppressing = true;
            tc.Allowing = false;
        }
        else
        {
            tc.Allowing = true;
            tc.Suppressing = false;
        }
        se.Engine.TimeKeeper.Add(start, () =>
        {
            tc.Active = true;
        });
        se.Engine.TimeKeeper.Add(end, () =>
        {
            tc.Active = false;
        });
    }

    public void SetDeadhead(SchedulingEquipment se, IEnumerable<SourceTask> tasks, TimeSpan delay, Process process)
    {
        int taskCount = 0;

        foreach (var task in tasks) 
		{
			taskCount += 1;
            task.TaskCompleted += (sender, args) =>
            {
                taskCount--;

                if (taskCount == 0)
                    se.Delay(process, delay);
            };
		}
    }

    public void SetWaitFree(SchedulingEquipment se, IEnumerable<ITask> tasks, TimeSpan delay, int lineNumber, string originalLine, string directive)
    {
        var successorHash = new HashSet<ITask>(tasks);
        var predecessorTasksList = successorHash.SelectMany(x => x.Predecessors).Select(x => x.Predecessor).Where(x => !successorHash.Contains(x)).ToList();

        SetWait(se, successorHash, predecessorTasksList, delay, lineNumber, originalLine, directive);
    }

    public void SetWaitOn(SchedulingEquipment se, IEnumerable<ITask> successorTasks, IEnumerable<ITask> predecessorTasks, TimeSpan delay, int lineNumber, string originalLine, string directive)
    {
        var successorHash = new HashSet<ITask>(successorTasks);
        // the predecessorTasksList is filtered to exclude predecessors that are THEMSELVES part of the specified successors. this is so one can do something like: 
        //     A/1/1-5 <Coal> '!waiton A/1/1-5
        // if A/1/1-5 just contains both <Excavator> and <Coal> tasks, it is pointless to make them wait on their own completion as this is effectively a circular dependency
        var predecessorTasksHash = new HashSet<ITask>(predecessorTasks.Where(x => !successorHash.Contains(x)));
        SetWait(se, successorHash, predecessorTasksHash, delay, lineNumber, originalLine, directive);
    }

    public void SetDependFree(IEnumerable<ITask> successor, TimeSpan delay, string ruleName)
    {
        var successorHash = new HashSet<ITask>(successor);
        var predecessorList = successorHash.SelectMany(x => x.Predecessors).Select(x => x.Predecessor).Where(x => !successorHash.Contains(x)).ToList();

        foreach (var successorTask in successorHash)
        {
            if (successorTask.Predecessors.Any(x => successorHash.Contains(x.Predecessor)))
                continue;

            foreach (var predecessorTask in predecessorList)
                successorTask.Predecessors.Add(predecessorTask, delay, new InlineDependencyRule(ruleName, dummyAction));
        }
    }

    public void SetDependency(IEnumerable<ITask> predecessor, IEnumerable<ITask> successor, TimeSpan delay, string ruleName)
    {
        var predecessorList = predecessor as IList<ITask> ?? predecessor.ToList();
        foreach (var successorTask in successor)
            foreach (var predecessorTask in predecessorList)
                successorTask.Predecessors.Add(predecessorTask, delay, new InlineDependencyRule(ruleName, dummyAction));
    }

    public void SetDeferredDependencyClear(SchedulingEngine se, IEnumerable<ITask> predecessors, IEnumerable<ITask> successors, DateTime clearDate)
    {
        HashSet<ITask> predecessorHash = null;
        if (predecessors != null)
            predecessorHash = new HashSet<ITask>(predecessors);

        se.TimeKeeper.Add(clearDate, _ =>
        {
            IList<Dependency> toRemove = new List<Dependency>();
            foreach (var successor in successors)
            {
                foreach (var dependency in successor.Predecessors)
                    if (predecessorHash == null || predecessorHash.Contains(dependency.Predecessor))
                        toRemove.Add(dependency);
                foreach (var dependency in toRemove)
                    successor.Predecessors.Remove(dependency);
                toRemove.Clear();
            }
        });
    }

    public void SetDeferredPathClear(SchedulingEngine se, SchedulingEquipment eq, IEnumerable<SourceTask> tasks, IList<SchedulingSourcePathSteps> steps, IEnumerable<ITask> targetTasks, string taskEvent, DateTime? clearDate, TimeSpan taskEventDelay)
    {
        if (clearDate.HasValue)
            se.TimeKeeper.Add(clearDate.Value, _ =>
            {
                foreach (var step in steps)
                    eq.SourcePath.Remove(step);
            });

        if (targetTasks != null)
        {
            var waitCounter = new WaitCounter();

            Action RemovePath = () =>
            {
                if (taskEventDelay == TimeSpan.Zero)
                {
                    foreach (var step in steps)
                        eq.SourcePath.Remove(step);
                }
                else
                {
                    var removeTime = se.CurrentDate + taskEventDelay;
                    if (removeTime > se.EndDate)
                        removeTime = se.EndDate;
                    se.TimeKeeper.Add(removeTime, _ =>
                    {
                        foreach (var step in steps)
                            eq.SourcePath.Remove(step);
                    });
                }

            };

            Action<IEnumerable<ITask>> HandleTasks = ts =>
            {
                foreach (var task in ts)
                {
                    if (task.State == SchedulingTaskState.Complete)
                        continue;

                    var st = task as SourceTask;
                    var dt = task as DestinationTask;

                    waitCounter.Counter++;

                    if (st != null)
                        st.TaskCompleted += (sender, args) =>
                        {
                            waitCounter.Counter--;
                            waitCounter.LastDecremented = args.Engine.CurrentDate;

                            if (waitCounter.Counter <= 0)
                                RemovePath();
                        };
                    else if (dt != null)
                        dt.TaskCompleted += (sender, args) =>
                        {
                            waitCounter.Counter--;
                            waitCounter.LastDecremented = args.Engine.CurrentDate;

                            if (waitCounter.Counter <= 0)
                                RemovePath();
                        };
                }

                if (waitCounter.Counter == 0)
                    RemovePath();
            };

            if (taskEvent == "completed")
            {
                HandleTasks(targetTasks);
            }
            else if (taskEvent == "available")
            {
                var targetTasksHash = new HashSet<ITask>(targetTasks);
                var targetTasksPredecessor = targetTasksHash.SelectMany(x => x.Predecessors).Select(x => x.Predecessor).Where(x => !targetTasksHash.Contains(x)).ToList();
                HandleTasks(targetTasksPredecessor);
            }
            else
            {
                throw new Exception("Unknown clear mode (expect completed/available): " + taskEvent);
            }
        }
    }

    void ProcessInjectSchedulePath(SchedulingEquipment se, Command cmd, int lineNumber, string line, List<SchedulingSourcePathSteps> pathSteps, ref ISchedulingSourcePathElement elementAfter)
    {
        var case_arg = cmd.namedargs.GetValueOrDefault("case");
        if (string.IsNullOrWhiteSpace(case_arg))
            throw new Exception("No case specified");
        var cse = se.BaseEquipment.Project.Scenarios.GetCaseOrThrow(case_arg);

        IEnumerable<ScheduleStep> schedulesteps;
        var schedule_arg = cmd.namedargs.GetValueOrDefault("type");
        if (schedule_arg == null || EqualsIgnoreCase(schedule_arg, "output"))
            schedulesteps = cse.Schedule;
        else if (EqualsIgnoreCase(schedule_arg, "input"))
            schedulesteps = cse.InputSchedule;
        else
            throw new Exception("Unknown schedule type (should be input/output): " + schedule_arg);

        var start = ParseDate(cmd.namedargs.GetValueOrDefault("start"), DateTime.MinValue);
        var end = ParseDate(cmd.namedargs.GetValueOrDefault("end"), DateTime.MaxValue);

        Equipment sourceEquipment = null;
        var equipment_arg = cmd.namedargs.GetValueOrDefault("equipment");
        if (equipment_arg != null)
            sourceEquipment = cse.Equipment.GetEquipmentOrThrow(equipment_arg);
        else
            sourceEquipment = cse.Equipment.GetEquipmentOrThrow(se.BaseEquipment.FullName);

        bool ignoreDependencies = true;
        bool ignoreConstraints = true;
        var obey = MatchEnum(cmd.namedargs.GetValueOrDefault("obey"),
            "both", "all", null,
            "dep", null,
            "con", null
            );
		
		if (obey == null)
			ignoreDependencies = ignoreConstraints = true;
        else if (obey == "both")
            ignoreDependencies = ignoreConstraints = false;
        else if (obey == "dep")
            ignoreDependencies = false;
        else if (obey == "con")
            ignoreConstraints = false;
        else
            throw new Exception("Did not understand obey= arg. Value should be both/dep/con but was " + cmd.namedargs.GetValueOrDefault("obey"));

        foreach (var step in schedulesteps)
        {
            if (step.Equipment != sourceEquipment)
                continue;

            long overlapTicks = Math.Min(step.End.Ticks, end.Ticks) - Math.Max(step.Start.Ticks, start.Ticks);
            if (step.Duration.Ticks > 0 && overlapTicks <= 0)
                continue; // non instant completion step is out of the range
            if (step.Duration.Ticks == 0 && overlapTicks < 0)
                continue; // instant completion step is out of range

            double percentageOverlap = ((double)overlapTicks) / (step.End.Ticks - step.Start.Ticks);
            if (double.IsNaN(percentageOverlap))
                percentageOverlap = 1.0; // occurs for instantaneous tasks
            
            var process = se.BaseEquipment.Case.Processes.GetOrThrow(step.Process.Name);
            if (step.Process.Productive)
            {
                var task = se.Engine.SourceTasks[step.Source, process];

                var inject = new SchedulingSourcePathSteps() { IgnoreConstraints = ignoreConstraints, IgnoreDependencies = ignoreDependencies, AllowAdvance = false };
                inject.Add((SourceTask)task, PathValueType.Quantity, step.SourceQuantity * percentageOverlap, RateModifierType.Absolute, step.SourceHourlyRate, EquipmentCountModifierType.Absolute, step.EquipmentCount);

                if (elementAfter == null)
                    // null element means inject was first thing in path. add to start, rely on setting elementAfter 
                    se.SourcePath.AddFirst(inject);
                else
                {
                    // there is a bug and AddAfter actually performs an AddBefore
                    //se.SourcePath.AddAfter(elementAfter, inject);
                    if (elementAfter.Next == null)
                        se.SourcePath.AddLast(inject);
                    else
                        se.SourcePath.AddBefore(elementAfter.Next, inject);
                }
                elementAfter = inject;
            }
            else
            {
                TimeSpan durationOverlapped = TimeSpan.FromTicks((long)(step.Duration.Ticks * percentageOverlap));
                if (elementAfter == null)
                    // null element means inject was first thing in path. add to start, rely on setting elementAfter 
                    elementAfter = se.SourcePath.AddFirst(process, durationOverlapped);
                else
                {
                    //elementAfter = se.SourcePath.AddAfter(elementAfter, process, step.Duration);
                    if (elementAfter.Next == null)
                        elementAfter = se.SourcePath.AddLast(process, durationOverlapped);
                    else
                        elementAfter = se.SourcePath.AddBefore(elementAfter.Next, process, durationOverlapped);
                }
            }
        }
    }

    public static bool EqualsIgnoreCase(string a, string b)
    {
        return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
    }
}

static class Extension {
	public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key)
	{
	    return dict.GetValueOrDefault(key, default(V));
	}
	
	public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key, V defVal)
	{
	    return dict.GetValueOrDefault(key, () => defVal);
	}
	
	public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key, Func<V> defValSelector)
	{
	    V value;
	    return dict.TryGetValue(key, out value) ? value : defValSelector();
	}
}

// parsing
partial class EquipmentPathAnnotation
{
	public class Command
	{
		public string name;
		public string defaultarg;
		public Dictionary<string, string> namedargs;
	
		public struct Token {
			public int start;
			public int end;
			
			public static Token Make(int start, int end) {
				return new Token() { start = start, end = end };
			}
			
			public static Token Fail(ref int start, int x) {
				start = x;
				return new Token() { start = x, end = x };
			}
			
			public bool fail() {
				return start == end;
			}
		}
		
		Token FAIL = Token.Make(-1, -1);
	
		public static Command Parse(string line, ref int i)
		{
			var command = parse_command(line, ref i);
			
			if (command.fail()) {
				return null;
			}
			
			var default_arg = parse_defaultarg(line, ref i);
	
			Dictionary<string, string> namedArgs = new Dictionary<string, string>();
	
			for (var named_arg = parse_namedarg(line, ref i); !named_arg.Item1.fail(); named_arg = parse_namedarg(line, ref i))
			{
				if (!named_arg.Item1.fail()) {
					namedArgs = namedArgs ?? new Dictionary<string, string>();
					namedArgs[line.Substring(named_arg.Item1.start, named_arg.Item1.end - named_arg.Item1.start)] = line.Substring(named_arg.Item2.start, named_arg.Item2.end - named_arg.Item2.start);
				}
			}
			
			return new Command() { 
				name = line.Substring(command.start, command.end - command.start),
				defaultarg = !default_arg.fail() ? line.Substring(default_arg.start, default_arg.end - default_arg.start) : null,
				namedargs = namedArgs,
			};
		}
		
		public static IEnumerable<Command> Parse(string line)
		{
			for (int i = line.IndexOf('!'); i != -1; i = i+1 < line.Length ? line.IndexOf('!', i+1) : -1) 
			{
				int j = i;
				var cmd = Parse(line, ref j);
				if (cmd != null)
					yield return cmd;
			}
		}

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("!");
            sb.Append(name);
            if (defaultarg != null)
            {
                sb.Append(" ");
                sb.Append(defaultarg);
            }
            if (namedargs.Count > 0)
            {
                sb.Append("(");
                int count = 0;
                foreach (var kvp in namedargs)
                {
                    if (++count > 1)
                        sb.Append(",");
                    sb.Append(kvp.Key);
                    sb.Append("=");
                    sb.Append(kvp.Value);
                }
                sb.Append(")");
            }

            return sb.ToString();
        }

        public static Token parse_command(string s, ref int i)
		{
			int start = i;
			var excl = parse_char(s, ref i, '!');
			if (excl.fail())
				return Token.Make(start, start);
			var word = parse_word(s, ref i);
			if (word.fail())
				return Token.Make(start, start);
				
			//var eq = parse_char(s, ref i, '=');
			//if (!eq.fail())
			//	// if next token after the word is =, this cannot be a command
			//	return Token.Make(start, start);
			
			return word;
		}
		
		public static ValueTuple<Token, Token> parse_namedarg(string s, ref int i)
		{
			int start = i;
			var word = parse_word(s, ref i);
			if (word.fail())
				return ValueTuple.Create(Token.Make(start, start), Token.Make(start, start));
			var eq = parse_char(s, ref i, '=');
			if (eq.fail())
				return ValueTuple.Create(Token.Make(start, start), Token.Make(start, start));
	
			var value = parse_namedarg_value(s, ref i);
	
			return ValueTuple.Create(word, value);
		}
		
		public static Token parse_namedarg_value(string s, ref int i)
		{
			var start = i;
            var ws = parse_whitespace(s, ref i);
			for (; i < s.Length && s[i] != '!' && s[i] != '=' && s[i] != '\''; i++)
				;
			
			backparse(s, ws.end, ref i);

            if (ws.end == i)
            {   // no word like chars
                i = start;
            }
	
			return Token.Make(start, i);
		}
		
		public static Token parse_defaultarg(string s, ref int i) {
			var start = i;

            var ws = parse_whitespace(s, ref i);

			for (; i < s.Length && s[i] != '!' && s[i] != '=' && s[i] != '\''; i++)
				;
				
			backparse(s, ws.end, ref i);

            if (ws.end == i)
            {   // no word like chars can form default
                i = start;
                return Token.Make(start, start);
            }
	
			return Token.Make(ws.end, i);
		}
		
		// foo = bar
		//     ^ start here
		// ^ end here
		public static void backparse(string s, int start, ref int i) {
			if (i < s.Length && s[i] == '=')
			{
				int end = i;
				while (i > start && char.IsWhiteSpace(s[i-1]))
					i--;
					
				int end_word = i;
				
				while (i > start && is_word_char(s[i-1]))
					i--;
	
				int start_word = i;

                if (start_word == end_word)
					// couldn't find a word 
					i = end; 
			}

            while (i > start && char.IsWhiteSpace(s[i - 1]))
                i--;

        }

        public static Token parse_whitespace(string s, ref int i)
		{
			var start = i;
			for (; i < s.Length && char.IsWhiteSpace(s[i]); i++)
				;
				
				
			return Token.Make(start, i);
		}
		
		public static Token parse_char(string s, ref int i, char c) 
		{
			var ws = parse_whitespace(s, ref i);
			if (i < s.Length && s[i] == c)
				i++;
				
			return Token.Make(ws.end, i);
		}
	
		public static Token parse_word(string s, ref int i) 
		{
			var ws = parse_whitespace(s, ref i);
			int start = i;
			for (; i < s.Length && is_word_char(s[i]); i++)
				;
				
			return Token.Make(start, i);
		}

        public static bool is_word_char(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
	
	}

    public TimeSpan ParseTimeSpan(string value, TimeSpan? nullValue, string errorDesc = null)
    {
        if (value == null)
        {
            if (nullValue.HasValue)
                return nullValue.Value;
            else
                throw new Exception(string.Format("{0} Argument is missing and was expected (no default)", errorDesc));
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new Exception("Delay argument is blank");

        var unitIdx = -1;
        for (var i = 0; i < value.Length; i++)
            if (char.IsLetter(value[i]))
            {
                unitIdx = i;
                break;
            }
        if (unitIdx == -1)
            throw new Exception("No unit specified for time delay");

        double delayLength = 0;
        if (!double.TryParse(value.Substring(0, unitIdx), out delayLength))
            throw new Exception(string.Format("{0} cannot parse delay length ", errorDesc));

        var timeUnit = char.ToLowerInvariant(value[unitIdx]);
        if (timeUnit == 'm')
            return TimeSpan.FromMinutes(delayLength);
        else if (timeUnit == 'h')
            return TimeSpan.FromHours(delayLength);
        else if (timeUnit == 'd')
            return TimeSpan.FromDays(delayLength);
        else if (timeUnit == 'w')
            return TimeSpan.FromDays(delayLength * 7);
        else
            throw new Exception(string.Format("{0} unknown time unit: {1} - expected m/h/d/w", errorDesc, timeUnit));
    }

    public DateTime ParseDate(string value, DateTime? nullValue, string errorDesc = null)
    {
        if (value == null)
        {
            if (nullValue.HasValue)
                return nullValue.Value;
            else
                throw new Exception(string.Format("{0} Argument is missing and was expected (no default)", errorDesc));
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new Exception("Date argument is blank");

        DateTime ret;

        if (DateTime.TryParse(value, out ret))
            return ret;
        else
            throw new Exception("Cannot parse delay: " + value);
    }

    public string MatchEnum(string value, params string[] matches)
    {
        string returnValue = null;
        for (int i = 0; i <= matches.Length; i++)
        {
            if (returnValue == null)
                returnValue = matches[i];
            if (matches[i] == null)
                returnValue = null;

            if (string.Equals(value, matches[i], StringComparison.InvariantCultureIgnoreCase))
                return returnValue;
        }
        return value;
    }

    public Process ParseProcess(string processName, Case c, Process valueIfNull)
    {
        if (processName == null)
            return valueIfNull;

        var ret = c.Processes.Get(processName);
        if (ret == null)
            throw new Exception(string.Format("Couldn't get process named: {0}", processName));
        return ret;
    }


    private Regex processMatcher = new Regex("<((\\w+),?)*>"); // compiled regexpr used in GetTasks below. Designed to match <A, B, C> into capture groups of A, B, C
    // convert s:A/1/2 or d:A/1/2 or A/1/2 to a bunch of tasks. if there is no d: or s: prefix then we use the source arg
    private Regex processStarMatcher = new Regex("<\\w*\\*\\w*>");

    // task argument parsing can be complex. things to consider for tasks parsing:
    // - s: or d: prefix for source/destination
    // { and } for array expansion
    // < > for process filtering
    // limitations of access to parsers from scripting that can easily handle arrays and processes
    // %selfrange% interpolation - this is why the line is passed in
    public IEnumerable<ITask> ParseTasks(string tasks, string line)
    {
        if (tasks == null)
            return null;
        bool source;
        if (tasks.StartsWith("s:", StringComparison.InvariantCultureIgnoreCase))
            source = true;
        else if (tasks.StartsWith("d:", StringComparison.InvariantCultureIgnoreCase))
            source = false;
        else
            source = true;

        if (tasks.IndexOf("%selfrange%", StringComparison.InvariantCultureIgnoreCase) != -1)
        {
            // replaces the below:
            //    Alpha\S1\B1-B12 <Drill> (12%) ' !waiton %selfrange% <Blast>
            // with:
            //    Alpha\S1\B1-B12 <Drill> (12%) ' !waiton Alpha\S1\B1-B12 <Blast>
            // this currently deliberately interpolates for destination tasks as well. this generally not useful but might be valid for rare situations when the source and destination table have the same level/position structure.
            string selfRange;
            var rateOrPartial = line.IndexOfAny(new[] { '[', '(', '\'' });
            if (rateOrPartial == -1)
                selfRange = line;
            else
                selfRange = line.Substring(0, rateOrPartial);

            tasks = tasks.Replace("%selfrange%", selfRange);
        }

        if (source)
        {
            if (tasks.IndexOf('{') == -1)
            {
                // no arrays so can just use the TextSourceTasks parse (which doesn't support arrays)
                return new TextSourceTasks(tasks).BuildSourceTasks(Engine);
            }
            else
            {
                // arrays, so need to use Nodes.Range lookup which supports arrays
                // however, while it accepts < Process >, it returns nodes so effectively this does nothing and we must painfully filter processes manually
                var nodes = Engine.Case.SourceTable.Nodes.Range[tasks].Where(x => x.IsLeaf);

                var processMatch = processMatcher.Match(tasks); // capture < P1, P2, P3 > - or < * >
                if (processMatch.Success)
                {
                    var processNames = processMatch.Groups[2].Captures.Cast<Capture>().Select(c => c.Value);
                    List<SchedulingProcess> processes = new List<SchedulingProcess>();
                    foreach (var processName in processNames)
                    {
                        if (processName == "*") // should have been trimmed
                        {
                            processes = null;
                            break;
                        }

                        var process = Engine.Case.Processes[processName];
                        if (process == null)
                            throw new Exception("processName " + processName + " produced null (spelling?)");
                        var schedulingProcess = Engine.Processes[process];
                        if (schedulingProcess == null)
                            continue; // might be disabled, gracefully continue
                        processes.Add(schedulingProcess);
                    }

                    if (processes != null)  // if null, then was set to null above because matched *
                    {
                        return nodes.SelectMany(leaf => Engine.SourceTasks[leaf, processes]);
                    }
                    else
                    {
                        return nodes.SelectMany(leaf => Engine.SourceTasks[leaf]);
                    }
                }
            }
        }
        else if (Engine.Case.RunDestinationScheduling)
        {
            // is a destination task, and destination scheduling is on. If destination scheduling is off, tasks should be ignored
            if (tasks.IndexOf('{') == -1)
            {
                return new TextDestinationTasks(tasks).BuildDestinationTasks(Engine);
            }
            else
            {
                var nodes = Engine.Case.DestinationTable.Nodes.Range[tasks].Where(x => x.IsLeaf);
                return nodes.Select(leaf => Engine.DestinationTasks[leaf]);

            }
        }

        return null;
    }
}

// hook-in API
partial class EquipmentPathAnnotation
{
    public class ProcessLineEvent : EventArgs
    {
        public readonly SchedulingEquipment SchedulingEquipment;
        public readonly IList<ISchedulingSourcePathElement> PathElements;
        public readonly IList<SchedulingSourcePathSteps> PathSteps;
        public readonly IList<SchedulingSourcePathDelayTimeSpan> PathTimeDelay;
        public readonly IList<SchedulingSourcePathDelayDate> PathDateDelay;
        public readonly IList<Command> Commands;
        public readonly string Line;
        public readonly int LineNumber;

        public ProcessLineEvent(SchedulingEquipment schedulingEquipment, IList<ISchedulingSourcePathElement> pathElements, IList<SchedulingSourcePathSteps> pathSteps, IList<SchedulingSourcePathDelayTimeSpan> pathTimeDelay, IList<SchedulingSourcePathDelayDate> pathDateDelay, IList<Command> commands, string line, int lineNumber)
        {
            SchedulingEquipment = schedulingEquipment;
            PathElements = pathElements;
            PathSteps = pathSteps;
            PathTimeDelay = pathTimeDelay;
            PathDateDelay = pathDateDelay;
            Commands = commands;
            Line = line;
            LineNumber = lineNumber;
        }
    }

    public event EventHandler<ProcessLineEvent> ProcessLine;

    public event EventHandler SetupCompleted;
}