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

public partial class RangeCollapser
{
	[PrecisionMining.Spry.Scripting.ContextMenuEntryPoint]
    internal static PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<Range> HaulageAcrossStringreminder()
    {
        var ret = new PrecisionMining.Spry.Scripting.ContextMenuEntryPoint<Range>();
        ret.Name = range =>  "Range Collapse to Text";
        ret.Visible = range => true;
		ret.Enabled = range => true;
        ret.Execute = range =>
        {
			var table = range.Table;
			var nodes = table.Nodes.Leaves.Where(x=> range.IsInRange(x)).ToList();
			
			var newrange = table.Ranges.GetOrCreateTextRange(range.FullName + "_collapsed");
			
			newrange.RangeText = NodePathTools.CollapseNodePaths(nodes);
			
		};
        return ret;
    }
}