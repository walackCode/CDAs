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
        var customDesignAction = new CustomDesignAction("Calculate Area", "", "Geometry", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("test");
			var outputSelection = form.Options.AddAttributeSelect("Attribute", false);
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var inputs = customDesignAction.ActionInputs.Shapes;
			var tris = customDesignAction.ActionInputs.Triangulations;
			PrecisionMining.Common.Design.Attribute outputAttribute = (PrecisionMining.Common.Design.Attribute) customDesignAction.ActionSettings[0].Value;
			foreach(var tri in tris)
			{
				var polyList = tri.Data.CreateSilhouette(new Vector3D(0,0,1), true);
				var areaSum = 0.0;
				foreach(var poly in polyList)
				{
					List<Point3D> pointList = new List<Point3D>();
					foreach(var point in poly)
					{
						pointList.Add(point);
					}
					Polygon2D poly2D = new Polygon2D(pointList);
					areaSum += poly2D.Area;
				}
				tri.AttributeValues.SetValue(outputAttribute,areaSum);
			}
			
			Console.WriteLine(inputs.Count());
			foreach(var shape in inputs)
			{
				List<Point3D> pointList = new List<Point3D>();
				foreach(var point in shape)
				{
					pointList.Add(point);
				}
				Polygon2D poly = new Polygon2D(pointList);
				shape.AttributeValues.SetValue(outputAttribute,poly.Area);
			}
		};
		return customDesignAction;
    }
}
