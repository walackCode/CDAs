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

public partial class IndividualDestinationLimitConstraint : ISourceDestinationConstraint 
{
	static double MAX_FILL = 0.48;
	
	public HashSet<SourceTask> sourceTasks = new HashSet<SourceTask>();
	public HashSet<DestinationTask> destTasks = new HashSet<DestinationTask>();
	public Dictionary<DestinationTask, double> percentCompleted = new Dictionary<DestinationTask, double>();
	public Dictionary<DestinationTask, double> percentRate = new Dictionary<DestinationTask, double>();
	
    public static void EngineSetup(SchedulingEngine engine)
    {
		engine.Constraints.Add(new DestinationLimitConstraint());
    }
	
	public void PrescheduleSetup(SchedulingEngine engine)
    {
		sourceTasks = new HashSet<SourceTask>(engine.SourceTasks.Range[@"*"]);
		destTasks = new HashSet<DestinationTask>(engine.DestinationTasks.Range[@"*"]);
		percentCompleted = destTasks.ToDictionary(x => x, x => 0.0);
		
		engine.AfterCalculateProductivities += (o, acpe) =>
		{
			percentRate.Clear();
			foreach (var eq in engine.Equipment) {
				if (eq.ActiveSourceTask == null || !sourceTasks.Contains(eq.ActiveSourceTask)) {
					continue;
				}
				if (eq.ActiveDestinationTask == null || !destTasks.Contains(eq.ActiveDestinationTask)) {
					continue;
				} 
				
				if (!percentRate.ContainsKey(eq.ActiveDestinationTask)) {
					percentRate[eq.ActiveDestinationTask] = eq.DestinationHourlyPercentageProductivity.Value;
				} else {
					percentRate[eq.ActiveDestinationTask] += eq.DestinationHourlyPercentageProductivity.Value;
				}
				
			}
			
			foreach (var task in percentRate) {
				var percentageToFill = MAX_FILL - percentCompleted[task.Key];
				var timeToReach = percentageToFill / task.Value;
				//Console.WriteLine("{0} {1}", task.Key, timeToReach);
				
				if (timeToReach > 0) {
					var hours = TimeSpan.FromHours(timeToReach);
					var completionDate = engine.CurrentDate + hours;
					
					engine.TimeKeeper.Add(completionDate, () => { 
						//Console.WriteLine("Complete trigger for {0}", task.Key);
						percentCompleted[task.Key] += 1.0; // just spam to large number to force finish - imprecision may not quite get there
					});
					
				}
			}
		};
		
		foreach (var eq in engine.Equipment) {
			eq.Updated += (o, eue) => {
				if (eq.ActiveSourceTask == null || !sourceTasks.Contains(eq.ActiveSourceTask)) {
					return;
				}
				if (eq.ActiveDestinationTask == null || !destTasks.Contains(eq.ActiveDestinationTask)) {
					return;
				}

				if (!percentCompleted.ContainsKey(eue.DestinationTask)) {
					percentCompleted[eue.DestinationTask] = eue.DestinationPercentageComplete;
				} else {
					percentCompleted[eue.DestinationTask] += eue.DestinationPercentageComplete;
				}
			};
		}
			
//			eq.TaskSelected += (o, tse) => {
//				if (tse.SourceTask == null || !sourceTasks.Contains(tse.SourceTask)) {
//					return;
//				}
//				if (tse.DestinationTask == null || !destTasks.Contains(tse.DestinationTask)) {
//					return;
//				}
				
//			};		
//		}
	}

	
	public bool Available(SchedulingEngine engine, SchedulingEquipment equipment, SourceTask sourceTask, DestinationTask destinationTask, DateTime date)
    {
		if (destinationTask == null || !destTasks.Contains(destinationTask)) {
			return true;
		}
		
		double percent = 0.0;
		if (percentCompleted.ContainsKey(destinationTask)) {
			percent = percentCompleted[destinationTask];
			//Console.WriteLine("Percent {0} is {1}", destinationTask, percent);
		}
		
		return percent <= MAX_FILL - 0.00001;
    }
	
	public string Name { get { return FullName; } }
​
    public string FullName { get { return "Topsoil Constrainer"; } }
​
    public bool Enabled { get { return true; } }
} 