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
#endregion

public partial class DragTheDirt
{
    public static void PushIt()
    {
        var optionsForm = OptionsForm.Create("Attributes from Fields");
		var caseSelect = optionsForm.Options.AddCaseSelect("Select Case")
						.Validators.Add(x => x != null, "Please select case")
            			.RestoreValue("Case ", _case => _case.FullName, str => Case.TryGet(str));
		var positiveBlockOffset = optionsForm.Options.AddSpinEdit("Positive Block Offset")
						.RestoreValue("Positive-Block-Offset");
		var negativeBlockOffset = optionsForm.Options.AddSpinEdit("Negative Block Offset")
						.RestoreValue("Negative-Block-Offset");
		var stripOffset = optionsForm.Options.AddSpinEdit("Strip Offset")
						.RestoreValue("Strip-Offset");
		var pitPrefix = optionsForm.Options.AddTextEdit("Pit Prefix")
						.RestoreValue("Pit-Prefix");
		
		var dumpPrefix = optionsForm.Options.AddTextEdit("Dump Prefix")
						.RestoreValue("Dump-Previx");
		var draglineProcess = optionsForm.Options.AddProcessSelect("Dragline Process", true, true, false);
		draglineProcess.SetCase(caseSelect.Value);
        draglineProcess.RestoreValues("DL-PROCESSES",
            x => draglineProcess.IncludeAll ? "< All >" : string.Join(",", draglineProcess.Processes.Select(process => process.Name)),
            x =>
            {
                var @case = caseSelect.Value;
                if (@case == null || x == null)
                    return null;

                if (x.Equals("< All >"))
                {
                    draglineProcess.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(process => @case.Processes.Get(process)).Where(process => process != null);
            });
		
		var draglineEquipment = optionsForm.Options.AddEquipmentSelect("Equipment", true);
		draglineEquipment.SetCase(caseSelect.Value);
        draglineEquipment.RestoreValues("DL-Equipment",
            x => draglineEquipment.IncludeAll ? "< All >" : string.Join(",", draglineEquipment.Equipment.Select(equipment => equipment.FullName)),
            x =>
            {
                var @case = caseSelect.Value;
                if (@case == null || x == null)
                    return null;

                if (x.Equals("< All >"))
                {
                    draglineEquipment.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(equipment => @case.Equipment.GetEquipment(equipment)).Where(equipment => equipment != null);
            });
				
		
		caseSelect.ValueChanged += (s,e) =>
		{
			if (caseSelect.Value != null)
			{
				draglineProcess.SetCase(caseSelect.Value);
				draglineEquipment.SetCase(caseSelect.Value);
			}
		};
		
        if (optionsForm.ShowDialog() == DialogResult.Cancel)
                    return;
		
		var c = caseSelect.Value;
		var sourceTable = c.SourceTable;
		var destinationTable = c.DestinationTable;
		var strips = sourceTable.Levels["Strip"].Positions;
		var blocks = sourceTable.Levels["Block"].Positions;
		foreach(var strip in strips)
		{
			foreach(var block in blocks)
			{
				var destinationPath = c.DestinationPaths.GetOrCreateDestinationPath(@"Created from dragline script\" + strip.Name + @"\" + block.Name);
				destinationPath.SourceCompositeRange.InlineRange.Text = pitPrefix.Value + "@" + strip.Index + @"/@" + block.Index;
				if (positiveBlockOffset.Value == 0 && negativeBlockOffset.Value == 0)
				{
					destinationPath.Path = dumpPrefix.Value + "@" + block.Index;
				}
				else
				{
					destinationPath.Path = dumpPrefix.Value + "@" + Math.Min(Math.Max(0,strip.Index + stripOffset.Value),strips.Count - 1) + @"/" + "@" + Math.Max((block.Index - negativeBlockOffset.Value),0) + " - @" + Math.Min((block.Index + positiveBlockOffset.Value),blocks.Count - 1);
				}
				destinationPath.IncludeAllProcesses = false;
				var p = destinationPath.Processes;
				foreach(var process in draglineProcess.Values)
				{
					p.Add(process);
				}
				destinationPath.IncludeAllEquipment = false;
				var e = destinationPath.Equipment;
				foreach(var equipment in draglineEquipment.Values)
				{
					e.Add(equipment);
				}
			}
		}
    }
}
