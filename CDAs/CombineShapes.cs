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

public partial class CalculateArea
{
	
	[CustomDesignAction]
	internal static CustomDesignAction Calculate()
	{		
        var customDesignAction = new CustomDesignAction("Merge Shapes", "", "Shape", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("test");
			var layerSelection = form.Options.AddLayerSelect("Output",false);
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var inputs = customDesignAction.ActionInputs.Shapes;

			Layer outputLayer = (Layer) customDesignAction.ActionSettings[0].Value;
			
			HashSet<Point3D> pointList = new HashSet<Point3D>();
			var firstStart = inputs.First().First();
			var secondStart = inputs.First().Last();
			
			pointList.Add(firstStart);
			
			
			List<Shape> shapeList = new List<Shape>();
			
			foreach(var shape in inputs)
			{
				var shape2 = (Shape) shape;
				shapeList.Add(shape2);
			}
			
			pointList = addPointsForward(pointList, shapeList);
			
			Shape finalShape = new Shape(pointList);
			
			foreach(var attribute in Project.ActiveProject.Design.Attributes.AllAttributes)
			{
				finalShape.AttributeValues[attribute] = inputs.First().AttributeValues[attribute];
			}
			
			outputLayer.Shapes.Add(finalShape);
			
		};
		return customDesignAction;
    }
	
	static public HashSet<Point3D> addPointsForward(HashSet<Point3D> line, List<Shape> shapes)
	{
		var matchingShapes = shapes.Where(x => x.Where(y => y == line.Last()).Count() > 0);
		Console.WriteLine(matchingShapes.Count());
		if (matchingShapes.Count() > 1)
			Console.WriteLine("Diverging shapes detected");
		if (matchingShapes.Count() == 0)
		{
			return line;
		}
		foreach(var s in matchingShapes)
		{
			foreach(var p in s)
			{
				line.Add(p);
			}
		}
		shapes.Remove(matchingShapes.First());
		return addPointsForward(line,shapes);
	}
}
