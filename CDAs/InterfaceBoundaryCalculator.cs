#region Using Directives
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

public partial class ShapePerimeter
{
	public static bool debug = false;
	public static bool DrawShapes = false;
	
    private static readonly TimeSpan OuterGroupTimeout = TimeSpan.FromSeconds(5);
    private static int MaxUnionShapesPerBatch = 4;
    private static int MaxUnionPointsPerBatch = 200;
    private static int MaxUnionBatchIterations = 4;
    private static double UnionTolerance = 0.01d;
	
    static List<Polygon> selectedShapeBoundary = new List<Polygon>();	
	public static List<PrecisionMining.Common.Design.Attribute> groupingAttributes;
	public static List<PrecisionMining.Common.Design.Attribute> splittingAttributes;
	private static AttributeSelectOption groupingExpression;
	private static AttributeSelectOption splittingExpression;
	private static SpinEditOption<int> maxUnionShapesPerBatch;
	private static SpinEditOption<int> maxUnionPointsPerBatch;
	private static SpinEditOption<int> maxUnionBatchIterations;
	private static SpinEditOption<double> unionTolerance;
	private static CheckBoxOption drawOutputs;
    
    [CustomDesignAction]
    internal static CustomDesignAction CreateOutline()
    {		
        var customDesignAction = new CustomDesignAction("Calculate Interface Boundary", "", "Boundaries", Array.Empty<Keys>());
        customDesignAction.SelectionPanelEnabled = true;
        customDesignAction.SetupAction += (s,e) =>
        {
            customDesignAction.SetSelectMode(SelectMode.SelectElements);
        };
        customDesignAction.OptionsForm += (s,e) => 
        {
            var form = OptionsForm.Create("test");
            			
            groupingExpression = form.Options.AddAttributeSelect("Grouping Attributes", true);
            splittingExpression = form.Options.AddAttributeSelect("Splitting Attributes", true);
						
			maxUnionShapesPerBatch = form.Options.AddSpinEdit<int>("Max Shapes per Batch").SetValue(4).Validators.Add(x => x > 0, "Max Shapes value must be greater than zero.");
			maxUnionPointsPerBatch = form.Options.AddSpinEdit<int>("Max Points per Batch").SetValue(200).Validators.Add(x => x > 0, "Max Points value must be greater than zero.");
			maxUnionBatchIterations = form.Options.AddSpinEdit<int>("Max Batch Iterations").SetValue(4).Validators.Add(x => x > 0, "Max Iterations value must be greater than zero.");
			unionTolerance = form.Options.AddSpinEdit<double>("Union Tolerance").SetValue(0.1).Validators.Add(x => x >= 0, "Max Shapes value must be non-negative.");			
			drawOutputs = form.Options.AddCheckBox("Draw Outputs").SetValue(false);					
			
			e.OptionsForm = form;
        };
		
        customDesignAction.ApplyAction += (s,e) =>
        {
            try
            {           
                var inputs = customDesignAction.ActionInputs.Triangulations;           
	
				groupingAttributes = groupingExpression.Values.ToList();
				splittingAttributes = splittingExpression.Values.ToList();
				
				MaxUnionShapesPerBatch = maxUnionShapesPerBatch.Value;
				MaxUnionPointsPerBatch = maxUnionPointsPerBatch.Value;
				MaxUnionBatchIterations = maxUnionBatchIterations.Value;
				UnionTolerance = unionTolerance.Value;
				DrawShapes = drawOutputs.Value;
				
                // Group by a composite key derived from the selected attributes using the helper GetAttributes
                var outerGroups = inputs.GroupBy(x => GetAttributes(x, groupingAttributes)).ToList();

                if (outerGroups.Count == 0)
                {
                    CustomProgress.Label = "No interface groups detected";
                    CustomProgress.Percentage = 1d;
                    return;
                }

                // Initialize progress tracking
                CustomProgress.Bind("Calculating Interface Boundaries");
                CustomProgress.Indeterminate = false;
                var timedOutGroupOrder = new List<string>();
                var timedOutGroupSet = new HashSet<string>();
                var cancellationToken = CustomProgress.Token;
                
                for (int currentGroupIndex = 0; currentGroupIndex < outerGroups.Count; currentGroupIndex++)
                {
                    var outerGroup = outerGroups[currentGroupIndex];
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Update progress
                    double progress = (double)currentGroupIndex / outerGroups.Count;
                    CustomProgress.Percentage = progress;
                    CustomProgress.Label = string.Format("Processing group {0} of {1}: {2}", 
                        currentGroupIndex + 1, outerGroups.Count, outerGroup.Key);

                    if (debug)
                    {
                        int outerGroupTriangulations = outerGroup.Count();
                        int estimatedInnerGroups = 0;
                        if (splittingAttributes != null && splittingAttributes.Count > 0)
                        {
                            estimatedInnerGroups = outerGroup
                                .Select(x => GetAttributes(x, splittingAttributes))
                                .Distinct()
                                .Count();
                        }
                        Out.WriteLine(string.Format(
                            "Outer group {0}: {1} triangulations, {2} distinct inner keys",
                            FormatGroupKey(outerGroup.Key),
                            outerGroupTriangulations,
                            estimatedInnerGroups));
                    }

                    bool completed = ExecuteOuterGroupWithTimeout(outerGroup, cancellationToken);
                    if (!completed)
                    {
                        if (debug)
                            Out.WriteLine("Outer group timed out: " + outerGroup.Key);
                        CustomProgress.Label = string.Format("Skipped group {0} due to timeout", FormatGroupKey(outerGroup.Key));
                        if (timedOutGroupSet.Add(outerGroup.Key))
                            timedOutGroupOrder.Add(outerGroup.Key);
                        continue;
                    }
                }

                if (timedOutGroupOrder.Count > 0)
                {
                    double timeoutSeconds = OuterGroupTimeout.TotalSeconds;
                    Out.WriteLine(string.Format("Interface Boundary calculation skipped the following groups due to {0:0.###}s timeout:", timeoutSeconds));
                    foreach (var group in timedOutGroupOrder)
                    {
                        Out.WriteLine(" - " + FormatGroupKey(group));
                    }
                }
            }
                catch (OperationCanceledException)
                {
                    Out.WriteLine("Interface Boundary calculation cancelled by user.");
                }
            finally
            {
                CustomProgress.Close("Interface Boundary calculation complete");
            }
        };
        return customDesignAction;
    }

    private static string FormatGroupKey(string key)
    {
        return string.IsNullOrEmpty(key) ? "<blank key>" : key;
    }

    private static bool ExecuteOuterGroupWithTimeout(IGrouping<string, ILayerTriangulation> outerGroup, CancellationToken globalToken)
    {
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken))
        {
            var workerTask = Task.Factory.StartNew(() =>
            {
                ProcessOuterGroup(outerGroup, linkedCts.Token);
            }, linkedCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                if (workerTask.Wait(OuterGroupTimeout))
                {
                    workerTask.GetAwaiter().GetResult();
                    return true;
                }
                else
                {
                    linkedCts.Cancel();
                    try
                    {
                        workerTask.Wait();
                    }
                    catch { }
                    return false;
                }
            }
            catch (AggregateException ex)
            {
                linkedCts.Cancel();
                var flattened = ex.Flatten();
                if (flattened.InnerExceptions.Count == 1)
                {
                    var oce = flattened.InnerExceptions[0] as OperationCanceledException;
                    if (oce != null)
                        throw oce;
                }
//                throw;
				return false;
            }
        }
    }

    private static void ProcessOuterGroup(IGrouping<string, ILayerTriangulation> outerGroup, CancellationToken cancellationToken)
    {
        Dictionary<string, List<Polygon>> dict = new Dictionary<string, List<Polygon>>();

        var innerGroups = outerGroup.GroupBy(x => GetAttributes(x, splittingAttributes)).ToList();

        if (debug)
        {
            Out.WriteLine(string.Format(
                "Outer group {0}: {1} inner groups detected",
                FormatGroupKey(outerGroup.Key),
                innerGroups.Count));
        }

        if (innerGroups.Count <= 1)
            return;

        foreach (var innerGroup in innerGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<TriangleMesh> triangleList = new List<TriangleMesh>();

            foreach (var tri in innerGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();
                triangleList.Add(tri.Data);
            }

            cancellationToken.ThrowIfCancellationRequested();
            TriangleMesh triangleMesh = TriangleMesh.UnionSolids(triangleList).OutputMesh;
            selectedShapeBoundary = triangleMesh.CreateSilhouette(Vector3D.UpVector, true);

            if (debug)
            {
                Out.WriteLine(string.Format(
                    "Inner group {0}: {1} triangulations merged, silhouette polygons {2}",
                    FormatGroupKey(innerGroup.Key),
                    triangleList.Count,
                    selectedShapeBoundary != null ? selectedShapeBoundary.Count : 0));
            }

            if (!dict.ContainsKey(innerGroup.Key))
                dict[innerGroup.Key] = new List<Polygon>(selectedShapeBoundary);
        }

        var dict2D = FlatUnionShapes(dict);
        if (debug)
        {
            int flattenedShapeCollections = dict2D.Sum(kvp => kvp.Value != null ? kvp.Value.Count : 0);
            int flattenedPointCount = dict2D.Sum(kvp => CountShapePoints(kvp.Value));
            Out.WriteLine(string.Format(
                "Outer group {0}: flattened to {1} 2D shape lists ({2} shapes, {3} points)",
                FormatGroupKey(outerGroup.Key),
                dict2D.Count,
                flattenedShapeCollections,
                flattenedPointCount));
        }
        var keys = dict2D.Keys.ToList();

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (debug)
                    Out.WriteLine("Inner Group String: " + keys[i] + " merge with: " + keys[j]);

                var interfaceAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("CDA\\Interface Boundary\\" + keys[i] + " and " + keys[j]);

                List<Shape> shapesA = dict2D[keys[i]];
                List<Shape> shapesB = dict2D[keys[j]];
                List<Shape> combinedShapes = shapesA.Concat(shapesB).ToList();

                if (debug)
                {
                    int shapesAPoints = CountShapePoints(shapesA);
                    int shapesBPoints = CountShapePoints(shapesB);
                    int combinedPoints = CountShapePoints(combinedShapes);
                    Out.WriteLine(string.Format(
                        "Union prep {0} vs {1}: A={2} shapes/{3} pts, B={4} shapes/{5} pts, combined={6} shapes/{7} pts",
                        FormatGroupKey(keys[i]),
                        FormatGroupKey(keys[j]),
                        shapesA != null ? shapesA.Count : 0,
                        shapesAPoints,
                        shapesB != null ? shapesB.Count : 0,
                        shapesBPoints,
                        combinedShapes.Count,
                        combinedPoints));
                }

                cancellationToken.ThrowIfCancellationRequested();
                var shapesU = RunUnionWithBatching(
                    combinedShapes,
                    cancellationToken,
                    keys[i],
                    keys[j],
                    outerGroup.Key);

                if (shapesU == null || shapesU.Count == 0)
                    continue;

                if (debug)
                {
                    Out.WriteLine(string.Format(
                        "Union result {0} vs {1}: {2} shapes/{3} pts",
                        FormatGroupKey(keys[i]),
                        FormatGroupKey(keys[j]),
                        shapesU.Count,
                        CountShapePoints(shapesU)));
                }

                if (DrawShapes)
                {
                    Layer outputLayer = Project.ActiveProject.Design.LayerData.GetOrCreateLayer(@"CDA Debug\Interface Boundary\Outlines");
                    Layer outputLayerU = Project.ActiveProject.Design.LayerData.GetOrCreateLayer(@"CDA Debug\Interface Boundary\Unions");

                    if (shapesA != null)
                    {
                        shapesA.ForEach(x => outputLayer.Shapes.Add(x));
                    }
                    if (shapesB != null)
                    {
                        shapesB.ForEach(x => outputLayer.Shapes.Add(x));
                    }
                    if (shapesU != null)
                        shapesU.ForEach(x => outputLayerU.Shapes.Add(x));
                }

                double currentGroupTotalPerimeter = shapesA.Sum(x => x.ShapeLength);
                double otherGroupTotalPerimeter = shapesB.Sum(x => x.ShapeLength);
                double unionGroupsTotalPerimeter = shapesU.Sum(x => x.ShapeLength);

                double interfaceLength = 0.5 * (currentGroupTotalPerimeter + otherGroupTotalPerimeter - unionGroupsTotalPerimeter);

                var matchingGroups = innerGroups.Where(g => g.Key == keys[i] || g.Key == keys[j]);

                cancellationToken.ThrowIfCancellationRequested();
                if (matchingGroups != null)
                {
                    int updatedTriangles = 0;
                    foreach (var tri in matchingGroups.SelectMany(x => x))
                    {
                        tri.AttributeValues.SetValue(interfaceAttribute, interfaceLength);
                        updatedTriangles++;
                    }

                    if (debug)
                    {
                        Out.WriteLine(string.Format(
                            "Interface attribute applied to {0} triangulations for pair {1} vs {2} (length {3:0.###})",
                            updatedTriangles,
                            FormatGroupKey(keys[i]),
                            FormatGroupKey(keys[j]),
                            interfaceLength));
                    }
                }
            }
        }
    }

    private static int CountShapePoints(IEnumerable<Shape> shapes)
    {
        if (shapes == null)
            return 0;

        int total = 0;
        foreach (var shape in shapes)
        {
            if (shape == null)
                continue;
            total += CountPoints(shape);
        }

        return total;
    }

    private static int CountPoints(IEnumerable<Point3D> geometry)
    {
        if (geometry == null)
            return 0;

        int count = 0;
        foreach (var _ in geometry)
            count++;

        return count;
    }

    private static List<Shape> RunUnionWithBatching(List<Shape> shapes, CancellationToken cancellationToken, string keyA, string keyB, string outerGroupKey)
    {
        if (shapes == null || shapes.Count == 0)
            return null;

        var workingShapes = new List<Shape>(shapes);
        int iteration = 0;

        while (RequiresUnionBatching(workingShapes) && iteration < MaxUnionBatchIterations)
        {
            iteration++;
            var batches = SliceShapeBatches(workingShapes).ToList();

            if (debug)
            {
                int batchShapeCount = batches.Sum(b => b.Count);
                int batchPointCount = batches.Sum(b => CountShapePoints(b));
                Out.WriteLine(string.Format(
                    "Union batching iteration {0} for {1} vs {2}: {3} batches covering {4} shapes/{5} pts",
                    iteration,
                    FormatGroupKey(keyA),
                    FormatGroupKey(keyB),
                    batches.Count,
                    batchShapeCount,
                    batchPointCount));
            }

            var mergedShapes = new List<Shape>();
            int batchIndex = 0;
            foreach (var batch in batches)
            {
                batchIndex++;
                cancellationToken.ThrowIfCancellationRequested();
                var options = Actions.UnionPolygons.CreateOptions(batch, null, UnionTolerance);
                var results = Actions.UnionPolygons.Run(options);

                if (debug)
                {
                    Out.WriteLine(string.Format(
                        "Union batch {0}.{1} for {2} vs {3}: {4} shapes/{5} pts -> success={6}, polygons={7}",
                        iteration,
                        batchIndex,
                        FormatGroupKey(keyA),
                        FormatGroupKey(keyB),
                        batch.Count,
                        CountShapePoints(batch),
                        results.Success,
                        results.Polygons != null ? results.Polygons.Count : 0));
                }

                if (!results.Success || results.Polygons == null || results.Polygons.Count == 0)
                    continue;

                foreach (var poly in results.Polygons)
                {
                    var shape = new Shape(poly) { Closed = true };
                    mergedShapes.Add(shape);
                }
            }

            if (mergedShapes.Count == 0)
                break;

            workingShapes = mergedShapes;
        }

        if (RequiresUnionBatching(workingShapes))
        {
            if (debug)
            {
                Out.WriteLine(string.Format(
                    "Union aborted for {0} vs {1} in outer group {2}: still over batch limits after {3} iterations (shapes={4}, points={5})",
                    FormatGroupKey(keyA),
                    FormatGroupKey(keyB),
                    FormatGroupKey(outerGroupKey),
                    iteration,
                    workingShapes != null ? workingShapes.Count : 0,
                    CountShapePoints(workingShapes)));
            }
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var finalOptions = Actions.UnionPolygons.CreateOptions(workingShapes, null, UnionTolerance);
        var finalResults = Actions.UnionPolygons.Run(finalOptions);

        if (debug)
        {
            Out.WriteLine(string.Format(
                "Union final run for {0} vs {1}: success={2}, polygons={3}, shapes input={4}, points input={5}",
                FormatGroupKey(keyA),
                FormatGroupKey(keyB),
                finalResults.Success,
                finalResults.Polygons != null ? finalResults.Polygons.Count : 0,
                workingShapes.Count,
                CountShapePoints(workingShapes)));
        }

        if (!finalResults.Success || finalResults.Polygons == null || finalResults.Polygons.Count == 0 || finalResults.Polygons[0] == null)
            return null;

        var finalShapes = finalResults.Polygons.Select(x => new Shape(x)).ToList();
        finalShapes.ForEach(x => x.Closed = true);
        return finalShapes;
    }

    private static bool RequiresUnionBatching(List<Shape> shapes)
    {
        if (shapes == null || shapes.Count == 0)
            return false;

        if (shapes.Count > MaxUnionShapesPerBatch)
            return true;

        int pointCount = CountShapePoints(shapes);
        return pointCount > MaxUnionPointsPerBatch;
    }

    private static IEnumerable<List<Shape>> SliceShapeBatches(List<Shape> shapes)
    {
        if (shapes == null || shapes.Count == 0)
            yield break;

        var current = new List<Shape>();
        int currentPoints = 0;

        foreach (var shape in shapes)
        {
            if (shape == null)
                continue;

            int shapePoints = CountPoints(shape);
            bool exceedsShapeLimit = current.Count >= MaxUnionShapesPerBatch;
            bool exceedsPointLimit = currentPoints + shapePoints > MaxUnionPointsPerBatch;

            if (current.Count > 0 && (exceedsShapeLimit || exceedsPointLimit))
            {
                yield return current;
                current = new List<Shape>();
                currentPoints = 0;
            }

            current.Add(shape);
            currentPoints += shapePoints;
        }

        if (current.Count > 0)
            yield return current;
    }
    
    internal static string GetAttributes(ILayerTriangulation tri, List<PrecisionMining.Common.Design.Attribute> attributes)
    {
        if (attributes == null || attributes.Count == 0)
            return string.Empty;

        string ret = "";
        
        foreach (var att in attributes)
        {    
            var valObj = tri.AttributeValues.GetValue(att);
            var value = valObj != null ? valObj.ToString() : string.Empty;
            ret += value + "_";           
        }
        
        return ret;        
    }
    
    internal static Dictionary<string, List<Shape>> FlatUnionShapes(Dictionary<string, List<Polygon>> input)
    {
        var ret = new Dictionary<string, List<Shape>>();
        
        foreach (var key in input.Keys)
        {           
            List<Shape> retShape = new List<Shape>();
			
            var unionPolyOptions = Actions.UnionPolygons.CreateOptions(input[key], null, UnionTolerance);
            var unionPolyResults = Actions.UnionPolygons.Run(unionPolyOptions);
			
            if (!unionPolyResults.Success || unionPolyResults.Polygons == null || unionPolyResults.Polygons.Count == 0 || unionPolyResults.Polygons[0] == null)
				continue;
			
			retShape = unionPolyResults.Polygons.Select(x => new Shape(x)).ToList();			

            if (debug)
            {
                Out.WriteLine(string.Format(
                    "Flattening key {0}: input polygons={1}, output shapes={2}, output points={3}",
                    FormatGroupKey(key),
                    input[key] != null ? input[key].Count : 0,
                    retShape.Count,
                    CountShapePoints(retShape)));
            }
			foreach (var shape in retShape)
			{
				List<Point3D> pointList = new List<Point3D>();
								
				foreach (var point in shape.ToList())
					pointList.Add(new Point3D(point.CloneXY(0).X,point.CloneXY(0).Y,0));
				
				Shape newShape = new Shape(pointList);				
				newShape.Closed = true;
							
				if (!ret.ContainsKey(key))
	    			ret[key] = new List<Shape> {newShape};
				else
					ret[key].Add(newShape);
			}
        }
		
        return ret;       
    }
}

public class CustomProgress
{
    private static bool DEBUG = true;
    private static CancellationTokenSource _cts = new CancellationTokenSource();
    public static CancellationToken Token
    {
        get { return _cts.Token; }
    }
    public static void Cancel()
    {
        _cts.Cancel();
    }

    public enum ProgressFormState
    {
        None,
        Initializing,
        Ready,
        Closing,
        Closed
    }

    private static volatile ProgressFormState _state = ProgressFormState.None;

    #region Inner Form
    class ProgressForm : Form
    {
        public ProgressBar bar;
        public Label label;
        Label eta;
        Stopwatch sw;
        Button cancel;
        bool _cancelled;
        public bool Cancelled { get { return _cancelled; } }

        public ProgressForm()
        {
            this.Height = 160;
            this.Width = 450;
            this.ShowIcon = false;

            bar = new ProgressBar();
            bar.Top = 40;
            bar.Width = 325;
            bar.Left = 20;
            bar.Height = 30;
            bar.Minimum = 0;
            bar.Maximum = 1000;
            bar.MarqueeAnimationSpeed = 40;
            bar.Style = ProgressBarStyle.Marquee;
            this.Controls.Add(bar);

            label = new Label();
            label.Parent = this;
            label.Top = 10;
            label.Width = Width;
            label.Left = bar.Left;

            eta = new Label();
            eta.Parent = this;
            eta.Top = 80;
            eta.Width = Width;
            eta.Left = bar.Left;
            sw = Stopwatch.StartNew();

            _cancelled = false;
            cancel = new Button();
            cancel.Top = bar.Top;
            cancel.Left = 350;
            cancel.Height = 30;
            cancel.Width = 64;
            cancel.Text = "Cancel";
            cancel.Click += (o, e) =>
            {
                _cancelled = true;
                cancel.Enabled = false;
                cancel.Text = "Cancelling";

                // Use Task.Run instead of creating a new Thread
                Task.Run(() =>
                {
                    try
                    {
                        CustomProgress.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cancellation error: " + ex.Message);
                    }
                });

                // Safety timer to ensure the form closes even if something goes wrong
                var safetyTimer = new System.Windows.Forms.Timer
                {
                    Interval = 2000, // 2 seconds
                    Enabled = true
                };

                safetyTimer.Tick += (s, args) =>
                {
                    safetyTimer.Stop();
                    try
                    {
                        // Force UI thread to close the form
                        if (!this.IsDisposed && this.Visible)
                        {
                            this.Close();
                        }
                    }
                    catch { /* Ignore errors during forced close */ }
                };
            };
            this.Controls.Add(cancel);
        }

        public new void Update()
        {
            var elapsed = sw.Elapsed;

            string msg = "Elapsed: " + PrecisionMining.Spry.Log.FormatTimeSpan(elapsed);

            double secPerPct = elapsed.TotalSeconds / (double)bar.Value;
            double secRem = (1000 - bar.Value) * secPerPct;
            if (bar.Style != ProgressBarStyle.Marquee)
            {
                msg += " Estimated completion in: ";
                if (secRem > 360000)
                    msg += "more than 100 hours";
                else
                {
                    var rem = TimeSpan.FromSeconds(secRem);
                    msg += PrecisionMining.Spry.Log.FormatTimeSpan(rem);
                }
            }

            eta.Text = msg;
            base.Update();
        }
    }
    #endregion

    bool _displayed;
    string _title;
    ProgressForm _form;
    Stopwatch _redraw;
    int pct;
    string label;
    bool indeterminate;

    static CustomProgress _bound;
    private static readonly object _lockObject = new object();

    public static double Percentage
    {
        set
        {
            DoLocked(f =>
            {
                int clamped = (int)(Math.Max(Math.Min(value, 1), 0) * 1000);
                f.pct = clamped;
                f.indeterminate = false;
                f.Update();
            });
        }
    }

    public static string Label
    {
        set
        {
            if (value == null) return;
            DoLocked(f =>
            {
                f.label = value;
                f.Update();
            });
        }
    }

    public static bool Cancelled
    {
        get
        {
            lock (_lockObject) // Assuming _lockObject is a static field for synchronizing access to _bound
            {
                ProgressForm currentForm = null;
                if (_bound != null)
                {
                    // Assuming _form is an instance field of the CustomProgress class
                    // and _bound is the static instance of CustomProgress
                    currentForm = _bound._form;
                }

                if (currentForm == null)
                {
                    // No CustomProgress instance is bound, or it has no associated form.
                    // In this case, cannot determine if cancelled via the form.
                    // Returning false means operations depending on this flag will continue.
                    return false;
                }

                try
                {
                    // Check if the form has been disposed.
                    if (currentForm.IsDisposed)
                    {
                        // If the form is disposed, we cannot reliably query its "Cancelled" state.
                        // To prevent the calling code from throwing
                        // an OperationCanceledException based on a potentially stale or inaccessible state,
                        // and to allow the program to "continue" (as per the request to ignore the exception),
                        // we return false. This means the loop in code will not be
                        // prematurely terminated by an OperationCanceledException due to this check.
                        // Note: This might mean the loop continues even if the user intended to cancel
                        // and the form closed as a result. A more robust cancellation mechanism
                        // would involve CustomProgress having its own _cancelled flag that is
                        // definitively set upon user cancellation before the form is disposed.
                        return false;
                    }

                    // If the form is not disposed, query its Cancelled property.
                    // This assumes ProgressForm has a public bool Cancelled property.
                    return currentForm.Cancelled;
                }
                catch (ObjectDisposedException)
                {
                    // This catch block handles the case where IsDisposed was false,
                    // but the form became disposed before or during the access to currentForm.Cancelled.
                    // Similar to the IsDisposed check, return false to allow the program to continue
                    // and avoid an OperationCanceledException from being thrown by the caller.
                    return false;
                }
                catch (Exception ex)
                {
                    // For any other unexpected exceptions while accessing form state,
                    // it's good practice to log the error if a logging mechanism is available.
                    // Example: PrecisionMining.Spry.Out.WriteLine($"Error in CustomProgress.Cancelled: {ex.ToString()}");
                    // Return false to be safe and prevent halting the calling process
                    // due to an issue within this progress checking mechanism.
                    return false;
                }
            }
        }
    }

    public static bool Indeterminate
    {
        get
        {
            lock (_lockObject)
            {
                if (_bound == null)
                    return false;
                else
                    return _bound.indeterminate;
            }
        }
        set
        {
            DoLocked(f =>
            {
                f.indeterminate = value;
                f.Update();
            });
        }
    }


    private static void DoLocked(Action<CustomProgress> action)
    {
        lock (_lockObject)
        {
            if (_bound != null && !_bound._form.IsDisposed)
            {
                try
                {
                    action(_bound);
                }
                catch (ObjectDisposedException)
                {
                    // Handle disposed form
                    _bound = null;
                }
            }
        }
    }

    private CustomProgress()
    {
        indeterminate = true;
        _redraw = Stopwatch.StartNew();
    }
    private static void ResetCancellation()
    {
        lock (_lockObject)
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Dispose();
                }
            }
            catch
            {
                // Ignore disposal errors
            }

            _cts = new CancellationTokenSource();
        }
    }

    private static string FormatTimestamp(string message)
    {
        return string.Format("[{0:HH:mm:ss.fff}] {1}", DateTime.Now, message);
    }

    public static void InitializeProgress(string title)
    {
        // Skip recursive calls
        if (_state == ProgressFormState.Initializing)
        {
            Console.WriteLine(FormatTimestamp("Ignoring recursive initialization attempt"));
            return;
        }

        ResetCancellation();
        lock (_lockObject)
        {
            if (_bound != null)
            {
                if (_bound._form != null && !_bound._form.IsDisposed)
                {
                    _bound._form.Text = title;
                    return;
                }

                // Only close if not already in closing/closed state
                if (_state != ProgressFormState.Closing && _state != ProgressFormState.Closed)
                {
                    _state = ProgressFormState.Closing;
                    Close("Closing before new initialization");
                }
            }

            _state = ProgressFormState.Initializing;
            if (DEBUG)
                Console.WriteLine(FormatTimestamp("Setting state to Initializing"));

            var independentProgress = new CustomProgress();
            independentProgress._title = title;

            // TWO task completion sources - one for object creation, one for form shown
            var formCreated = new TaskCompletionSource<ProgressForm>();
            var formShown = new TaskCompletionSource<bool>();

            Thread progressThread = new Thread(() =>
            {
                try
                {
                    if (DEBUG)
                        Console.WriteLine(FormatTimestamp(string.Format("Starting progress initialization on thread {0}", Thread.CurrentThread.ManagedThreadId)));

                    // Create the form
                    ProgressForm form = new ProgressForm();
                    form.Text = title;

                    // Additional event tracking
                    if (DEBUG)
                    {
                        form.HandleCreated += (s, e) => Console.WriteLine(FormatTimestamp(string.Format("Form handle created on thread {0}", Thread.CurrentThread.ManagedThreadId)));
                        form.FormClosing += (s, e) => Console.WriteLine(FormatTimestamp(string.Format("Form closing (reason: {0}) on thread {1}, Cancel={2}", e.CloseReason, Thread.CurrentThread.ManagedThreadId, e.Cancel)));
                        form.FormClosed += (s, e) => Console.WriteLine(FormatTimestamp(string.Format("Form closed on thread {0}", Thread.CurrentThread.ManagedThreadId)));
                    }


                    // THIS IS KEY: Signal when form is actually shown and ready
                    form.Shown += (s, e) =>
                    {
                        if (DEBUG)
                            Console.WriteLine(FormatTimestamp(string.Format("Form shown on thread {0}", Thread.CurrentThread.ManagedThreadId)));
                        formShown.TrySetResult(true); // Signal that form is FULLY ready
                    };

                    // Store form reference BEFORE signaling
                    independentProgress._form = form;

                    // Signal that form OBJECT exists but isn't showing yet
                    formCreated.SetResult(form);

                    if (DEBUG)
                        Console.WriteLine(FormatTimestamp(string.Format("About to start Application.Run on thread {0}", Thread.CurrentThread.ManagedThreadId)));

                    Application.Run(form);

                    if (DEBUG)
                        Console.WriteLine(FormatTimestamp(string.Format("Application.Run completed on thread {0}", Thread.CurrentThread.ManagedThreadId)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Progress error: {0}\nStack trace: {1}", ex.Message, ex.StackTrace));
                    formCreated.TrySetException(ex);
                    formShown.TrySetException(ex);
                }
            });

            progressThread.SetApartmentState(ApartmentState.STA);
            progressThread.IsBackground = true;
            progressThread.Start();

            // IMPROVED SYNCHRONIZATION: First wait for form object creation
            try
            {
                if (formCreated.Task.Wait(TimeSpan.FromSeconds(5)))
                {
                    if (DEBUG)
                        Console.WriteLine(FormatTimestamp("Form object created, waiting for it to be shown"));

                    // Store bound reference after form object exists
                    _bound = independentProgress;

                    // THEN wait for the form to be fully shown
                    if (formShown.Task.Wait(TimeSpan.FromSeconds(5)))
                    {
                        if (DEBUG)
                            Console.WriteLine(FormatTimestamp("Form fully shown and ready for operations"));
                        _state = ProgressFormState.Ready;

                        if (DEBUG)
                            Console.WriteLine(FormatTimestamp("Setting state to Ready"));
                    }
                    else
                    {
                        throw new TimeoutException("Progress form failed to show within timeout");
                    }
                }
                else
                {
                    throw new TimeoutException("Progress form failed to initialize");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error during form initialization: {0}\nStack trace: {1}", ex.Message, ex.StackTrace));
                _state = ProgressFormState.Closed;
                Console.WriteLine(FormatTimestamp("Setting state to Closed due to error"));
                // Make sure to clean up
                _bound = null;
            }
        }
    }

    public static void Bind(string title)
    {
        lock (_lockObject)
        {
            if (_bound == null)
            {
                InitializeProgress(title);
            }
            else if (_bound._form == null || _bound._form.IsDisposed)
            {
                InitializeProgress(title);
            }
            else
            {
                _bound._title = title;
                _bound._form.Text = title;
            }
        }
    }

    public static void Close(string reason = "Unknown")
    {
        if (DEBUG)
        {
            Console.WriteLine(FormatTimestamp("Close requested. Reason: " + reason));
            Console.WriteLine(FormatTimestamp("Close stack trace: " + Environment.StackTrace));
        }

        // Only allow closing from Ready state
        if (_state != ProgressFormState.Ready && reason != "Force Close")
        {
            Console.WriteLine(FormatTimestamp("Skipping close because state is " + _state));
            return;
        }

        _state = ProgressFormState.Closing;

        Console.WriteLine(FormatTimestamp("Setting state to Closing"));

        // First try to cancel any ongoing operations
        try
        {
            lock (_lockObject)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format("Error during cancellation: {0}", ex.Message));
        }

        lock (_lockObject)
        {
            if (_bound == null) return;

            if (_bound._form != null && !_bound._form.IsDisposed)
            {
                try
                {
                    // Store a local reference to the form
                    var form = _bound._form;

                    // Check if the handle has been created yet
                    if (!form.IsHandleCreated)
                    {
                        Console.WriteLine(FormatTimestamp("Form handle not yet created, delaying close operation"));
                        Console.WriteLine(string.Format("Form state: Created={0}, Visible={1}, IsDisposed={2}", form.Created, form.Visible, form.IsDisposed));

                        // Set up a timer to try closing again after a short delay
                        System.Windows.Forms.Timer delayedClose = new System.Windows.Forms.Timer();
                        delayedClose.Interval = 500; // 500ms delay
                        delayedClose.Tick += (s, e) =>
                        {
                            delayedClose.Stop();
                            Close(); // Try closing again
                            delayedClose.Dispose();
                        };
                        delayedClose.Start();

                        return; // Exit for now, will retry via timer
                    }

                    if (form.InvokeRequired)
                    {
                        // Clear references first to prevent reentrant calls
                        _bound._form = null;
                        _bound = null;

                        form.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // Disable controls first
                                if (form.bar != null && !form.IsDisposed)
                                    form.bar.Enabled = false;

                                // Close and dispose the form
                                if (!form.IsDisposed)
                                {
                                    form.Close();
                                    form.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(string.Format("Error closing progress form: {0}\nStack trace: {1}", ex.Message, ex.StackTrace));
                            }
                        }));
                    }
                    else
                    {
                        // Clear references first
                        _bound._form = null;
                        _bound = null;

                        form.Close();
                        form.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Error in Close(): {0}\nStack trace: {1}", ex.Message, ex.StackTrace));
                }
            }
            else
            {
                _bound = null;
            }

            // Always clean up the cancellation token source
            try
            {
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource(); // Create a fresh one for next use
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error disposing CancellationTokenSource: {0}", ex.Message));
            }
        }

        _state = ProgressFormState.Closed;
        Console.WriteLine(FormatTimestamp("Setting state to Closed"));
    }

    void Update()
    {
        if (!CanDraw())
            return;

        MaybeDisplay();

        Action update = () =>
        {
            _form.label.Text = label ?? "";
            if (indeterminate && _form.bar.Style != ProgressBarStyle.Marquee)
            {
                _form.bar.Style = ProgressBarStyle.Marquee;
            }
            else if (!indeterminate)
            {
                _form.bar.Style = ProgressBarStyle.Blocks;
                _form.bar.Value = pct;
            }

            try { _form.Update(); } catch { }
        };

        if (_form.InvokeRequired)
            _form.Invoke(update);
        else
            update();
    }

    void MaybeDisplay()
    {
        if (!_displayed && _state != ProgressFormState.Initializing)
        {
            _displayed = true;
            // Don't call InitializeProgress directly if we're already initializing
            if (_state == ProgressFormState.None)
            {
                InitializeProgress(_title);
            }
        }
    }

    bool CanDraw()
    {
        if (_redraw == null)
            return true;

        if (_redraw.ElapsedMilliseconds > 200)
        {
            _redraw.Restart();
            return true;
        }
        else
        {
            return false;
        }
    }

    void RedrawTimer()
    {
        while (true)
        {
            Thread.Sleep(1000);
            if (_bound == null)
                break;

            DoLocked(x => x.Update());
        }
    }

    public static void Test()
    {
        CustomProgress.Bind("Testing title");

        CustomProgress.Label = "Test elapsed indeterminate";
        CustomProgress.Indeterminate = true;
        Thread.Sleep(5000);

        for (int x = 0; x < 5; x++)
        {
            for (int i = 0; i < 1000; i++)
            {

                long currentIteration = x * 1000L + i;
                long totalIterations = 5 * 1000L;

                CustomProgress.Label = string.Format("Done {0} out of {1}", currentIteration, totalIterations);

                CustomProgress.Indeterminate = false;
                CustomProgress.Percentage = (double)currentIteration / totalIterations;

                CustomProgress.Token.ThrowIfCancellationRequested();

                System.Threading.Thread.Sleep(10);
            }
        }

        CustomProgress.Close();
    }
}


