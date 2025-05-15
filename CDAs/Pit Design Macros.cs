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
    public static void OffsetInputLayer()
    {
		var inputLayer = Layer.GetOrThrow("Input");
		var outputLayer = Layer.GetOrCreate("Offset Lines");
		List<Shape> inputShapesList = new List<Shape>();
		foreach(var shape in inputLayer.Shapes)
		{
			inputShapesList.Add(shape);
			outputLayer.Shapes.Add(shape.Clone());
		}
		var settings = new ProjectionSettings(5,0,0);
		var progress = Progress.CreateProgressOptions();
        var options = Actions.ProjectShapes.CreateOptions(inputShapesList,settings,progress, isOffset: true, repetitionCount: 2);
		var projectedLines = Actions.ProjectShapes.Run(options);
		foreach(var shape in projectedLines.ProjectedShapes)
			outputLayer.Shapes.Add(shape);
    }
	
	public static void AttributeStripLines()
	{
		var inputLayer = Layer.GetOrThrow("Offset Lines");
		var inputElements = inputLayer.Shapes;
		var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
		var progress = Progress.CreateProgressOptions();
		var direction = new Vector3D(0,1,0);
		var valueType = AttributeValueType.Number;
		var initialValue = 1;
		var increment = 1;
		
		var options = Actions.AssignAttributeByDirection.CreateOptions(inputElements, attribute, progress, direction, valueType, initialValue, increment);
		Actions.AssignAttributeByDirection.Run(options);
	}
	
	public static void ProjectStrips()
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
			var settings = new ProjectionSettings(10,75,ProjectionType.Relative);
			var options = Actions.ProjectShapes.CreateOptions(inputShapeList,settings,progress);
			var projectedLines = Actions.ProjectShapes.Run(options);
			foreach(var line in projectedLines.ProjectedShapes)
			{
				inputShapeList.Add(line as Shape);
			}
		}
		Console.WriteLine(inputShapeList.Count);
		
		using(var progress = Progress.CreateProgressOptions())
		{
			var groupingAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Strip");
			var groupedInput = inputShapeList.GroupBy(x => x.AttributeValues.GetValue(groupingAttribute));
			Console.WriteLine(groupedInput.Count());
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
}