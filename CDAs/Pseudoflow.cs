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

public partial class PsuedoflowCDA
{
	private static RangeSelectOption rangeSelect;
	private static FieldSelectOption revenueFieldSelect;
	private static FieldSelectOption costFieldSelect;
	private static FieldSelectOption stageFieldSelect;
	private static FieldSelectOption revFactorFieldSelect;
	private static SpinEditOption<double> maximumRevenueFactorSpin;
	private static SpinEditOption<double> revenueFactorIteractionSpin;
	
	[CustomDesignAction]
    public static CustomDesignAction MakeCDA()
    {
        var cda = new CustomDesignAction("Pseudoflow Cost Analysis", "", "Scheduling");
		cda.Visible = DesignButtonVisibility.Animation;
		cda.OptionsForm += (s,e) =>
		{
			string pseudoflowName = "PSEUDOFLOWCDA";
			
			var form = OptionsForm.Create("form");
			rangeSelect = form.Options.AddRangeSelect("Filter");
			revenueFieldSelect = form.Options.AddFieldSelect("Revenue Field");
			costFieldSelect = form.Options.AddFieldSelect("Cost Field");
			stageFieldSelect = form.Options.AddFieldSelect("Output Stage Field");
			revFactorFieldSelect = form.Options.AddFieldSelect("Output Revenue Factor Field");
			form.Options.BeginGroup("Iterations");
			maximumRevenueFactorSpin = form.Options.AddSpinEdit("Maximum Revenue Factor").SetValue(1).RestoreValue(pseudoflowName+"max rev factor");
			revenueFactorIteractionSpin = form.Options.AddSpinEdit("Number of Iterations").SetValue(10).RestoreValue(pseudoflowName+"iterations");
			revenueFactorIteractionSpin.AllowFloatValues = false;
			form.Options.EndGroup();
			rangeSelect.Table = cda.ActiveCase.SourceTable;
			revenueFieldSelect.Table = cda.ActiveCase.SourceTable;
			costFieldSelect.Table = cda.ActiveCase.SourceTable;
			stageFieldSelect.Table = cda.ActiveCase.SourceTable;
			revFactorFieldSelect.Table = cda.ActiveCase.SourceTable;
			rangeSelect.RestoreValue(pseudoflowName+"Filter Range", r => r.FullName, name => cda.ActiveCase.SourceTable.Ranges.GetRange(name), true, true);
			revenueFieldSelect.RestoreValue(pseudoflowName+"revenue field", f => f.FullName, name => cda.ActiveCase.SourceTable.Schema.GetField(name), true, true);
			costFieldSelect.RestoreValue(pseudoflowName+"cost field", f => f.FullName, name => cda.ActiveCase.SourceTable.Schema.GetField(name), true, true);
			stageFieldSelect.RestoreValue(pseudoflowName+"stage field", f => f.FullName, name => cda.ActiveCase.SourceTable.Schema.GetField(name), true, true);
			revFactorFieldSelect.RestoreValue(pseudoflowName+"rev factor field", f => f.FullName, name => cda.ActiveCase.SourceTable.Schema.GetField(name), true, true);
			
			e.OptionsForm = form;
		};
		
		cda.ApplyAction += (s,e) =>
		{
			var range = rangeSelect.Value;
			var pf = Actions.Pseudoflow.CreateOptions(cda.ActiveCase, range, revenueFieldSelect.Value, costFieldSelect.Value, maximumRevenueFactorSpin.Value, (int) revenueFactorIteractionSpin.Value, stageFieldSelect.Value,  revFactorFieldSelect.Value);
			Actions.Pseudoflow.Run(pf);
			e.Completed = true;
		};
		
		return cda;
    }
}
	