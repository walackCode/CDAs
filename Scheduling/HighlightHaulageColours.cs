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
    [ContextMenuEntryPoint]
    internal static ContextMenuEntryPoint<NetworkLayer> Highight()
    {
        ContextMenuEntryPoint<NetworkLayer> ret = new ContextMenuEntryPoint<NetworkLayer>();

        ret.Name = x => "Attach/Detach Hightlight";
        ret.Enabled = x => true;
        ret.Visible = x => true;
        ret.Execute = sourceLayer =>
        {

            var attdet = new Color();
            var att = new Color();
            var det = new Color();
            var rem = new Color();
            var ste = new Color();

            #region Create String list of Colours
            var ListofColours = new List<string>();
            var colourslist = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().Select(Color.FromKnownColor).ToList();
            foreach (Color col in colourslist)
            {
                if (col.IsSystemColor)
                {
                    continue;
                }
                else
                {
                    ListofColours.Add(col.Name);
                }
            }
            #endregion


            var TheCase = sourceLayer.Case;

            var maxGrade = TheCase.MaximumProfileGrade;

            var check = new List<string>();

            check.Add("ANDColourSelect");

            check.Add("AColourSelect");

            check.Add("DColourSelect");

            check.Add("SteepColourSelect");

            check.Add("ElseColourSelect");

            var skip = true;
            var Keys = Project.ActiveProject.Scripting.OptionValueCache.Keys;


            foreach (var a in check)
            {
                if (!Keys.Contains(a))
                    skip = false;

            }



            #region Form			
            if (!skip)
            {

                var form = OptionsForm.Create("Colour Selection");

                var attanddet = form.Options.AddComboBox<String>("Attach and Detach Colour").Items.AddRange(ListofColours)
                    .SetValue("Purple")
                .RestoreValue("ANDColourSelect");

                var attach = form.Options.AddComboBox<String>("Attach Colour").Items.AddRange(ListofColours)
                    .SetValue("Magenta")
                .RestoreValue("AColourSelect");

                var detach = form.Options.AddComboBox<String>("Detach Colour").Items.AddRange(ListofColours)
                    .SetValue("Cyan")
                .RestoreValue("DColourSelect");

                var steep = form.Options.AddComboBox<String>("Steep Grade Colour").Items.AddRange(ListofColours)
                .SetValue("Red")
                .RestoreValue("SteepColourSelect");

                var remain = form.Options.AddComboBox<String>("Remaining Colour").Items.AddRange(ListofColours)
                .SetValue("Green")
                .RestoreValue("ElseColourSelect");

                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) // stops the window from closing
                {
                    return;
                }


                attdet = Color.FromName(attanddet.Value);
                att = Color.FromName(attach.Value);
                det = Color.FromName(detach.Value);
                rem = Color.FromName(remain.Value);
                ste = Color.FromName(steep.Value);



            }
            #endregion


            #region no form
            if (skip)
            {
                attdet = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("ANDColourSelect"));
                att = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("AColourSelect"));
                det = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("DColourSelect"));
                rem = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("ElseColourSelect"));
                ste = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("SteepColourSelect"));
            }

            #endregion

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

            var attdet = new Color();
            var att = new Color();
            var det = new Color();
            var rem = new Color();
            var ste = new Color();

            #region Create String list of Colours
            var ListofColours = new List<string>();
            var colourslist = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().Select(Color.FromKnownColor).ToList();
            foreach (Color col in colourslist)
            {
                if (col.IsSystemColor)
                {
                    continue;
                }
                else
                {
                    ListofColours.Add(col.Name);
                }
            }
            #endregion


            var TheCase = sourceFolder.Case;

            var maxGrade = TheCase.MaximumProfileGrade;

            var check = new List<string>();

            check.Add("ANDColourSelect");

            check.Add("AColourSelect");

            check.Add("DColourSelect");

            check.Add("SteepColourSelect");

            check.Add("ElseColourSelect");

            var skip = true;
            var Keys = Project.ActiveProject.Scripting.OptionValueCache.Keys;


            foreach (var a in check)
            {
                if (!Keys.Contains(a))
                    skip = false;

            }



            #region Form			
            if (!skip)
            {

                var form = OptionsForm.Create("Colour Selection");

                var attanddet = form.Options.AddComboBox<String>("Attach and Detach Colour").Items.AddRange(ListofColours)
                    .SetValue("Purple")
                .RestoreValue("ANDColourSelect");

                var attach = form.Options.AddComboBox<String>("Attach Colour").Items.AddRange(ListofColours)
                    .SetValue("Magenta")
                .RestoreValue("AColourSelect");

                var detach = form.Options.AddComboBox<String>("Detach Colour").Items.AddRange(ListofColours)
                    .SetValue("Cyan")
                .RestoreValue("DColourSelect");

                var steep = form.Options.AddComboBox<String>("Steep Grade Colour").Items.AddRange(ListofColours)
                .SetValue("Red")
                .RestoreValue("SteepColourSelect");

                var remain = form.Options.AddComboBox<String>("Remaining Colour").Items.AddRange(ListofColours)
                .SetValue("Green")
                .RestoreValue("ElseColourSelect");

                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) // stops the window from closing
                {
                    return;
                }


                attdet = Color.FromName(attanddet.Value);
                att = Color.FromName(attach.Value);
                det = Color.FromName(detach.Value);
                rem = Color.FromName(remain.Value);
                ste = Color.FromName(steep.Value);



            }
            #endregion


            #region no form
            if (skip)
            {
                attdet = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("ANDColourSelect"));
                att = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("AColourSelect"));
                det = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("DColourSelect"));
                rem = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("ElseColourSelect"));
                ste = Color.FromName(Project.ActiveProject.Scripting.OptionValueCache.GetValue<string>("SteepColourSelect"));
            }

            #endregion
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


        #region Create String list of Colours
        var ListofColours = new List<string>();
        var colourslist = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().Select(Color.FromKnownColor).ToList();
        foreach (Color col in colourslist)
        {
            if (col.IsSystemColor)
            {
                continue;
            }
            else
            {
                ListofColours.Add(col.Name);
            }
        }
        #endregion

        var form = OptionsForm.Create("Colour Selection");

        var attanddet = form.Options.AddComboBox<String>("Attach and Detach Colour").Items.AddRange(ListofColours)
            .SetValue("Purple")
        .RestoreValue("ANDColourSelect");

        var attach = form.Options.AddComboBox<String>("Attach Colour").Items.AddRange(ListofColours)
            .SetValue("Magenta")
        .RestoreValue("AColourSelect");

        var detach = form.Options.AddComboBox<String>("Detach Colour").Items.AddRange(ListofColours)
            .SetValue("Cyan")
        .RestoreValue("DColourSelect");

        var steep = form.Options.AddComboBox<String>("Steep Grade Colour").Items.AddRange(ListofColours)
        .SetValue("Red")
        .RestoreValue("SteepColourSelect");

        var remain = form.Options.AddComboBox<String>("Remaining Colour").Items.AddRange(ListofColours)
        .SetValue("Green")
        .RestoreValue("ElseColourSelect");

        if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) // stops the window from closing
        {
            return;
        }


    }

}

