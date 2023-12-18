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

public partial class EquipmentDelayr
{
    public static void SetupEngine(SchedulingEngine engine)
    {
		SetupEquipment(engine, "EX01", "Maintenance", 24);
    }

    public static void SetupEquipment(SchedulingEngine engine, string eqName, string delayProcessName, double delayHours)
    {
        var se = engine.Equipment[eqName];
        if (se == null)
        {
            throw new ArgumentNullException("No equipment " + eqName);
        }
		
        Process delayProcess = se.Engine.Case.Processes.GetOrThrow(delayProcessName);

        engine.TimeKeeper.Add(engine.StartDate, () =>
        {
            SetupDelays(se, delayProcess, delayHours);
        });
    }

    public static void SetupDelays(SchedulingEquipment se, Process delayProcess, double delayHours)
    {
        TimeSpan delayTime = TimeSpan.FromHours(delayHours);
        
		ISchedulingSourcePathElement x = se.SourcePath.First();

        ISchedulingSourcePathElement lastElement = null;
        SchedulingSourcePathSteps lastSteps = null;
        SchedulingSourcePathStep lastStep = null;

        foreach (SchedulingSourcePathSteps step in se.SourcePath.ToList())
        {
            var currentSteps = step as SchedulingSourcePathSteps;
            var currentStep = currentSteps != null ? currentSteps.First() : null;
			
			//Criteria for delaying equipment after current task goes here
            if (currentStep.Task.Node.FullName == @"Alpha\S5\B7\D\120")
            {
                se.SourcePath.AddAfter(step, delayProcess, delayTime);
            }

            lastElement = step;
            lastSteps = currentSteps;
            lastStep = currentStep;
        }
    }
}
