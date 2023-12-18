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

public partial class DrillDelay
{
    public static void SetupEngine(SchedulingEngine engine)
    {
        Residency.Residency.OutputAvailability(engine);
		SetupEquipment(engine, "1_Drill and Blast/Drills", "Relocation", 24);
		//SetupEquipment(engine, "1_Drill and Blast/Drills Sprint", "Relocation", 12);
        SetupEquipment(engine, "1_Drill and Blast/Blast Crew", "Tie up", 24);
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

        // List<SchedulingSourcePathSteps> needsDelayAfter = new List<SchedulingSourcePathSteps>();

        foreach (var step in se.SourcePath.ToList())
        {
            var currentSteps = step as SchedulingSourcePathSteps;
            var currentStep = currentSteps != null ? currentSteps.First() : null;

            if (lastElement == lastSteps && lastSteps != null && currentSteps != null && lastStep.PathLineNumber != currentStep.PathLineNumber)
            { // last element is a step, and current element is a step, and path lines don't match - add to todo list
                // can't edit in place because we're iterating over the sourcepath
                // needsDelayAfter.Add(currentSteps);
                se.SourcePath.AddAfter(step, delayProcess, delayTime);
            }

            lastElement = step;
            lastSteps = currentSteps;
            lastStep = currentStep;
        }

        // foreach (var step in needsDelayAfter)
        // {
            // se.SourcePath.AddAfter(step, delayProcess, delayTime);
        // }
    }
}