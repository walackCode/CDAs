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
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.Util.OptionsForm;
using System.Diagnostics;
using System.Windows.Forms;
using PrecisionMining.Spry.Scripting;
#endregion

public partial class Renamr
{
    public static void RangeRenamr()
    {
       var form = OptionsForm.Create("Rename Nodes");
		
		var table = form.Options.AddTableSelect("Table").RestoreValue("RENAMR_Table_", (t) => t.FullName, (s) => Project.ActiveProject.Data.Tables.Get(s), true, true);
		
		var range = form.Options.AddRangeSelect("Range").SetTable(table.Value).RestoreValue("RENAMR_Range_", (f) => f.FullName, (s) =>
            {
                if (table.Value != null)
                    return table.Value.Ranges.GetRange(s);
                return null;
            }, true, true);;
		
		var level = form.Options.AddLevelSelect("Level",false);
			level.SetValue(null);
			level.RestoreValue("RENAMR_Level_", (l) => l.Name, (s) =>
        {
            if (table.Value == null)
                return null;
            return table.Value.Levels.Get(s);
        }, true, true);
		
		var delete = form.Options.AddCheckBox("Delete Original Node").RestoreValue("RENAMR_Delete_");
		
		var newlevelname = form.Options.AddTextEdit("New Position Name").RestoreValue("RENAMR_Name_");
		
		        table.ValueChanged += (s, e) =>
        {
            range.SetTable(table.Value);
            level.SetTable(table.Value);
        };
		
		
		
		table.Validators.Add(x => x != null, "Select a Table");
		range.Validators.Add(x => x != null, "Select a Range");
		level.Validators.Add(x => x != null, "Select a Level");
		newlevelname.Validators.Add(x => !string.IsNullOrEmpty(x), "enter a new level name");
		
		
		
		if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK) // stops the window from closing
        {
            return;
        }
		CreateNodes(table.Value, range.Value, newlevelname.Value, level.Value, delete.Value);	
    }
	
	
	public static void CreateNodes(Table t, Range r, string newPosition, Level l, bool delete) {
		List<Node> leaves = t.Nodes.Leaves.Where(x => r.IsInRange(x)).Where(x => x.IsLeaf).ToList();
		var startLevels = t.Levels.Count;
		
		if(l.Positions.Contains(newPosition) == false){
			l.Positions.Add(newPosition);
		}

		int overall = 0;	
		foreach (Node n in leaves) {
			
			var names = n.FullName.Split('\\');
			names[l.Index] = newPosition;
			var newnode = string.Join("\\",names);
			var count = 0;
			while(t.Nodes.Get(newnode) != null)
			{
				count++;
				overall++;
				string updatenewposition = newPosition + " Renamr Overlap " + count.ToString();
			
			if(l.Positions.Contains(updatenewposition) == false){
				l.Positions.Add(updatenewposition);
				}
				
				
				names = n.FullName.Split('\\');
				names[l.Index] = updatenewposition;
				newnode = string.Join("\\",names);
			}
			
			
			var b = t.Nodes.Create(newnode);
			
			foreach(var field in t.Schema.AllFields.Where(x => !x.IsCalculatedField)) 
				{
				b.Data[field] = n.Data[field];
				}
			
			
		}
		if(overall > 0)
		{
			MessageBox.Show("There would have been some overlap, check to find where! \n this was done to not delete data!");
			
		}
		
		
		if(delete) {
			foreach (Node n in leaves)
			{
			t.Nodes.Remove(n.FullName);
			}
		}
	}

}