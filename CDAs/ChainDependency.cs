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
using PrecisionMining.Spry.UI.Design;
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

//v2 add new chains into existing range dependency
//v3 add preivous nodes as display items
//v4 fix visualisation bug added process select
public partial class WakeUpHoney
{

    #region Declarations

    private static readonly List<INodeSolid> CurrentNodes = new List<INodeSolid>();
	private static readonly List<List<INodeSolid>> PreviousNodes = new List<List<INodeSolid>>();
	
    #endregion

    #region Methods

    [CustomDesignAction]
    internal static CustomDesignAction CreateChainDependency()
    {
        var customDesignAction = new CustomDesignAction("Create Chain Dependency", "", "Dependency", Array.Empty<Keys>());
        customDesignAction.Visible = DesignButtonVisibility.Animation;
        customDesignAction.SetupAction += (s, e) =>
        {
            customDesignAction.SetSelectMode(SelectMode.SelectElements);
            CurrentNodes.Clear();
			PreviousNodes.Clear();
		};
        customDesignAction.OptionsForm += (s, e) =>
        {
            var form = OptionsForm.Create("test");
            var nameOption = form.Options.AddTextEdit("Name");
            nameOption.Value = "New Chain Dependency";
            nameOption.RestoreValue("CHAINDEPENDENCYNAME");
            var prefixOption = form.Options.AddTextEdit("Entry Prefix");
            prefixOption.Value = "Entry " + DateTime.Now.ToString("yyyyMMddHHmmss");
			
            var predProc = form.Options.AddProcessSelect("Predecessor Process", true, true, false);
			predProc.Case = customDesignAction.ActiveCase;
			predProc.IncludeAll = true;
			predProc.RestoreValue("CHAINDEPENDENCYPProcess",x => {
				if(predProc.IncludeAll)
					return "ALL";
				else 
					return string.Join("\n", predProc.Values.Select(y => y.Name));
			}, x => {
				if(x == "ALL")
					predProc.IncludeAll = true;
				else
				{
					predProc.IncludeAll = false;
					var text = x.Split('\n');
					var proc = text.Select(y => predProc.Case.Processes.Get(y)).ToList();
					predProc.Values = proc;
				}
				return null;
			}, true, true);
			
            var succProc = form.Options.AddProcessSelect("Successor Process", true, true, false);
			succProc.Case = customDesignAction.ActiveCase;
			succProc.IncludeAll = true;
			succProc.RestoreValue("CHAINDEPENDENCYSProcess",x => {
				if(succProc.IncludeAll)
					return "ALL";
				else 
					return string.Join("\n", succProc.Values.Select(y => y.Name));
			}, x => {
				if(x == "ALL")
					succProc.IncludeAll = true;
				else
				{
					succProc.IncludeAll = false;
					var text = x.Split('\n');
					var proc = text.Select(y => predProc.Case.Processes.Get(y)).ToList();
					succProc.Values = proc;
				}
				return null;
			}, true, true);

            e.OptionsForm = form;
        };
        customDesignAction.SelectAction += (s, e) =>
        {
            var selectedSolid = customDesignAction.SelectedSolids.FirstOrDefault();
            if (selectedSolid == null)
                return;
            Console.WriteLine(selectedSolid.Node.FullName);
            CurrentNodes.Add(selectedSolid);
            var boundary = selectedSolid.Solid.TriangleMesh.CreateSilhouette(Vector3D.UpVector, true);
            RenderNodes(customDesignAction);
        };
        customDesignAction.BackAction += (s, e) =>
        {
            if (CurrentNodes.Count == 0)
            {
                e.Completed = true;
                return;
            }
            CurrentNodes.RemoveAt(CurrentNodes.Count - 1);
            RenderNodes(customDesignAction);
        };
        customDesignAction.ApplyAction += (s, e) =>
        {
			var rule = GetOrCreateDependencyRule(customDesignAction.ActiveCase, customDesignAction.ActionSettings[0].Value.ToString());
			var predProc = customDesignAction.ActionSettings[2] as ProcessSelectOption;
			var succProc = customDesignAction.ActionSettings[3] as ProcessSelectOption;
            INodeSolid previous = null;
            foreach (var item in CurrentNodes)
            {
                if (previous != null)
                {
                    var entry = new RangeDependencyEntry(string.Format("Entry {0}", rule.Entries.Count + 1));
                    entry.PredecessorTextRange = previous.Node.FullName;
                    entry.SuccessorTextRange = item.Node.FullName;
                    rule.Entries.Add(entry);
                    entry.Name = customDesignAction.ActionSettings[1].Value.ToString() + entry.Name;
					entry.IncludeAllPredecessorProcesses = predProc.IncludeAll;
					if(predProc.Values.Any())
						foreach(var p in predProc.Values)
							entry.PredecessorProcesses.Add(p);
					entry.IncludeAllSuccessorProcesses = succProc.IncludeAll;
					if(succProc.Values.Any())
						foreach(var p in succProc.Values)
							entry.SuccessorProcesses.Add(p);
                }
                previous = item;
            }
            e.Completed = false;
			PreviousNodes.Add(CurrentNodes.ToList());
            CurrentNodes.Clear();
            RenderNodes(customDesignAction);
        };
        return customDesignAction;
    }

	private static RangeDependencyRule GetOrCreateDependencyRule(Case scenario, string name, bool create = true)
	{
        RangeDependencyRule rule;
        if (create && !scenario.DependencyRules.ContainsName(name))
        {
            rule = new RangeDependencyRule(name);
            scenario.DependencyRules.DependencyRules.Add(rule);
        }
        else
            rule = scenario.DependencyRules.DependencyRules.Get(name) as RangeDependencyRule;
		return rule;
	}
	
    internal static void RenderNodes(CustomDesignAction customDesignAction)
    {
        customDesignAction.ClearTemporaryShapes();
        CreateRenderNodes(customDesignAction, CurrentNodes, 0);
		foreach(var node in PreviousNodes)
        	CreateRenderNodes(customDesignAction, node, 1);		
    }

	private static void CreateRenderNodes(CustomDesignAction customDesignAction, List<INodeSolid> nodes, int colourIndex)
	{
		Point3D previousCentroid = null;
        foreach (var node in nodes)
        {
            var solid = node.Solid;
            if (previousCentroid != null)
            {
                var backwardsVector = previousCentroid - solid.ApproximateCentroid;
                if (backwardsVector.ZeroLengthXY)
                    continue;
                var point1 = previousCentroid;
                var vector1 = Vector3D.FromDistanceBearingGrade(1, Bearing.FromDegrees(backwardsVector.Bearing.Degrees + 30), 0);
                var vector2 = Vector3D.FromDistanceBearingGrade(1, Bearing.FromDegrees(backwardsVector.Bearing.Degrees - 30), 0);
                var point2 = solid.ApproximateCentroid;
                var point3 = solid.ApproximateCentroid + vector1;
                var point4 = solid.ApproximateCentroid + vector2;
                var arrow = new Shape(new List<Point3D> { point1, point2, point3, point2, point4 });
                arrow.UseColourTable = true;
                arrow.ColourIndex = colourIndex;
                customDesignAction.AddClonedTemporaryShape(arrow, true, true, true, null);
            }
            else
            {
                var point = new Shape(new List<Point3D> { solid.ApproximateCentroid });
                point.UseColourTable = true;
                point.ColourIndex = colourIndex;
                customDesignAction.AddClonedTemporaryShape(point, true, true, true, null);
            }
            previousCentroid = solid.ApproximateCentroid;
        }
	}
	
    #endregion

}