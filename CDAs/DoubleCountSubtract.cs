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

public partial class DoubleCountRemoval
{
    public static void RunSmartSubtract()
    {
        var form = OptionsForm.Create("Smart Subtract");
		
		var inputLayerSelect = form.Options.AddLayerSelect("Input Layer", false);
		var outputLayerSelect = form.Options.AddLayerSelect("Output Layer", false);
		
		var groupingAttributeSelect = form.Options.AddAttributeSelect("Exclusion Grouping", false);
		var priorityAttributeSelect = form.Options.AddAttributeSelect("Priority", false);
		
		if(form.ShowDialog() != DialogResult.OK)
			return;
		
		var inputLayer = inputLayerSelect.Value;
		var outputLayer = outputLayerSelect.Value;
		var inputShapes = inputLayer.Shapes.ToList();
		if(priorityAttributeSelect.Value != null)
		{
			inputShapes = inputShapes.OrderBy(x => x.AttributeValues[priorityAttributeSelect.Value]).ToList();
		}
		Dictionary<Polygon,int> grouping = null;
		List<Polygon> polygons = null;
		if(groupingAttributeSelect.Value != null)
		{
			int index = 0;
			var groupingId = new Dictionary<object,int>();
			foreach(var shape in inputShapes)
			{
				if (!groupingId.ContainsKey(shape.AttributeValues[groupingAttributeSelect.Value]))
					groupingId[shape.AttributeValues[groupingAttributeSelect.Value]] = index++;
			}
			grouping = inputShapes.ToDictionary(x => new Polygon(x), y => groupingId[y.AttributeValues[groupingAttributeSelect.Value]]);
			polygons = grouping.Keys.ToList();
		}
		else
		{
			polygons = inputShapes.Select(x => new Polygon(x)).ToList();
		}
		var output = SmartSubtract.Subtract(polygons, null, null, grouping);
		outputLayer.Shapes.Clear();
		for(int i = 0; i < inputShapes.Count; i++)
		{
			var shape = inputShapes[i];
			var polygon = polygons[i];
			if(!output.ContainsKey(polygon))
				continue;
			var results = output[polygon];
			foreach(var result in results)
				outputLayer.Shapes.Add(shape.Clone(result));
		}
		
    }
}
