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

public partial class CalculateAdvanceDirection
{
	[CustomDesignAction]
	internal static CustomDesignAction Calculate()
	{		
        var customDesignAction = new CustomDesignAction("Calculate Advance Direction", "", "Geometry", Array.Empty<Keys>());
		customDesignAction.SelectionPanelEnabled = true;
		customDesignAction.SetupAction += (s,e) =>
		{
			customDesignAction.SetSelectMode(SelectMode.SelectElements);
		};
		customDesignAction.OptionsForm += (s,e) => 
		{
			var form = OptionsForm.Create("test");
			var directionSelection = form.Options.AddVector3DEdit("Direction");
			var getVecButton = form.Options.AddButtonEdit("Get Vector");
			getVecButton.ClickAction = (o) => {
				customDesignAction.SecondaryActions.GetVectorAction((done,vec) => {
					if(done)
						directionSelection.Value = vec.Normalise();
				});
			};
			e.OptionsForm = form;
		};

		customDesignAction.ApplyAction += (s,e) =>
		{
			var direction = (Vector3D) customDesignAction.ActionSettings[0].Value;
			var selectedSolids = customDesignAction.SelectedSolids.ToList();
			var firstSolid = selectedSolids.First();
			var table = firstSolid.Node.Table;
			List<String> solidNames = new List<String>();
			foreach(var field in table.Schema.AllFields)
			{
				if(field.DataType.ToString() == "PrecisionMining.Common.Design.Data.Solid")
					solidNames.Add(field.FullName);
			}
			foreach(var solidLoc in solidNames)
				Console.WriteLine(solidLoc);
			foreach(var solid in selectedSolids)
			{
				var newSolid = new Solid(solid.Solid.TriangleMesh);
				newSolid.AdvanceDirection = solid.Solid.AdvanceDirection;
				foreach(var solidLocation in solidNames)
				{
					if (solid.Node.Data.Solid[solidLocation] == null)
						continue;
					if(solid.Node.Data.Solid[solidLocation].Equals(newSolid))
					{
						Console.WriteLine("yes");
						newSolid.AdvanceDirection = direction;
						solid.Node.Data.Solid[solidLocation] = newSolid;
					}
				}
			}
		};
		return customDesignAction;
    }
}
