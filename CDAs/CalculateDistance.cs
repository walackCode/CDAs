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

public partial class CalculateDistance
{
	
	[CustomDesignAction]
	internal static CustomDesignAction CreateDistance()
	{		
        var customDesignAction = new CustomDesignAction("Invoke Euclid", "", "Geometry", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("test");
			var outputSelection = form.Options.AddAttributeSelect("Attribute", false);
			var directionSelection = form.Options.AddVector3DEdit("Direction");
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var inputs = customDesignAction.ActionInputs.Triangulations;
			PrecisionMining.Common.Design.Attribute outputAttribute = (PrecisionMining.Common.Design.Attribute) customDesignAction.ActionSettings[0].Value;
			var direction = (Vector3D) customDesignAction.ActionSettings[1].Value;
			
			foreach(var solid in inputs)
			{				
				var centroid = solid.Data.Properties.VolumetricCentroid;
				var offsetDistance = (solid.Data.Aabb.Maximum-solid.Data.Aabb.Minimum).Length;
				var pointOutside = new Point3D(centroid.X + direction.X * offsetDistance,centroid.Y + direction.Y * offsetDistance,centroid.Z + direction.Z * offsetDistance);
				var directionBack = direction.Reverse();
				var ray = new Ray3D(pointOutside,directionBack);
				Console.WriteLine(centroid.X);
				Console.WriteLine(centroid.Y);
				Console.WriteLine(centroid.Z);
				Console.WriteLine(pointOutside.X);
				Console.WriteLine(pointOutside.Y);
				Console.WriteLine(pointOutside.Z);
				var point1 = new Point3D(0,0,0);
				var point2 = new Point3D(0,0,0);
				var firstPoint = true;
				foreach(var tri in solid.Data)
				{
					var intersect = CheckInterSects(tri,directionBack, pointOutside.Subtract(new Point3D(0,0,0)));
					Console.WriteLine(intersect);
					if (!intersect)
						continue;
					
					var intersectPoint = ray.GetPlaneIntersection(new Plane(tri.FirstPoint,tri.SecondPoint,tri.ThirdPoint));
					if(firstPoint)
					{
						point1 = intersectPoint;
						firstPoint = false;
						continue;
					}
					if(intersectPoint == point1)
						continue;
					point2 = intersectPoint;
					break;
				}
				var distance = (point1-point2).Length;
				Console.WriteLine(outputAttribute.Name);
				solid.AttributeValues.SetValue(outputAttribute,distance);
			}
		};
		return customDesignAction;
    }
	static bool CheckInterSects(TriangleMeshTriangle t, Vector3D rayDirection, Vector3D rayOrigin)
	{
		var distance = double.MaxValue;
		Vector3D v1 = new Vector3D(t.FirstPoint.X,t.FirstPoint.Y,t.FirstPoint.Z);
		Vector3D v2 = new Vector3D(t.SecondPoint.X,t.SecondPoint.Y,t.SecondPoint.Z);
		Vector3D v3 = new Vector3D(t.ThirdPoint.X,t.ThirdPoint.Y,t.ThirdPoint.Z);
		const float EPSILON = 0.000001f;
		Vector3D edge1, edge2, h, s, q;
		double a, f, u, v;
		
		edge1 = v2-v1;
		edge2 = v3-v1;
		h = rayDirection.CrossProduct(edge2);
        a = edge1.DotProduct(h);

        if (a > -EPSILON && a < EPSILON)
            return false;

        f = 1.0f / a;
        s = rayOrigin - v1;
        u = f * s.DotProduct(h);

        if (u < 0.0 || u > 1.0)
            return false;

        q = s.CrossProduct(edge1);
        v = f * rayDirection.DotProduct(q);

        if (v < 0.0 || u + v > 1.0)
            return false;

        distance = f * edge2.DotProduct(q);

        if (distance > EPSILON)
            return true;

        return false;
	}
}