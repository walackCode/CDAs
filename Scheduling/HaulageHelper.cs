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

public partial class DrawHaulageHelper
{
    public static Color attachcol = Color.LimeGreen;
    public static Color detachcol = Color.Blue;
    public static Color neithercol = Color.Black;
    public static Color bothcol = Color.Maroon;
    public static SegmentCode attachsc = null;
    public static SegmentCode detachsc = null;
    public static SegmentCode neithersc = null;
    public static SegmentCode bothsc = null;
    public static float LW = 3;
	
    public static string stopaskingrestore = "Stopaskingnwcdamax";

    private static bool checknulls()
    {
        var myarray = new SegmentCode[] { attachsc, detachsc, neithersc, bothsc };
        return myarray.Any(x => x == null);
    }

    #region Context Menu Methods

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> SetAttach()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "01 Set Attach";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            foreach (var nw in s.ToList())
                SetProps(nw, "Attach");
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> SetDateLimit()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "05 Set Date Limit ";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            var workingcase = s.First().Case;
            var dates = OptionsForm.Create("Date Limits");
            var startdate = dates.Options.AddDateTime("Start Date").SetValue(workingcase.ScheduleStart).RestoreValue("datesstartpotato");
            var enddate = dates.Options.AddDateTime("End Date").SetValue(workingcase.ScheduleEnd).RestoreValue("datesendpotato");

            if (dates.ShowDialog() != DialogResult.OK)
                return;

            foreach (var nw in s.ToList())
            {
                nw.DateLimited = true;
                nw.StartDateLimit = startdate.Value;
                nw.EndDateLimit = enddate.Value;
            }
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> SetDateLimitfiltered()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "07 Set Date Limit filtered";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            var workingcase = s.First().Case;
            var dates = OptionsForm.Create("Date Limits");
            var startdate = dates.Options.AddDateTime("Start Date").SetValue(workingcase.ScheduleStart).RestoreValue("datesstartpotato");
            var enddate = dates.Options.AddDateTime("End Date").SetValue(workingcase.ScheduleEnd).RestoreValue("datesendpotato");

            if (dates.ShowDialog() != DialogResult.OK)
                return;

            foreach (var nw in s.ToList())
            {
                Out.WriteLine(nw.StartDateLimit + " " + nw.EndDateLimit);

                if ((nw.StartDateLimit != DateTime.MinValue || nw.StartDateLimit != DateTime.MaxValue) && nw.DateLimited == true)
                    continue;

                nw.DateLimited = true;
                nw.StartDateLimit = startdate.Value;
                nw.EndDateLimit = enddate.Value;
            }
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> RemoveDateLimit()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "06 Remove Date Limit ";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            foreach (var nw in s.ToList())
            {
                nw.DateLimited = false;
            }
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> SetDetach()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "02 Set Detach";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            foreach (var nw in s.ToList())
                SetProps(nw, "Detach");
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> Default()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "03 Set Default";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            foreach (var nw in s.ToList())
                SetProps(nw, "Default");
        };
        return entryPoint;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<List<NetworkShape>> Both()
    {
        var entryPoint = new ContextMenuEntryPoint<List<NetworkShape>>();
        entryPoint.SubMenu = s => "Set Type";
        entryPoint.Name = s => "04 Set Attach and Detach";
        entryPoint.Execute = s =>
        {
            if (checknulls())
                Form(s.First().Case);

            foreach (var nw in s.ToList())
                SetProps(nw, "Attach and Detach");
        };
        return entryPoint;
    }

    #endregion

    #region Custom Design Actions
	[CustomDesignAction]
	internal static CustomDesignAction NSA()
	{		

		Color c = attachcol;
        var customDesignAction = new CustomDesignAction("01a Draw Attach", "", "Haulage", Array.Empty<Keys>());
		
		PrepareCda(customDesignAction,"Attach",c);
		
		return customDesignAction;
    }
	
	[CustomDesignAction]
	internal static CustomDesignAction NSD()
	{		
		int currenthandlegrade = 0;
        int currenthandleline = 0;
		int currentoverallshape = 0;
		Color c = detachcol;
        var customDesignAction = new CustomDesignAction("02a Draw Detach", "", "Haulage", Array.Empty<Keys>());

		PrepareCda(customDesignAction,"Detach",c);
		
		return customDesignAction;
    }
	
	[CustomDesignAction]
	internal static CustomDesignAction NSB()
	{		
		int currenthandlegrade = 0;
        int currenthandleline = 0;
		int currentoverallshape = 0;
		Color c = bothcol;
        var customDesignAction = new CustomDesignAction("03a Draw Attach and Detach", "", "Haulage", Array.Empty<Keys>());
		
		PrepareCda(customDesignAction,"Attach and Detach",c);
		
		return customDesignAction;
    }
	
	[CustomDesignAction]
	internal static CustomDesignAction NSDef()
	{		
		int currenthandlegrade = 0;
        int currenthandleline = 0;
		int currentoverallshape = 0;
		Color c = neithercol;
        var customDesignAction = new CustomDesignAction("04a Draw Default", "", "Haulage", Array.Empty<Keys>());
		
		PrepareCda(customDesignAction,"Default",c);
		
		return customDesignAction;
    }
	
    [CustomDesignAction]
    internal static CustomDesignAction NSWA()
    {
        var cda = new CustomDesignAction("01 Set Attach", "", "Haulage", Array.Empty<Keys>());
        cda.SelectionPanelEnabled = true;
        cda.Visible = DesignButtonVisibility.Animation;
        cda.SetSelectMode(SelectMode.SelectSegmentOnShape);
        cda.ProgressSteps = new List<string> { "Click on Network Shapes to Set to Attach, Right click to exit" };

        cda.SetupAction += (s, e) =>
        {
            cda.ApplyButtonEnabled = false;
            cda.ApplyButtonVisible = false;
            if (checknulls()) Form(cda.ActiveCase);
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            foreach (var nw in d) SetProps(nw, "Attach");
            cda.ExecuteAction();
        };

        cda.SelectAction += (s, e) =>
        {
            if (checknulls()) Form(cda.ActiveCase);
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            foreach (var nw in d) SetProps(nw, "Attach");
        };

        cda.ApplyAction += (s, e) => { e.Completed = true; };
        return cda;
    }

    [CustomDesignAction]
    internal static CustomDesignAction NSWD()
    {
        var cda = new CustomDesignAction("02 Set Detach", "", "Haulage", Array.Empty<Keys>());
        cda.SelectionPanelEnabled = true;
        cda.Visible = DesignButtonVisibility.Animation;
        cda.SetSelectMode(SelectMode.SelectSegmentOnShape);
        cda.ProgressSteps = new List<string> { "Click on Network Shapes to Set to Detach, Right click to exit" };

        cda.SetupAction += (s, e) =>
        {
            cda.ApplyButtonEnabled = false;
            cda.ApplyButtonVisible = false;
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Detach");
            cda.ExecuteAction();
        };

        cda.SelectAction += (s, e) =>
        {
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Detach");
        };

        cda.ApplyAction += (s, e) => { e.Completed = true; };
        return cda;
    }

    [CustomDesignAction]
    internal static CustomDesignAction NSWR()
    {
        var cda = new CustomDesignAction("03 Set Default", "", "Haulage", Array.Empty<Keys>());
        cda.SelectionPanelEnabled = true;
        cda.Visible = DesignButtonVisibility.Animation;
        cda.SetSelectMode(SelectMode.SelectSegmentOnShape);
        cda.ProgressSteps = new List<string> { "Click on Network Shapes to Set to Default, Right click to exit" };

        cda.SetupAction += (s, e) =>
        {
            cda.ApplyButtonEnabled = false;
            cda.ApplyButtonVisible = false;
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Default");
            cda.ExecuteAction();
        };

        cda.SelectAction += (s, e) =>
        {
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Default");
        };

        cda.ApplyAction += (s, e) => { e.Completed = true; };
        return cda;
    }

    [CustomDesignAction]
    internal static CustomDesignAction NSWAD()
    {
        var cda = new CustomDesignAction("04 Set Attach and Detach", "", "Haulage", Array.Empty<Keys>());
        cda.SelectionPanelEnabled = true;
        cda.Visible = DesignButtonVisibility.Animation;
        cda.SetSelectMode(SelectMode.SelectSegmentOnShape);
        cda.ProgressSteps = new List<string> { "Click on Network Shapes to Set to Attach and Detach, Right click to exit" };

        cda.SetupAction += (s, e) =>
        {
            cda.ApplyButtonEnabled = false;
            cda.ApplyButtonVisible = false;
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Attach and Detach");
            cda.ExecuteAction();
        };

        cda.SelectAction += (s, e) =>
        {
            var d = cda.Selection.SelectedDesignElements.OfType<NetworkShape>().ToList();
            if (checknulls()) Form(cda.ActiveCase);
            foreach (var nw in d) SetProps(nw, "Attach and Detach");
        };

        cda.ApplyAction += (s, e) => { e.Completed = true; };
        return cda;
    }

    [CustomDesignAction]
    internal static CustomDesignAction SetColours()
    {
        var cda = new CustomDesignAction("05 Change Settings", "", "Haulage", Array.Empty<Keys>());
        cda.SelectionPanelEnabled = true;
        cda.Visible = DesignButtonVisibility.Animation;
        cda.SetupAction += (s, e) =>
        {
            Form(cda.ActiveCase, true);
            cda.ExecuteAction();
        };
        cda.ApplyAction += (s, e) => { e.Completed = true; };
        return cda;
    }

    #endregion

    public static void SetProps(NetworkShape nw, string type)
    {
        switch (type)
        {
            case "Attach":
                nw.Attachable = true;
                nw.Detachable = false;
                nw.SegmentCode = attachsc;
                nw.RgbColour = attachcol;
                break;

            case "Detach":
                nw.Attachable = false;
                nw.Detachable = true;
                nw.SegmentCode = detachsc;
                nw.RgbColour = detachcol;
                break;

            case "Attach and Detach":
                nw.Attachable = true;
                nw.Detachable = true;
                nw.SegmentCode = bothsc;
                nw.RgbColour = bothcol;
                break;

            default:
                nw.Attachable = false;
                nw.Detachable = false;
                nw.SegmentCode = neithersc;
                nw.RgbColour = neithercol;
                break;
        }
        nw.UseColourTable = false;
        nw.LineWidth = LW;
    }

    public static void Form(Case c, bool setup = false)
    {
        var form = OptionsForm.Create("Colour Selection");
        form.Options.AddCaseSelect("Current Case").SetVisible(false);

        var attach = form.Options.AddColourSelect("Attach Colour").SetValue(attachcol).RestoreValue("AColourSelect");
        var attachrd = form.Options.AddComboBox<SegmentCode>("Attach Segment Code");
        attachrd.Items.AddRange(c.SegmentCodes.ToList());
        attachrd.SetValue(attachrd.Items.First());
        attachrd.RestoreValue("rdca", x => x.Name, x => c.SegmentCodes.Get(x));

        var detach = form.Options.AddColourSelect("Detach Colour").SetValue(detachcol).RestoreValue("DColourSelect");
        var detachrd = form.Options.AddComboBox<SegmentCode>("Detach Segment Code");
        detachrd.Items.AddRange(c.SegmentCodes.ToList());
        detachrd.SetValue(detachrd.Items.First());
        detachrd.RestoreValue("rdcd", x => x.Name, x => c.SegmentCodes.Get(x));

        var both = form.Options.AddColourSelect("Attach and Detach Colour").SetValue(bothcol).RestoreValue("both");
        var bothrd = form.Options.AddComboBox<SegmentCode>("Attach and Detach Segment Code");
        bothrd.Items.AddRange(c.SegmentCodes.ToList());
        bothrd.SetValue(detachrd.Items.First());
        bothrd.RestoreValue("bothrd", x => x.Name, x => c.SegmentCodes.Get(x));

        var reg = form.Options.AddColourSelect("Road Colour").SetValue(neithercol).RestoreValue("RColourSelect");
        var regrd = form.Options.AddComboBox<SegmentCode>("Road Segment Code");
        regrd.Items.AddRange(c.SegmentCodes.ToList());
        regrd.SetValue(regrd.Items.First());
        regrd.RestoreValue("rdcr", x => x.Name, x => c.SegmentCodes.Get(x));

        var linewidth = form.Options.AddSpinEdit<float>("Line Width").SetValue(LW).RestoreValue("LW");
        var stopasking = form.Options.AddCheckBox("Stop Asking").RestoreValue(stopaskingrestore).SetVisible(false);

        if (setup) stopasking.Value = false;

        if (!stopasking.Value)
        {
            stopasking.Value = true;
            if (form.ShowDialog() != DialogResult.OK) return;
        }

        attachcol = attach.Value;
        detachcol = detach.Value;
        neithercol = reg.Value;
        bothcol = both.Value;
        attachsc = attachrd.Value;
        detachsc = detachrd.Value;
        neithersc = regrd.Value;
        bothsc = bothrd.Value;
        LW = linewidth.Value;
    }
	
	public static void createshape(CustomDesignAction cda, List<Point3D> points, string method)
	{
				NetworkLayer activeLayer = cda.GetActiveLayer() as NetworkLayer;		
				var newestshape = new NetworkShape(points);			
				SetProps(newestshape,method);		
				cda.AddAndRedoBufferEntry(new haulShapeBuffer(activeLayer,newestshape));
				activeLayer.Shapes.Add(newestshape);
	}
	
	public class haulShapeBuffer : IBufferEntry
{
    private readonly NetworkLayer sourcelayer;
    private readonly NetworkShape sourceshape;

	public haulShapeBuffer(NetworkLayer _sourcelayer, NetworkShape _sourceshape)
    {
        sourcelayer = _sourcelayer;
		sourceshape = _sourceshape;
    }
	
    public void Undo()
    {
		if(sourcelayer != null && sourceshape != null)
    	    sourcelayer.Shapes.Remove(sourceshape);
    }

    public void Redo()
    {
		if(sourcelayer != null && sourceshape != null)
	        sourcelayer.Shapes.Add(sourceshape);
    }

	}
	
	public static void PrepareCda(CustomDesignAction customDesignAction, string method, Color c)
	{
		int currenthandlegrade = 0;
        int currenthandleline = 0;
		int currentoverallshape = 0;
		
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.ProgressSteps = new List<string>(2);
	    customDesignAction.ProgressSteps.Add("Click to start drawing, King (or Queen), or Right click to Exit.");
	    customDesignAction.ProgressSteps.Add("Right Click to Start a New Shape");
		
		customDesignAction.Visible = DesignButtonVisibility.Animation;
		
		var points = new List<Point3D>();

		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectPoint);
			var currentlayer = customDesignAction.GetActiveLayer();
			if(!(currentlayer is NetworkLayer))
			{
				MessageBox.Show("Active Layer is not network Layer");
				customDesignAction.ExecuteAction();
			}

			
		};
		
		customDesignAction.ApplyAction += (s,e) => {
		
			if(points.Count > 0)
			{

					createshape(customDesignAction,points,method);
				
			}
		 	e.Completed = true;
		
		};
		
		customDesignAction.SelectAction += (s,e) =>
		{
			customDesignAction.ProgressStep = 1;
			points.Add(customDesignAction.Selection.SelectedPoint);
			
			if(points.Count > 1)
			{
				customDesignAction.RemoveTemporaryShape(currentoverallshape);
				currentoverallshape = customDesignAction.AddColourTemporaryShape(points.ToArray(), false, c, true, true, true, null);
				
			}
			
		};
				
		customDesignAction.MouseMoveAction += (s,e) => 		{ 	

			if(points.Count > 0)
			{
				var location1 = points.Last();
				var location2 = e.MouseLocation;
				customDesignAction.RemoveTemporaryRenderable(currenthandlegrade);
                customDesignAction.RemoveTemporaryShape(currenthandleline);
                var pointsarray = new Point3D[] { location1, location2};
                var segment = new Segment3D(location1, location2);
                var midpoint = (location1 + location2) / 2;
                var slope = segment.Vector.Slope;
                var gradestring = "";
                if (!double.IsNegativeInfinity(slope))
                    gradestring = slope.ToString("###0.00");

				var maxgrade = customDesignAction.ActiveCase.MaximumProfileGrade;
				var percentagecolour = Color.BlueViolet;
				bool steep = slope >  maxgrade ;
				if(steep)
					percentagecolour = Color.Red;
					
                currenthandlegrade = customDesignAction.AddTemporaryText(gradestring, midpoint.CloneXY(midpoint.Z + 10), percentagecolour, true, false, true, null);
                currenthandleline = customDesignAction.AddColourTemporaryShape(pointsarray, false, steep ? percentagecolour : c, true, false, true, null);
			
				
			}
		};

		customDesignAction.OptionsForm += (s,e) => 
		
		{
			Form(customDesignAction.ActiveCase);
			
		};
		
		customDesignAction.BackAction += (s, e) =>
            {
                if (customDesignAction.ProgressStep == 0)
                {
                    e.Completed = true;
                }
                else
                {
				if(points.Count > 1)
				{
					createshape(customDesignAction,points,method);
					
				}
				
				points.Clear();
				customDesignAction.ProgressStep = 0;
				customDesignAction.ClearTemporaryRenderables();
				customDesignAction.ClearTemporaryShapes();
				}
            };
	}
}
