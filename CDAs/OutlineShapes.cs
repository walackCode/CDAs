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

public partial class CuttingShapes
{
	static List<Polygon> selectedShapeBoundary = new List<Polygon>();
	
	[CustomDesignAction]
	internal static CustomDesignAction CreateOutline()
	{		
        var customDesignAction = new CustomDesignAction("Create Outline", "", "Outlines", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("test");
			var outputSelection = form.Options.AddLayerSelect("Output",false);
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var inputs = customDesignAction.ActionInputs.Triangulations;
			Layer outputLayer = (Layer) customDesignAction.ActionSettings[0].Value;
	
			List<TriangleMesh> triangleList = new List<TriangleMesh>();
			foreach(var tri in inputs)
			{
				triangleList.Add(tri.Data);
			}
			TriangleMesh triangleMesh = TriangleMesh.UnionSolids(triangleList).OutputMesh;
			selectedShapeBoundary = triangleMesh.CreateSilhouette(Vector3D.UpVector, true);
			var newShapes = new List<Shape>();
			foreach(var poly in selectedShapeBoundary)
			{
				newShapes.Add(new Shape(poly));			
			}
			SetLayerData(outputLayer.FullName, newShapes);
		};
		return customDesignAction;
    }
	internal static void SetLayerData(string path, List<Shape> shapes)
    {
        var layer = Layer.GetOrCreate(path);

        foreach(var shape in shapes)
            layer.Shapes.Add(shape);
    }
}
