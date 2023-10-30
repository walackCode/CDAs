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

public partial class FieldsFromAttributes
{
	public static void Main()
    {
        var attributes = Project.ActiveProject.Design.Attributes.AllAttributes;
		
		var optionsForm = OptionsForm.Create("Fields from Attributes");
		
		optionsForm.Options.AddTextLabel("This script will create fields to match design attributes in a selected table");
		
		var tableSelect = optionsForm.Options.AddTableSelect("Select Source Table");
                tableSelect.RestoreValue("tablestorage", tt => tableSelect.Value.FullName, Table.TryGet);
                tableSelect.Validators.Add(x => x != null, "Please Select a Table");
		
		​
        if (optionsForm.ShowDialog() == DialogResult.Cancel)
                    return;

		

		var table = tableSelect.Value;
		foreach (var attribute in attributes)
		{
			Console.WriteLine(attribute.FullName);
			table.Schema.CreateField(attribute.FullName,attribute.Description,attribute.DataType);
		}
    }
}