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
#endregion

public class Halver : IProductivityCalculator
{
	//change this for a different change to Utilisation
	double multiplier = 0.5;
    private IProductivityCalculator m_original;
	
	public Halver(IProductivityCalculator original)
	{
		m_original = original;
	}
	
	public double Rate(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask sourceTask, DestinationTask destinationTask, DateTime time)
	{
		var allfreedig = true;
		foreach(var scheq in engine.Equipment.Where(x => x.Name == "EX1" || x.Name == "EX2"))
		{
			if(scheq.ActiveSourceTask != null)
			{
				if(scheq.ActiveSourceTask.Process.Name != "Freedig")
					allfreedig= false;
			}
		}
		if(allfreedig)
			return m_original.Rate(engine,equipment,sourceTask,destinationTask,time);
		else
			return m_original.Rate(engine,equipment,sourceTask,destinationTask,time) * multiplier;
	}
	
	public Percentage Utilisation(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask sourceTask, DestinationTask destinationTask, DateTime time)
	{
		var allfreedig = true;
		foreach(var scheq in engine.Equipment.Where(x => x.Name == "EX1" || x.Name == "EX2"))
		{
			if(scheq.ActiveSourceTask != null)
			{
				if(scheq.ActiveSourceTask.Process.Name != "Freedig")
					allfreedig= false;
			}
		}
		if(allfreedig)
			return m_original.Utilisation(engine,equipment,sourceTask,destinationTask,time);
		else
			return m_original.Utilisation(engine,equipment,sourceTask,destinationTask,time) * multiplier;
	}

	public Percentage Availability(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask sourceTask, DestinationTask destinationTask, DateTime time)
	{
		return m_original.Availability(engine,equipment,sourceTask,destinationTask,time);
	}

	public double EquipmentCount(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask sourceTask, DestinationTask destinationTask, DateTime time)
	{
		return m_original.EquipmentCount(engine,equipment,sourceTask,destinationTask,time);
	}
}

public partial class EquipmentHalver
{
    public static void SetupEngine(SchedulingEngine engine)
    {
		engine.BeforeScheduleRun += (s,e) => {
			var se = engine.Equipment["EX3"];
			var originalProd = se.ProductivityCalculator;
			se.ProductivityCalculator = new Halver(originalProd);
		};
    }
}
