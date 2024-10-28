#region Using Directives
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PrecisionMining.Common.Design;
using PrecisionMining.Common.Design.Data;
using PrecisionMining.Common.Math;
using PrecisionMining.Common.UI.Design;
using PrecisionMining.Common.Units;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Util;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Design;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Haulage;
using PrecisionMining.Spry.Scenarios.Scheduling;
using PrecisionMining.Spry.Scripting;
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.Util.OptionsForm;
using PrecisionMining.Spry.Util.UI;
using PrecisionMining.Spry.UI;
#endregion
public partial class EPAHelperMenu
{
    public enum MenuType
    {
        TextEdit,
        RangeSelect,
        FieldSelect,
        SupressMode,
        DateSelect,
        NonProductiveProcessSelect,
        ProductiveProcessSelect,
        EventMode,
        ObeyMode,
        CaseSelect,
        EquipmentSelect,
        ScheduleTypeSelect,
    }
        
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> TimeConstrain()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Time Constrain";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("mode", MenuType.SupressMode),
                Tuple.Create("start", MenuType.DateSelect),
                Tuple.Create("end", MenuType.DateSelect),
            };
            CreateScriptInsertMenu(s, "timeconstrain", null, menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> Deadhead()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Deadhead";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("process", MenuType.NonProductiveProcessSelect)
            };
            CreateScriptInsertMenu(s, "deadhead", Tuple.Create("Time", MenuType.TextEdit), menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> DependOn()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Depend On";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("delay", MenuType.TextEdit)
            };
            CreateScriptInsertMenu(s, "dependon", Tuple.Create("inline range", MenuType.TextEdit), menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> WaitOn()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Wait On";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("Process", MenuType.ProductiveProcessSelect)
            };
            CreateScriptInsertMenu(s, "waiton", Tuple.Create("inline range", MenuType.TextEdit), menuItems);
        };
        return entryPoint;
    }
    
        
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> DependFree()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Depend Free";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("delay", MenuType.TextEdit)
            };
            CreateScriptInsertMenu(s, "dependfree", null , menuItems);
        };
        return entryPoint;
    }
        
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> WaitFree()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Wait Free";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("delay", MenuType.TextEdit)
            };
            CreateScriptInsertMenu(s, "waitfree", null , menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> Remove()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Remove";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("tasks", MenuType.TextEdit),
                Tuple.Create("event", MenuType.EventMode),
                Tuple.Create("delay", MenuType.TextEdit)
            };
            CreateScriptInsertMenu(s, "remove", null , menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> ForceCompletion()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Force Completion";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("on", MenuType.DateSelect),
                Tuple.Create("obey", MenuType.ObeyMode),
            };
            CreateScriptInsertMenu(s, "forcecompletion", null , menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> reprioritize()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Reprioritise";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
            };
            CreateScriptInsertMenu(s, "reprioritise", null , menuItems);
        };
        return entryPoint;
    }
    
    [ContextMenuEntryPoint]
    public static ContextMenuEntryPoint<SourcePathSelection> InjectSchedulePath()
    {
        var entryPoint = new ContextMenuEntryPoint<SourcePathSelection>();
        entryPoint.SubMenu = s => "Script Insert";
        entryPoint.Name = s => "Inject Schedule Path";
        entryPoint.Execute = s => 
        {
            var menuItems = new List<Tuple<string,MenuType>>() 
            {
                Tuple.Create("case", MenuType.CaseSelect),
                Tuple.Create("start", MenuType.DateSelect),
                Tuple.Create("end", MenuType.DateSelect),
                Tuple.Create("type", MenuType.ScheduleTypeSelect),
                Tuple.Create("equipment", MenuType.EquipmentSelect),
                Tuple.Create("obey", MenuType.ObeyMode),
            };
            CreateScriptInsertMenu(s, "injectschedulepath", null , menuItems);
        };
        return entryPoint;
    }
    
    public static void CreateScriptInsertMenu(SourcePathSelection selection, string command, Tuple<string,MenuType> defaultCommand, List<Tuple<string,MenuType>> menuItems)
    {
        var form = OptionsForm.Create(command + " Setup");
        
        var clears = new List<Action>();
        var getTexts = new List<Func<string>>();
        
        Action clearDefault = null;
        Func<string> getNameDefault = null;
        if (defaultCommand != null)
        {
            AddItem(selection.Case, form, defaultCommand, out clearDefault, out getNameDefault);
            clears.Add(clearDefault);   
        }
        
        foreach(var menuItem in menuItems)
        {
            Action clear;
            Func<string> getName;
            AddItem(selection.Case, form, menuItem, out clear, out getName);
            clears.Add(clear);
            getTexts.Add(getName);
        }
    
        var button = form.Options.AddButtonEdit("Clear Values");
        button.ClickAction = (o) =>{
            foreach(var clear in clears)
                clear();
        };
        
        var helpButton = form.Options.AddButtonEdit("Help");
        helpButton.ClickAction = (o) =>{
            var link = @"https://webhelp.micromine.com/spry/latest/English/Content/spscenario/IDH_PATH_ANNOTATION_SCRIPT.htm?tocpath=Tutorials%7CPath%20Annotation%20Script%7C_____0#!";
            link += command.Length < 7 ? command : command.Substring(0,7);
            System.Diagnostics.Process.Start(link);
        };
        
        if(form.ShowDialog() != DialogResult.OK)
            return;
        var texts = getTexts.Select(x => x()).Where(x => !string.IsNullOrEmpty(x));
        var text = "'!" + command;
        if(defaultCommand != null)
        {
            var defaultCommandText = getNameDefault();
            if (!string.IsNullOrEmpty(defaultCommandText))
                text += " " + defaultCommandText.Substring(defaultCommand.Item1.Length + 1);
        }
        if (texts.Any())
            text += " " + string.Join(" ", texts);
        selection.ReplaceSelectedText(text);
    }
    
    public static void AddItem(Case @case, IOptionsForm form, Tuple<string,MenuType> menuItem, out Action clear, out Func<string> getName)
    {
        clear = null;
        getName = null;
        switch(menuItem.Item2)
        {
            case MenuType.TextEdit:
                var textEdit = form.Options.AddTextEdit(menuItem.Item1);
                clear = new Action( () => textEdit.Value = "");
                getName = new Func<string>( () => string.IsNullOrEmpty(textEdit.Value) ? "" : menuItem.Item1 + "=" + textEdit.Value);
                break;
            case MenuType.RangeSelect:
                var rangeEdit = form.Options.AddRangeSelect(menuItem.Item1);
                rangeEdit.SetTable(@case.SourceTable);
                clear = new Action( () => rangeEdit.Value = null);
                getName = new Func<string>(() => rangeEdit.Value == null ? "" : menuItem.Item1 + "=" + rangeEdit.Value.FullName);
                break;
            case MenuType.FieldSelect:
                var fieldSelect = form.Options.AddFieldSelect(menuItem.Item1, false);
                fieldSelect.SetTable(@case.SourceTable);
                clear  = new Action( () => fieldSelect.Value = null);
                getName = new Func<string>(() => fieldSelect.Value == null ? "" : menuItem.Item1 + "=" + fieldSelect.Value.FullName);
                break;
            case MenuType.DateSelect:
                var dateSelect = form.Options.AddDateTime(menuItem.Item1);
                dateSelect.Value = DateTime.MinValue;
                clear = new Action(() => dateSelect.Value = DateTime.MinValue);
                getName = new Func<string>(() => dateSelect.Value == DateTime.MinValue ? "" : menuItem.Item1 + "=" + dateSelect.Value.ToShortDateString());
                break;
            case MenuType.SupressMode:
                var modeSelect = form.Options.AddComboBox<string>(menuItem.Item1);
                modeSelect.Items.Add("");
                modeSelect.Items.Add("allow");
                modeSelect.Items.Add("suppress");
                clear = new Action(() => modeSelect.Value = "");
                getName = new Func<string>(() => modeSelect.Value == "" ? "" : menuItem.Item1 + "=" + modeSelect.Value);
                break;
            case MenuType.NonProductiveProcessSelect:
                var nonProductiveProcessSelect = form.Options.AddProcessSelect(menuItem.Item1, false, false, true);
                nonProductiveProcessSelect.SetCase(@case);
                clear = new Action( () => nonProductiveProcessSelect.Value = null);
                getName = new Func<string>(() => nonProductiveProcessSelect.Value == null ? "" : menuItem.Item1 + "=" + nonProductiveProcessSelect.Value.Name);
                break;
            case MenuType.ProductiveProcessSelect:
                var processSelect = form.Options.AddProcessSelect(menuItem.Item1, false, true, false);
                processSelect.SetCase(@case);
                clear = new Action( () => processSelect.Value = null);
                getName = new Func<string>(() => processSelect.Value == null ? "" : menuItem.Item1 + "=" + processSelect.Value.Name);
                break;
            case MenuType.EventMode:
                var eventSelect = form.Options.AddComboBox<string>(menuItem.Item1);
                eventSelect.Items.Add("");
                eventSelect.Items.Add("available");
                eventSelect.Items.Add("completed");
                clear = new Action(() => eventSelect.Value = "");
                getName = new Func<string>(() => eventSelect.Value == "" ? "" : menuItem.Item1 + "=" + eventSelect.Value);
                break;
            case MenuType.ObeyMode:
                var obeySelect = form.Options.AddComboBox<string>(menuItem.Item1);
                obeySelect.Items.Add("");
                obeySelect.Items.Add("all");
                obeySelect.Items.Add("dep");
                obeySelect.Items.Add("con");
                clear = new Action(() => obeySelect.Value = "");
                getName = new Func<string>(() => obeySelect.Value == "" ? "" : menuItem.Item1 + "=" + obeySelect.Value);
                break;
            case MenuType.CaseSelect:
                var caseSelect = form.Options.AddCaseSelect(menuItem.Item1);
                clear = new Action( () => caseSelect.Value = null);
                getName = new Func<string>(() => caseSelect.Value == null ? "" : menuItem.Item1 + "=" + caseSelect.Value.FullName);
                break;
            case MenuType.EquipmentSelect:
                var equipmentSelect = form.Options.AddEquipmentSelect(menuItem.Item1, false);
                equipmentSelect.SetCase(@case);
                clear = new Action( () => equipmentSelect.Value = null);
                getName = new Func<string>(() => equipmentSelect.Value == null ? "" : menuItem.Item1 + "=" + equipmentSelect.Value.FullName);
                break;
            case MenuType.ScheduleTypeSelect:
                var scheduleTypeSelect = form.Options.AddComboBox<string>(menuItem.Item1);
                scheduleTypeSelect.Items.Add("");
                scheduleTypeSelect.Items.Add("input");
                scheduleTypeSelect.Items.Add("output");
                clear = new Action(() => scheduleTypeSelect.Value = "");
                getName = new Func<string>(() => scheduleTypeSelect.Value == "" ? "" : menuItem.Item1 + "=" + scheduleTypeSelect.Value);
                break;

        }
    }
}
