#region Using Directives
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrecisionMining.Common.Design.Data;
using PrecisionMining.Common.Math;
using PrecisionMining.Common.Units;
using PrecisionMining.Spry;
using PrecisionMining.Spry.Util;
using PrecisionMining.Spry.Data;
using PrecisionMining.Spry.Design;
using PrecisionMining.Spry.Scenarios;
using PrecisionMining.Spry.Scenarios.Haulage;
using PrecisionMining.Spry.Scenarios.Scheduling;
using PrecisionMining.Spry.Scripting;
using PrecisionMining.Spry.Util.OptionsForm;
using System.Threading.Tasks;
using pm = PrecisionMining.Common.Design;
using System.Windows.Forms;
#endregion


public partial class NetworkHighlighter
{
	public static string stopaskingrestore = "StopaskingNWHL";
	
	
    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<NetworkLayer> Highight()
    {
        ContextMenuEntryPoint<NetworkLayer> ret = new ContextMenuEntryPoint<NetworkLayer>();

        ret.Name = x => "Attach/Detach Hightlight";
        ret.Enabled = x => true;
        ret.Visible = x => true;
        ret.Execute = sourceLayer =>
        {

            var TheCase = sourceLayer.Case;

            var maxGrade = TheCase.MaximumProfileGrade;

			var colours = Form(Project.ActiveProject.Scripting.OptionValueCache.Contains(stopaskingrestore)).ToArray();
			var attdet = colours[0];
            var att = colours[1];
            var det = colours[2];
			var ste = colours[3];
            var rem = colours[4];

            #region
            foreach (NetworkShape s in sourceLayer.Shapes.ToList())
            {


                if (s.Detachable && s.Attachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = attdet;
                    s.LineWidth = 15;
                    s.PointSize = 10;

                }
                else if (s.Attachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = att;
                    s.LineWidth = 15;
                    s.PointSize = 10;
                }
                else if (s.Detachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = det;
                    s.LineWidth = 15;
                    s.PointSize = 10;
                }
                else
                {
                    s.UseColourTable = false;
                    s.RgbColour = rem;
                    s.LineWidth = 5;
                    s.PointSize = 30;
                }
                var k = s.ToArray();

                for (int i = 0; i < s.Count - 1; i++)
                {
                    var seg = new Segment3D(k[i], k[i + 1]);

                    if (seg.Vector.Length != 0)
                    {
                        if (seg.Vector.Grade > maxGrade || seg.Vector.Grade < -maxGrade)
                        {
                            s.UseColourTable = false;
                            s.RgbColour = ste;
                        }

                    }
                }

            }
            #endregion




        };
        return ret;
    }

    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<NetworkLayerFolder> HighightFolder()
    {
        ContextMenuEntryPoint<NetworkLayerFolder> ret = new ContextMenuEntryPoint<NetworkLayerFolder>();

        ret.Name = x => "Attach/Detach Hightlight";
        ret.Enabled = x => true;
        ret.Visible = x => true;
        ret.Execute = sourceFolder =>
        {



            var TheCase = sourceFolder.Case;

            var maxGrade = TheCase.MaximumProfileGrade;
			
			var colours = Form(Project.ActiveProject.Scripting.OptionValueCache.Contains(stopaskingrestore)).ToArray();	
			var attdet = colours[0];
            var att = colours[1];
            var det = colours[2];
			var ste = colours[3];
            var rem = colours[4];
			

			foreach( NetworkLayer sourceLayer in sourceFolder.NetworkLayers.AllNetworkLayers.ToList()) {
            foreach (NetworkShape s in sourceLayer.Shapes.ToList())
            {

                if (s.Detachable && s.Attachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = attdet;
                    s.LineWidth = 15;
                    s.PointSize = 10;

                }
                else if (s.Attachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = att;
                    s.LineWidth = 15;
                    s.PointSize = 10;
                }
                else if (s.Detachable)
                {
                    s.UseColourTable = false;
                    s.RgbColour = det;
                    s.LineWidth = 15;
                    s.PointSize = 10;
                }
                else
                {
                    s.UseColourTable = false;
                    s.RgbColour = rem;
                    s.LineWidth = 5;
                    s.PointSize = 30;
                }
                var k = s.ToArray();

                for (int i = 0; i < s.Count - 1; i++)
                {
                    var seg = new Segment3D(k[i], k[i + 1]);

                    if (seg.Vector.Length != 0)
                    {
                        if (seg.Vector.Grade > maxGrade || seg.Vector.Grade < -maxGrade)
                        {
                            s.UseColourTable = false;
                            s.RgbColour = ste;
                        }

                    }
                }

            }
			}



		
        };
        return ret;
    }

    public static void ColourSelection()
    {
		
		Form(false);


    }
	
	
	public static List<Color> Form(bool restore){
	
	   var form = OptionsForm.Create("Colour Selection");

                var attanddet = form.Options.AddColourSelect("Attach and Detach Colour").SetValue(Color.Purple)
                .RestoreValue("ANDColourSelect");

                var attach = form.Options.AddColourSelect("Attach Colour")
                    .SetValue(Color.Magenta)
			        .RestoreValue("AColourSelect");

		
                var detach = form.Options.AddColourSelect("Detach Colour").SetValue(Color.Cyan)
                .RestoreValue("DColourSelect");

                var steep = form.Options.AddColourSelect("Steep Grade Colour").SetValue(Color.Red)
                .RestoreValue("SteepColourSelect");

                var remain = form.Options.AddColourSelect("Remaining Colour").SetValue(Color.Green)
                .RestoreValue("ElseColourSelect");
		
				var stopasking = form.Options.AddCheckBox("Stop Asking").RestoreValue(stopaskingrestore).SetValue(restore).SetVisible(false);
			
			if(!stopasking.Value)
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) // stops the window from closing
                {
                    return null;
                }
	

			return new List<Color>{attanddet.Value,attach.Value,detach.Value,steep.Value,remain.Value};
				
				
	}
}
