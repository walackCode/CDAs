// Path Macro v4
#region Using Directives
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrecisionMining.Common.Design.Data;
using PrecisionMining.Common.Math;
using PrecisionMining.Common.Units;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Util;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Design;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Haulage;
using PrecisionMining.Spry.Scenarios.Scheduling;
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.Util.OptionsForm;
using PrecisionMining.Spry.Scripting;
#endregion

public partial class PathMacro
{
    #region Set Up

    // The ranges folder that has the dig areas specified.
    const string RANGE_FOLDER = "Scheduling/Dig Areas";
    // currently not used
    const string DIG_AREA_FIELD = "Other/Dig Area";
    // The scenario to apply dependencies and constraints to.
    const string CASE_NAME = "Schedule";

    // Entries to add to dependencies within dig areas.
    // Each entry is given a description, an array of predecessor processes, an array of successor processors,
    // and a delay expression.
    static RangeDependencyEntry[] WithinAreaDepEntries(Case c)
    {
        return new[] {
            CreateEntry(c, "Load waits on Drill", new [] { "Drill" }, new [] { "Loading" }, "TimeSpanFromHours(48)"),
            CreateEntry(c, "Doze,Exc waits on Loading", new [] { "Loading" }, new [] { "Dozer Push", "Excavator" }, "TimeSpanFromHours(48)"),
            CreateEntry(c, "Exc waits on Doze", new [] { "Dozer Push" }, new [] { "Excavator" }, "TimeSpanFromHours(24)"),
            CreateEntry(c, "Wedge,Coal waits on Doze,Exc", new [] { "Dozer Push", "Excavator" }, new [] { "Wedge", "Coal" }, "TimeSpanFromHours(24)")
        };
    }

    // Entry to be added for dependencies between dig areas.
    // Used to control the predecessor and successor processes.
    static RangeDependencyEntry BetweenAreaDepEntry(Case c, double daysDelay)
    {
        return CreateEntry(c, "", new[] { "Excavator", "Coal" }, new[] { "Drill" }, "TimeSpanFromDays(" + daysDelay + ")");
    }

    // The processes to create proximity constraints between within a dig area.
    static string[] ProximityConstraintProcesses()
    {
        return new[] { "Dozer Push", "Excavator", "Wedge", "Coal" };
    }

    #endregion
    #region Logic

    public static void ExpandPathMacros(SchedulingEngine se)
    {
        // clear range names on table -- DISABLED FOR NOW
        //		foreach (var leaf in se.Case.SourceTable.Nodes.Leaves)
        //			leaf.Data.String[DIG_AREA_FIELD] = "";

        //		// Write the scheduling ranges to the table for help in deps
        //		foreach (var range in se.Case.SourceTable.Ranges.Folders.GetOrThrow(RANGE_FOLDER).AllRanges)
        //		{
        //			foreach (var leaf in range.CachedNodes.Where(x => x.IsLeaf))
        //				leaf.Data.String[DIG_AREA_FIELD] = range.Name;
        //		}

        // Alter source paths
        foreach (var eq in se.Equipment)
            ExpandEquipmentPath(eq.BaseEquipment);
    }

    public static void ExpandPathMacros()
    {
        var form = OptionsForm.Create("Expand Path Macros");
        var caseSelect = form.Options.AddCaseSelect("Case")
                .RestoreValue("PATH-MACRO-CASE", x => x.FullName, x => Case.GetOrThrow(x))
                .Validators.Add(x => x == null ? "Select case" : null);

        if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        foreach (var eq in caseSelect.Value.Equipment.AllEquipment)
            ExpandEquipmentPath(eq);
    }

    static void ExpandEquipmentPath(Equipment eq)
    {
        var path = ExpandPathMacros(eq.SourcePath, RANGE_FOLDER);
        //		Out.WriteLine(eq.FullName + " ----------------------------------------------------------------------------");
        //		Out.WriteLine(path);
        eq.SourcePath = path;
    }

    static string ExpandPathMacros(string path, string digAreaRangePath)
    {
        const string RANGE_MARKER = "range!(";
        const string PATH_MARKER = "path!(";

        var lines = new List<string>();

        using (StringReader rdr = new StringReader(path))
        {
            string line;
            while ((line = rdr.ReadLine()) != null)
                lines.Add(line);
        }

        int i = 0;
        while (i < lines.Count)
        {
            string line = lines[i];
            var slice = line.TrimStart();
            if (slice.StartsWith("'"))
            {
                slice = slice.Substring(1).TrimStart();
                if (slice.StartsWith(RANGE_MARKER))
                {
                    slice = slice.Substring(RANGE_MARKER.Length);
                    int closedParenIdx = IdxOfMatchedClosedParen(slice);
                    if (closedParenIdx >= 0)
                    {
                        string suffix = slice.Substring(closedParenIdx + 1).TrimEnd();
                        slice = slice.Substring(0, closedParenIdx).Trim();
                        var split = slice.Split(new[] { ',' }, 2);
                        string name = split[0].Trim();
                        string additionals = split.Length == 2 ? split[1].Trim() : "*";
                        string x = string.Format("Label:{0}; In Range:{1}/{0}; {2}; In Range: All; Label: ; {3}", name, digAreaRangePath, additionals, suffix);
                        i += 1;
                        if (i == lines.Count)
                            lines.Add("");
                        lines[i] = x;
                    }
                }
                else if (slice.StartsWith(PATH_MARKER))
                {
                    slice = slice.Substring(PATH_MARKER.Length);
                    int closedParenIdx = IdxOfMatchedClosedParen(slice);
                    if (closedParenIdx >= 0)
                    {
                        string suffix = slice.Substring(closedParenIdx + 1).TrimEnd();
                        slice = slice.Substring(0, closedParenIdx).Trim();
                        var split = slice.Split(new[] { ',' }, 2);
                        slice = split[0].Trim();
                        string xpath = "";
                        if (split.Length > 0)
                            xpath = split[1].Trim();
                        string x = string.Format("Label:{0}; {1}; Label: ; {2}", slice, xpath, suffix);
                        i += 1;
                        if (i == lines.Count)
                            lines.Add("");
                        lines[i] = x;
                    }
                }
            }

            i += 1;
        }

        return string.Join(Environment.NewLine, lines);
    }

    static int IdxOfMatchedClosedParen(string str)
    {
        int openedParens = 0;
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '(')
                openedParens += 1;
            else if (c == ')' && openedParens == 0)
                return i;
            else if (c == ')')
                openedParens -= 1;
        }
        return -1;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> BlastAreaNameToClipboard()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true));
        ret.SubMenu = MenuName;
        ret.Name = node => "Copy Blast Area Name";
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
            {
                var thread = new System.Threading.Thread(() => System.Windows.Forms.Clipboard.SetText(name));
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
            }
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> InlineRangeToClipboard()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true));
        ret.SubMenu = MenuName;
        ret.Name = node => "Copy Inline Range to Clipboard";
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
            {
                var thread = new System.Threading.Thread(() => System.Windows.Forms.Clipboard.SetText("In Range:" + RANGE_FOLDER + "/" + name + "; *"));
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
            }
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> CreateWithinBlastAreaDep()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true));
        ret.SubMenu = MenuName;
        ret.Name = node => "Create Dependency Within Dig Area";
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
                CreateAndAddBlastAreaDependency(Case.GetOrThrow(CASE_NAME), node.Table, name);
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> CreateWithinBlastAreaProxConstraint()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true));
        ret.SubMenu = MenuName;
        ret.Name = node => "Create Proximity Constraint Within Dig Area";
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
                CreateAndAddBlastAreaProximity(Case.GetOrThrow(CASE_NAME), node.Table, name);
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> SetBetweenBlastAreaDepPredecessor()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true));
        ret.SubMenu = MenuName;
        ret.Name = node => "Set as predecessor";
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
                PREDECESSOR_NAME = name;
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> CreateBetweenBlastAreaDep()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => !string.IsNullOrEmpty(BlastAreaName(node, true)) && !string.IsNullOrEmpty(PREDECESSOR_NAME);
        ret.SubMenu = MenuName;
        ret.Name = node => "Create dependency on " + PREDECESSOR_NAME;
        ret.Execute = node =>
        {
            string name = BlastAreaName(node, true);
            if (!string.IsNullOrEmpty(name))
                CreateAndAddBetweenBlastAreaDependency(Case.GetOrThrow(CASE_NAME), node.Table, name);
        };

        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<Node> FetchBlastAreaName()
    {
        var ret = new ContextMenuEntryPoint<Node>();
        ret.Enabled = node => true;
        ret.Visible = node => true;
        ret.SubMenu = MenuName;
        ret.Name = node => "Re-fetch Name";
        ret.Execute = node => BlastAreaName(node, false);

        return ret;
    }

    static string MenuName(Node node)
    {
        string name = BlastAreaName(node, true);
        if (string.IsNullOrEmpty(name))
            return "no blast area specified";
        else
            return "blast area: " + name;
    }

    static void CreateAndAddBlastAreaDependency(Case c, Table table, string name)
    {
        var range = table.Ranges.GetTextRange(RANGE_FOLDER + "/" + name);
        if (range == null)
            return;

        string dir = "Mining/Within Dig Area/";
        string depName = dir + name;
        var dep = c.DependencyRules.GetDependency(depName);
        if (dep != null)
            dep.Remove();

        var rangeDep = new RangeDependencyRule(name);

        rangeDep.DependencyRelationship = DependencyRelationship.SourceSource;
        rangeDep.UseRanges = true;
        rangeDep.SourcePredecessorCompositeRange.TableRange = range;
        rangeDep.SourceSuccessorCompositeRange.TableRange = range;

        foreach (var entry in WithinAreaDepEntries(c))
		{
			entry.PredecessorTextRange = range.RangeText;
			entry.SuccessorTextRange = range.RangeText;
            rangeDep.Entries.Add(entry);
		}

        c.DependencyRules.Folders.GetOrCreate(dir).DependencyRules.Add(rangeDep);
    }

    static void CreateAndAddBetweenBlastAreaDependency(Case c, Table table, string name)
    {
        if (string.IsNullOrEmpty(PREDECESSOR_NAME))
            return;
		var predecessorRange = table.Ranges.GetTextRange(RANGE_FOLDER + "/" + PREDECESSOR_NAME);
        var successorRange = table.Ranges.GetTextRange(RANGE_FOLDER + "/" + name);
        if (successorRange == null || predecessorRange == null)
            return;

        var form = OptionsForm.Create("Time Delay");
        var timeDelay = form.Options.AddSpinEdit<double>("Delay in days").SetFormat("0.0").SetValue(7).RestoreValue("DAYS_SAVE").SetRange(0, 1e7);
        if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        string dir = "Mining/Between Dig Area/";
		string depName = name + " on " + PREDECESSOR_NAME;
        string depPath = dir + depName;
        var dep = c.DependencyRules.GetDependency(depName);
        if (dep != null)
            dep.Remove();

		var rangeDep = new RangeDependencyRule(depName);
		
        rangeDep.DependencyRelationship = DependencyRelationship.SourceSource;
        rangeDep.UseRanges = true;
        rangeDep.SourcePredecessorCompositeRange.TableRange = predecessorRange;
        rangeDep.SourceSuccessorCompositeRange.TableRange = successorRange;

		var entry = BetweenAreaDepEntry(c, timeDelay.Value);
		entry.PredecessorTextRange = predecessorRange.RangeText;
		entry.SuccessorTextRange = successorRange.RangeText;
        rangeDep.Entries.Add(entry);
		
		c.DependencyRules.Folders.GetOrCreate(dir).DependencyRules.Add(rangeDep);
    }

    static RangeDependencyEntry CreateEntry(Case c, string name, string[] pred, string[] succ, string delayExpr)
    {
        var entry = new RangeDependencyEntry(name, true)
        {
            IncludeAllPredecessorProcesses = false,
            IncludeAllSuccessorProcesses = false,
            PredecessorTextRange = "*",
            SuccessorTextRange = "*",
            ReleaseDelayExpression = delayExpr
        };
        entry.PredecessorProcesses.Clear();
        foreach (var p in pred)
            entry.PredecessorProcesses.Add(c.Processes.GetOrThrow(p));
        entry.SuccessorProcesses.Clear();
        foreach (var p in succ)
            entry.SuccessorProcesses.Add(c.Processes.GetOrThrow(p));
        return entry;
    }

    static void CreateAndAddBlastAreaProximity(Case c, Table table, string name)
    {
        var range = table.Ranges.GetRange(RANGE_FOLDER + "/" + name);
        if (range == null)
            return;

        string dir = "Mining/Within Dig Area/";
        string depName = dir + name;
        var dep = c.Constraints.GetConstraint(depName);
        if (dep != null)
            dep.Remove();

        var proxCon = new ProximityConstraint(name);

        proxCon.ConstraintType = ConstraintType.Source;
        proxCon.SourceCompositeRange.TableRange = range;
        proxCon.IncludeAllProcesses = false;
        foreach (var process in ProximityConstraintProcesses())
            proxCon.Processes.Add(c.Processes.GetOrThrow(process));
        proxCon.IncludeAllEquipment = true;
        proxCon.GroupingExpression = "\"all\"";
        proxCon.ValueExpression = "1";
        proxCon.MaximumExpression = "1";
        proxCon.DistanceLimited = false;

        c.Constraints.Folders.GetOrCreate(dir).Constraints.Add(proxCon);
    }

    static string PREDECESSOR_NAME;
    static Dictionary<Node, string> CACHED = new Dictionary<Node, string>();

    static string BlastAreaName(Node node, bool useCache)
    {
        if (!useCache || !CACHED.ContainsKey(node))
        {
            var areas = node.Table.Ranges.Folders.Get(RANGE_FOLDER);
            if (areas == null)
                return null;

            Range range = null;
			
			foreach (var r in areas.AllRanges)
			{
				if (r.IsInRange(node))
				{
					range = range ?? r;
					Out.WriteLine(node.FullName + " is in " + r.FullName);
				}
			}
			
            if (range != null)
            {
                foreach (Node n in range.CachedNodes)
                    CACHED[n] = range.Name;
                return range.Name;
            }
            else
                return null;
        }
        else
        {
            return CACHED[node];
        }
    }
    #endregion
}