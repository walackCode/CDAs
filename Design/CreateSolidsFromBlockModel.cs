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
using System.Threading.Tasks;
#endregion

/// <summary>
/// This script has been updated to work with Spry 25.0.1988
/// Updated to not do box face filtering for large block model compatibility
/// </summary>
public partial class BlockModels
{
    public static BlockModelSelectOption blockModelSelect;
    //public static CheckedComboBoxOption<string> fieldSelect;
    public static CheckedComboBoxOption<string> fieldSelect2;
    public static LayerSelectOption layerSelect;
    //[CustomDesignAction]
    public static void CreateGroupedBlocks()
    {
        var form = OptionsForm.Create("form");
        var blockModelSelect = form.Options.AddBlockModelSelect("Block Model", false)
            .RestoreValue("GB Block Model", x => x.FullName, x => Project.ActiveProject.Design.BlockModelData.GetBlockModel(x), true, true);

        var fieldSelect = form.Options.AddCheckedComboBox<string>("Grouping Fields");
        //var fieldSelectNumber = form.Options.AddCheckedComboBox<string>("Grouping Fields Number (Optional)");

        List<string> fields = new List<string>();
        List<string> fieldsNumber = new List<string>();

        blockModelSelect.ValueChanged += (s, e) =>
        {
            var blockModel = blockModelSelect.Value;
            if (blockModel == null)
                return;
            fieldSelect.Items.Clear();
            //fieldSelectNumber.Items.Clear();
            fieldSelect.Items.AddRange(blockModel.Manager.Fields.Where(x => x.Enabled).Select(x => x.Name).ToList());
            //fieldSelectNumber.Items.AddRange(blockModel.BlockModelManager.Fields.Where(x => x.IsNumeric && x.Enabled).Select(x => x.Name).ToList());
            //int numner 

            fields.Clear();
            //fieldsNumber.Clear();


        };
        blockModelSelect.Validators.Add(x => blockModelSelect.Value != null, "Please Select a Field");


        fieldSelect.ValueChanged += (s, e) =>
        {
            fields.Clear(); // this is clearing the values
            var blockModel = blockModelSelect.Value;
            if (blockModel == null)
                return;
            var currentValues = fieldSelect.Values.ToList();
            fields.AddRange(currentValues);

        };
        fieldSelect.Validators.Add(x => fieldSelect.Values.Count() > 0, "Please Select a Field");
        fieldSelect.RestoreValues("GB Grouping Fields", x => string.Join("_", x), x => x.Split('_').ToList(), true, true);


        //add the text fields for the Folder Name
//        var gridsRoofFolder = form.Options
//                        .AddTextEdit("Grids Roof Folder")
//                        .RestoreValue("GRID-CONVERT-ROOF")
//                        .Validators.Add(x => !string.IsNullOrEmpty(x), "Input a valid path for the roof folder");

//        var gridsFloorFolder = form.Options
//                        .AddTextEdit("Grids Floor Folder")
//                        .RestoreValue("GRID-CONVERT-FLOOR")
//                        .Validators.Add(x => !string.IsNullOrEmpty(x), "Input a valid path for the foor folder");


        // Add option for 3D solid generation
        //var createSolids = form.Options.AddCheckBox("Create 3D Solids")
        //                .RestoreValue("CREATE_3D_SOLIDS", false);

        //var solidsFolder = form.Options
        //               .AddTextEdit("3D Solids Folder")
        //               .RestoreValue("SOLIDS-3D")
        //               .Validators.Add(x => !createSolids.Value || !string.IsNullOrEmpty(x), "Input a valid path for the solids folder when creating 3D solids");



        blockModelSelect.ForceValueChangedEvent(); // field list values are populated here

        fieldSelect.RestoreValues("GB Grouping Fields", x => string.Join("_", x), x => x.Split('_').ToList(), true, true); // at his point there are not values in the field list
                                                                                                                           //fieldSelectNumber.RestoreValues("GB Grouping Fields Number", x => string.Join("_", x), x => x.Split('_').ToList(), true, true);

        fieldSelect.ForceValueChangedEvent();
        //fieldSelectNumber.ForceValueChangedEvent();

        if (form.ShowDialog() != DialogResult.OK)
            return;

        GroupBlocks(blockModelSelect.Value, fields, "", "");


        //return cda;
    }

	public static void GroupBlocks(PrecisionMining.Spry.Design.BlockModel blockModel, List<string> groupingFields, string gridsRoofFolder, string gridsFloorFolder)
	{
	    try
	    {
	        Out.WriteLine("=== Starting GroupBlocks ===");
	        
	        if (blockModel == null)
	        {
	            Out.WriteLine("ERROR: blockModel is null");
	            return;
	        }
	        
	        Out.WriteLine("Block Model: " + blockModel.FullName);
	        
	        var manager = blockModel.Manager;
	        
	        if (manager == null)
	        {
	            Out.WriteLine("ERROR: BlockModelManager is null");
	            return;
	        }
	        
	        Out.WriteLine("Manager OK");
	        
	        // Check Fields property
	        if (manager.Fields == null)
	        {
	            Out.WriteLine("ERROR: manager.Fields is null");
	            return;
	        }
	        
	        Out.WriteLine("Fields property OK");
	        
	        // Get size fields with null checks
	        var XSizeField = manager.Fields.XSizeField;
	        if (XSizeField == null)
	        {
	            Out.WriteLine("ERROR: XSizeField is null");
	            return;
	        }
	        
	        var YSizeField = manager.Fields.YSizeField;
	        if (YSizeField == null)
	        {
	            Out.WriteLine("ERROR: YSizeField is null");
	            return;
	        }
	        
	        var zSizeField = manager.Fields.ZSizeField;
	        if (zSizeField == null)
	        {
	            Out.WriteLine("ERROR: ZSizeField is null");
	            return;
	        }
	        
	        Out.WriteLine("Size fields OK: X=" + XSizeField.Name + ", Y=" + YSizeField.Name + ", Z=" + zSizeField.Name);
	        
	        // Build field lookup for grouping fields
	        var fieldLookup = new Dictionary<BlockField, PrecisionMining.Common.Design.Attribute>();
	        List<BlockField> blockFields = new List<BlockField>();
	
	        Out.WriteLine("Processing " + groupingFields.Count + " grouping fields");
	        
	        foreach (var fieldName in groupingFields)
	        {
	            Out.WriteLine("  Looking for field: " + fieldName);
	            var field = manager.Fields[fieldName];
	            if (field == null)
	            {
	                Out.WriteLine("  WARNING: Field '" + fieldName + "' not found");
	                continue;
	            }
	            if (!field.Enabled)
	            {
	                Out.WriteLine("  WARNING: Field '" + fieldName + "' not enabled");
	                continue;
	            }
	            
	            Out.WriteLine("  Field '" + fieldName + "' found and enabled");
	            blockFields.Add(field);
	            
	            if (field.DataType == typeof(double))
	            {
	                var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(field.Name, "Interrogation Field", typeof(double));
	                fieldLookup.Add(field, attribute);
	            }
	            else if (field.DataType == typeof(string))
	            {
	                var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(field.Name, "Interrogation Field", typeof(string));
	                fieldLookup.Add(field, attribute);
	            }
	        }
	        
	        Out.WriteLine("Found " + blockFields.Count + " valid grouping fields");
	        
	        // Prepare all fields to load
	        var fieldsToLoad = new List<BlockField>();
	        fieldsToLoad.AddRange(fieldLookup.Keys);
	        fieldsToLoad.Add(XSizeField);
	        fieldsToLoad.Add(YSizeField);
	        fieldsToLoad.Add(zSizeField);
	        
	        Out.WriteLine("Loading " + fieldsToLoad.Count + " fields (including size fields)...");
	
	        using (var progress = Progress.CreateProgressOptions())
	        {
	            List<string> errorMessages;
	            
	            if (!manager.LoadFields(out errorMessages, fieldsToLoad, progress))
	            {
	                Out.WriteLine("ERROR loading fields:");
	                foreach (var msg in errorMessages)
	                {
	                    Out.WriteLine("  - " + msg);
	                }
	                MessageBox.Show(string.Join("\n", errorMessages));
	                return;
	            }
	            
	            Out.WriteLine("Fields loaded successfully");
	        }
	
	        Out.WriteLine("Attempting to get blocks...");
	        
//	        IEnumerable<Block> blockEnumerable = null;
//			List<Block> allBlocks = null;
			
//	        try
//			{
			    var framework = manager.Framework;
				if(framework == null)
					
				{
					Out.WriteLine("ERROR: Framework is null");
					return;
				}
				
			Out.WriteLine("Attempting to get blocks from framework...");
            
//            var blockEnumerable = manager.Data;// .blframework.Blocks; 

			var emstring = new List<String>();	
				
			manager.LoadBlockData(out emstring);
				
			Out.WriteLine(emstring);	
				
			 var blockEnumerable = manager.Data;// .blframework.Blocks; 
	
				
//            if (blockEnumerable == null)
//            {
//                Out.WriteLine("ERROR: Framework.Blocks returned null");
//                return;
//            }
				
//				blockEnumerable = blockModel.LoadAllBlocks(fieldsToLoad);
			
//			    if (blockEnumerable == null)
//			    {
//			        Out.WriteLine("ERROR: LoadAllBlocks returned null");
//			        return;
//			    }
			
//			    Out.WriteLine("Successfully loaded " + blockEnumerable.Count() + " blocks");
//			}
//			catch (Exception ex)
//			{
//			    Out.WriteLine("ERROR while loading blocks: " + ex.Message);
//			}
//	        if (blockEnumerable == null)
//	        {
//	            Out.WriteLine("ERROR: GetBlocks() returned null");
	            
//	            // Try alternative method
//	            Out.WriteLine("Trying to enumerate framework blocks...");
//	            try
//	            {
//	                var framework = manager.Framework;
//	                if (framework == null)
//	                {
//	                    Out.WriteLine("ERROR: Framework is null");
//	                    return;
//	                }
	                
//	                Out.WriteLine("Framework OK, attempting to get blocks from framework...");
//	                blockEnumerable = framework.TryGetBlocks();
	                
//	                if (blockEnumerable == null)
//	                {
//	                    Out.WriteLine("ERROR: Framework.GetBlocks() also returned null");
//	                    return;
//	                }
//	            }
//	            catch (Exception ex2)
//	            {
//	                Out.WriteLine("ERROR with framework approach: " + ex2.Message);
//	                return;
//	            }
//	        }
	        
//	        Out.WriteLine("Converting blocks to list...");
//	        var allBlocks = blockEnumerable.ToList();
	        
//	        if (allBlocks == null)
//	        {
//	            Out.WriteLine("ERROR: ToList() returned null");
//	            return;
//	        }
	        
//	        Out.WriteLine("Successfully loaded " + allBlocks.Count + " blocks");
	        
//	        if (allBlocks.Count == 0)
//	        {
//	            Out.WriteLine("ERROR: No blocks in block model");
//	            return;
//	        }
	
//	        // Get dimensions from first block
//	        Block firstBlock = allBlocks.FirstOrDefault();
//	        if (firstBlock == null)
//	        {
//	            Out.WriteLine("ERROR: FirstOrDefault returned null despite having blocks");
//	            return;
//	        }
	        
			Out.WriteLine(blockEnumerable == null ? "It's fucked " :  "RGo");	
				
			var firstBlock = blockEnumerable.Where(x=> x.Value != null).FirstOrDefault().Value;
			var allBlocks = blockEnumerable.Select(x=> x.Value).ToList();	
				
	        var dimX = XSizeField.GetValueOrDefault(firstBlock);
	        var dimY = YSizeField.GetValueOrDefault(firstBlock);
	        
	        Out.WriteLine("Block dimensions: X=" + dimX + ", Y=" + dimY);
	        
	        // Group blocks by field values
	        var values = new Dictionary<string, Dictionary<PrecisionMining.Common.Design.Attribute, string>>();
	        Dictionary<string, List<Block>> seamGroupedBlocks = new Dictionary<string, List<Block>>();
	
	        Out.WriteLine("Grouping " + allBlocks.Count + " blocks by " + blockFields.Count + " fields");
	        
	        int processedCount = 0;
	        foreach (var block in allBlocks)
	        {
	            processedCount++;
	            if (processedCount % 10000 == 0)
	            {
	                Out.WriteLine("  Processed " + processedCount + " of " + allBlocks.Count);
	            }
	            
	            List<string> keyParts = new List<string>();
	            foreach (var field in blockFields)
	            {
	                object fieldValue;
	                if (field.TryGetValue(block, out fieldValue) && fieldValue != null)
	                {
	                    keyParts.Add(field.Name + ":" + fieldValue.ToString());
	                }
	            }
	            
	            if (keyParts.Count == 0)
	                continue;
	            
	            string compositeKey = string.Join("|", keyParts);
	            if (!seamGroupedBlocks.ContainsKey(compositeKey))
	            {
	                seamGroupedBlocks[compositeKey] = new List<Block>();
	                values[compositeKey] = fieldLookup.ToDictionary(x => x.Value, x => x.Key.GetValueOrDefault(block).ToString());
	            }
	            seamGroupedBlocks[compositeKey].Add(block);
	        }
	        
	        Out.WriteLine("Created " + seamGroupedBlocks.Count + " groups");
	
	        // Process each group to create solids
	        int groupIndex = 0;
	        foreach(var sbg in seamGroupedBlocks)
	        {
	            groupIndex++;
	            Out.WriteLine("\n=== Group " + groupIndex + "/" + seamGroupedBlocks.Count + " ===");
	            Out.WriteLine("Key: " + sbg.Key);
	            Out.WriteLine("Blocks: " + sbg.Value.Count);
	            
	            var mycubes = new List<TriangleMesh>();
	            object lockObj = new object();
	
	            Parallel.ForEach(sbg.Value, b =>
	            {
	                try
	                {
	                    var aabb = GetBlockAabb(b, XSizeField, YSizeField, zSizeField);
	                    var corners = GetCorrectOrderBoundingBoxPoints(aabb)
	                        .Select(x => manager.Framework.ModelToWorldMatrix * x)
	                        .ToList();
	                    
	                    lock(lockObj)
	                        mycubes.Add(MakeTSolid(corners));
	                }
	                catch (Exception ex)
	                {
	                    lock(lockObj)
	                        Out.WriteLine("  Block error: " + ex.Message);
	                }
	            });
	            
	            Out.WriteLine("Created " + mycubes.Count + " cubes");
	            
	            if (mycubes.Count == 0)
	                continue;
	            
	            try
	            {
	                var triangleslist = mycubes.SelectMany(x => x.ToList()).ToList();
	                var trisdepleted = triangleslist.GroupBy(x => x)
	                    .Where(g => g.Count() == 1)
	                    .Select(g => g.Key)
	                    .ToList();
	                
	                Out.WriteLine("Unique triangles: " + trisdepleted.Count);
	                
	                if (trisdepleted.Count == 0)
	                    continue;
	                
	                var makingtri = new TriangleMesh(trisdepleted);
	                
	                var repairoptions = new RepairSolidsOptions();
	                repairoptions.RemoveInteriorFacets = true;
	                TriangleMesh.RepairSolid(makingtri, repairoptions);
	                
	                var outlayer = Layer.GetOrCreate(blockModel.FullName);
	                var attribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(
	                    "Block Model Aggregation\\Grouping String",
	                    "the grouping used to make the solid",
	                    typeof(string));
	                
	                var outputlayertri = new LayerTriangulation(makingtri);
	                outputlayertri.AttributeValues[attribute] = sbg.Key;
	                outlayer.Triangulations.Add(outputlayertri);
	                
	                Out.WriteLine("Added to layer successfully");
	            }
	            catch (Exception ex)
	            {
	                Out.WriteLine("Triangulation error: " + ex.Message);
	            }
	        }
	        
	        Out.WriteLine("\n=== GroupBlocks completed ===");
	    }
	    catch (Exception ex)
	    {
	        Out.WriteLine("\nFATAL ERROR: " + ex.Message);
	        Out.WriteLine("Stack trace: " + ex.StackTrace);
	        MessageBox.Show("Fatal Error: " + ex.Message + "\n\nCheck Output window for details");
	    }
	}
	
	private static Aabb GetBlockAabb(Block block, XSizeBlockField xField, YSizeBlockField yField, ZSizeBlockField zField)
	{
	    if (block.Aabb != null)
	        return block.Aabb;
	    
	    var xSizeObj = xField.GetValueOrDefault(block);
	    var ySizeObj = yField.GetValueOrDefault(block);
	    var zSizeObj = zField.GetValueOrDefault(block);
	    
	    if (xSizeObj == null || ySizeObj == null || zSizeObj == null)
	        throw new InvalidOperationException("Block size is null");
	    
	    double xSize = Convert.ToDouble(xSizeObj);
	    double ySize = Convert.ToDouble(ySizeObj);
	    double zSize = Convert.ToDouble(zSizeObj);
	    
	    var centroid = block.Centroid;
	    double halfX = xSize / 2.0;
	    double halfY = ySize / 2.0;
	    double halfZ = zSize / 2.0;
	    
	    return new Aabb(
	        centroid.X - halfX, centroid.Y - halfY, centroid.Z - halfZ,
	        centroid.X + halfX, centroid.Y + halfY, centroid.Z + halfZ
	    );
	}
	
    private static Dictionary<string, GridMesh> CreateGrid(Dictionary<string, List<GridMeshPoint3D>> surface, int xgridspacing, int ygridspacing, GridMesh basegrid, string folderPrefix)
    {
        var createdGrids = new Dictionary<string, GridMesh>();

        foreach (var kvp in surface)
        {
            if (kvp.Value.Count == 0)

            {
                Out.WriteLine("No surfaces to create for " + kvp.Key);

            }

            List<GridMeshPoint3D> currentpoints = kvp.Value.ToList();

            string gridName = kvp.Key.Replace(":", "_").Replace("|", "_");
            var grid = Project.ActiveProject.Design.GridData.GetOrCreateGrid(folderPrefix + "\\" + gridName);
            var gg = new GridMesh(currentpoints, xgridspacing, ygridspacing);
            var finalGrid = GridMesh.Union(gg, basegrid, (real, fake) => real != null ? real : fake);
            grid.Data = finalGrid;
            createdGrids.Add(kvp.Key, finalGrid);

        }
        return createdGrids;
    }
	
    private static void Create3DSolids(Dictionary<string, GridMesh> roofGrids,
                                  Dictionary<string, GridMesh> floorGrids,
                                  string solidsFolder, bool processEdges)
    {

        int stepCount = 0;

        foreach (var roofKvp in roofGrids)
        {

            if (!floorGrids.ContainsKey(roofKvp.Key))
            {
//                Out.WriteLine($"No corresponding floor grid found for {roofKvp.Key}");
                continue;
            }

            var roofGrid = roofKvp.Value;
            var floorGrid = floorGrids[roofKvp.Key];

            // Create 3D solid from roof and floor grids
            var triangleMeshes = new List<TriangleMesh>();

            // Iterate through grid cells to create solid elements
            for (int x = 0; x < roofGrid.XCount - 1; x++)
            {
                for (int y = 0; y < roofGrid.YCount - 1; y++)
                {
                    // Check if we're at the edge and if edge processing is enabled
                    bool isEdge = (x == 0 || x == roofGrid.XCount - 2 ||
                                  y == 0 || y == roofGrid.YCount - 2);

                    if (isEdge && !processEdges)
                        continue;

                    try
                    {
                        //var solidElement = MakeTSolid(roofGrid, floorGrid, x, y, isEdge);
                        //if (solidElement != null)
                        //    triangleMeshes.Add(solidElement);
                    }
                    catch (Exception ex)
                    {
//                        Out.WriteLine($"Error creating solid element at [{x},{y}] for {roofKvp.Key}: {ex.Message}");
                    }
                }
            }

            if (triangleMeshes.Count > 0)
            {

            }
            else
            {
//                Out.WriteLine($"No valid solid elements created for {roofKvp.Key}");
            }
        }
    }
	
    private static Dictionary<string, List<GridMeshPoint3D>> CreateSeamSurfaces(Dictionary<string, List<Block>> seamGroupedBlocks, BlockField zspan, bool createRoof = true)
    {
        Dictionary<string, List<GridMeshPoint3D>> outputsurfaces = new Dictionary<string, List<GridMeshPoint3D>>();

        foreach (var seamGroup in seamGroupedBlocks)
        {
            string seamName = string.Join("|", seamGroup.Key);
            List<Block> blocks = seamGroup.Value;

            // Extract all blocks centroids
            List<GridMeshPoint3D> centroids = blocks.Select(x =>
            {
                var startcentroid = x.Centroid;
                object Delta = zspan.GetValueOrDefault(x);

                var finalcentroid = new GridMeshPoint3D(startcentroid, true);

                if (Delta != null)
                {
                    var zspanamount = double.Parse(Delta.ToString());

                    // Use the createRoof parameter to determine whether to add or subtract
                    if (createRoof)
                    {
                        finalcentroid = new GridMeshPoint3D(startcentroid.X, startcentroid.Y,
                            startcentroid.Z + zspanamount / 2);
                    }
                    else // Create floor
                    {
                        finalcentroid = new GridMeshPoint3D(startcentroid.X, startcentroid.Y,
                            startcentroid.Z - zspanamount / 2);
                    }
                }

                return finalcentroid;
            }).ToList();

            outputsurfaces.Add(seamName, centroids);
        }

        return outputsurfaces;
    }

    static TriangleMesh MakeTSolid(List<Point3D> points)
    {        
        if (points.Count < 8)
            throw new ArgumentException("At least 8 points are required to create a Cube.");
        var p1 = points[0];
        var p2 = points[1];
        var p3 = points[2];
        var p4 = points[3];
        var p5 = points[4];
        var p6 = points[5];
        var p7 = points[6];
        var p8 = points[7];



        var tris = new Triangle[] {
            new Triangle(p1,p4,p3),
            new Triangle(p1,p2,p3),
            new Triangle(p5,p8,p4),
            new Triangle(p5,p1,p4),
            new Triangle(p6,p7,p3),
            new Triangle(p6,p2,p3),
            new Triangle(p6,p5,p1),
            new Triangle(p6,p2,p1),
            new Triangle(p5,p6,p7),
            new Triangle(p5,p8,p7),
            new Triangle(p7,p8,p4),
            new Triangle(p7,p3,p4)
        };

        return new TriangleMesh(tris);
    }
	
	static void hitwithray(List<Triangle> inputmesh)
	{
		var r = new Ray3D(new Point3D(0 ,0,0),new Point3D(0,0,1));
		
		var v = r.Direction;
		
		v = v.Normalise();
//		r.GetShortestDistanceToLine();
		
//		v.DotProduct()
		
	}
	
	public class RayTriangleIntersection
{
	public static void FindIntersects()
	{
		var layerinput = Layer.TryGet("flavraAREAEXU");
		var layeroutput = Layer.GetOrCreate(layerinput.FullName + "_intersected Tris"); 
		var tris = layerinput.Triangulations.FirstOrDefault().Data.ToList();
		var rays = CreateHybridRays(layerinput.Triangulations.FirstOrDefault().Data.Aabb,50);
		
		Out.WriteLine("Rays: " + rays.Count);
		Out.WriteLine("Tris: " + tris.Count);
//		return;
		layeroutput.Shapes.Clear();
		tris = tris.Take(5000).ToList();
		var outputshapes = new List<Shape>();
		
		Parallel.ForEach(tris,t =>
		{
			foreach(var r in rays)
			if(IntersectsWinding(r,t))
			{
				lock(outputshapes)
				outputshapes.Add(new Shape(new List<Point3D>{t.FirstPoint,t.SecondPoint,t.ThirdPoint,t.FirstPoint}));
			}
			
		});
		
		foreach(var ss in outputshapes)
		{
			layeroutput.Shapes.Add(ss);
		}
		
		
	}
	
	
	
public static bool Intersects(
        Ray3D ray,
        TriangleMeshTriangle Tri    	)
    {
        const float EPSILON = 0.0000001f; // A small epsilon value for floating-point comparisons, basically to assume close to zero
        Vector3D edge1 = Tri.SecondPoint - Tri.FirstPoint;
        Vector3D edge2 = Tri.ThirdPoint - Tri.FirstPoint;;

        Vector3D pvec = ray.Direction.CrossProduct(edge2);		
        double det = edge1.DotProduct(pvec);

        double t = 0;
        double barycentricU = 0;
        double barycentricV = 0;

        // If det is close to 0, the ray is parallel to the triangle's plane.
        if (det > -EPSILON && det < EPSILON)
        {
            return false;
        }

        double invDet = 1.0f / det;

        Vector3D tvec = ray.Origin - Tri.FirstPoint;
        barycentricU = tvec.DotProduct(pvec) * invDet;

        // Check barycentric U coordinate
        if (barycentricU < 0.0f || barycentricU > 1.0f)
        {
            return false;
        }

        Vector3D qvec = tvec.CrossProduct(edge1);
        barycentricV = ray.Direction.DotProduct(qvec) * invDet;

        // Check barycentric V coordinate
        if (barycentricV < 0.0f || barycentricU + barycentricV > 1.0f)
        {
            return false;
        }

        t = edge2.DotProduct(qvec) * invDet;

        // Check if the intersection point is in front of the ray origin
        return t >= EPSILON;
    }
	
private static bool IsPointInTriangle(Point3D point, Point3D v0, Point3D v1, Point3D v2, Vector3D normal)
{
    // Project triangle and point onto a 2D plane
    // Choose the plane that gives the largest projection (most stable numerically)
    int maxAxis = GetMaxAxis(normal);
    
    Vector2D p = ProjectToPlane(point, maxAxis);
    Vector2D a = ProjectToPlane(v0, maxAxis);
    Vector2D b = ProjectToPlane(v1, maxAxis);
    Vector2D c = ProjectToPlane(v2, maxAxis);
    
    // Calculate winding number
    double windingNumber = 0.0;
    windingNumber += CalculateWindingContribution(p, a, b);
    windingNumber += CalculateWindingContribution(p, b, c);
    windingNumber += CalculateWindingContribution(p, c, a);
    
    // Point is inside if winding number is non-zero (typically ±1)
    return Math.Abs(windingNumber) > 0.5;
}

public static bool IntersectsWinding(Ray3D ray, TriangleMeshTriangle tri)
{
    const double EPSILON = 1e-10;
    
    // First, find intersection with triangle's plane
    Vector3D edge1 = tri.SecondPoint - tri.FirstPoint;
    Vector3D edge2 = tri.ThirdPoint - tri.FirstPoint;
    Vector3D normal = edge1.CrossProduct(edge2);
    
    // Check if ray is parallel to triangle plane
    double rayDotNormal = ray.Direction.DotProduct(normal);
    if (Math.Abs(rayDotNormal) < EPSILON)
        return false;
    
    // Calculate intersection point with plane
    Vector3D toTriangle = tri.FirstPoint - ray.Origin;
    double t = toTriangle.DotProduct(normal) / rayDotNormal;
    
    // Check if intersection is behind ray origin
    if (t < EPSILON)
        return false;
    
    // Calculate intersection point
    Point3D intersectionPoint = ray.Origin + ray.Direction * t;
    
    // Now use winding number to check if point is inside triangle
    return IsPointInTriangle(intersectionPoint, tri.FirstPoint, tri.SecondPoint, tri.ThirdPoint, normal);
}

private static int GetMaxAxis(Vector3D normal)
{
    double absX = Math.Abs(normal.X);
    double absY = Math.Abs(normal.Y);
    double absZ = Math.Abs(normal.Z);
    
    if (absX >= absY && absX >= absZ) return 0; // Drop X, use YZ plane
    if (absY >= absZ) return 1; // Drop Y, use XZ plane
    return 2; // Drop Z, use XY plane
}

private static Vector2D ProjectToPlane(Point3D point, int dropAxis)
{
    switch (dropAxis)
    {
        case 0: return new Vector2D(point.Y, point.Z); // YZ plane
        case 1: return new Vector2D(point.X, point.Z); // XZ plane
        case 2: return new Vector2D(point.X, point.Y); // XY plane
        default: throw new ArgumentException("Invalid axis");
    }
}
	
private static double CalculateWindingContribution(Vector2D point, Vector2D start, Vector2D end)
{
    // Calculate the contribution of one edge to the winding number
    Vector2D edge = end - start;
    Vector2D toPoint = point - start;
    
    // Use atan2 to get the angle change
    double startAngle = Math.Atan2(toPoint.Y, toPoint.X);
    
    Vector2D toPointEnd = point - end;
    double endAngle = Math.Atan2(toPointEnd.Y, toPointEnd.X);
    
    // Calculate angle difference, handling wraparound
    double angleDiff = endAngle - startAngle;
    
    // Normalize to [-π, π]
    while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
    while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;
    
    return angleDiff / (2 * Math.PI);
}

private static double CalculateDiagonalLength(Aabb bounds)
    {
		var diagonal = (bounds.Maximum - bounds.Minimum);
		
        return diagonal.Length;
    }
	
	
 public static List<Ray3D> CreateAxisAlignedRays(Aabb bounds, double spacing)
    {
        var rays = new List<Ray3D>();
        
        // Get bounding box extents
        double minX = bounds.MinimumX;
        double maxX = bounds.MaximumX;
        double minY = bounds.MinimumY;
        double maxY = bounds.MaximumY;
        double minZ = bounds.MinimumZ;
        double maxZ = bounds.MaximumZ;
        
        // +X direction (from left side)
        for (double y = minY; y <= maxY; y += spacing)
        {
            for (double z = minZ; z <= maxZ; z += spacing)
            {
                Point3D origin = new Point3D(minX - 1, y, z);
                Vector3D direction = new Vector3D(1, 0, 0);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        // -X direction (from right side)
        for (double y = minY; y <= maxY; y += spacing)
        {
            for (double z = minZ; z <= maxZ; z += spacing)
            {
                Point3D origin = new Point3D(maxX + 1, y, z);
                Vector3D direction = new Vector3D(-1, 0, 0);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        // +Y direction (from bottom)
        for (double x = minX; x <= maxX; x += spacing)
        {
            for (double z = minZ; z <= maxZ; z += spacing)
            {
                Point3D origin = new Point3D(x, minY - 1, z);
                Vector3D direction = new Vector3D(0, 1, 0);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        // -Y direction (from top)
        for (double x = minX; x <= maxX; x += spacing)
        {
            for (double z = minZ; z <= maxZ; z += spacing)
            {
                Point3D origin = new Point3D(x, maxY + 1, z);
                Vector3D direction = new Vector3D(0, -1, 0);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        // +Z direction (from front)
        for (double x = minX; x <= maxX; x += spacing)
        {
            for (double y = minY; y <= maxY; y += spacing)
            {
                Point3D origin = new Point3D(x, y, minZ - 1);
                Vector3D direction = new Vector3D(0, 0, 1);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        // -Z direction (from back)
        for (double x = minX; x <= maxX; x += spacing)
        {
            for (double y = minY; y <= maxY; y += spacing)
            {
                Point3D origin = new Point3D(x, y, maxZ + 1);
                Vector3D direction = new Vector3D(0, 0, -1);
                rays.Add(new Ray3D(origin, direction));
            }
        }
        
        return rays;
    }
	
 public static List<Ray3D> CreateRaysInDirection(Aabb bounds, Point3D center, Vector3D direction, double spacing)
    {
        var rays = new List<Ray3D>();
        
        // Normalize the direction vector
        Vector3D NormaliseDirection = direction.Normalise();
        
        // Calculate distance to place the ray origin plane
        double boundsDiagonal = CalculateDiagonalLength(bounds);
        double distance = boundsDiagonal * 1.5; // Place plane well outside bounds
        
        // Calculate the center point of the ray origin plane
        Point3D planeCenter = center - NormaliseDirection * distance;
        
        // Create two orthogonal vectors for the plane
        Vector3D up, right;
        
        // Choose up vector that's not parallel to direction
        if (Math.Abs(NormaliseDirection.DotProduct(new Vector3D(0, 0, 1))) < 0.9)
        {
            up = new Vector3D(0, 0, 1);
        }
        else
        {
            up = new Vector3D(1, 0, 0);
        }
        
        // Create orthogonal basis
        right = NormaliseDirection.CrossProduct(up).Normalise();
        up = right.CrossProduct(NormaliseDirection).Normalise();
        
        // Calculate grid size to ensure full coverage
        double gridSize = boundsDiagonal * 1.2;
        int steps = (int)Math.Ceiling(gridSize / (2 * spacing));
        
        // Create grid of rays on the plane
        for (int i = -steps; i <= steps; i++)
        {
            for (int j = -steps; j <= steps; j++)
            {
                double offsetX = i * spacing;
                double offsetY = j * spacing;
                
                // Calculate ray origin on the plane
                Point3D rayOrigin = planeCenter + right * offsetX + up * offsetY;
                
                // Create ray pointing in the specified direction
                rays.Add(new Ray3D(rayOrigin, NormaliseDirection));
            }
        }
        
        return rays;
    }

public static List<Ray3D> CreateHybridRays(Aabb bounds, double spacing)
    {
        var rays = new List<Ray3D>();
        
        // Calculate center point of the bounding box
        Point3D center = (bounds.Maximum +bounds.Minimum)/2;
        
        
        // Primary coverage: 6 axis-aligned directions with fine spacing
        rays.AddRange(CreateAxisAlignedRays(bounds, spacing));
        
        // Secondary coverage: Additional directions for better coverage
        var additionalDirections = new Vector3D[]
        {
            // Face diagonals (12 directions)
            new Vector3D(1, 1, 0).Normalise(),    // XY plane diagonals
            new Vector3D(1, -1, 0).Normalise(),
            new Vector3D(-1, 1, 0).Normalise(),
            new Vector3D(-1, -1, 0).Normalise(),
            
            new Vector3D(1, 0, 1).Normalise(),    // XZ plane diagonals
            new Vector3D(1, 0, -1).Normalise(),
            new Vector3D(-1, 0, 1).Normalise(),
            new Vector3D(-1, 0, -1).Normalise(),
            
            new Vector3D(0, 1, 1).Normalise(),    // YZ plane diagonals
            new Vector3D(0, 1, -1).Normalise(),
            new Vector3D(0, -1, 1).Normalise(),
            new Vector3D(0, -1, -1).Normalise(),
            
            // Cube corner diagonals (8 directions)
            new Vector3D(1, 1, 1).Normalise(),    // All 8 corners of a cube
            new Vector3D(1, 1, -1).Normalise(),
            new Vector3D(1, -1, 1).Normalise(),
            new Vector3D(1, -1, -1).Normalise(),
            new Vector3D(-1, 1, 1).Normalise(),
            new Vector3D(-1, 1, -1).Normalise(),
            new Vector3D(-1, -1, 1).Normalise(),
            new Vector3D(-1, -1, -1).Normalise()
        };
        
        // Use slightly coarser spacing for diagonal rays to balance coverage vs performance
        double diagonalSpacing = spacing * 1.414; // √2 factor
        
        // Add rays for each additional direction
        foreach (var direction in additionalDirections)
        {
            rays.AddRange(CreateRaysInDirection(bounds, center, direction, diagonalSpacing));
        }
        
        return rays;
    }

}

	public static Point3D[] GetCorrectOrderBoundingBoxPoints(Aabb aabb)
	{
		return new []
            {
                new Point3D(aabb.MinimumX, aabb.MinimumY, aabb.MaximumZ),
                new Point3D(aabb.MaximumX, aabb.MinimumY, aabb.MaximumZ),
                new Point3D(aabb.MaximumX, aabb.MaximumY, aabb.MaximumZ),
                new Point3D(aabb.MinimumX, aabb.MaximumY, aabb.MaximumZ),
                new Point3D(aabb.MinimumX, aabb.MinimumY, aabb.MinimumZ),
                new Point3D(aabb.MaximumX, aabb.MinimumY, aabb.MinimumZ),
                new Point3D(aabb.MaximumX, aabb.MaximumY, aabb.MinimumZ),
                new Point3D(aabb.MinimumX, aabb.MaximumY, aabb.MinimumZ),
            };
	}

}