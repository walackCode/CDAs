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

public partial class GeometricDependencyCDA
{
    [CustomDesignAction]
    public static CustomDesignAction CreateGeometricDependency()
    {
		var customDesignAction = new CustomDesignAction("Create Geometric Dependency", "", "Dependency", Array.Empty<Keys>());
        customDesignAction.Visible = DesignButtonVisibility.Animation;
        customDesignAction.OptionsForm += (s, e) =>
        {

	        var form = OptionsForm.Create("test");
	        var nameOption = form.Options.AddTextEdit("Name");
	        nameOption.Value = "New Sphere Dependency";
	        nameOption.RestoreValue("GeometricDependencyCDA_NAME");
			
			var searchLocationComboBox = form.Options.AddComboBox<string>("Search Location");
			searchLocationComboBox.Items.Add("From Predecessor");
			searchLocationComboBox.Items.Add("From Successor");
			searchLocationComboBox.Value = "From Predecessor";
			searchLocationComboBox.RestoreValue("GeometricDependencyCDA_Search Location");
	        var predeccessorRangeOption = form.Options.AddRangeSelect("Predeccessor Range");
	        predeccessorRangeOption.Table = customDesignAction.ActiveCase.SourceTable;
	        predeccessorRangeOption.RestoreValue("GeometricDependencyCDA_predeccessorRangeOption", x => x.FullName, x => predeccessorRangeOption.Table.Ranges.GetRange(x), true, true);
			
			var successorRangeOption = form.Options.AddRangeSelect("Successor Range");
	        successorRangeOption.Table = customDesignAction.ActiveCase.SourceTable;
	        successorRangeOption.RestoreValue("GeometricDependencyCDA_successorRangeOption", x => x.FullName, x => successorRangeOption.Table.Ranges.GetRange(x), true, true);
			
			var geometryComboBox = form.Options.AddComboBox<string>("Geometry").RestoreValue("GeometricDependencyCDA_geometry");
			geometryComboBox.Items.Add("Sphere");
			geometryComboBox.Value = "Sphere";
			geometryComboBox.Enabled = false;
			
			var radiusSpinEdit = form.Options.AddSpinEdit("Radius");
			radiusSpinEdit.Value = 10;
			radiusSpinEdit.RestoreValue("GeometricDependencyCDA_radius");
			var circularCheckBox = form.Options.AddCheckBox("Avoid Circular Dependencies (Coming Soon)").RestoreValue("GeometricDependencyCDA_avoid");
			circularCheckBox.Enabled = false;
			var previewButton = form.Options.AddButtonEdit("Preview");
			previewButton.ClickAction = (o) =>
			{
				List<Node> leaves = customDesignAction.ActiveCase.SourceTable.Nodes.Leaves.ToList();

				if (searchLocationComboBox.Value == "From Predecessor")
				{
					var predeccessorRange = predeccessorRangeOption.Value;
					if (predeccessorRange != null)
						leaves = predeccessorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
				}
				else
				{
					var successorRange = successorRangeOption.Value;
					if (successorRange != null)
						leaves = successorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
				}
				var solids = new List<Solid>();
				
				foreach(var node in leaves)
				{
					solids.AddRange(GetSolidsForNode(customDesignAction.ActiveCase, node));
				}
				PreviewSpheres(customDesignAction, solids, radiusSpinEdit.Value);
			};
			var warningDescription = form.Options.AddTextLabel("This will create a sphere located at the approximate centroid of each shape in the Search Location range (predeccessor or successor), it will then check the centroid of each shape in the other range to see if it is inside the sphere, and then create a range dependency entry between the two nodes. If you turn on 'Avoid Circular Dependencies' it will treat the current dependencies as a source of truth before adding new ones");
			e.OptionsForm = form;
		};
		customDesignAction.ProgressSteps = new List<string>() {"Set a Predeccessor and Successor Range and a Radius, Inspect the Sphere's to make sure it is doing what you expect."};
		customDesignAction.ApplyAction += (s, e) =>
		{
			var scenario = customDesignAction.ActiveCase;
            var name = customDesignAction.ActionSettings[0].Value as string;
            var searchOption = customDesignAction.ActionSettings[1] as ComboBoxOption<string>;
            var predeccessorRangeOption = customDesignAction.ActionSettings[2] as RangeSelectOption;
            var successorRangeOption = customDesignAction.ActionSettings[3] as RangeSelectOption;
            var geometryOption = customDesignAction.ActionSettings[4] as ComboBoxOption<string>;
            var radius = (double) customDesignAction.ActionSettings[5].Value;
            var noCirculars = (bool) customDesignAction.ActionSettings[6].Value;

			if (searchOption.Value == "From Predecessor")
				CreateSphereDependencyFromPredeccessor(scenario, predeccessorRangeOption.Value, successorRangeOption.Value, radius, noCirculars, name);
			else
				CreateSphereDependencyFromSuccessor(scenario, predeccessorRangeOption.Value, successorRangeOption.Value, radius, noCirculars, name);
		};
		return customDesignAction;
    }
	
	public static void CreateSphereDependencyFromPredeccessor(Case scenario, Range predeccessorRange, Range successorRange, double radius, bool avoidCircularDependencies, string name)
	{
		List<Node> predeccessorLeaves = scenario.SourceTable.Nodes.Leaves.ToList();
		if (predeccessorRange != null)
			predeccessorLeaves = predeccessorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
		List<Node> successorLeaves = scenario.SourceTable.Nodes.Leaves.ToList();
		if (successorRange != null)
			successorLeaves = successorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
		
		var pairs = new Dictionary<Node,List<Node>>();
		foreach(var predeccessorNode in predeccessorLeaves)
		{
			var predeccessorSolid = GetSolidsForNode(scenario, predeccessorNode).FirstOrDefault();
			if(predeccessorSolid == null)
				continue;
			var predeccessorCentroid = predeccessorSolid.ApproximateCentroid;
			var aabbVector = new Vector3D(radius,radius,radius);
			var aabb = new Aabb(predeccessorCentroid -aabbVector, predeccessorCentroid + aabbVector);
			foreach(var successorNode in successorLeaves)
			{
				var successorSolid = GetSolidsForNode(scenario, successorNode).FirstOrDefault();
				if(successorSolid == null)
					continue;
				var successorCentroid = successorSolid.ApproximateCentroid;
				if (!aabb.Inside(successorCentroid)) {
					continue;
				}
				var vector = successorCentroid - predeccessorCentroid;
				if (vector.Length < radius)
				{
					if(!pairs.ContainsKey(predeccessorNode))
						pairs[predeccessorNode] = new List<Node>();
					pairs[predeccessorNode].Add(successorNode);
				}
			}
		}
		var rangeDependency = scenario.DependencyRules.DependencyRules.Get(name) as RangeDependencyRule;
		if (rangeDependency == null)
		{
			rangeDependency = new RangeDependencyRule(name);
			scenario.DependencyRules.DependencyRules.Add(rangeDependency);
		}
		while(rangeDependency.Entries.Count > 0)
			rangeDependency.Entries.RemoveAt(0);
		foreach(var item in pairs)
		{
			var entry = new RangeDependencyEntry(string.Format("Entry {0}", rangeDependency.Entries.Count + 1));
            entry.PredecessorTextRange = item.Key.FullName;
            entry.SuccessorTextRange = string.Join("\n", item.Value.Select(x => x.FullName));
            rangeDependency.Entries.Add(entry);
		}
		scenario.DependencyRules.DependencyRules.Add(rangeDependency);
	}
	
	public static void CreateSphereDependencyFromSuccessor(Case scenario, Range predeccessorRange, Range successorRange, double radius, bool avoidCircularDependencies, string name)
	{
		List<Node> predeccessorLeaves = scenario.SourceTable.Nodes.Leaves.ToList();
		if (predeccessorRange != null)
			predeccessorLeaves = predeccessorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
		List<Node> successorLeaves = scenario.SourceTable.Nodes.Leaves.ToList();
		if (successorRange != null)
			successorLeaves = successorRange.CachedNodes.Where(x => x.IsLeaf).ToList();
		
		var pairs = new Dictionary<Node,List<Node>>();
		foreach(var successorNode in successorLeaves)
		{				
			var successorSolid = GetSolidsForNode(scenario, successorNode).FirstOrDefault();
			if(successorSolid == null)
				continue;
			var successorCentroid = successorSolid.ApproximateCentroid;
			var aabbVector = new Vector3D(radius,radius,radius);
			var aabb = new Aabb(successorCentroid -aabbVector, successorCentroid + aabbVector);
			foreach(var predeccessorNode in predeccessorLeaves)
			{
				var predeccessorSolid = GetSolidsForNode(scenario, predeccessorNode).FirstOrDefault();
				if(predeccessorSolid == null)
					continue;
				var predeccessorCentroid = predeccessorSolid.ApproximateCentroid;
				if (!aabb.Inside(predeccessorCentroid)) {
					continue;
				}
				var vector = successorCentroid - predeccessorCentroid;
				if (vector.Length < radius)
				{
					if(!pairs.ContainsKey(predeccessorNode))
						pairs[predeccessorNode] = new List<Node>();
					pairs[predeccessorNode].Add(successorNode);
				}
			}
		}
		var rangeDependency = scenario.DependencyRules.DependencyRules.Get(name) as RangeDependencyRule;
		if (rangeDependency == null)
		{
			rangeDependency = new RangeDependencyRule(name);
			scenario.DependencyRules.DependencyRules.Add(rangeDependency);
		}
		while(rangeDependency.Entries.Count > 0)
			rangeDependency.Entries.RemoveAt(0);
		foreach(var item in pairs)
		{
			var entry = new RangeDependencyEntry(string.Format("Entry {0}", rangeDependency.Entries.Count + 1));
            entry.PredecessorTextRange = item.Key.FullName;
            entry.SuccessorTextRange = string.Join("\n", item.Value.Select(x => x.FullName));
            rangeDependency.Entries.Add(entry);
		}
		scenario.DependencyRules.DependencyRules.Add(rangeDependency);
	}
	
	public static List<Solid> GetSolidsForNode(Case scenario, Node node)
	{
		var output = new List<Solid>();
		foreach(var process in scenario.Processes)
		{
			if(process.Active && (process.DefaultSourceQuantityField != null && node.Data.Double[process.DefaultSourceQuantityField] > 0) && ((process.SolidsField != null && node.Data.Solid[process.SolidsField] != null) || (process.AlternateSolidsField != null && node.Data.Solid[process.AlternateSolidsField] != null)))
				output.Add(node.Data.Solid[process.SolidsField] ?? node.Data.Solid[process.AlternateSolidsField]);
		}
		return output.Distinct().ToList();
	}
	
	public static void PreviewSpheres(CustomDesignAction action, List<Solid> solids, double radius)
	{
		action.ClearTemporaryRenderables();
		foreach(var solid in solids)
		{
			var handle = action.AddTemporaryMesh(CreateSphereMesh(solid.ApproximateCentroid, radius), Color.AliceBlue, true, false, false, null);
		}
	}
	
	public static void Test()
	{
		var layer = Layer.GetOrCreate("Test");
		var otherLayer = Layer.GetOrCreate(@"SCHEDULER\CENTRELINES");
		var point = otherLayer.Shapes.First().First();
		var sphere = CreateSphereMesh(point, 10);
		layer.Triangulations.Add(new LayerTriangulation(sphere));
	}
	
	public static TriangleMesh CreateSphereMesh(Point3D centroid, double radius, int latitudeSegments = 16, int longitudeSegments = 16)
    {
        // Calculate the number of vertices and triangles
        int numVertices = (latitudeSegments + 1) * (longitudeSegments + 1);
        int numTriangles = latitudeSegments * longitudeSegments * 2;

        // Generate vertices
        Point3D[] vertices = new Point3D[numVertices];
        double latitudeStep = 2 * Math.PI / latitudeSegments;
        double longitudeStep = Math.PI / longitudeSegments;

        int vertexIndex = 0;
        for (int latitude = 0; latitude <= latitudeSegments; latitude++)
        {
            for (int longitude = 0; longitude <= longitudeSegments; longitude++)
            {
                double latAngle = latitude * latitudeStep;
                double lonAngle = longitude * longitudeStep;

                double x = centroid.X + radius * Math.Sin(lonAngle) * Math.Cos(latAngle);
                double y = centroid.Y + radius * Math.Sin(lonAngle) * Math.Sin(latAngle);
                double z = centroid.Z + radius * Math.Cos(lonAngle);

                vertices[vertexIndex] = new Point3D(x, y, z);
                vertexIndex++;
            }
        }

        // Generate triangles
        //int[] triangles = new int[numTriangles * 3];
        int triangleIndex = 0;
        int vertexCount = longitudeSegments + 1;
		var triangleVertices = new List<TriangleVertices>();
        for (int latitude = 0; latitude < latitudeSegments; latitude++)
        {
            for (int longitude = 0; longitude < longitudeSegments; longitude++)
            {
                int currentVertex = latitude * vertexCount + longitude;

				triangleVertices.Add(new TriangleVertices(currentVertex, currentVertex + 1, currentVertex + vertexCount));
				triangleVertices.Add(new TriangleVertices(currentVertex + 1, currentVertex + vertexCount + 1, currentVertex + vertexCount));
//                triangles[triangleIndex] = currentVertex;
//                triangles[triangleIndex + 1] = currentVertex + 1;
//                triangles[triangleIndex + 2] = currentVertex + vertexCount;

//                triangles[triangleIndex + 3] = currentVertex + 1;
//                triangles[triangleIndex + 4] = currentVertex + vertexCount + 1;
//                triangles[triangleIndex + 5] = currentVertex + vertexCount;

                triangleIndex += 6;
            }
        }
	
        return new TriangleMesh(vertices, triangleVertices);
    }
}