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

// 2025.06.18.2030 initial impl
// 2025.08.08.1220 prototype apparent dip calculations added, fix bug in dip range 1 not being converted to radians

public partial class CalculateDip {
	static string DEBUG_LAYER_NAME = "DEBUG_FLOOR_TRIANGLES";
	const int DEBUGGING_LIMIT = 10;

	static string APPARENT_DIP_MODE_NONE = "None";
	static string APPARENT_DIP_MODE_FROM_ATTRIBUTE = "Bearing from Attribute";
	static string APPARENT_DIP_MODE_MANUAL_VALUE = "Bearing from manual value";
	private static ComboBoxOption<string> comboCrossDipMode;
	private static AttributeSelectOption apparentDipAttributeSelectionOption;
	private static SpinEditOption<double> apparentDipManualValueOption;

	[CustomDesignAction]
	internal static CustomDesignAction Calculate() {

		var customDesignAction = new CustomDesignAction("Calculate Dip", "", "Geometry", Array.Empty<Keys>());
		TextLabel textError = null;
		CheckBoxOption debugCheckbox = null;

		SpinEditOption<double> range1SpinEdit = null;
		SpinEditOption<double> range2SpinEdit = null;
		AttributeSelectOption outputAttributeSelectionOption = null;
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s, e) => {
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.SelectionFilter += (s, e) => {
			//e.Filtered = e.DesignCollectionElement is LayerTriangulation;
			e.Filtered = true;
			//e.Filtered = false;
		};
		customDesignAction.OptionsForm += (s, e) => {
			var form = OptionsForm.Create("test");
			var notesButton = form.Options.AddButtonEdit("Warning/Notes");
			notesButton.ClickAction += (x) => {
				var warning = PrecisionMining.Spry.Util.OptionsForm.OptionsForm.Create("Notes/Warning");
				warning.Options.AddTextLabel("Warning", "This is still highly experimental!");
				warning.Options.AddTextLabel("", "Debugging will show debugging triangles " + DEBUG_LAYER_NAME + " layer, limited to " + DEBUGGING_LIMIT + " elements");
				warning.Options.AddTextLabel("", "Triangles with dip outside of range limits will be ignored");
				warning.Options.AddTextLabel("", "This is an dialogue because of bugs or inability to use CDA"); // todo is there a better way than this?
				warning.ShowDialog();
			};

			comboCrossDipMode = form.Options.AddComboBox<string>("Apparent Dip Calculation");
			comboCrossDipMode.Items.Add(APPARENT_DIP_MODE_NONE, APPARENT_DIP_MODE_NONE);
			comboCrossDipMode.Items.Add(APPARENT_DIP_MODE_FROM_ATTRIBUTE, APPARENT_DIP_MODE_FROM_ATTRIBUTE);
			comboCrossDipMode.Items.Add(APPARENT_DIP_MODE_MANUAL_VALUE, APPARENT_DIP_MODE_MANUAL_VALUE);
			comboCrossDipMode.RestoreValue("CALCULATE_APPARENT_DIP_MODE", a => a, a => a);

			apparentDipAttributeSelectionOption = form.Options.AddAttributeSelect("Apparent Dip Attribute (degrees)", false)
				.RestoreValue("CALCULATE_APPARENT_DIP_ATTRIBUTE", a => a.FullName, a => Project.ActiveProject.Design.Attributes.GetAttributeOrThrow(a));

			apparentDipManualValueOption = form.Options.AddSpinEdit("Apparent Dip Manual Value (degrees)")
				.SetValue(0)
				.RestoreValue("APPARENT_DIP_MANUAL_VALUE", a => a, a => double.Parse(a.ToString()));

			comboCrossDipMode.ValueChanged += (x, y) => {
				if (comboCrossDipMode.Value == APPARENT_DIP_MODE_NONE) {
					apparentDipAttributeSelectionOption.Enabled = false;
					apparentDipManualValueOption.Enabled = false;
				}
				else if (comboCrossDipMode.Value == APPARENT_DIP_MODE_FROM_ATTRIBUTE) {
					apparentDipAttributeSelectionOption.Enabled = true;
					apparentDipManualValueOption.Enabled = false;
				}
				else if (comboCrossDipMode.Value == APPARENT_DIP_MODE_MANUAL_VALUE) {
					apparentDipAttributeSelectionOption.Enabled = false;
					apparentDipManualValueOption.Enabled = true;
				}
			};
			comboCrossDipMode.ForceValueChangedEvent(); // trigger initial state

			outputAttributeSelectionOption = form.Options.AddAttributeSelect("Output Attribute", false)
	.RestoreValue("CALCULATE_DIP_OUTPUT_ATTRIBUTE", a => a.FullName, a => Project.ActiveProject.Design.Attributes.GetAttributeOrThrow(a));
			debugCheckbox = form.Options.AddCheckBox("Debug");
			range1SpinEdit = form.Options.AddSpinEdit("Limit 1 (degrees)").SetValue(0).RestoreValue("DIP_CALC_LIMIT_1");
			range2SpinEdit = form.Options.AddSpinEdit("Limit 2 (degrees)").SetValue(30).RestoreValue("DIP_CALC_LIMIT_2");
			textError = form.Options.AddTextLabel("", "");

			outputAttributeSelectionOption.Validators.Add(x => {
				if (x == null)
					return "No output attribute selected";
				return null;
			});

			// debugCheckbox.Validators.Add(check =>
			// {
			// 	// todo - is there a better way to do this
			// 	var countTake11 = customDesignAction.Selection.SelectedDesignElements.Take(11).Count(); // clip to 11, avoids counting entire collection as unclear if this is list backed rn
			// 	//Console.WriteLine("debug validator checkbox {0} ", countTake11);
			// 	if (!check)
			// 		return null;
			// 	if (countTake11 > 10)
			// 		return "Cannot debug with > 10 solids";
			// 	return null;

			// });

			// todo
			//form.Validators.Add(() => "don't use this, it doesn't appear to be triggered");

			e.OptionsForm = form;
		};
		customDesignAction.SelectAction += (s, e) => {
			if (customDesignAction.SelectedSolids.Any())
				textError.SetValue("Scheduling solids appear to be selected - these will be ignored as this action is for design solids only");
			else
				textError.SetValue("");

			debugCheckbox.ForceValueChangedEvent();
		};

		customDesignAction.ApplyAction += (s, e) => {
			var inputs = customDesignAction.ActionInputs.Shapes;
			var tris = customDesignAction.ActionInputs.Triangulations;
			PrecisionMining.Common.Design.Attribute outputAttribute = outputAttributeSelectionOption.Value;
			var debug = debugCheckbox.Value;
			var range1 = range1SpinEdit.Value;
			var range2 = range2SpinEdit.Value;

			List<DipDebugInfo> debugging = null;
			if (debug)
				debugging = new List<DipDebugInfo>();

			foreach (var tri in tris) {
				double? bearingRadians = null;
				if (comboCrossDipMode.Value == APPARENT_DIP_MODE_FROM_ATTRIBUTE) {
					var bearingDegrees = (double)tri.AttributeValues.GetValue(apparentDipAttributeSelectionOption.Value);
					bearingRadians = BearingDegreesToPolarRadians(bearingDegrees);
				}
				else if (comboCrossDipMode.Value == APPARENT_DIP_MODE_MANUAL_VALUE) {
					var bearingDegrees = apparentDipManualValueOption.Value;
					bearingRadians = BearingDegreesToPolarRadians(bearingDegrees);
				}

				var dip = CalculateDipRadians(tri.Data, range1 * Math.PI / 180, range2 * Math.PI / 180, bearingRadians, debugging, DEBUGGING_LIMIT);
				tri.AttributeValues.SetValue(outputAttribute, dip.GetValueOrDefault() * 180 / Math.PI);
			}

			if (debugging != null) {
				var floorTriLayer = Layer.GetOrCreate(DEBUG_LAYER_NAME);
				floorTriLayer.Triangulations.Clear();
				floorTriLayer.Texts.Clear();

				foreach (var x in debugging) {
					var centroid = GetCentroid(x.triangle.FirstPoint, x.triangle.SecondPoint, x.triangle.ThirdPoint);
					Text t = new Text(string.Format("{0} {1} {2}", x.dip, x.area, x.in_range ? "NOT in range" : "in range"), centroid + Vector3D.UpVector, 1f, 0);
					floorTriLayer.Texts.Add(t);
					floorTriLayer.Triangulations.Add(new LayerTriangulation(new TriangleMesh(new[] { x.triangle })));
				}

			}

		};
		return customDesignAction;
	}


	public class DipDebugInfo {
		public TriangleMeshTriangle triangle;
		public double dip;
		public double area;
		internal bool in_range;
	}

	public static double? CalculateDipRadians(TriangleMesh tm, double dipRange1, double dipRange2, double? bearingRadians = null, List<DipDebugInfo> debugging = null, int debugging_limit = DEBUGGING_LIMIT) {
		// Assuming s.TriangleMesh provides access to the triangles of the solid
		IEnumerable<TriangleMeshTriangle> allTriangles = tm;

		var floorTriangles = GetRoofFloor(allTriangles, false);

		if (!floorTriangles.Any()) {
			return null; // No floor triangles found
		}

		double totalWeightedDip = 0;
		double totalArea = 0;

		// Ensure dipRange1 is the smaller value and dipRange2 is the larger value
		double minDip = Math.Min(dipRange1, dipRange2);
		double maxDip = Math.Max(dipRange1, dipRange2);

		foreach (var tri in floorTriangles) {
			double dip = GetDip(tri, bearingRadians);
			dip = NormaliseDipRadians(dip);
			double area = GetArea(tri); // Using the implemented GetArea or tri.Area
			bool in_range = dip >= minDip && dip <= maxDip;

			// Check if the dip is within the specified range
			if (in_range) {
				totalWeightedDip += dip * area;
				totalArea += area;
			}

			if (debugging != null && debugging.Count < debugging_limit) {
				var debug = new DipDebugInfo() {
					triangle = tri,
					dip = dip,
					area = area,
					in_range = in_range,
				};
				debugging.Add(debug);
			}
		}

		if (totalArea < 1e-9) {
			// Avoid division by zero with a small tolerance
			return null; // Total area is effectively zero
		}

		return totalWeightedDip / totalArea;
	}

	public static double NormaliseDipRadians(double dip) {
		return Math.Min(Math.Abs(dip), Math.Abs(dip - Math.PI));
	}

	public static List<Point3D> GetCentroids(IEnumerable<TriangleMeshTriangle> tris) {
		List<Point3D> centroids = new List<Point3D>();
		foreach (var tri in tris) {
			centroids.Add(GetCentroid(tri.FirstPoint, tri.SecondPoint, tri.ThirdPoint));
		}
		return centroids;
	}

	public static List<Point2D> GetCentroids2D(IEnumerable<TriangleMeshTriangle> tris) {
		List<Point2D> centroids2D = new List<Point2D>();
		foreach (var tri in tris) {
			Point3D centroid3D = GetCentroid(tri.FirstPoint, tri.SecondPoint, tri.ThirdPoint);
			// Assuming Point2D constructor takes X and Y
			centroids2D.Add(new Point2D(centroid3D.X, centroid3D.Y));
		}
		return centroids2D;
	}

	public static List<TriangleMeshTriangle> GetRoofFloor(IEnumerable<TriangleMeshTriangle> tris, bool roof) {
		// get all the centroids of the triangles
		List<Point2D> centroids2D = GetCentroids2D(tris);

		List<TriangleMeshTriangle> roofFloorTriangles = new List<TriangleMeshTriangle>();

		// use them as sampling points to get the roof or floor for a given point
		foreach (var centroid2D in centroids2D) {
			TriangleMeshTriangle resultTriangle = GetRoofFloor(tris, roof, centroid2D);
			if (resultTriangle != null) {
				roofFloorTriangles.Add(resultTriangle);
			}
		}

		// return the uniques
		return roofFloorTriangles.Distinct().ToList();
	}

	public static TriangleMeshTriangle GetRoofFloor(IEnumerable<TriangleMeshTriangle> tris, bool roof, Point2D ray) {
		TriangleMeshTriangle resultTriangle = null;
		double? bestZ = null;

		foreach (var tri in tris) {
			double? intersectionZ = GetZIntersection(tri.FirstPoint, tri.SecondPoint, tri.ThirdPoint, ray);

			if (intersectionZ.HasValue) {
				if (resultTriangle == null || (roof && intersectionZ.Value > bestZ.Value) || (!roof && intersectionZ.Value < bestZ.Value)) {
					bestZ = intersectionZ.Value;
					resultTriangle = tri;
				}
			}
		}

		return resultTriangle;
	}

	public static Point3D GetCentroid(Point3D a, Point3D b, Point3D c) {
		var sum = a + b + c;
		return new Point3D(sum.X / 3, sum.Y / 3, sum.Z / 3);
	}

	public static double? GetZIntersection(Point3D a, Point3D b, Point3D c, Point2D xy) {

		// Calculate vectors in the plane
		Vector3D v1 = b - a;
		Vector3D v2 = c - a;

		// Calculate the normal vector of the plane
		Vector3D normal = v1.CrossProduct(v2);

		// Check if the plane is vertical (normal.Z is close to zero)
		if (System.Math.Abs(normal.Z) < 1e-9) {
			// No unique intersection point for a vertical plane
			return null;
		}

		// Check if the 2D point xy is inside the 2D triangle formed by the projection of a, b, and c
		double epsilon = 1e-9;

		double cp1 = (b.X - a.X) * (xy.Y - a.Y) - (b.Y - a.Y) * (xy.X - a.X);
		double cp2 = (c.X - b.X) * (xy.Y - b.Y) - (c.Y - b.Y) * (xy.X - b.X);
		double cp3 = (a.X - c.X) * (xy.Y - c.Y) - (a.Y - c.Y) * (xy.X - c.X);

		bool isInside = (cp1 >= -epsilon && cp2 >= -epsilon && cp3 >= -epsilon) ||
						(cp1 <= epsilon && cp2 <= epsilon && cp3 <= epsilon);

		if (!isInside) {
			// The 2D point is not within the triangle projection
			return null;
		}

		// Calculate the parameter t for the intersection point on the line (xy.X, xy.Y, z)
		// The equation of the plane is normal . (p - a) = 0
		// The line can be parameterized as p = (xy.X, xy.Y, a.Z) + t * (0, 0, 1)
		// Substituting the line equation into the plane equation:
		// normal . ((xy.X, xy.Y, a.Z + t) - a) = 0
		// normal . (xy.X - a.X, xy.Y - a.Y, a.Z + t - a.Z) = 0
		// normal . (xy.X - a.X, xy.Y - a.Y, t) = 0
		// normal.X * (xy.X - a.X) + normal.Y * (xy.Y - a.Y) + normal.Z * t = 0
		// Solving for t:
		// normal.Z * t = -normal.X * (xy.X - a.X) - normal.Y * (xy.Y - a.Y)
		// t = (-normal.X * (xy.X - a.X) - normal.Y * (xy.Y - a.Y)) / normal.Z

		double t = (-normal.X * (xy.X - a.X) - normal.Y * (xy.Y - a.Y)) / normal.Z;

		// The Z coordinate of the intersection point is a.Z + t
		double z = a.Z + t;

		return z;
	}

	public static double GetDip(TriangleMeshTriangle tri, double? bearingRadians = null) {
		Vector3D normal = tri.Normal;
		Vector3D up = Vector3D.UpVector;

		// Calculate the cosine of the angle between the normal and the up vector
		double dot = normal.DotProduct(up);

		// Clamp the dot product to the valid range [-1, 1] to avoid floating-point issues with Acos
		dot = System.Math.Max(-1.0, System.Math.Min(1.0, dot));

		// Calculate the angle between the normal and the up vector in radians (true dip)
		double trueDipRadians = System.Math.Acos(dot);

		if (bearingRadians == null)
			return trueDipRadians;

		// Calculate dip direction (azimuth) in XY plane
		// Dip direction is perpendicular to the strike, which is the projection of the normal onto XY
		double dipDirRadians = System.Math.Atan2(-normal.X, -normal.Y); // North=0, East=π/2

		// Calculate angle between dip direction and bearing
		double phi = bearingRadians.Value - dipDirRadians;

		// Calculate apparent dip
		double apparentDip = Math.Atan(Math.Tan(trueDipRadians) * Math.Cos(phi));
		return Math.Abs(apparentDip);
	}

	public static double GetArea(TriangleMeshTriangle tri) {
		return tri.Area;
	}

	/// <summary>
	/// Converts a bearing in degrees (0° = North/Y, clockwise) to polar radians (0 = East/X, counter-clockwise).
	/// </summary>
	/// <param name="bearingDegrees">Bearing in degrees (0° = North, 90° = East, 180° = South, 270° = West)</param>
	/// <returns>Angle in radians (0 = East/X, π/2 = North/Y, π = West/-X, 3π/2 = South/-Y), counter-clockwise</returns>
	public static double BearingDegreesToPolarRadians(double bearingDegrees) {
		// Convert degrees to radians
		double bearingRadians = bearingDegrees * Math.PI / 180.0;
		// Convert from bearing (clockwise from North) to polar (counter-clockwise from East)
		// Polar = π/2 - bearing
		double polarRadians = (Math.PI / 2.0) - bearingRadians;
		// Normalize to [0, 2π)
		if (polarRadians < 0)
			polarRadians += 2 * Math.PI;
		return polarRadians;
	}
}
