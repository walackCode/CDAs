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

public partial class ClassName
{
	[PrecisionMining.Spry.Scripting.ContextMenuEntryPoint]
    internal static PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<Shape>> HaulageAcrossStringreminder()
    {
        var ret = new PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<Shape>>();
        ret.Name = shape =>
        {
			return "Haulage is Run on a Single String, Please select only one";
        };
        ret.Visible = shapes => shapes.Count > 1;
		ret.Enabled = shapes => false;
         ret.Execute = shapes =>
        {
		};
        return ret;
    }
	
	[PrecisionMining.Spry.Scripting.ContextMenuEntryPoint]
    internal static PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<Shape>> HaulageAcrossString()
    {
        var ret = new PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<Shape>>();
        ret.Name = shape =>
        {
			return "Run Quick Haulage Assumption";
        };
        ret.Visible = shapes => shapes.Count == 1;
		
        ret.Execute = shapes =>
        {

			var selectform = OptionsForm.Create("Haulage Calculation");
			
			var caseselect = selectform.Options.AddCaseSelect("Select Case - For truck");
			
			var truckselect = selectform.Options.AddComboBox<Truck>("Select Truck");
			
			var maxgrade = selectform.Options.AddSpinEdit<double>("Max Grade").SetValue(10).RestoreValue("gradientstorage");
		
			
			caseselect.ValueChanged += (sender, obj) =>
        {
           truckselect.Items.AddRange(caseselect.Value.Trucks.AllTrucks.ToList());
		};
		
		if(caseselect.Value != null)
			truckselect.Items.AddRange(caseselect.Value.Trucks.AllTrucks.ToList());
		
		caseselect.RestoreValue("haulagetester", tt => caseselect.Value.FullName, Case.TryGet , true, true);
		
		truckselect.RestoreValue("Truck option", tt => truckselect.Value.FullName, x=> caseselect.Value == null ? null :  caseselect.Value.Trucks.GetTruckOrThrow(x));
		
		
		if (selectform.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				return;
		var code = caseselect.Value.SegmentCodes.FirstOrDefault();
		
		
		
		var k = new WorkingProfile(caseselect.Value);
		
		foreach( var b in shapes.FirstOrDefault().ToList())
			k.Points.Add(b,code);
				
		if(k.Segments.Any(x=> Math.Abs(x.Grade) > maxgrade.Value/100))
		{
			Out.WriteLine("String has segments greater than maximum grade ");
		}
		else
		{
		TravelTimeSimulator simulationfull = TravelTimeSimulator.Simulate(truckselect.Value,k,true);
		TravelTimeSimulator simulationempty = TravelTimeSimulator.Simulate(truckselect.Value,k,false);
		Out.WriteLine("Truck: " + truckselect.Value.FullName + "\n One Way Distance: " + k.Length + "\n Time Loaded: " +  simulationfull.TravelTime + "\n Time Empty: " + simulationempty.TravelTime + "\n Elevation Change: " + (k.PositiveElevationChange-k.NegativeElevationChange));
		}
		};
        return ret;
    }
}