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

public partial class PitDesignHelper
{	
	private static TextEditOption folderName;
	private static SpinEditOption<double> stripSpacing;
	private static SpinEditOption<double> stripRepetitions;
	private static Vector3DEditOption stripDirectionSelection;
	private static List<IShape> selectedLine;
	[CustomDesignAction]
	internal static CustomDesignAction CreatePolys()
	{
		var customDesignAction = new CustomDesignAction("Create Strip and Block Polys", "", "Design", Array.Empty<Keys>());
		var eventContainer = new EventContainer();
		eventContainer.CDA = customDesignAction;
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElement);
		};
		
		customDesignAction.OptionsForm += (s,e) =>
		{
			var form = OptionsForm.Create("form");
			folderName = form.Options.AddTextEdit("Output Folder Name").SetValue("test").RestoreValue("FolderName",true,true);
			stripSpacing = form.Options.AddSpinEdit("Strip Spacing").SetValue(10).RestoreValue("StripSpacing",true,true);
			stripRepetitions = form.Options.AddSpinEdit("Number of strips").SetValue(10).RestoreValue("StripNumber",true,true);
			stripSpacing.ValueChanged += eventContainer.EditValueChanged;
			stripRepetitions.ValueChanged += eventContainer.EditValueChanged;
			stripDirectionSelection = form.Options.AddVector3DEdit("Strip Direction").RestoreValue("StripDirection", vector => vector.X.ToString() + "/" + vector.Y.ToString() + "/" + vector.Z.ToString(), str => {
				var splits = str.Split('/');
				return new Vector3D(Convert.ToDouble(splits[0]),Convert.ToDouble(splits[1]),Convert.ToDouble(splits[2]));
			},true,true);
			stripDirectionSelection.ValueChanged += eventContainer.EditValueChanged;
			var getVecButton1 = form.Options.AddButtonEdit("Get Vector");
			getVecButton1.ClickAction = (o) => {
				customDesignAction.SecondaryActions.GetVectorAction((done,vec) => {
					if(done)
						stripDirectionSelection.Value = vec.Normalise();
				});
			};
			var blockSpacing = form.Options.AddSpinEdit("Block Spacing").SetValue(10).RestoreValue("BlockSpacing",true,true);
			var blockDirectionSelection = form.Options.AddVector3DEdit("Block Direction").RestoreValue("BlockDirection", vector => vector.X.ToString() + "/" + vector.Y.ToString() + "/" + vector.Z.ToString(), str => {
				var splits = str.Split('/');
				return new Vector3D(Convert.ToDouble(splits[0]),Convert.ToDouble(splits[1]),Convert.ToDouble(splits[2]));
			},true,true);
			var getVecButton2 = form.Options.AddButtonEdit("Get Vector");
			getVecButton2.ClickAction = (o) => {
				customDesignAction.SecondaryActions.GetVectorAction((done,vec) => {
					if(done)
						blockDirectionSelection.Value = vec.Normalise();
				});
			};
			e.OptionsForm = form;
		};
		
		customDesignAction.SelectAction += (s,e) =>
		{
			var x = new List<IShape>();
			x.Add((IShape)customDesignAction.Selection.SelectedDesignElement);
			selectedLine = x;
			customDesignAction.ClearTemporaryShapes();
			CreatePreview(customDesignAction);
		};
		
		customDesignAction.ApplyAction += (s,e) =>
		{
			var stripSpacing = (double) customDesignAction.ActionSettings[1].Value;
			var stripSpacingDirection = GuessSpacingDirection(selectedLine[0], stripSpacing, stripDirectionSelection.Value);
			stripSpacing *= stripSpacingDirection;
			var stripRepetitions = (double) customDesignAction.ActionSettings[2].Value;
			var stripDirection = (Vector3D) customDesignAction.ActionSettings[3].Value;
			var blockSpacing = (double) customDesignAction.ActionSettings[5].Value;
			var blockDirection = (Vector3D) customDesignAction.ActionSettings[6].Value;
			var strips = OffsetInputLine(selectedLine, stripSpacing, stripRepetitions);
			List<IShape> allStrips = selectedLine;
			allStrips.AddRange(strips);
			var stripLineLayer = Layer.GetOrCreate(folderName.Value + @"\Strip Lines");
			stripLineLayer.Shapes.Clear();
			foreach(var strip in allStrips)
			{
				stripLineLayer.Shapes.Add(strip.Clone());
			}
			var blocks = CreateBlockPolys(stripLineLayer.Shapes, blockSpacing);
			var blockLayer = Layer.GetOrCreate(folderName.Value + @"\Block Polys");
			blockLayer.Shapes.Clear();
			foreach(var block in blocks)
			{
				blockLayer.Shapes.Add(block.Clone());
			}
			AttributeLines(blockLayer.Shapes,blockDirection,"Block");
			var stripPolys = CreateStripPolys(stripLineLayer.Shapes);
			var stripPolyLayer = Layer.GetOrCreate(folderName.Value + @"\Strip Polys");
			stripPolyLayer.Shapes.Clear();
			foreach(var strip in stripPolys)
			{
				stripPolyLayer.Shapes.Add(strip.Clone());
			}
			AttributeLines(stripPolyLayer.Shapes,stripDirection,"Strip");
			var finalPolys = CreateFinalPolys(stripPolyLayer, blockLayer);
			var finalPolyLayer = Layer.GetOrCreate(folderName.Value + @"\Final Polys");
			finalPolyLayer.Shapes.Clear();
			foreach(var poly in finalPolys)
			{
				finalPolyLayer.Shapes.Add(poly);
			}
			e.Completed = true;
		};
		return customDesignAction;
	}
	
	internal static double GuessSpacingDirection(IShape shape, double spacing, Vector3D direction) {
		if (direction.Length == 0) {
			return 1.0;
		}
		using (var progress = Progress.CreateProgressOptions())
		{
			var settings = new ProjectionSettings(spacing,0,0);
			var options = Actions.ProjectShapes.CreateOptions(new[] { shape }, settings, progress, isOffset: true, repetitionCount: 1);
			var projectedLines = Actions.ProjectShapes.Run(options);
			var testLine1 = projectedLines.ProjectedShapes.FirstOrDefault();
			if (testLine1 == null)
				return 0.0;
			
			settings.Distance = -spacing;
			options = Actions.ProjectShapes.CreateOptions(new[] { shape }, settings, progress, isOffset: true, repetitionCount: 1);
			projectedLines = Actions.ProjectShapes.Run(options);
			var testLine2 = projectedLines.ProjectedShapes.FirstOrDefault();
			if (testLine2 == null)
				return 0.0;
			
			var point = shape[0] + direction.Normalise() * spacing;
			var testPoint1 = new Line3D(testLine1[0], testLine1[1]).GetClosestPointOnLine(point);
			var testPoint2 = new Line3D(testLine2[0], testLine2[1]).GetClosestPointOnLine(point);
			var testDistance1 = (testPoint1 - point).Length;
			var testDistance2 = (testPoint2 - point).Length;
			
			if ((point - testPoint1).Length < (point - testPoint2).Length) {
				return 1.0;
			} else {
				return -1.0;
			}
		}
	}
	
	[CustomDesignAction]
	internal static CustomDesignAction CutPit()
	{
		var customDesignAction = new CustomDesignAction("Cut up Pit Solid", "", "Design", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = false;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.None);
		};
		
		customDesignAction.OptionsForm += (s,e) =>
		{
			var form = OptionsForm.Create("form");
			folderName = form.Options.AddTextEdit("Output Folder Name").SetValue("test").RestoreValue("FolderName",true,true);
			var pitLayer = form.Options.AddLayerSelect("Pit Layer").RestoreValue("PitLayer", layer => layer.FullName, str => Layer.GetOrThrow(str),true,true);
			var polyLayer = form.Options.AddLayerSelect("Poly Layer").RestoreValue("PolyLayer", layer => layer.FullName, str => Layer.GetOrThrow(str),true,true);
			var stratigraphy = form.Options.AddTextEdit("Stratigraphy Name").RestoreValue("Stratigraphy",true,true);
			var benchHeight = form.Options.AddSpinEdit("Bench Height").SetValue(5).RestoreValue("BenchHeight",true,true);
			var referenceBench = form.Options.AddSpinEdit("Reference Bench").SetValue(0).RestoreValue("ReferenceBench",true,true);
			var pitName = form.Options.AddTextEdit("Pit Name").SetValue("Default").RestoreValue("PitName",true,true);
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var outputPrefix = (string) customDesignAction.ActionSettings[0].Value;	
			var pitLayer = (Layer) customDesignAction.ActionSettings[1].Value;
			var polyLayer = (Layer) customDesignAction.ActionSettings[2].Value;
			var stratigraphyName = (string) customDesignAction.ActionSettings[3].Value;
			var benchHeight = (double) customDesignAction.ActionSettings[4].Value;
			var referenceBench = (double) customDesignAction.ActionSettings[5].Value;
			var pitName = (string) customDesignAction.ActionSettings[6].Value;
			var volumeAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Volume");
			var stripsAndBlocks = CutPitToPolys(pitLayer,polyLayer,pitName);
			var stripsAndBlocksLayer = Layer.GetOrCreate(outputPrefix + @"/Strips and Blocks");
			stripsAndBlocksLayer.Triangulations.Clear();
			foreach(var tri in stripsAndBlocks)
				stripsAndBlocksLayer.Triangulations.Add(tri);
			var cutStratigraphy = CutStratigraphy(stripsAndBlocksLayer, stratigraphyName);
			var stratigraphyLayer = Layer.GetOrCreate(outputPrefix + @"/Stratigraphy");
			stratigraphyLayer.Triangulations.Clear();
			foreach(var tri in cutStratigraphy)
				stratigraphyLayer.Triangulations.Add(tri);
			var cutBenches = CutBenches(stratigraphyLayer, benchHeight, referenceBench);
			var benchedLayer = Layer.GetOrCreate(outputPrefix + @"/Benched");
			benchedLayer.Triangulations.Clear();
			foreach(var tri in cutBenches)
			{
				tri.AttributeValues.SetValue(volumeAttribute,tri.GetTriangulationVolume(true));
				benchedLayer.Triangulations.Add(tri);
			}
			e.Completed = true;
		};
		return customDesignAction;
	}
	
	private static void CreatePreview(CustomDesignAction customDesignAction)
	{
		var offset = GuessSpacingDirection(selectedLine[0], stripSpacing.Value, stripDirectionSelection.Value);
		var strips = OffsetInputLine(selectedLine, stripSpacing.Value * offset, stripRepetitions.Value);
		foreach(var strip in strips)
		{
			customDesignAction.AddClonedTemporaryShape(strip,true,false,true,null);
		}
	}
	
	public class EventContainer
	{
		public CustomDesignAction CDA { get; set;}
		public void EditValueChanged(object sender, EventArgs e)
		{
			if (CDA == null)
				return;
			CDA.ClearTemporaryShapes();
			CreatePreview(CDA);
		}	
	}
	
    private static List<IShape> OffsetInputLine(List<IShape> inputShapesList, double spacing, double repetitions)
    {
		using (var progress = Progress.CreateProgressOptions())
		{
			var settings = new ProjectionSettings(spacing,0,0);
			var options = Actions.ProjectShapes.CreateOptions(inputShapesList,settings,progress, isOffset: true, repetitionCount: Convert.ToInt32(repetitions));
			var projectedLines = Actions.ProjectShapes.Run(options);
			return projectedLines.ProjectedShapes;
		}
    }
	
	private static void AttributeLines(Shapes inputElements, Vector3D direction, string attributeName)
	{
		using (var progress = Progress.CreateProgressOptions())
		{
			var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(attributeName);
			var valueType = AttributeValueType.Number;
			var initialValue = 1;
			var increment = 1;
			var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
			Actions.AssignAttributeByDirection.Run(options);
		}
	}
	
	//example of how to project strip faces
	private static void ProjectStrips()
	{
		var inputLayer = Layer.GetOrThrow("Offset Lines");
		var outputLayer = Layer.GetOrCreate("Strip Faces");
		var inputShapeList = new List<Shape>();
		foreach(var shape in inputLayer.Shapes)
		{
			inputShapeList.Add(shape);
		}
		using(var progress = Progress.CreateProgressOptions())
		{
			var settings = new ProjectionSettings(250,75,ProjectionType.Relative);
			var options = Actions.ProjectShapes.CreateOptions(inputShapeList,settings,progress);
			var projectedLines = Actions.ProjectShapes.Run(options);
			foreach(var line in projectedLines.ProjectedShapes)
			{
				inputShapeList.Add(line as Shape);
			}
		}
		
		using(var progress = Progress.CreateProgressOptions())
		{
			var groupingAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
			var groupedInput = inputShapeList.GroupBy(x => x.AttributeValues.GetValue(groupingAttribute));
			var groupingAttributes = new List<PrecisionMining.Common.Design.Attribute>();
			groupingAttributes.Add(groupingAttribute);
			var options = Actions.TriangulateShapes.CreateOptions(groupedInput,outputLayer,progress,attributesTo: groupingAttributes);
			var stripFaces = Actions.TriangulateShapes.Run(options);
			foreach(var strip in stripFaces.Triangulations)
			{
				outputLayer.Triangulations.Add(strip);
			}
		}
	}
	
	//example of how to create pit solid from a pit shell and topo
	private static void CreatePitSolid()
	{
		var inputLayer = Layer.GetOrThrow("Pit Shell");
		var outputLayer = Layer.GetOrCreate("Pit Solid");
		var inputMeshes = new List<TriangleMesh>();
		foreach(var triangulation in inputLayer.Triangulations)
		{
			inputMeshes.Add(triangulation.Data);
		}
		using (var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.CreateAllClosedVolumes.CreateOptions(inputMeshes, progress);
			var pitSolid = Actions.CreateAllClosedVolumes.Run(options);
			foreach(var solid in pitSolid.ClosedVolumes)
			{
				outputLayer.Triangulations.Add(new LayerTriangulation(solid));
			}
		}
	}
	
	//example of how to cut strips with angled faces
	private static void CutStrips()
	{
		var pitLayer = Layer.GetOrThrow("Pit Solid");
		var stripLayer = Layer.GetOrThrow("Strip Faces");
		var outputLayer = Layer.GetOrCreate("Strips");
		
		using(var progress = Progress.CreateProgressOptions())
		{
			var inputMeshes = new List<TriangleMesh>();
			foreach(var triangulation in pitLayer.Triangulations)
			{
				inputMeshes.Add(triangulation.Data);
			}		
			foreach(var triangulation in stripLayer.Triangulations)
			{
				inputMeshes.Add(triangulation.Data);
			}
					var options = Actions.CreateAllClosedVolumes.CreateOptions(inputMeshes, progress);
			var pitSolid = Actions.CreateAllClosedVolumes.Run(options);
			foreach(var solid in pitSolid.ClosedVolumes)
			{
				outputLayer.Triangulations.Add(new LayerTriangulation(solid));
			}
		}
		
		using(var progress = Progress.CreateProgressOptions())
		{
			var inputLayer = outputLayer;
			var inputElements = inputLayer.Triangulations;
			var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
			var direction = new Vector3D(1,0,0);
			var valueType = AttributeValueType.Number;
			var initialValue = 1;
			var increment = 1;
			
			var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
			Actions.AssignAttributeByDirection.Run(options);
		}
	}
	
	private static List<Shape> CreateBlockPolys(Shapes inputStrips, double blockSpacing)
	{
		var firstLine = inputStrips.First();
		var firstLinePoint = firstLine.First();
		var lastLine = inputStrips.Last();
		var lastLinePoint = lastLine.First();
		var blockLine = new Shape();
		blockLine.Add(firstLinePoint);
		blockLine.Add(lastLinePoint);
		List<Shape> inputShapesList = new List<Shape>();
		var totalLength = (firstLine.First().CloneXY(0) - firstLine.Last().CloneXY(0)).Length;
		var totalCount = Math.Floor(totalLength/blockSpacing);
		
		var blockDirection = new Vector3D(firstLine.Last().X - firstLine.First().X, firstLine.Last().Y - firstLine.First().Y, 0);
		
		var spacingToggle = GuessSpacingDirection(blockLine,blockSpacing,blockDirection);
		blockSpacing *= spacingToggle;
		
		using(var progress = Progress.CreateProgressOptions())
		{
			inputShapesList.Add(blockLine);
			var settings = new ProjectionSettings(blockSpacing,0,0);
	        var options = Actions.ProjectShapes.CreateOptions(inputShapesList,settings,progress, isOffset: true, repetitionCount: Convert.ToInt32(totalCount));
			var projectedLines = Actions.ProjectShapes.Run(options);
			foreach(var shape in projectedLines.ProjectedShapes)
				inputShapesList.Add(new Shape(shape));
		}
		
		inputShapesList.Add(firstLine);
		inputShapesList.Add(lastLine);
		List<Shape> shapeList = new List<Shape>();
		using(var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.CreateAllClosedAreas.CreateOptions(inputShapesList,progress);
			var polys = Actions.CreateAllClosedAreas.Run(options);
			foreach(var poly in polys.Polygons)
			{
				var pointList = new List<Point3D>();
				foreach(var point in poly)
				{
					pointList.Add(point);
				}
				var shape = new Shape(pointList);
				shape.Closed = true;
				shapeList.Add(shape);
			}
		}
		return shapeList;
	}
	
	private static List<Shape> CreateStripPolys(Shapes inputStrips)
	{
		var firstLine = inputStrips.First();
		var firstLineFirstPoint = firstLine.First();
		var firstLineLastPoint = firstLine.Last();
		var lastLine = inputStrips.Last();
		var lastLineFirstPoint = lastLine.First();
		var lastLineLastPoint = lastLine.Last();
		var firstBlockLine = new Shape();
		firstBlockLine.Add(firstLineFirstPoint);
		firstBlockLine.Add(lastLineFirstPoint);
		var lastBlockLine = new Shape();
		lastBlockLine.Add(firstLineLastPoint);
		lastBlockLine.Add(lastLineLastPoint);
		
		List<Shape> inputShapesList = new List<Shape>();
		foreach(var shape in inputStrips)
			inputShapesList.Add(shape);
		inputShapesList.Add(firstBlockLine);
		inputShapesList.Add(lastBlockLine);
		List<Shape> shapeList = new List<Shape>();
		using(var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.CreateAllClosedAreas.CreateOptions(inputShapesList,progress);
			var polys = Actions.CreateAllClosedAreas.Run(options);
			foreach(var poly in polys.Polygons)
			{
				var pointList = new List<Point3D>();
				foreach(var point in poly)
				{
					pointList.Add(point);
				}
				var shape = new Shape(pointList);
				shape.Closed = true;
				shapeList.Add(shape);
			}
		}
		
		return shapeList;
	}
	
	private static List<IShape> CreateFinalPolys(Layer stripPolysLayer, Layer blockPolysLayer)
	{
		List<PrecisionMining.Common.Design.Attribute> attributeList = new List<PrecisionMining.Common.Design.Attribute>();
		var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Block");
		attributeList.Add(attribute);
		List<Shape> stripPolys = new List<Shape>();
		foreach(var shape in stripPolysLayer.Shapes)
		{
			stripPolys.Add(shape);
		}
		List<Shape> blockPolys = new List<Shape>();
		foreach(var block in blockPolysLayer.Shapes)
		{
			blockPolys.Add(block);
		}
		List<LayerTriangulation> emptyTriangulationList = new List<LayerTriangulation>();
		using(var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.IntersectWithPolygonRegions.CreateOptions(stripPolys, emptyTriangulationList, blockPolys, progress, attributeList);
			var polys = Actions.IntersectWithPolygonRegions.Run(options);
			return polys.Shapes;
		}
	}
	
	private static List<LayerTriangulation> CutPitToPolys(Layer pitLayer, Layer polyLayer, string pitName)
	{
		List<PrecisionMining.Common.Design.Attribute> attributeList = new List<PrecisionMining.Common.Design.Attribute>();
		var attribute1 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Block");
		var attribute2 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
		var attribute3 = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Pit","",pitName.GetType());
		attributeList.Add(attribute1);
		attributeList.Add(attribute2);
		var inputShapes = new List<Shape>();
		var triangulationList = new List<LayerTriangulation>();
		foreach(var tri in pitLayer.Triangulations)
		{
			tri.AttributeValues.SetValue(attribute3,pitName);
			triangulationList.Add(tri);
		}
		var polyList = new List<Shape>();
		foreach(var shape in polyLayer.Shapes)
		{
			polyList.Add(shape);
		}
		using(var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.IntersectWithPolygonRegions.CreateOptions(inputShapes, triangulationList,polyList,progress,attributeList);
			var cutPit = Actions.IntersectWithPolygonRegions.Run(options);
			var outputTris = new List<LayerTriangulation>();
			foreach(var tri in cutPit.Triangulations)
			{
				outputTris.Add(tri as LayerTriangulation);
			}
			return outputTris;
		}
	}
	
	private static List<LayerTriangulation> CutStratigraphy(Layer blockedAndStrippedLayer, string stratigraphyName)
	{
		List<ILayerTriangulation> inputTris = blockedAndStrippedLayer.Triangulations.Cast<ILayerTriangulation>().ToList();
		var stratigraphy = Project.ActiveProject.Design.Stratigraphies.GetStratigraphy(stratigraphyName);
		var horizon = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Horizon", "", stratigraphyName.GetType());
		var material = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Material", "", stratigraphyName.GetType());
		var sequence = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Sequence");
		using (var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.CutStratigraphy.CreateOptions(inputTris,stratigraphy,progress,true,horizon,material,sequence);
			var cutStratigraphy = Actions.CutStratigraphy.Run(options);
			List<LayerTriangulation> outputList = new List<LayerTriangulation>();
			foreach(var tri in cutStratigraphy.CutTriangulations)
			{
				outputList.Add(tri as LayerTriangulation);
			}
			return outputList;
		}
	}
	
	private static List<ILayerTriangulation> CutBenches(Layer stratigraphyLayer, double benchThickness, double referenceBench)
	{
		var inputs = stratigraphyLayer.Triangulations.Cast<ILayerTriangulation>().ToList();
		var mode = CutBlockAndBenchesTriangulationOptions.CutBlocksAndBenchesMode.CutBenches;
		List<IShape> emptyBlocks = new List<IShape>();
		var benchFloorAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Bench");
		var benchRoofAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Roof");
		var blockAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Block");
		using(var progress = Progress.CreateProgressOptions())
		{
			var options = Actions.CutBlocksAndBenches.CreateOptions(inputs, mode, progress, emptyBlocks, benchThickness, referenceBench, blockAttribute,  benchRoofAttribute, benchFloorAttribute);
			var cutBenches = Actions.CutBlocksAndBenches.Run(options);
			return cutBenches.CutTriangulations;
		}
	}
}
