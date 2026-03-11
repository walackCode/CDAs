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
using PrecisionMining.Common.UI;
using PrecisionMining.Common.UI.Design;
using PrecisionMining.Common.Units;
using PrecisionMining.Common.Util;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Design;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Haulage;
using PrecisionMining.Spry.Scenarios.Scheduling;
using PrecisionMining.Spry.Scripting;
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.UI.Scripting;
using PrecisionMining.Spry.Util;
using PrecisionMining.Spry.Util.OptionsForm;
using PrecisionMining.Spry.Util.UI;
using PrecisionMining.Spry.Workflows;
using PrecisionMining.Spry.UI.Data;
#endregion

public partial class OpenPitDesignWorkflow
{
    public static void Workflow(IWorkflow workflow)
    {
		//Set the name of the workflow
        workflow.Caption = "Open Pit Design Workflow";
		//Restore previous state of workflow
		workflow.RestoreValues("OpenPitDesignWorkflow");
		//Add variables stored between steps
		Layer pitLayer = null;
		Layer firstStripLineLayer = null;
		Layer stripLineLayer = null;
		Layer blockPolyLineLayer = null;
		//Add steps
		var step1 = workflow.Add("Create all attributes");
		//add what the step does when executed
		step1.OnExecute += (s,e) =>
		{
			var attributes = Project.ActiveProject.Design.Attributes;
			attributes.GetOrCreateAttribute("Pit", "", "".GetType());
			attributes.GetOrCreateAttribute("Strip");
			attributes.GetOrCreateAttribute("Block");
			attributes.GetOrCreateAttribute("Seam", "", "".GetType());
			attributes.GetOrCreateAttribute("Bench");
			e.Success = true;
		};
		//Navigation.Dialogs.DesignAttributes;
		step1.OnNavigateTo += (s,e) => Navigation.Dialogs.DesignAttributes();
			
		var step2 = workflow.Add("Select a pit solid layer", "Design Elements");
		step2.OnExecute += (s,e) =>
		{
			var optionsForm = OptionsForm.Create("Select Pit Layer");
        	var layerSelect = optionsForm.Options.AddLayerSelect("Layer");
        	// Assign existing layer if relevant
        	layerSelect.Value = pitLayer;
        	// Show UI
        	optionsForm.ShowDialog();
        	// Set layer value and report error or success
       		pitLayer = layerSelect.Value;
	        if (pitLayer == null)
	            step2.AddErrors("Please select a layer.");
	        else
				e.Success = true;
		};
		step2.OnNavigateTo += (s,e) => Navigation.GoToAction("Triangulation/Create All Closed Volumes");
		
		var step3 = workflow.Add("Select a strip line layer", "Design Elements");
		step3.OnExecute += (s,e) =>
		{
			var optionsForm = OptionsForm.Create("Select strip line Layer");
        	var layerSelect = optionsForm.Options.AddLayerSelect("Layer");
        	// Assign existing layer if relevant
        	layerSelect.Value = firstStripLineLayer;
        	// Show UI
        	optionsForm.ShowDialog();
        	// Set layer value and report error or success
       		firstStripLineLayer = layerSelect.Value;
	        if (firstStripLineLayer == null)
	            step3.AddErrors("Please select a layer.");
			else if (firstStripLineLayer.Shapes.Count != 1)
				step3.AddErrors("Please make sure layer has 1 shape");
	        else
			{
				blockPolyLineLayer = Layer.GetOrCreate("block poly line");
				blockPolyLineLayer.Shapes.Add(firstStripLineLayer.Shapes.First().Clone());
				stripLineLayer = Layer.GetOrCreate("Strip Lines");
				stripLineLayer.Shapes.Clear();
				stripLineLayer.Shapes.Add(firstStripLineLayer.Shapes.First().Clone());
				e.Success = true;
			}
		};
		step3.OnNavigateTo += (s,e) => Navigation.GoToAction("CreateDesignElement");		
		
		var step4 = workflow.Add("Create offset strip lines", "Design Elements");
		step4.OnExecute += (s,e) =>
		{
			var optionsForm = OptionsForm.Create("Inputs to offset strip line");
			var distance = optionsForm.Options.AddSpinEdit("Distance");
			var repetitions = optionsForm.Options.AddSpinEdit("Number of Offsets");
			distance.Value = 100;
			repetitions.Value = 10;
						
			optionsForm.ShowDialog();
			
			using(var progress = Actions.CreateProgressOptions())
			{
				var settings = new ProjectionSettings(distance.Value,0,0);
				var options = Actions.ProjectShapes.CreateOptions(stripLineLayer.Shapes.ToList(),settings,progress, isOffset: true, repetitionCount: Convert.ToInt32(repetitions.Value));
				var projectedLines = Actions.ProjectShapes.Run(options);
				foreach(var line in projectedLines.ProjectedShapes)
				{
					stripLineLayer.Shapes.Add(line);
					blockPolyLineLayer.Shapes.Add(line.Clone());
				}
			}
			
			e.Success = true;
		};
		step4.OnNavigateTo += (s,e) => Navigation.GoToAction("ProjectShapes");
		
		var step5 = workflow.Add("Number the strip lines", "Design Elements");
		step5.OnExecute += (s,e) =>
		{
			using (var progress = Progress.CreateProgressOptions())
			{
				var direction = new Vector3D(1,0,0);
				var inputElements = stripLineLayer.Shapes.ToList();
				var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
				var valueType = AttributeValueType.Number;
				var initialValue = 1;
				var increment = 1;
				var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
				Actions.AssignAttributeByDirection.Run(options);
			}
			e.Success = true;
		};
		step5.OnNavigateTo += (s,e) => Navigation.GoToAction("AssignAttributesByDirection");
		
		var step6 = workflow.Add("Project the strip lines", "Design Elements");
		step6.OnExecute += (s,e) =>
		{
			var optionsForm = OptionsForm.Create("Inputs to project strip lines");
			var distance = optionsForm.Options.AddSpinEdit("Distance");
			var angle = optionsForm.Options.AddSpinEdit("Angle");
			distance.Value = 220;
			angle.Value = 80;
						
			optionsForm.ShowDialog();
			
			using(var progress = Actions.CreateProgressOptions())
			{
				var settings = new ProjectionSettings(distance.Value,Angle.FromDegrees(angle.Value),ProjectionType.Relative);
				var options = Actions.ProjectShapes.CreateOptions(stripLineLayer.Shapes.ToList(),settings,progress);
				var projectedLines = Actions.ProjectShapes.Run(options);
				foreach(var line in projectedLines.ProjectedShapes)
				{
					stripLineLayer.Shapes.Add(line);
				}
			}
			e.Success = true;
		};
		step6.OnNavigateTo += (s,e) => Navigation.GoToAction("ProjectShapes");
		
		var step7 = workflow.Add("Create the strip faces", "Design Elements");
		step7.OnExecute += (s,e) =>
		{
			using(var progress = Actions.CreateProgressOptions())
			{
				var stripFaceLayer = Layer.GetOrCreate("Strip Faces");
				stripFaceLayer.Triangulations.Clear();
				var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
				var options = Actions.TriangulateShapes.CreateOptions(stripLineLayer.Shapes.ToList().GroupBy(x=>x.AttributeValues.GetValue(attribute)),stripFaceLayer,progress);
				var shapes = Actions.TriangulateShapes.Run(options);

				foreach(var tri in shapes.Triangulations)
				{
					stripFaceLayer.Triangulations.Add(tri);
				}
			}
			e.Success = true;
		};
		step7.OnNavigateTo += (s,e) => Navigation.GoToAction("TriangulateShapes");
		
		var step8 = workflow.Add("Cut the pit by strip faces", "Design Elements");
		step8.OnExecute += (s,e) =>
		{
			using(var progress = Actions.CreateProgressOptions())
			{
				var stripFaceLayer = Layer.GetOrCreate("Pit cut into Strips");
				stripFaceLayer.Triangulations.Clear();
				var inputTris = pitLayer.Triangulations.Select(x=> x.Data).ToList();
				
				foreach(var tri in Layer.GetOrThrow("Strip Faces").Triangulations)
				{
					inputTris.Add(tri.Data);
				}
								
				var options = Actions.CreateAllClosedVolumes.CreateOptions(inputTris,progress);
				var shapes = Actions.CreateAllClosedVolumes.Run(options);
				foreach(var tri in shapes.ClosedVolumes)
				{
					var layerTri = new LayerTriangulation(tri);
					stripFaceLayer.Triangulations.Add(layerTri);
				}
			}
			e.Success = true;
		};
		step8.OnNavigateTo += (s,e) => Navigation.GoToAction("CreateAllClosedVolumes");
		
		var step9 = workflow.Add("Number the strip solids", "Design Elements");
		step9.OnExecute += (s,e) =>
		{
			using (var progress = Progress.CreateProgressOptions())
			{
				var direction = new Vector3D(1,0,0);
				var inputElements = Layer.GetOrThrow("Pit cut into Strips").Triangulations.ToList();
				var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
				var valueType = AttributeValueType.Number;
				var initialValue = 1;
				var increment = 1;
				var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
				Actions.AssignAttributeByDirection.Run(options);
			}
			e.Success = true;
		};
		step9.OnNavigateTo += (s,e) => Navigation.GoToAction("AssignAttributesByDirection");
		
		var step10 = workflow.Add("Create block polys", "Design Elements");
		step10.OnExecute += (s,e) =>
		{
			var inputShapes = new List<IShape>();
			var firstStripLine = blockPolyLineLayer.Shapes.First();
			var firstLineFirstPoint = firstStripLine.First();
			var firstLineLastPoint = firstStripLine.Last();
			var lastStripLine = blockPolyLineLayer.Shapes.Last();
			var lastLineFirstPoint = lastStripLine.First();
			var lastLineLastPoint = lastStripLine.Last();
			var blockPolyLayer = Layer.GetOrCreate("Block polys");
			blockPolyLayer.Shapes.Clear();
			
			var firstBlockLinePoints = new List<Point3D>();
			firstBlockLinePoints.Add(firstLineFirstPoint);
			firstBlockLinePoints.Add(lastLineFirstPoint);
			var firstBlockLine = new Shape(firstBlockLinePoints);
			var inputLine = new List<Shape>();
			inputLine.Add(firstBlockLine);
			inputShapes.Add(blockPolyLineLayer.Shapes.First().Clone());
			inputShapes.Add(blockPolyLineLayer.Shapes.Last().Clone());
			
			using(var progress = Actions.CreateProgressOptions())
			{
				var settings = new ProjectionSettings(-100,0,0);
				var options = Actions.ProjectShapes.CreateOptions(inputLine,settings,progress, isOffset: true, repetitionCount: Convert.ToInt32(32));
				var projectedLines = Actions.ProjectShapes.Run(options);
				foreach(var line in projectedLines.ProjectedShapes)
				{
					inputShapes.Add(line);
				}
			}
			
			using(var progress = Progress.CreateProgressOptions())
			{
				var options = Actions.CreateAllClosedAreas.CreateOptions(inputShapes,progress);
				var outputPolys = Actions.CreateAllClosedAreas.Run(options);
				foreach(var poly in outputPolys.Polygons)
				{
					blockPolyLayer.Shapes.Add(new Shape(poly.ToList()));
				}
			}
			
			using (var progress = Progress.CreateProgressOptions())
			{
				var direction = new Vector3D(0,-1,0);
				var inputElements = blockPolyLayer.Shapes.ToList();
				var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Block");
				var valueType = AttributeValueType.Number;
				var initialValue = 1;
				var increment = 1;
				var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
				Actions.AssignAttributeByDirection.Run(options);
			}
			e.Success = true;
		};
		step10.OnNavigateTo += (s,e) => Navigation.GoToAction("CreateAllClosedAreas");
		
		var step11 = workflow.Add("Run cut blocks and benches", "Design Elements");
		step11.OnExecute += (s,e) =>
		{
			List<PrecisionMining.Common.Design.Attribute> attributeList = new List<PrecisionMining.Common.Design.Attribute>();
			var blockPolyLayer = Layer.GetOrCreate("Block polys");
			var cutPitLayer = Layer.GetOrCreate("Pit cut into Strips");
			var attribute1 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Block");
			var attribute3 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Pit","","".GetType());
			var attribute4 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Bench");
			var inputShapes = new List<Shape>();
			var triangulationList = new List<ILayerTriangulation>();
			foreach(var tri in cutPitLayer.Triangulations)
			{
				tri.AttributeValues.SetValue(attribute3,"Alpha");
				triangulationList.Add(tri);
			}
			var polyList = new List<Shape>();
			foreach(var shape in blockPolyLayer.Shapes)
			{
				polyList.Add(shape);
			}
			
			using(var progress = Progress.CreateProgressOptions())
			{
				var mode = CutBlocksAndBenchesOptions.CutBlocksAndBenchesMode.CutBlocksAndBenches;
				var options = Actions.CutBlocksAndBenches.CreateOptions(triangulationList,mode,progress,polyList,40.0,0.0,attribute1,attribute4);
				var cutResults = Actions.CutBlocksAndBenches.Run(options);
				var outputLayer = Layer.GetOrCreate("Blocked and Benched layer");
				outputLayer.Triangulations.Clear();
				
				foreach(var output in cutResults.CutTriangulations)
				{
					var volume = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Volume");
					output.AttributeValues.SetValue(volume,output.GetTriangulationVolume(true));
					outputLayer.Triangulations.Add(output);
				}
			}
			e.Success = true;
		};
		step11.OnNavigateTo += (s,e) => Navigation.GoToAction("CutBlocksAndBenches");
		
		var step12 = workflow.Add("Import data to table");
		step12.OnExecute += (s,e) =>
		{
			e.Success = true;
		};
		step12.OnNavigateTo += (s,e) => Navigation.Dialogs.TableImportSolidsFromDesignLayerInThisProject(Table.GetOrThrow("Example"));
		
		var step13 = workflow.Add("Run data flows");
		step13.OnExecute += (s,e) =>
		{
			var t = Table.GetOrThrow("Example");
			t.DataFlows.First().Run();
			e.Success = true;
		};
		step13.OnNavigateTo += (s,e) => Navigation.GoToTable(Table.GetOrThrow("Example"),PrecisionMining.Spry.UI.Data.ActiveTablePanel.DataFlows);
		
		var step14 = workflow.Add("Run schedule");
		step14.OnExecute += (s,e) =>
		{
			var engine = new SchedulingEngine(Case.GetOrThrow("Example"));
			RunEngine(engine);
			e.Success = true;
		};
		step14.OnNavigateTo += (s,e) => Navigation.GoToCase(Case.GetOrThrow("Example"),PrecisionMining.Spry.UI.Scenarios.ActiveCasePanel.Animation);
    }
	
	public static void RunEngine(SchedulingEngine engine)
    {
		engine.Run();
    }
}
