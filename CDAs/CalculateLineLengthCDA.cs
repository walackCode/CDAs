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

public partial class StringLengthCalc
{
	[CustomDesignAction]
    public static CustomDesignAction CreateAction()
    {
        var customDesignAction = new CustomDesignAction("Calculate String Length", "", "Utilities", Array.Empty<Keys>());
        customDesignAction.SelectionPanelEnabled = true;
        customDesignAction.SetupAction += (s,e) =>
        {
            customDesignAction.SetSelectMode(SelectMode.SelectElements);
        };
        customDesignAction.OptionsForm += (s,e) => 
        {
            var form = OptionsForm.Create("test");
            var outputSelection = form.Options.AddAttributeSelect("Output Attribute",false).RestoreValue("CalcStringLengthAttribute",x => x.FullName, x => Project.ActiveProject.Design.Attributes.GetAttribute(x),true,true);
			outputSelection.Validators.Add(x => x != null && x.DataType == typeof(double), "Please select a numeric float attribute.");
            e.OptionsForm = form;
        };
		customDesignAction.SelectionFilter += (s,e) =>
		{
			e.Filtered = e.DesignCollectionElement is IShape;
		};
		
		customDesignAction.ApplyAction += (s,e) =>
		{
			var attribute = customDesignAction.ActionSettings[0].Value as PrecisionMining.Common.Design.Attribute;
			if(attribute == null)
				return;
			foreach(var item in customDesignAction.ActionInputs.Shapes)
			{
				var length = 0d;
				int first = item.Closed ? item.Count - 1 : 0;
				int second = item.Closed ? 0 : 1;
				for(int i = first, j = second; j < item.Count; i = j++)
				{
					var point1 = item[i];
					var point2 = item[j];
					var vector = point1 - point2;
					var segmentLength = vector.Length;
					length += segmentLength;
				}
				item.AttributeValues[attribute] = length;
			}
			e.Completed = true;
		};
		return customDesignAction;
    }
}