#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PrecisionMining.Common.UI.Design;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scripting;
using PrecisionMining.Spry.Util.OptionsForm;
using PrecisionMining.Common.Util;
using System.Drawing;

#endregion

//v2 add new chains into existing range dependency
public partial class FieldOffsetDependency
{

    #region Constants

    public const string DependencyName = "Field Offset Dependency";
    public const bool FIELDOFFSETDEBUG = true;

    public const string ReadMe =
        @"<i><b>Field Offset Dependency allows the use of fields to create groups and then build dependency relationships between them.</b>
</i>
Offset Options: In describing offsets, the number of offsets must match the number of fields selected. 

1. Single Entries - e.g. 0/0/0/1 are all single entry relationships 

2. Multiple Entries - these are denoted by the use of <b>','</b> e.g. 0/0/0/1,-1

3. All Entries - You either omit this field entirely, or use <b>'*'</b> to denote all values in this field.  e.g. 0/0/*/1

2. Relative Entries - require three specific tokens
a. <b>':'</b> - the beginning of a relative entry
b. <b>'<='</b> or <b>'=>'</b> - describes the link being setup with the assumption that the arrow points to the predecessor.
c. <b>'@'</b> - a relative index with @1 meaning first, @2 meaning second and so on. Furthermore @-1 means last, @-2 second last.
e.g. 0/0/0/1:@1=>@-1 - this builds dependencies from the last in this group is the predecessor to the first in the next.";

    #endregion

    #region Declarations

    public static NodeFieldGroupingRoot Root;

    #endregion

    #region Methods

    [CustomDesignAction]
    internal static CustomDesignAction CreateFieldOffsetDependency()
    {
        var customDesignAction = new CustomDesignAction("Create Field Offset Dependency", "", "Dependency", Array.Empty<Keys>());
        customDesignAction.Visible = DesignButtonVisibility.Animation;
        customDesignAction.SetupAction += (s, e) => { customDesignAction.SetSelectMode(SelectMode.SelectElements); };
        customDesignAction.OptionsForm += (s, e) =>
        {
            var form = OptionsForm.Create("test");
            var actionhelperOption = form.Options.AddTextLabel(ReadMe, true);

            var nameOption = form.Options.AddTextEdit("Name");
            nameOption.Value = "New Field Offset Dependency";
            nameOption.RestoreValue("FIELDOFFSETDEPENDENCYNAME");
            var rangeOption = SetupRangeOption("Range", form, customDesignAction.ActiveCase.SourceTable);
            var predecessorRangeOption = SetupRangeOption("Predecessor Range", form, customDesignAction.ActiveCase.SourceTable);
            var predecessorProcessesOption = SetupProcessOption("Predecessor Processes", form, customDesignAction.ActiveCase);
            var successorRangeOption = SetupRangeOption("Successor Range", form, customDesignAction.ActiveCase.SourceTable);
            var successorProcessesOption = SetupProcessOption("Successor Processes", form, customDesignAction.ActiveCase);

            var errors = new Errors();
            var fieldOption = form.Options.AddFieldSelect("Fields", true);
            var helperOption = form.Options.AddTextLabel("");
            fieldOption.Table = customDesignAction.ActiveCase.SourceTable;
            fieldOption.RestoreValue(string.Format("FIELDOFFSETDEPENDENCY {0}", customDesignAction.ActiveCase.FullName),
                x => fieldOption.Values == null ? "" : string.Join("\n", fieldOption.Values.Select(y => y.FullName)),
                y =>
                {
                    if (string.IsNullOrEmpty(y))
                        return null;
                    var names = y.Split('\n');
                    var fields = names.Select(name => fieldOption.Table.Schema.GetField(name)).Where(f => f != null).ToList();
                    fieldOption.Values = fields;
                    return null;
                }, true, true);
            var offsetOption = form.Options.AddTextEdit("Offset").RestoreValue("FIELDOFFSETDEPENDENCYOFFSET");

            var gapfillSearch = form.Options.AddCheckBox("Gapfill Search").RestoreValue("FIELDOFFSETDEPENDENCYGAPFILL");
            var useRangeForTablePositionsOptions = form.Options.AddCheckBox("Use Range For Table Positions").RestoreValue("FIELDOFFSETDEPENDENCYUSERANGEFORTABLEPOSITIONS");
            var useRangeForTableNodesOptions = form.Options.AddCheckBox("Use Range For Table Nodes").RestoreValue("FIELDOFFSETDEPENDENCYUSERANGEFORTABLENODES");
            var drawGroupOptions = form.Options.AddCheckBox("Draw Groups").SetValue(true).RestoreValue("FIELDOFFSETDEPENDENCYDRAWGROUPS");

            fieldOption.Validators.Add(x => !(fieldOption.Values == null || fieldOption.Values.Count() == 0), "Please select at least one grouping field");
            offsetOption.Validators.Add(value =>
            {
                if (fieldOption.Values == null || fieldOption.Values.Count() == 0)
                    return true;
                if (string.IsNullOrEmpty(value))
                    return false;
                return true;
            }, "No Offset");
            offsetOption.Validators.Add(value =>
            {
                if (fieldOption.Values == null || fieldOption.Values.Count() == 0)
                    return true;
                if (value == null)
                    return true;
                var split = value.Split('\\', '/');
                return split.Length == fieldOption.Values.Count();
            }, "levels do not match offset");

            offsetOption.Validators.Add(value =>
            {
                errors.Items.Clear();
                if (fieldOption.Values == null || fieldOption.Values.Count() == 0)
                    return "";
                if (value == null)
                    return "";
                var split = value.Split('\\', '/').ToList();
                if (split.Count != fieldOption.Values.Count())
                    return "";
                var offsetConnections = OffsetConnection.ParseOffsetsForConnections(split, errors);
                return errors.Count == 0 && offsetConnections.Count == split.Count ? "" : string.Join("\n", errors);
            });
            form.Validators.Add(() =>
            {
                if (errors.Any())
                    return "stringasdf";
                if (drawGroupOptions.Checked && !customDesignAction.GetTemporaryRenderables().Any())
                    return "Ranges have been set up in a way that no dependencies will result";
                return "";
            });

            rangeOption.ValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            predecessorRangeOption.ValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            successorRangeOption.ValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            drawGroupOptions.ValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            useRangeForTableNodesOptions.ValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            fieldOption.OptionValueChanged += (s1, e1) => UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            UpdateUI(customDesignAction, helperOption, fieldOption.Table, fieldOption.Values == null ? new List<Field>() : fieldOption.Values.ToList(), rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, useRangeForTablePositionsOptions.Checked, useRangeForTableNodesOptions.Checked, drawGroupOptions.Checked);
            e.OptionsForm = form;
        };
        customDesignAction.ApplyAction += (s, e) =>
        {
            var index = 0;
            var actionHelperOption = customDesignAction.ActionSettings[index++];
            var name = customDesignAction.ActionSettings[index++].Value as string;
            var rangeOption = customDesignAction.ActionSettings[index++] as RangeSelectOption;
            var predecessorRangeOption = customDesignAction.ActionSettings[index++] as RangeSelectOption;
            var predecessorProcessesOption = customDesignAction.ActionSettings[index++] as ProcessSelectOption;
            var successorRangeOption = customDesignAction.ActionSettings[index++] as RangeSelectOption;
            var successorProcessesOption = customDesignAction.ActionSettings[index++] as ProcessSelectOption;
            var fieldOption = customDesignAction.ActionSettings[index++] as FieldSelectOption;
            var fieldHelperOption = customDesignAction.ActionSettings[index++];
            var offsetText = customDesignAction.ActionSettings[index++].Value.ToString();
            var gapfill = (bool) customDesignAction.ActionSettings[index++].Value;
            var useRangeForTablePositions = (bool) customDesignAction.ActionSettings[index++].Value;
            var useRangeForTableNodes = (bool) customDesignAction.ActionSettings[index++].Value;

            var c = customDesignAction.ActiveCase;
            var fields = fieldOption.Values.ToList();
            var split = offsetText.Split('\\', '/').ToList();
            var errors = new Errors();
            CreateFieldOffsetDependency(Root, c, name, fields, split, gapfill, useRangeForTableNodes, rangeOption.Value, predecessorRangeOption.Value, successorRangeOption.Value, errors, useRangeForTablePositions, predecessorProcessesOption.Processes.ToList(), successorProcessesOption.Processes.ToList());
            if (errors.Any())
                MessageBox.Show(string.Format("{0}", string.Join("\n", errors)), "Creating Field Offset Dependency", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        return customDesignAction;
    }

    private static RangeSelectOption SetupRangeOption(string name, IOptionsForm form, Table activeCaseSourceTable)
    {
        var rangeOption = form.Options.AddRangeSelect(name);
        rangeOption.Table = activeCaseSourceTable;
        rangeOption.SetCanDeselect(true);
        rangeOption.RestoreValue(String.Format("FIELDOFFSETDEPENDENCY{0}", name.ToUpper()), x => x.FullName, x => rangeOption.Table.Ranges.GetRange(x), true, true);
        return rangeOption;
    }

    private static ProcessSelectOption SetupProcessOption(string name, IOptionsForm form, Case @case)
    {
        var processOption = form.Options.AddProcessSelect(name, true, true, false);
        processOption.SetCase(@case);
        processOption.IncludeAll = true;
        processOption.RestoreValues(String.Format("FIELDOFFSETDEPENDENCY{0}", name.ToUpper()),
            x => processOption.IncludeAll ? "< All >" : string.Join(",", processOption.Processes.Select(process => process.Name)),
            x =>
            {
                if (x.Equals("< All >"))
                {
                    processOption.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(process => @case.Processes.Get(process)).Where(process => process != null);
            });

        processOption.Validators.Add(x =>
        {
            if (!processOption.Processes.Any() && !processOption.IncludeAll)
                return "Please choose at least one process";
            return null;
        });
        return processOption;
    }

    private static void UpdateUI(CustomDesignAction customDesignAction, TextLabel helperOption, Table fieldOptionTable, List<Field> fieldOptionValues, Range rangeOptionValue, Range predecessorRangeOptionValue, Range successorRangeOptionValue, bool useRangeForTablePositions, bool useRangeForTableNodes, bool drawGroups)
    {
        if (fieldOptionValues == null || !fieldOptionValues.Any())
            helperOption.Value = "";
        else
            helperOption.Value = string.Join("\\", fieldOptionValues.Select(x => x.Name));

        Root = RecalculateRootGroups(customDesignAction, fieldOptionTable, fieldOptionValues, rangeOptionValue, predecessorRangeOptionValue, successorRangeOptionValue, useRangeForTablePositions, useRangeForTableNodes, drawGroups);
    }

    private static NodeFieldGroupingRoot RecalculateRootGroups(CustomDesignAction customDesignAction, Table table, List<Field> fieldOptionValues, Range range, Range predecessorRangeOptionValue, Range successorRangeOptionValue, bool useRangeForTablePositions, bool useRangeForTableNodes, bool drawGroups)
    {
        if (customDesignAction == null || table == null || fieldOptionValues == null || fieldOptionValues.Count == 0)
            return null;

        customDesignAction.ClearTemporaryRenderables();

        var fieldLevels = new FieldLevels(table, useRangeForTablePositions ? range : null);
        foreach (var field in fieldOptionValues)
            fieldLevels.Add(field);
        var root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FIELDOFFSETDEBUG, useRangeForTableNodes ? range : null);
        var colourIndex = 0;
        if (!drawGroups)
            return root;

        foreach (var leafGroup in root.GetLeafGroups())
        {
            var colour = Colors.GetUniqueColor(colourIndex++);
            foreach (var leaf in leafGroup.GetLeavesAsNodes())
            {
                if (range != null && !range.IsInRange(leaf))
                    continue;
                if (predecessorRangeOptionValue != null && successorRangeOptionValue != null && !predecessorRangeOptionValue.IsInRange(leaf) && !successorRangeOptionValue.IsInRange(leaf))
                    continue;
                var solid = leaf.Data.Solid[@"Imported\Solid"];
                if (solid == null)
                    continue;
                var triangleMesh = solid.TriangleMesh;
                if (triangleMesh == null)
                    continue;
                customDesignAction.AddTemporaryMesh(triangleMesh, colour, true, false, true, null);
            }
        }
        return root;
    }

    #endregion

    #region Constants 
 
    public static int[] AttributeSizes = {8, 11}; 
     
    #endregion 
 
    #region Methods 
 
    public static void Test() 
    { 
        var table = Table.GetOrThrow("X"); 
        var c = Case.GetOrThrow("X"); 
        var fields = new List<Field>() 
        { 
            //			table.Schema.GetFieldOrThrow(@"Imported\Attribute Fields\Level"), 
            //			table.Schema.GetFieldOrThrow(@"Imported\Attribute Fields\ActivityType"), 
        }; 
        var offset = new List<string>() 
        { 
            "-1", 
            "0" 
        }; 
 
        CreateFieldOffsetDependency(null, c, "TEST", fields, offset, false, false, null, null, null, new Errors()); 
    } 
 
    public static void RegenerateFieldOffsetDependency(RangeDependencyRule rangeDependencyRule, Errors errors) 
    { 
        var entries = rangeDependencyRule.Entries.Where(x => !x.Active).ToList(); 
        var inputStrings = rangeDependencyRule.Description.Split('|'); 
        if (!AttributeSizes.Contains(inputStrings.Length))
        { 
            errors.Add(new Error(string.Format("Description of Field Offset was expected to have either {0} inputs not {1}.", string.Join(" or ", AttributeSizes), inputStrings.Length))); 
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

        if (inputStrings.Length == 8)
        {
            ProcessRegenerationFieldOffsetDependencyFor8Attributes(table, rangeDependencyRule, index, inputStrings, entries, errors);
        } else if (inputStrings.Length == 11)
        {
            ProcessRegenerationFieldOffsetDependencyFor11Attributes(table, rangeDependencyRule, index, inputStrings, entries, errors);

        }
    }

    private static void ProcessRegenerationFieldOffsetDependencyFor11Attributes(Table table, RangeDependencyRule rangeDependencyRule, int index, string[] inputStrings, List<RangeDependencyEntry> entries, Errors errors)
    {
        var gapFill = false; 
        var useRangeForTableNodes = false; 
        var useRangeForTablePositions = false;
        var @case = rangeDependencyRule.Case;
        var rangeName = inputStrings[index++].Trim(); 
        var predecessorRangeName = inputStrings[index++].Trim(); 
        var predecessorProcessesName = inputStrings[index++].Trim(); 
        var successorRangeName = inputStrings[index++].Trim(); 
        var successorProcessesName = inputStrings[index++].Trim(); 
        var groupingFields = inputStrings[index++].Split(','); 
        var continuousSearchBack = inputStrings[index++].Trim(); 
        var rangeForPositionString = inputStrings[index++].Trim(); 
        var rangeForTableString = inputStrings[index++].Trim(); 
        var offsets = inputStrings[index++].Split('\\').ToList(); 
 
        var dependencyName = rangeDependencyRule.FullName; 
        Range range = null; 
        Range predecessorRange = null; 
        Range sucessorRange = null;
        List<Process> predecessorProcesses = null;
        List<Process> successorProcesses = null;
 
        if (!string.IsNullOrEmpty(rangeName)) 
        { 
            range = table.Ranges.GetRange(rangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
 
        if (!string.IsNullOrEmpty(predecessorRangeName)) 
        { 
            predecessorRange = table.Ranges.GetRange(predecessorRangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Predecessor Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
 
        if (!string.IsNullOrEmpty(successorRangeName)) 
        { 
            sucessorRange = table.Ranges.GetRange(successorRangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Successor Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
        
        if (!string.IsNullOrEmpty(predecessorProcessesName))
        {
            var processes = predecessorProcessesName.Split(',').Select(x => @case.Processes.Get(x));
            if (processes.Any(x => x != null))
            {
                errors.Add(new Error(string.Format("Could not find all predecessor processes '{0}'", predecessorProcessesName))); 
            } else if (!processes.Any())
            {
                errors.Add(new Error(string.Format("Could not find any predecessor processes '{0}'", predecessorProcessesName))); 
            }

            predecessorProcesses = processes.ToList();
        } 
        
        if (!string.IsNullOrEmpty(successorProcessesName))
        {
            var processes = successorProcessesName.Split(',').Select(x => @case.Processes.Get(x));
            if (processes.Any(x => x != null))
            {
                errors.Add(new Error(string.Format("Could not find all successor processes '{0}'", successorProcessesName))); 
            } else if (!processes.Any())
            {
                errors.Add(new Error(string.Format("Could not find any successor processes '{0}'", successorProcessesName))); 
            }

            successorProcesses = processes.ToList();
        } 

        if (groupingFields.Length != offsets.Count) 
        { 
            errors.Add(new Error(string.Format("Grouping Fields Length '{0}' did not match offsets '{1}'", inputStrings[1], inputStrings[4]))); 
            return; 
        } 
        if (!Boolean.TryParse(continuousSearchBack, out gapFill)) 
        { 
            errors.Add(new Error(string.Format("Could not parse '{0}' for GapFill Boolean.", continuousSearchBack))); 
            return; 
        } 
 
        if (!Boolean.TryParse(rangeForTableString, out useRangeForTableNodes)) 
        { 
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTableNodes Boolean ", rangeForTableString))); 
            return; 
        } 
        
        if (!Boolean.TryParse(rangeForPositionString, out useRangeForTablePositions)) 
        { 
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTablePositions Boolean ", rangeForPositionString))); 
            return; 
        } 
        
        var fields = groupingFields.Select(x => new { FullName = x, Field = table.Schema.GetField(x) }).ToList(); 
        if (fields.Any(x => x.Field == null)) 
        { 
            var nullFields = fields.Where(x => x.Field == null).Select(x => x.FullName); 
            errors.Add(new Error(string.Format("Could not find these {0} fields in Table '{1}'.", string.Join(", ", nullFields), table.Name))); 
            return; 
        } 
 
        if (errors.Any()) 
            return; 
		 
		//MessageBox.Show(string.Format("{0},{1},{2},{3},{4},{5}", rangeDependencyRule.FullName, range.FullName, predecessorRange.FullName, sucessorRange.FullName, string.Join("|",fields.Select(x => x.FullName)), string.Join("|",offsets.Select(x => x)))); 
        var rangeDependency = CreateFieldOffsetDependency(null, rangeDependencyRule.Case, dependencyName, fields.Select(x => x.Field).ToList(), offsets, gapFill, useRangeForTableNodes, range, predecessorRange, sucessorRange, errors, useRangeForTablePositions, predecessorProcesses, successorProcesses); 

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

    private static void ProcessRegenerationFieldOffsetDependencyFor8Attributes(Table table, RangeDependencyRule rangeDependencyRule, int index, string[] inputStrings, List<RangeDependencyEntry> entries, Errors errors)
    {
        var gapFill = false; 
        var useRangeForTableNodes = false; 
        var rangeName = inputStrings[index++].Trim(); 
        var predecessorRangeName = inputStrings[index++].Trim(); 
        var successorRangeName = inputStrings[index++].Trim(); 
        var groupingFields = inputStrings[index++].Split(','); 
        var continuousSearchBack = inputStrings[index++].Trim(); 
        var rangeForTableString = inputStrings[index++].Trim(); 
        var offsets = inputStrings[index++].Split('\\').ToList(); 
 
        var dependencyName = rangeDependencyRule.FullName; 
        Range range = null; 
        Range predecessorRange = null; 
        Range sucessorRange = null; 
 
        if (!string.IsNullOrEmpty(rangeName)) 
        { 
            range = table.Ranges.GetRange(rangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
 
        if (!string.IsNullOrEmpty(predecessorRangeName)) 
        { 
            predecessorRange = table.Ranges.GetRange(predecessorRangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Predecessor Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
 
        if (!string.IsNullOrEmpty(successorRangeName)) 
        { 
            sucessorRange = table.Ranges.GetRange(successorRangeName); 
            if (range == null) 
            { 
                errors.Add(new Error(string.Format("Could not find Successor Range '{0}' in Table {1}", rangeName, table.Name))); 
                return; 
            } 
        } 
 
        if (groupingFields.Length != offsets.Count) 
        { 
            errors.Add(new Error(string.Format("Grouping Fields Length '{0}' did not match offsets '{1}'", inputStrings[1], inputStrings[4]))); 
            return; 
        } 
        if (!Boolean.TryParse(continuousSearchBack, out gapFill)) 
        { 
            errors.Add(new Error(string.Format("Could not parse '{0}' for GapFill Boolean.", continuousSearchBack))); 
            return; 
        } 
 
        if (!Boolean.TryParse(rangeForTableString, out useRangeForTableNodes)) 
        { 
            errors.Add(new Error(string.Format("Could not parse '{0}' for UseRangeForTableNodes Boolean ", rangeForTableString))); 
            return; 
        } 
        var fields = groupingFields.Select(x => new { FullName = x, Field = table.Schema.GetField(x) }).ToList(); 
        if (fields.Any(x => x.Field == null)) 
        { 
            var nullFields = fields.Where(x => x.Field == null).Select(x => x.FullName); 
            errors.Add(new Error(string.Format("Could not find these {0} fields in Table '{1}'.", string.Join(", ", nullFields), table.Name))); 
            return; 
        } 
 
        if (errors.Any()) 
            return; 
		 
		//MessageBox.Show(string.Format("{0},{1},{2},{3},{4},{5}", rangeDependencyRule.FullName, range.FullName, predecessorRange.FullName, sucessorRange.FullName, string.Join("|",fields.Select(x => x.FullName)), string.Join("|",offsets.Select(x => x)))); 
        var rangeDependency = CreateFieldOffsetDependency(null, rangeDependencyRule.Case, dependencyName, fields.Select(x => x.Field).ToList(), offsets, gapFill, useRangeForTableNodes, range, predecessorRange, sucessorRange, errors); 

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

    public static RangeDependencyRule CreateFieldOffsetDependency(NodeFieldGroupingRoot root, Case scenario, string dependencyName, List<Field> groupingFields, List<string> offsets, bool continuousSearchBack, bool useRangeForTableNodes, Range range, Range predecessorRange, Range successorRange, Errors errors, bool useRangeForTablePositions = false, List<Process> predecessorProcesses = null, List<Process> successorProcesses = null) 
    { 
        var tryParseInts = offsets.Select(s => 
        { 
            int n; 
            if (int.TryParse(s, out n)) 
                return n; 
            return (int?) null; 
        }).ToList(); 
        var table = scenario.SourceTable; 
        var description = string.Join("|", DependencyName, range == null ? "" : range.Name, predecessorRange == null ? "" : predecessorRange.Name, successorRange == null ? "" : successorRange.Name, string.Join(", ", groupingFields.Select(x => x.FullName)), continuousSearchBack, useRangeForTableNodes, string.Join("\\", offsets), useRangeForTablePositions, predecessorProcesses == null ? "" : string.Join(", ", predecessorProcesses), successorProcesses == null ? "" : string.Join(", ", successorProcesses)); 
        var rangeDependency = SetupRangeDependency(scenario, dependencyName, range, predecessorRange, successorRange, description); 
        // if (tryParseInts.All(x => x.HasValue)) 
            // return CreateFieldOffsetIntegerDependency(root, rangeDependency, table, groupingFields, tryParseInts.Select(x => x.Value).ToList(), predecessorProcesses, successorProcesses, continuousSearchBack, useRangeForTablePositions, useRangeForTableNodes, range, errors); 
        // else 
            return CreateFieldOffsetStringDependency(root, rangeDependency, table, groupingFields, offsets, predecessorProcesses, successorProcesses, continuousSearchBack, useRangeForTablePositions, useRangeForTableNodes, range, errors); 
    } 
    
    public static RangeDependencyRule CreateFieldOffsetStringDependency(NodeFieldGroupingRoot root, RangeDependencyRule rangeDependency, Table table, List<Field> groupingFields, List<string> offsets, List<Process> predecessorProcesses, List<Process> successorProcesses, bool continuousSearchBack, bool useRangeForTablePositions, bool useRangeForTableNodes, Range range, Errors errors) 
    { 
        if (root == null) 
        { 
            var fieldLevels = new FieldLevels(table, useRangeForTablePositions ? range : null); 
            foreach (var field in groupingFields) 
                fieldLevels.Add(field); 
            root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FIELDOFFSETDEBUG, useRangeForTableNodes ? range : null); 
        } 
        var leafGroups = root.GetLeafGroups(); 
 
        var parsingErrors = new Errors(); 
        var offsetConnections = OffsetConnection.ParseOffsetsForConnections(offsets, parsingErrors); 
        if (parsingErrors.Count > 0 || offsetConnections.Count != offsets.Count) 
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
            if (offsetGroups == null || !offsetGroups.Any() || offsetGroups.Any(x => x == null)) 
                continue;
            if (leafPredecessorGroups == null|| !leafPredecessorGroups.Any()  || leafPredecessorGroups.Any(x => x == null)) 
                continue; 
            var entry = new RangeDependencyEntry(string.Format("Entry {0}", rangeDependency.Entries.Count + 1));
            SetupEntryProcesses(rangeDependency.Case, entry, predecessorProcesses, successorProcesses);
            entry.PredecessorTextRange = string.Join("\n", offsetGroups.SelectMany(x => x.GetLeavesAsNodes().Select(y => y.FullName)));
            entry.SuccessorTextRange = string.Join("\n", leafPredecessorGroups.SelectMany(x => x.GetLeavesAsNodes().Select(y => y.FullName))); 
            rangeDependency.Entries.Add(entry); 
        } 
 
        return rangeDependency; 
    }

    private static void SetupEntryProcesses(Case @case, RangeDependencyEntry entry, List<Process> predecessorProcesses, List<Process> successorProcesses)
    {
        entry.IncludeAllPredecessorProcesses = predecessorProcesses == null;
        var activeProcesses = @case.Processes.Where(x => x.Active && x.Productive).ToList();
        if (predecessorProcesses != null)
        {
            if (predecessorProcesses.Count == activeProcesses.Count)
                entry.IncludeAllPredecessorProcesses = true;
            else
            {
                entry.PredecessorProcesses.Clear();
                foreach (var process in predecessorProcesses)
                    entry.PredecessorProcesses.Add(process);
            }
        }
        entry.IncludeAllSuccessorProcesses = successorProcesses == null;
        if (successorProcesses != null)
        {
            if (successorProcesses.Count == activeProcesses.Count)
                entry.IncludeAllPredecessorProcesses = true;
            else
            {
                entry.SuccessorProcesses.Clear();
                foreach (var process in successorProcesses)
                    entry.SuccessorProcesses.Add(process);
            }
        }

    }

    public static RangeDependencyRule CreateFieldOffsetIntegerDependency(NodeFieldGroupingRoot root, RangeDependencyRule rangeDependency, Table table, List<Field> groupingFields, List<int> offsets, List<Process> predecessorProcesses, List<Process>successorProcesses, bool continuousSearchBack, bool useRangeForTablePositions, bool useRangeForTableNodes, Range range, Errors errors) 
    { 
        if (root == null) 
        { 
            var fieldLevels = new FieldLevels(table, useRangeForTablePositions ? range : null); 
            foreach (var field in groupingFields) 
                fieldLevels.Add(field); 
            root = NodeFieldGroupingBuilder.BuildTree(fieldLevels, FIELDOFFSETDEBUG, useRangeForTableNodes ? range : null); 
        } 
        var leafGroups = root.GetLeafGroups(); 
        foreach (var leafGroup in leafGroups) 
        { 
            var offsetGroup = continuousSearchBack ? root.GetGapfillOffset(leafGroup, offsets) : root.GetOffset(leafGroup, offsets); 
			if (offsetGroup == null) 
                continue; 
            var entry = new RangeDependencyEntry(string.Format("Entry {0}", rangeDependency.Entries.Count + 1)); 
            SetupEntryProcesses(rangeDependency.Case, entry, predecessorProcesses, successorProcesses);
            entry.PredecessorTextRange = string.Join("\n", offsetGroup.GetLeavesAsNodes().Select(x => x.FullName)); 
            entry.SuccessorTextRange = string.Join("\n", leafGroup.GetLeavesAsNodes().Select(x => x.FullName));		 
            rangeDependency.Entries.Add(entry); 
        } 
        return rangeDependency; 
    } 
 
    private static RangeDependencyRule SetupRangeDependency(Case scenario, string dependencyName, Range range, Range predecessorRange, Range successorRange, string description) 
    { 
        var rangeDependency = scenario.DependencyRules.DependencyRules.Get(dependencyName) as RangeDependencyRule; 
        if (rangeDependency == null) 
        { 
            //rangeDependency = new RangeDependencyRule(dependencyName); 
            //scenario.DependencyRules.DependencyRules.Add(rangeDependency); 
			rangeDependency = RangeDependencyUtil.GetDependency(scenario, dependencyName, true);

        } 
 
        while (rangeDependency.Entries.Count > 0) 
            rangeDependency.Entries.RemoveAt(0); 
 
        rangeDependency.Description = description; 
        rangeDependency.SourcePredecessorCompositeRange.Set(RangeMode.Table, predecessorRange ?? range, "", false); 
        rangeDependency.SourceSuccessorCompositeRange.Set(RangeMode.Table, successorRange ?? range, "", false); 
        rangeDependency.UseRanges = true; 
        return rangeDependency; 
    } 
 
    #endregion 
 
}

public class FieldLevel
{

    #region Declarations

    public Field Field { get; private set; }
    public FieldLevels FieldLevels { get; private set; }
    public FieldPositions Positions { get; private set; }

    #endregion

    #region Constructors

    public FieldLevel(FieldLevels fieldLevels, Field field)
    {
        FieldLevels = fieldLevels;
        Field = field;
        Positions = new FieldPositions(this);
        Positions.GeneratePositionsFromNodeData();
    }

    #endregion

    #region Properties

    public Table Table { get { return FieldLevels.Table; } }

    public int Index { get { return FieldLevels.Levels.IndexOf(this); } }

    #endregion

}

public class FieldPosition
{

    #region Declarations

    public FieldPositions FieldPositions { get; set; }
    public string PositionValue { get; set; }

    #endregion

    #region Properties

    public Table Table { get { return FieldPositions.Table; } }

    public Field Field { get { return FieldPositions.FieldLevel.Field; } }

    public int Index { get { return FieldPositions.Positions.IndexOf(this); } }

    #endregion

}

public class FieldLevels
{

    #region Declarations

    public Table Table { get; private set; }
    public List<FieldLevel> Levels { get; private set; }
    public Range Range { get; private set; }

    #endregion

    #region Constructors

    public FieldLevels(Table table, Range range)
    {
        Table = table;
        Levels = new List<FieldLevel>();
        Range = range;
    }

    #endregion

    #region Properties

    public FieldLevel this[int i] { get { return Levels[i]; } }

    #endregion

    #region Methods

    public FieldLevel Add(Field field)
    {
        var fieldLevel = new FieldLevel(this, field);
        Levels.Add(fieldLevel);
        return fieldLevel;
    }

    public FieldLevel Add(string fieldPath)
    {
        var field = Table.Schema.GetFieldOrThrow(fieldPath);
        return Add(field);
    }

    public int Count()
    {
        return Levels.Count;
    }

    #endregion

}

public class FieldPositions
{

    #region Declarations

    public FieldLevel FieldLevel { get; set; }
    public Dictionary<string, FieldPosition> PositionLookup { get; private set; }
    public List<FieldPosition> Positions { get; private set; }

    #endregion

    #region Constructors

    public FieldPositions(FieldLevel level)
    {
        FieldLevel = level;
        PositionLookup = new Dictionary<string, FieldPosition>();
        Positions = new List<FieldPosition>();
    }

    #endregion

    #region Properties

    public Table Table { get { return FieldLevel.Table; } }

    public FieldPosition this[string o]
    {
        get
        {
            FieldPosition result;
            if (PositionLookup.TryGetValue(o, out result))
                return result;
            double doubleValue;
            return double.TryParse(o, out doubleValue) && PositionLookup.TryGetValue(doubleValue.ToString(), out result) ? result : null;
        }
    }

    public FieldPosition this[int i] { get { return Positions[i]; } }

    #endregion

    #region Methods

    public void GeneratePositionsFromNodeData()
    {
        Positions.Clear();
        var range = this.FieldLevel.FieldLevels.Range;
        var nodes = range == null ? Table.Nodes.Leaves : Table.Nodes.Leaves.Where(x => range.IsInRange(x));
        var objectValues = new HashSet<object>();
        var doubleStringDictionary = new Dictionary<double, object>();
        var allDoublesUnique = true;
        var allDoublesParseable = true;

        foreach (var node in nodes)
        {
            var value = node.Data[FieldLevel.Field];
            if (value == null)
                continue;
            objectValues.Add(value);
            double doubleValue = 0;
            if (allDoublesParseable && double.TryParse(value.ToString(), out doubleValue))
            {
                object objectResult = null;
                if (allDoublesUnique)
                {
                    if (doubleStringDictionary.TryGetValue(doubleValue, out objectResult))
                    {
                        if (!objectResult.Equals(value))
                            allDoublesUnique = false;
                    }
                    else
                        doubleStringDictionary.Add(doubleValue, value);
                }
            }
            else
                allDoublesParseable = false;
        }

        var values = allDoublesParseable && allDoublesUnique ? doubleStringDictionary.Keys.Select(x => (object) x).ToHashSet() : objectValues;
        foreach (var value in values.OrderBy(x => x))
        {
            var position = new FieldPosition() { FieldPositions = this, PositionValue = value.ToString() };
            Positions.Add(position);
            PositionLookup.Add(value.ToString(), position);
        }
    }

    public bool Contains(string key)
    {
        return PositionLookup.ContainsKey(key);
    }

    #endregion

}

public class NodeFieldGroupingBuilder
{

    #region Methods

    public static NodeFieldGroupingRoot BuildTree(FieldLevels levels, bool debug = false, Range range = null)
    {
        var rootNode = new NodeFieldGroupingRoot() { Table = levels.Table, FieldLevels = levels };
        var leaves = levels.Table.Nodes.Leaves.ToList();
        if (range != null)
            leaves = leaves.Where(x => range.IsInRange(x)).ToList();
        rootNode.Children = BuildTreeRecursive(levels.Levels.First(), leaves, rootNode);
        if (debug)
            rootNode.DebugFieldLevelsAndPositions();
        return rootNode;
    }

    public static Dictionary<FieldPosition, NodeFieldGrouping> BuildTreeRecursive(FieldLevel level, List<Node> nodes, NodeFieldGroupContainer parent)
    {
        var newNodes = new Dictionary<FieldPosition, List<Node>>();

        foreach (var node in nodes)
        {
            var value = node.Data[level.Field];
            if (value == null)
                continue;
            var position = level.Positions[value.ToString()];
            if (!newNodes.ContainsKey(position))
                newNodes.Add(position, new List<Node>());
            newNodes[position].Add(node);
        }

        var indexOfLevel = level.FieldLevels.Levels.IndexOf(level);
        var nextIndex = indexOfLevel + 1;
        var output = new Dictionary<FieldPosition, NodeFieldGrouping>();
        if (nextIndex == level.FieldLevels.Levels.Count)
        {
            foreach (var pair in newNodes)
            {
                var leaf = new NodeFieldGroupingLeaf() { Table = level.Table, FieldLevel = level, Position = pair.Key, Parent = parent };
                leaf.Leaves.AddRange(pair.Value.Select(x=> new NodeFieldLeaf(x)));
                output.Add(pair.Key, leaf);
            }
        }
        else
        {
            var nextLevel = level.FieldLevels.Levels[nextIndex];
            foreach (var pair in newNodes)
            {
                var newGrouping = new NodeFieldGrouping() { Table = level.Table, FieldLevel = level, Position = pair.Key, Parent = parent };
                newGrouping.Children = BuildTreeRecursive(nextLevel, pair.Value, newGrouping);
                output.Add(pair.Key, newGrouping);
            }
        }
        return output;
    }

    #endregion

}

public class NodeFieldGroupContainer
{

    #region Declarations

    public Dictionary<FieldPosition, NodeFieldGrouping> Children { get; set; }
    public NodeFieldGroupContainer Parent { get; set; }

    #endregion

    #region Methods

    public virtual List<Node> GetLeavesAsNodes()
    {
        var output = new List<Node>();
        foreach (var child in Children)
            output.AddRange(child.Value.GetLeavesAsNodes());
        return output;
    }
    public virtual List<NodeFieldGroupingLeaf> GetLeafGroups()
    {
        var output = new List<NodeFieldGroupingLeaf>();
        foreach (var child in Children)
            output.AddRange(child.Value.GetLeafGroups());
        return output;
    }

    #endregion

    public  virtual List<Node> GetFirstLeaves(Queue<OffsetConnection> offsetConnections)
    {
        var output = new List<Node>();
        foreach (var child in GetFirstChildren(Children, offsetConnections.Dequeue()))
            output.AddRange(child.Value.GetFirstLeaves(offsetConnections));
        return output;
    }

    private IEnumerable<KeyValuePair<FieldPosition, NodeFieldGrouping>> GetFirstChildren(Dictionary<FieldPosition, NodeFieldGrouping> nodeFieldGroupings, OffsetConnection dequeue)
    {
        if (dequeue.Type == OffsetConnectionType.Direct)
        {
            foreach (var child in nodeFieldGroupings)
                yield return child;
        }
        else
            yield return nodeFieldGroupings.First();
    }
    
    public  virtual List<Node> GetLastLeaves(Queue<OffsetConnection> offsetConnections)
    {
        var output = new List<Node>();
        foreach (var child in GetLastChildren(Children, offsetConnections.Dequeue()))
            output.AddRange(child.Value.GetLastLeaves(offsetConnections));
        return output;
    }

    private IEnumerable<KeyValuePair<FieldPosition, NodeFieldGrouping>> GetLastChildren(Dictionary<FieldPosition, NodeFieldGrouping> nodeFieldGroupings, OffsetConnection dequeue)
    {
        if (dequeue.Type == OffsetConnectionType.Direct)
        {
            foreach (var child in nodeFieldGroupings)
                yield return child;
        }
        else
            yield return nodeFieldGroupings.Last();
    }
}

public class TableGenerator
{

    #region Methods

    public static void GenerateTable(List<List<string>> tableData)
    {
        var columnCount = tableData.Count > 0 ? tableData[0].Count : 0;
        var columnWidths = new int[columnCount];
        foreach (var rowData in tableData)
        {
            for (var i = 0; i < columnCount; i++)
            {
                if (i >= rowData.Count)
                    continue;
                var contentWidth = rowData[i].Length;
                if (contentWidth > columnWidths[i])
                    columnWidths[i] = contentWidth;
            }
        }

        foreach (var rowData in tableData)
        {
            var formattedValues = new List<string>();
            for (var i = 0; i < columnCount; i++)
            {
                var value = i < rowData.Count ? rowData[i] : string.Empty;
                var formattedValue = value.PadRight(columnWidths[i]);
                formattedValues.Add(formattedValue);
            }
            Console.WriteLine(string.Join("\t", formattedValues));
        }
    }

    #endregion

}

public class NodeFieldGroupingRoot : NodeFieldGroupContainer
{

    #region Declarations

    public Table Table { get; set; }
    public FieldLevels FieldLevels { get; set; }

    #endregion

    #region Methods

    public void DebugFieldLevelsAndPositions()
    {
        var maxPositions = FieldLevels.Levels.Max(x => x.Positions.Positions.Count);
        var tableData = new List<List<string>>();
        tableData.Add(FieldLevels.Levels.Select(x => x.Field.Name).ToList());
        for (var i = 0; i < maxPositions; i++)
            tableData.Add(FieldLevels.Levels.Select(x => { return i < x.Positions.Positions.Count ? x.Positions.Positions[i].PositionValue : ""; }).ToList());

        TableGenerator.GenerateTable(tableData);
    }

    #endregion
    
}

public class NodeFieldGrouping : NodeFieldGroupContainer
{

    #region Declarations

    public Table Table { get; set; }
    public FieldLevel FieldLevel { get; set; }
    public FieldPosition Position { get; set; }

    #endregion

}

public class NodeFieldGroupingLeaf : NodeFieldGrouping
{

    #region Declarations

    public List<NodeFieldLeaf> Leaves { get; private set; }

    #endregion

    #region Constructors

    public NodeFieldGroupingLeaf()
    {
        Leaves = new List<NodeFieldLeaf>();
    }

    #endregion

    #region Methods

    public override List<Node> GetLeavesAsNodes()
    {
        return Leaves.Select(x=>x.Node).ToList();
    }
    
    public override List<NodeFieldGroupingLeaf> GetLeafGroups()
    {
        return new List<NodeFieldGroupingLeaf>() { this };
    }

    #endregion

}

public class NodeFieldLeaf : NodeFieldGrouping
{

    public Node Node  { get; private set; }
    public NodeFieldLeaf(Node node)
    {
        Node = node;
    }
    
    public override List<Node> GetLeavesAsNodes()
    {
        return new List<Node> { Node };
    }

}

public static class NodeFieldGroupingHelperFunctions
{

    #region Methods

    public static List<Node> GetNodes(this NodeFieldGroupingRoot root, List<List<string>> positionGroups)
    {
        var levels = root.FieldLevels;
        if (positionGroups.Count > levels.Count())
            throw new Exception("More Position Groups than Levels");
        var currentNodeGroupings = new List<NodeFieldGroupContainer>();
        currentNodeGroupings.Add(root);
        var nextNodeGroupings = new List<NodeFieldGroupContainer>();
        for (var i = 0; i < positionGroups.Count; i++)
        {
            var level = levels[i];
            var positionValues = positionGroups[i];
            var positions = positionValues.Where(x => level.Positions.Contains(x)).Select(x => level.Positions[x]);
            foreach (var nodeGroup in currentNodeGroupings)
            {
                foreach (var position in positions)
                {
                    if (nodeGroup.Children.ContainsKey(position))
                        nextNodeGroupings.Add(nodeGroup.Children[position]);
                }
            }
            currentNodeGroupings.Clear();
            currentNodeGroupings.AddRange(nextNodeGroupings);
            nextNodeGroupings.Clear();
        }
        return currentNodeGroupings.SelectMany(x => x.GetLeavesAsNodes()).ToList();
    }

    public static NodeFieldGrouping GetNode(this NodeFieldGroupingRoot root, List<string> path)
    {
        var levels = root.FieldLevels;
        if (path.Count > levels.Count())
            throw new Exception("More Path than Levels");
        var currentNodeGroupings = new List<NodeFieldGroupContainer>();
        currentNodeGroupings.Add(root);
        var nextNodeGroupings = new List<NodeFieldGroupContainer>();
        for (var i = 0; i < path.Count; i++)
        {
            var level = levels[i];
            var pathValue = path[i];
            var positions = new List<FieldPosition>();
            if (pathValue == null)
                throw new Exception("position does not exist " + pathValue);
            else
            {
                var position = level.Positions[pathValue];
                if (position == null)
                    throw new Exception("position does not exist " + pathValue);
                positions.Add(position);
            }

            foreach (var nodeGroup in currentNodeGroupings)
            {
                foreach (var position in positions)
                {
                    if (nodeGroup.Children.ContainsKey(position))
                        nextNodeGroupings.Add(nodeGroup.Children[position]);
                }
            }
            currentNodeGroupings.Clear();
            currentNodeGroupings.AddRange(nextNodeGroupings);
            nextNodeGroupings.Clear();
        }
        return currentNodeGroupings.Single() as NodeFieldGrouping;
    }

    public static NodeFieldGroupContainer GetOffset(this NodeFieldGroupingRoot root, NodeFieldGrouping item, List<int> offsets)
    {
        var itemLevel = item.FieldLevel;
        var index = itemLevel.Index;
        if (offsets.Count != index + 1)
            throw new Exception("not enough offset positions");

        var currentLevelStructure = new List<FieldPosition>();
        var node = item;
        while (node != null)
        {
            currentLevelStructure.Insert(0, node.Position);
            node = node.Parent as NodeFieldGrouping;
        }

        var positions = new List<FieldPosition>();
        for (var i = 0; i < offsets.Count; i++)
        {
            var level = root.FieldLevels[i];
            var offset = offsets[i];
            var currentPosition = currentLevelStructure[i];

            var levelPositions = new List<FieldPosition>();

            var currentIndex = currentPosition.Index;
            var nextIndex = currentIndex + offset;
            if (nextIndex < 0 || nextIndex >= level.Positions.Positions.Count())
                return null;
            var nextPosition = level.Positions[nextIndex];

            positions.Add(nextPosition);
        }

        NodeFieldGroupContainer output = root;
        foreach (var position in positions)
        {
            if (!output.Children.ContainsKey(position))
                return null;
            var next = output.Children[position];
            output = next;
        }
        return output;
    }

    public static NodeFieldGroupContainer GetGapfillOffset(this NodeFieldGroupingRoot root, NodeFieldGrouping item, List<int> offsets)
    {
        var itemLevel = item.FieldLevel;
        var index = itemLevel.Index;
        if (offsets.Count != index + 1)
            throw new Exception("not enough offset positions");

        var currentLevelStructure = new List<FieldPosition>();
        var node = item;
        while (node != null)
        {
            currentLevelStructure.Insert(0, node.Position);
            node = node.Parent as NodeFieldGrouping;
        }

        NodeFieldGroupContainer output = root;
        for (var i = 0; i < offsets.Count; i++)
        {
            var level = root.FieldLevels[i];
            var offset = offsets[i];
            var currentPosition = currentLevelStructure[i];

            var levelPositions = new List<FieldPosition>();

            var currentIndex = currentPosition.Index;
            var direction = Math.Sign(offset);
            var absolute = Math.Abs(offset);
            var nextIndex = currentIndex + direction;
            var count = 0;
            NodeFieldGroupContainer nextContainer = null;

            if (direction == 0)
            {
                if (output.Children.ContainsKey(currentPosition))
                    nextContainer = output.Children[currentPosition];
            }
            else
            {
                while (nextIndex >= 0 && nextIndex < level.Positions.Positions.Count())
                {
                    var nextPosition = level.Positions[nextIndex];
                    if (output.Children.ContainsKey(nextPosition))
                        count++;
                    if (count == absolute)
                    {
                        nextContainer = output.Children[nextPosition];
                        break;
                    }
                    nextIndex += direction;
                }
            }
            if (nextContainer == null)
                return null;
            output = nextContainer;
        }
        return output;
    }
    

    public static void GetManyOffsetWithOffsetConnections(this NodeFieldGroupingRoot root, NodeFieldGrouping item, List<OffsetConnection> offsets, out List<NodeFieldGroupContainer> offsetResults, out List<NodeFieldGroupContainer> leafPredecessorResults,Errors errors)
    {
        offsetResults = new List<NodeFieldGroupContainer>();
        
        var itemLevel = item.FieldLevel;
        var index = itemLevel.Index;
        if (offsets.Count != index + 1)
            throw new ArgumentException("not enough offset positions");

        var currentLevelStructure = new List<FieldPosition>();
        var node = item;
        while (node != null)
        {
            currentLevelStructure.Insert(0, node.Position);
            node = node.Parent as NodeFieldGrouping;
        }

        var fieldPositions = new List<List<FieldPosition>>();
        for (var i = 0; i < offsets.Count; i++)
            fieldPositions.Add(GetFieldPositionsForOffsetConnection(currentLevelStructure[i], offsets[i], root.FieldLevels[i], errors));

        if (fieldPositions.Any(x => x == null) || fieldPositions.Count() != offsets.Count())
            throw new ArgumentException("could not retrieve positions for all offsets");

        var flattenedPositionOptions = FlattenLists(fieldPositions);
        var results = new List<NodeFieldGroupContainer>(flattenedPositionOptions.Count);
        foreach (var positions in flattenedPositionOptions)
        {
            NodeFieldGroupContainer output = root;
            if (positions.Any(x => x == null))
                continue;
            foreach (var position in positions)
            {
                if (!output.Children.ContainsKey(position))
                    break;
                var next = output.Children[position];
                output = next;
            }
            if (output is NodeFieldGroupingLeaf)
                results.Add(output);
        }
        if (offsets.All(x => x.Type == OffsetConnectionType.Direct))
        {
            offsetResults = results;
            leafPredecessorResults = new List<NodeFieldGroupContainer>() { item };
            return;
        }
        
        var leafPredecessorLeafGroups = item.GetLeafGroups();
        var finalSuccessorResults = new List<NodeFieldGroupContainer>();
        var finalPredecessorResults = new List<NodeFieldGroupContainer>();
        for (var i = offsets.Count - 1; i >= 0; i--)
        {
            var offset = offsets[i];
            
            if (offset.Type == OffsetConnectionType.GroupRelative)
            {
                foreach (var leafGroup in leafPredecessorLeafGroups)
                    CullLeafGroupToLeaf(offset.PredecessorIndex, leafGroup, finalPredecessorResults, errors);
                foreach (var leafGroup in results.OfType<NodeFieldGroupingLeaf>())
                    CullLeafGroupToLeaf(offset.SuccessorIndex, leafGroup, finalSuccessorResults, errors);
            }

            break;
        }

        offsetResults = finalSuccessorResults;
        leafPredecessorResults = finalPredecessorResults;
    }

    private static void CullLeafGroupToLeaf(int leafIndex, NodeFieldGroupingLeaf leafGroup, List<NodeFieldGroupContainer> nodeFieldGroupContainers, Errors errors)
    {
        var leaves = leafGroup.Leaves;
        int groupIndex = Int32.MinValue;
        if (leafIndex > 0)
        {
            groupIndex = leafIndex - 1;
            if (groupIndex >= leaves.Count)
                errors.Add(new Error(String.Format("Successor Index exceeded leaves on group '{0}'", leafGroup)));
        }
        else
        {
            groupIndex = leaves.Count + leafIndex;
            if (groupIndex < 0)
                errors.Add(new Error(String.Format("Successor Index exceeded leaves on group '{0}'", leafGroup)));
        }
                    
        if (groupIndex == Int32.MinValue)
            errors.Add(new Error(String.Format("error encountered calculating Index")));
        else
            nodeFieldGroupContainers.Add(leaves[groupIndex]);
    }

    private static List<FieldPosition> GetFieldPositionsForOffsetConnection(FieldPosition currentPosition, OffsetConnection offset, FieldLevel level, Errors errors)
    {
        var currentIndex = currentPosition.Index;
        switch (offset.Type)
        {
            case OffsetConnectionType.Direct:
                switch (offset.DirectType)
                {
                    case DirectOffsetConnectionType.Offset:
                        return offset.OffsetIndexes.Select(x => GetNextOffsetPosition(currentIndex, level, x)).Where(x => x != null).ToList();
                    case DirectOffsetConnectionType.All:
                        return level.Positions.Positions;
                    default:
                        throw new ArgumentException("offset direct type");
                }
            case OffsetConnectionType.GroupDirect:
            case OffsetConnectionType.GroupRelative:
                return offset.OffsetIndexes.Select(x => GetNextOffsetPosition(currentIndex, level, x)).Where(x => x != null).ToList();
                // var predecesssorIndex = offset.PredecessorIndex;
                // return predecesssorIndex >= 0 ? predecesssorIndex >= level.Positions.Positions.Count() ? new List<FieldPosition>() : new List<FieldPosition>() { level.Positions.Positions[predecesssorIndex] } : level.Positions.Positions;
            default:
                throw new ArgumentException("offset");
        }
    }

    private static FieldPosition GetNextOffsetPosition(int currentIndex, FieldLevel level, int offsetCount)
    {
        var nextIndex = currentIndex + offsetCount;
        if (nextIndex < 0 || nextIndex >= level.Positions.Positions.Count())
            return null;
        return level.Positions[nextIndex];
    }

    private static List<List<T>> FlattenLists<T>(List<List<T>> listsOfItems)
    {
        var result = new List<List<T>>();
        FlattenListsHelper(listsOfItems, 0, new List<T>(), result);
        return result;
    }

    private static void FlattenListsHelper<T>(List<List<T>> listsOfItems, int index, List<T> currentCombination, List<List<T>> result)
    {
        if (index == listsOfItems.Count)
        {
            result.Add(new List<T>(currentCombination));
            return;
        }

        var currentList = listsOfItems[index];
        foreach (var item in currentList)
        {
            currentCombination.Add(item);
            FlattenListsHelper(listsOfItems, index + 1, currentCombination, result);
            currentCombination.RemoveAt(currentCombination.Count - 1);
        }
    }

    #endregion

}

public enum OffsetConnectionType
{
    Direct,
    GroupDirect,
    GroupRelative
}

public enum DirectOffsetConnectionType
{
    Offset,
    All
}

public class OffsetConnection
{

    #region Declarations

    private readonly OffsetConnectionType m_type;
    private readonly string m_offset;
    private readonly DirectOffsetConnectionType m_directType;
    private readonly int m_predecessorIndex;
    private readonly List<int> m_offsetIndexes;
    private readonly int m_successorIndex;

    #endregion

    #region Constructors

    public OffsetConnection(OffsetConnectionType type, DirectOffsetConnectionType directType, List<int> offsets)
    {
        m_type = type;
        m_directType = directType;
        m_offsetIndexes = offsets;
    }
    public OffsetConnection(OffsetConnectionType type, int offsetCountForDirectLinks, string offset, int predecesssorIndex, int successorIndex)
    {
        m_type = type;
        m_offset = offset;
        m_predecessorIndex = predecesssorIndex;
        m_successorIndex = successorIndex;
        m_offsetIndexes = new List<int>() { offsetCountForDirectLinks };
    }

    #endregion

    #region Properties

    public OffsetConnectionType Type { get { return m_type; } }

    public DirectOffsetConnectionType DirectType { get { return m_directType; } }

    public List<int> OffsetIndexes { get { return m_offsetIndexes; } }

    public int PredecessorIndex { get { return m_predecessorIndex; } }

    public int SuccessorIndex { get { return m_successorIndex; } }
    
    public string OffsetString { get { return m_offset;  } }

    #endregion

    #region Methods

    public static bool ParseRelativeAddressToken(string relativeAddressToken, out int relativeLinkInteger, Errors errors)
    {
        relativeLinkInteger = int.MinValue;
        relativeAddressToken = relativeAddressToken.Trim();
        if (!relativeAddressToken.StartsWith("@"))
        {
            errors.Add(new Error(string.Format("'{0}' did not start with @", relativeAddressToken)));
            return false;
        }
        var integerPart = relativeAddressToken.Substring(1);
        if (!int.TryParse(integerPart, out relativeLinkInteger))
        {
            errors.Add(new Error(string.Format("'{0}' did not end with a valid integer", relativeAddressToken)));
            return false;
        }

        if (relativeLinkInteger == 0)
        {
            errors.Add(new Error(string.Format("'{0}' relative address should be non-zero", relativeAddressToken)));
            return false;
        }
        return true;
    }

    public static List<OffsetConnection> ParseOffsetsForConnections(List<string> offsets, Errors errors)
    {
        errors.Items.Clear();
        var offsetConnections = new List<OffsetConnection>();
		int offsetCountForDirectLinks = 0;
        for (var i = 0; i < offsets.Count; i++)
        {
            var offset = offsets[i];
            var splitOnComma = offset.Split(',');
            if (splitOnComma.Length == 1)
            {
                int offsetCount;
                if (int.TryParse(offset, out offsetCount))
                    offsetConnections.Add(new OffsetConnection(OffsetConnectionType.Direct, DirectOffsetConnectionType.Offset, new List<int>() { offsetCount }));
                else if (offset.Equals("*"))
                    offsetConnections.Add(new OffsetConnection(OffsetConnectionType.Direct, DirectOffsetConnectionType.All, new List<int>() { -1 }));
                else if (offset.Contains("=>") || offset.Contains("<="))
                {
                    var predecessorFirst = offset.Contains("<=");
                    if (i != offsets.Count - 1)
                    {
                        errors.Add(new Error(string.Format("{0}: relative links can only occur on the final offset", offset)));
                        continue;
                    }
                    var offsetLinks = offset.Split(':');
                    if (offsetLinks.Length < 2)
                    {
                        errors.Add(new Error(string.Format("{0}: no direct links were not provided", offset)));
                        continue;
                    }
                    else if (offsetLinks.Length > 2)
                    {
                        errors.Add(new Error(string.Format("{0}: too many offset counts specified", offset)));
                        continue;
                    }
                    if (!int.TryParse(offsetLinks[0], out offsetCountForDirectLinks))
                    {
                        errors.Add(new Error(string.Format("{0}: could not parse Offset Count as integer", offset)));
                        continue;
                    }
                    var directLinks = offsetLinks[1].Split(new[] { "=>", "<=" }, StringSplitOptions.None);
                    if (directLinks.Length < 2)
                    {
                        errors.Add(new Error(string.Format("{0}: direct links were not completed", offset)));
                        continue;
                    }
                    else if (directLinks.Length > 2)
                    {
                        errors.Add(new Error(string.Format("{0}: too many direct links", offset)));
                        continue;
                    }

                    int n;
                    var tryParseInts = directLinks.Select(s => int.TryParse(s, out n) ? n : (int?) null).ToList();
                    if (tryParseInts.All(x => x.HasValue))
                        offsetConnections.Add(new OffsetConnection(OffsetConnectionType.GroupDirect, offsetCountForDirectLinks, offset, tryParseInts[0].Value, tryParseInts[1].Value));
                    else
                    {
                        var predecessorString = directLinks[0];
                        var sucessorString = directLinks[1];

                        int predecessorRelativeInt;
                        int sucessorRelativeInt = Int32.MinValue;

                        var validTokens = ParseRelativeAddressToken(predecessorString, out predecessorRelativeInt, errors);
                        validTokens = validTokens && ParseRelativeAddressToken(sucessorString, out sucessorRelativeInt, errors);

                        if (validTokens)
                            offsetConnections.Add(predecessorFirst ? new OffsetConnection(OffsetConnectionType.GroupRelative, offsetCountForDirectLinks, offset, sucessorRelativeInt, predecessorRelativeInt) : new OffsetConnection(OffsetConnectionType.GroupRelative, offsetCountForDirectLinks, offset, predecessorRelativeInt, sucessorRelativeInt));
                        else if (!errors.Any())
                            errors.Add(new Error(string.Format("{0}: did not have valid relative addressing tokens", offset)));
                    }
                }
                else
                    errors.Add(new Error(string.Format("{0}: did not recognise this address", offset)));
            }
            else
            {
                int n;
                var tryParseInts = splitOnComma.Select(s => int.TryParse(s, out n) ? n : (int?) null);
                if (tryParseInts.All(x => x.HasValue))
                    offsetConnections.Add(new OffsetConnection(OffsetConnectionType.Direct, DirectOffsetConnectionType.Offset, tryParseInts.Select(x => x.Value).ToList()));
                else
                    errors.Add(new Error(string.Format("{0}: did not recognise offset string", offset)));
            }
        }
        return offsetConnections;
    }

    #endregion

}

public partial class RangeDependencyUtil {
	// should log this 
	public static RangeDependencyRule GetDependency(Case c, string path, bool create) { 
		var probe = c.DependencyRules.GetDependency(path); 
		if (probe != null) { 
			if (!(probe is RangeDependencyRule)) { 
				throw new Exception(string.Format("{0} exists but is of type {1} - expecting a RangeDependencyRule", probe.GetType().FullName));
			} 
			return (RangeDependencyRule)probe; 
		}
		if (probe == null && !create) {
			return null;
		}
		 
		var split = path.Split(new [] {'\\', '/'}); 
		 
		if (split.Length == 1) {  
			var ret = new RangeDependencyRule(split[0]); 
			c.DependencyRules.DependencyRules.Add(ret); 
			return ret; 
		} 
		 
		DependencyRuleFolder df = c.DependencyRules.Folders.GetOrCreate(split[0]); 
		for (int i = 1; i < split.Length - 1; i++) { 
			df = df.Folders.GetOrCreate(split[i]); 
		} 
		 
		var ret2 = new RangeDependencyRule(split.Last()); 
		df.DependencyRules.Add(ret2); 
		return ret2; 
	} 
}

public class Colors
{

    #region Constants

    private static readonly int[] hues = { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 }; // Predefined set of hues

    #endregion

    #region Methods

    public static Color GetUniqueColor(int index)
    {
        var hueIndex = index % hues.Length; // Get hue index based on the remainder

        var hue = hues[hueIndex]; // Retrieve the hue from the predefined set
        var color = ColorFromHSV(hue, 0.6, 0.9); // Set saturation and value (brightness) values

        return color;
    }
    // Helper function to convert HSV values to a Color object
    public static Color ColorFromHSV(double hue, double saturation, double value)
    {
        var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        var f = hue / 60 - Math.Floor(hue / 60);

        value = value * 255;
        var v = Convert.ToInt32(value);
        var p = Convert.ToInt32(value * (1 - saturation));
        var q = Convert.ToInt32(value * (1 - f * saturation));
        var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

        if (hi == 0)
            return Color.FromArgb(255, v, t, p);
        else if (hi == 1)
            return Color.FromArgb(255, q, v, p);
        else if (hi == 2)
            return Color.FromArgb(255, p, v, t);
        else if (hi == 3)
            return Color.FromArgb(255, p, q, v);
        else if (hi == 4)
            return Color.FromArgb(255, t, p, v);
        else
            return Color.FromArgb(255, v, p, q);
    }

    #endregion

}
