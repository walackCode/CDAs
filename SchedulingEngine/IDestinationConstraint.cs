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

public partial class NewDestinationConstraint : IDestinationConstraint
{
	public bool Enabled { get; private set; }
	public string Name { get; private set; }
	public string FullName { get; private set; }
	
	static double fillToPercentage = 0.5;
	
    public static void Main(SchedulingEngine se)
    {
        se.Constraints.Add(new NewDestinationConstraint());
    }
	
	public bool Available(SchedulingEngine engine, SchedulingEquipment equipment, DestinationTask task, DateTime date)
    {
		if(task.PercentageComplete < fillToPercentage)
			return true;
		else
			return false;
		
	}
	
	public void PrescheduleSetup(SchedulingEngine engine)
    {
	 	Enabled = true;
		
		//add timekeeper entries to make equipment recalculate activities when hitting total percentage
		engine.AfterCalculateProductivities += (a,b) =>
		{
			foreach(var e in engine.Equipment)
			{
				if (e.ActiveSourceTask == null || e.ActiveDestinationTask == null)
					continue;
				var percentLeftToFill = fillToPercentage - e.ActiveDestinationTask.PercentageComplete;
				var completionDate = TimeSpan.FromMinutes(percentLeftToFill / e.ActiveDestinationTask.HourlyPercentageProductivity * 60);
				if (completionDate.Duration().TotalSeconds > 0)
				{
					engine.TimeKeeper.Add(completionDate, () => {});
				}
			}
		};
	}
}