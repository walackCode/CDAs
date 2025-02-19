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
        var customDesignAction = new CustomDesignAction("Draw Cricle", "", "Outlines", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectPoint);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("Draw Circles");
			var outputSelection = form.Options.AddLayerSelect("Output",false);
			var circleradii = form.Options.AddSpinEdit("Radius of Circle");
			e.OptionsForm = form;
		};
		customDesignAction.ApplyAction += (s,e) =>
		{
			var inputs = customDesignAction.ActionInputs.Triangulations;
			Layer outputLayer = (Layer) customDesignAction.ActionSettings[0].Value;
		    var radii = (double)customDesignAction.ActionSettings[1].Value;
			Out.WriteLine(customDesignAction.Selection.SelectedPoint.GetType());
            var point = (Point3D)customDesignAction.Selection.SelectedPoint;

            var centre = point.Clone();
            var tope = new Point3D(centre.X, centre.Y + radii, centre.Z);
            var angle = new Angle(360, AngleUnit.Degree);
            var vector3d = new Vector3D(1, 0, 0);

			var circle = CalculateHelixSection(tope,centre,angle,(int) angle.Degrees/2,0,ref vector3d); //some functions
			var shape = new Shape(circle);
			shape.Closed = true;
			
			outputLayer.Shapes.Add(shape);
			
		};
		return customDesignAction;
    }
	internal static void SetLayerData(string path, List<Shape> shapes)
    {
        var layer = Layer.GetOrCreate(path);

        foreach(var shape in shapes)
            layer.Shapes.Add(shape);
    }

	    public static List<Point3D> CalculateHelixSection(Point3D startPoint, Point3D centerOfRotationCircle, Angle angle, int pointCount, double grade, ref Vector3D forwardTangent)
    {
        var result = new List<Point3D>();
        double radius = (startPoint - centerOfRotationCircle).LengthXY;

        // Calculate the initial angle based on the start point
        var initialAngle = Math.Atan2(startPoint.Y - centerOfRotationCircle.Y, startPoint.X - centerOfRotationCircle.X);

        // Calculate the direction of rotation based on the initial forward tangent
        var rotationDirection = -Math.Sign(forwardTangent.X * (startPoint.Y - centerOfRotationCircle.Y) - forwardTangent.Y * (startPoint.X - centerOfRotationCircle.X));
        // Calculate the angle increment for each point
        var incrementAngle = rotationDirection * angle.Radians / pointCount;

        for (var i = 1; i <= pointCount; i++)
        {
            // Calculate the current angle
            var currentAngle = initialAngle + incrementAngle * i;

            // Convert the current angle to a 2D vector (you need to implement AngleToVector)
            var currentVector = AngleToVector(currentAngle);

            // Calculate the new X and Y coordinates
            var newX = centerOfRotationCircle.X + radius * currentVector.X;
            var newY = centerOfRotationCircle.Y + radius * currentVector.Y;

            // Calculate the new Z coordinate considering the grade
            var delta = radius * (Math.Abs(incrementAngle * i));  // The arc length of the small segment of the circle

            var newZ = startPoint.Z - grade * delta;

            // Create the new point and add it to the list
            var newPoint = new Point3D(newX, newY, newZ);
            result.Add(newPoint);

            // Update the forward tangent
            var nextVector = AngleToVector(currentAngle);
            forwardTangent = new Vector3D(-rotationDirection * nextVector.Y, rotationDirection * nextVector.X, 0).Normalise();
        }

        return result;
    }

    private static Vector2D AngleToVector(double angle)
    {
        return new Vector2D(Math.Cos(angle), Math.Sin(angle));
    }

}
