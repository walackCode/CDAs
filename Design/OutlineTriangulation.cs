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

public partial class TriOutlline
{
	[PrecisionMining.Spry.Scripting.ContextMenuEntryPoint]
    internal static PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<Triangulation> TriOutline()
    {
        var ret = new PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<Triangulation>();
        ret.Name = tri =>  "Get Outline";
        ret.Visible = tri => true;
		ret.Enabled = tri => true;
        ret.Execute = tri =>
        {
			var data = tri.Data;
			
			var outline = data.CreateSilhouette(new Vector3D(0,0,1),true);
			
			var layer = Layer.GetOrCreate(tri.FullName);
			foreach(var k in outline)
			layer.Shapes.Add(new Shape(k.ToList()));
			
		};
        return ret;
    }
	
		[PrecisionMining.Spry.Scripting.ContextMenuEntryPoint]
    internal static PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<LayerTriangulation>> TriOutlinelayer()
    {
        var ret = new PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<IList<LayerTriangulation>>();
        ret.Name = tri =>  "Get Outline";
        ret.Visible = tri => true;
		ret.Enabled = tri => true;
        ret.Execute = tri =>
        {

			foreach(var dd in tri)
			{
			var data = dd.Data;
			
			var outline = data.CreateSilhouette(new Vector3D(0,0,1),true);
			var layer = dd.Layer;
			foreach(var k in outline)
			layer.Shapes.Add(new Shape(k.ToList()));
			}
		};
        return ret;
    }
}