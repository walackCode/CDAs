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
using System.Text.RegularExpressions;
#endregion

public class Path_Scripts
{
	///<summary>
	///Takes any equipment with the comment "Clones:EquipmentName" and adds a region of the equipment path cloned, preserving any other text. Clones can only have one master.
	///</summary>
	/// 
	public static void EngineSetup(SchedulingEngine engine)
	{
		Case c = engine.Case;
		MasterClonePath(c);
	}
	
    public static void MasterClonePath(Case c)
    {
		bool debug = false;
		string cloneParseText = "'Clones:";
		if (debug)
			Out.Clear();
        foreach (var equipment in c.Equipment.AllEquipment)
		{
			if (equipment.SourcePath.Contains(cloneParseText))
			{
				//split off header (before clones text), clone line, and lines under the clone comment
				string[] headerSplit = equipment.SourcePath.Split(new string[] {cloneParseText}, 2, StringSplitOptions.None);
				string header = headerSplit[0] + cloneParseText; //retain this to rebuild because my regex isn't great
				string[] body = headerSplit[1].Split(new string[] {Environment.NewLine}, 2, StringSplitOptions.None);
				string masterEquipmentName = body[0].Trim(); //read the name from the front half of the split
				string remainingPath = body.Length == 1 ? "" : body[1]; //what remains of the path underneath Clones: from the cloning equipment. If there's nothing else it starts out empty
				if (debug)
				{
					Out.WriteLine(header);
					Out.WriteLine(masterEquipmentName);
					Out.WriteLine(remainingPath);
				}
				//try and get the equipment based on full name, normal name if that fails
				Equipment master;
				master = c.Equipment.AllEquipment.FirstOrDefault(x=> x.FullName == masterEquipmentName);
				if (master == null)
					master = c.Equipment.AllEquipment.FirstOrDefault(x=> x.Name == masterEquipmentName);
				if (master == null)
					continue; //failed to find the master
				if (master == equipment)
					continue; //cant copy yourself 
				
				string masterPath = master.SourcePath; //grabs the entire path from the master
				string regionFullName = "Cloned from Master Equipment: " + master.FullName; //if the user didn't use the full name, this will replace it
				string regionName = "Cloned from Master Equipment: " + masterEquipmentName; //a secondary regex lookup if the user used the regular name and not the full name
				
				Regex fullNameRegex = new Regex(@"\#region " + regionFullName.Replace(@"\",@"\\") + @"([^⌐]+)\#endregion Clone"); //primary regex (uses master equipment full name, safer)
				Regex nameRegex = new Regex(@"\#region " + regionName.Replace(@"\",@"\\") + @"([^⌐]+)\#endregion Clone");//backup regex (if user only uses first name)
				StringBuilder clonedRegion = new StringBuilder();
				clonedRegion.AppendLine("#region " + regionFullName);
				clonedRegion.AppendLine(masterPath);
				clonedRegion.Append("#endregion Clone");
				
				string result = header + master.FullName + Environment.NewLine; //readds
				if (fullNameRegex.IsMatch(remainingPath)) //full name found, use that
				{
					result += fullNameRegex.Replace(remainingPath, clonedRegion.ToString());
				}
				else if (nameRegex.IsMatch(remainingPath)) //full name not found but name found. rename to full name and replace region
				{
					result += nameRegex.Replace(remainingPath, clonedRegion.ToString());
				}
				else //no name found. add region to bottom of path
				{
					result += remainingPath + Environment.NewLine + clonedRegion.ToString();
				}
				equipment.SourcePath = result;
			}
		}
    }
}
