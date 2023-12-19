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

/// <summary>
/// script works off of ranges based on equipment Name. Each range will be an area and will put a delay after the last task is completed. 
/// For each equipment a folder with a range will be required, the folder can have no ranges in it. 
/// Script will struggle with ranges that have nodes with multiple processes
/// </summary>
public partial class InsertDelayTimeSpan
{
	public static void Run(SchedulingEngine e){
		e.BeforeScheduleRun += (_,_1) => 
		{
			//Changes here
			string processName = "Deadheading";
			double hoursOfDelay = 10;
			//end of changes
			var digAreas = new Dictionary<SchedulingEquipment, List<List<Node>>>();
			foreach (SchedulingEquipment scheq in e.Equipment)
			{
				var rangeFolder = e.SourceTasks.First().Node.Table.Ranges.Folders.GetOrThrow(scheq.Name);
				List<List<Node>> areasToAdd = new List<List<Node>>();
				foreach(var range in rangeFolder.AllRanges)
				{
					areasToAdd.Add(range.CachedNodes.Where(x => x.IsLeaf).ToList());
				}
				digAreas.Add(scheq,areasToAdd);
			}
			var delayProcess = e.Case.Processes.ToList().Where(x=> x.Name == processName).First();
			TimeSpan delayTime = TimeSpan.FromHours(hoursOfDelay);
			foreach (SchedulingEquipment scheq in e.Equipment)
			{
				scheq.TaskReleased += (obj, arg) => 
				{
					List<List<Node>> taskListList;
					if (digAreas.TryGetValue(scheq, out taskListList))
					{
						var releasedNode = arg.SourceTask.Node;
						var releasedNodePercentageCompleted = arg.SourceTask.PercentageRemaining;
						if(releasedNodePercentageCompleted < 0.00000000000000001)
						{
							var delayFlag = false;
							foreach(var taskList in taskListList)
							{
								taskList.Remove(releasedNode);
								if(taskList.Count == 0)
								{
									delayFlag = true;
								}
							}
							if(delayFlag)
							{
								scheq.Delay(delayProcess,delayTime);
								while(taskListList.Where(x=>x.Count == 0).Count() != 0)
								{
									taskListList.Remove(taskListList.Where(x=>x.Count == 0).First());
								}
							}
						}
					}
				};
			}
		};
	}
}