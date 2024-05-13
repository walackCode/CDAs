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

public partial class NewSourceConstraint : ISourceConstraint
{
	public bool Enabled { get; private set; }
	public string Name { get; private set; }
	public string FullName { get; private set; }
	
    public static void Main(SchedulingEngine se)
    {
        se.Constraints.Add(new NewSourceConstraint());
    }
	
	public bool Available(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask task, DateTime date)
    {
		if(task.Node.FullName == @"Alpha\S3\B1\F\50")
			return true;
		else
			return false;
	}
	
	public void PrescheduleSetup(SchedulingEngine engine)
    {
	 	Enabled = true;
	}
}