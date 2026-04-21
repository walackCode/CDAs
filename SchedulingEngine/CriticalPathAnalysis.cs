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

public partial class CriticalPathAnalysis
{
    public static void SetupEngine(SchedulingEngine engine)
    {
		CalculatePaths(engine);
    }

    private static void CalculatePaths(SchedulingEngine engine)
    {
		var taskTimeDictionary = new Dictionary<SourceTask,TimeSpan>();
		var taskDateDictionary = new Dictionary<SourceTask,DateTime>();
		engine.AfterCalculateCompletionDates += (s,e) =>
		{
			foreach(var scheq in e.Engine.Equipment)
			{
				if(scheq.ActiveSourceTask != null && !taskTimeDictionary.ContainsKey(scheq.ActiveSourceTask))
				{
					if (scheq.ActiveSourceTask.ScheduledCompletionDate != null)
					{
						DateTime timeToComplete = (DateTime)scheq.ActiveSourceTask.ScheduledCompletionDate;
						taskTimeDictionary.Add(scheq.ActiveSourceTask,timeToComplete - e.Engine.CurrentDate);
						taskDateDictionary.Add(scheq.ActiveSourceTask,timeToComplete);
					}
				}
			}
		};
		
		var successorCountDictionary = new Dictionary<SourceTask,int>();
		var noSuccessorNodes = new List<SourceTask>();
		var successorDictionary = new Dictionary<SourceTask,List<ITask>>();
		var predecessorDictionary = new Dictionary<SourceTask,List<ITask>>();
		engine.BeforeScheduleRun += (s,e) =>{
			foreach(var task in engine.SourceTasks)
			{
				if(!successorCountDictionary.ContainsKey(task))
					successorCountDictionary.Add(task, task.Successors.Count);
				if(!successorDictionary.ContainsKey(task))
					successorDictionary.Add(task,task.Successors.Select(x => x.Successor).ToList());
				if(!predecessorDictionary.ContainsKey(task))
					predecessorDictionary.Add(task,task.Predecessors.Select(x => x.Predecessor).ToList());
			}
			noSuccessorNodes = successorCountDictionary.Where(x => x.Value == 0).Select(x => x.Key).ToList();
		};

		var pathTimeDictionary = new Dictionary<SourceTask,DateTime>();
		engine.AfterScheduleRun += (s,e) =>{
			var i = 0;
			while (i < 1000)
			{
				Console.WriteLine(i);
				var dictionaryEntriesNoSucc = successorCountDictionary.Where(x => x.Value == 0).ToList();
				if (dictionaryEntriesNoSucc.Count() == 0)
					break;
				foreach(var entry in dictionaryEntriesNoSucc)
				{
					if(noSuccessorNodes.Contains(entry.Key) || entry.Key.Process.Name == "Coal")
					{
						var timeToEnter = new DateTime();
						if (taskDateDictionary.ContainsKey(entry.Key))
							timeToEnter = taskDateDictionary[entry.Key];
						else 
							timeToEnter = DateTime.MinValue;
						pathTimeDictionary.Add(entry.Key,timeToEnter);
					}
					else
					{
						var timeToEnter = new DateTime();
						if (!taskTimeDictionary.ContainsKey(entry.Key))
							timeToEnter = DateTime.MinValue;
						else
						{
							var nonZeroValues = pathTimeDictionary.Where(x => successorDictionary[entry.Key].Contains(x.Key)).Where(x => x.Value.Year > 5);
							if (nonZeroValues.Count() == 0)
								timeToEnter = DateTime.MinValue;
							else
								timeToEnter = nonZeroValues.Min(x => x.Value) - taskTimeDictionary[entry.Key];
						}
						pathTimeDictionary.Add(entry.Key,timeToEnter);
					}
					foreach(SourceTask pred in predecessorDictionary[entry.Key])
					{
						successorCountDictionary[pred] -= 1;
					}
					successorCountDictionary[entry.Key] -= 1;
				}
				i++;
			}
			var t = engine.Case.SourceTable;
			foreach(var node in t.Nodes.Leaves)
			{
				foreach(var process in engine.Case.Processes.Where(x => x.Active && x.Productive))
				{
					node.Data[@"CPA\" + process.Name] = new DateTime(1,1,1);
				}
			}
			foreach(var kvp in pathTimeDictionary)
			{
				var node = t.Nodes.GetOrThrow(kvp.Key.Node.FullName);
				if(kvp.Value != DateTime.MaxValue)
					node.Data[@"CPA\" + kvp.Key.Process.ToString()] = kvp.Value;
			}
		};
    }
}
