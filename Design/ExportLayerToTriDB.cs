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

public partial class ExportLayerToTriDB
{
	MicromineWireframeWriter micromineWriter;
	bool needToBuildAttributes;
	int count;
	string triExtension = ".tridb";
//	bool hasTriExtension;
	
	public partial class ContextMenus
{
    [ContextMenuEntryPoint]
	internal static ContextMenuEntryPoint<Layer> ExportLayerToTriDB()
	{
		var menuoption = new ContextMenuEntryPoint<Layer>();

		menuoption.Name = x => "Export Layer to TriDB";
		menuoption.Enabled = x => true;
		menuoption.Visible = x => true;
		menuoption.Execute = x => { 
			new ExportLayerToTriDB().ExportData_Instance(x);
		};
		return menuoption;
	}
}
	
    public static void RunExportLayerToTriDB()
    {
    	    new ExportLayerToTriDB().ExportData_Instance();
    }
	
	public void ExportData_Instance(Layer layer = null)
	{
		var of = OptionsForm.Create("Export Layer to TriDB");
		

		var layerSelect = of.Options.AddLayerSelect("Layer", false).Validators.Add((l) => l == null ? "No layer selected" : "").RestoreValue("ExportLayer", x => x.FullName, x => Project.ActiveProject.Design.LayerData.GetLayer(x), true, true);
		var fileSelect = of.Options.AddSaveFile("Export File").RestoreValue("EXPORT_TRIDB_FILENAME").EnsureFileIsWritable().SetValue(".tridb");
		fileSelect.Filter = "Tridb Files|*.tridb";
		
		of.Validators.Add(() => {
			if (string.IsNullOrWhiteSpace(fileSelect.Value)) {
				return "No file specified";
			}
			return null;
		});
		of.Validators.Add(() => {
			if (!fileSelect.Value.ToString().EndsWith(triExtension)) {
				return "Filetype must be .tridb";
			}
			return null;
		});
		of.Validators.Add(() => {
			if (fileSelect.Value.ToString().Length<7) {
				return "Invalid Save File Name - please check that is not empty and that it ends in .tridb";
			}
			return null;
		});		
		of.Validators.Add(() => {
			if (layerSelect.Value == null) {
				return "No layer specified";
			}
			return null;
		});
		
		if (layer != null)
			layerSelect.Value = layer;
		
		if (of.ShowDialog() != System.Windows.Forms.DialogResult.OK) 
			return;


		var inputLayer = layerSelect.Value;
		var fileName = fileSelect.Value;
		DoWork(inputLayer, fileName);
	}

	private void DoWork(Layer inputLayer, string fileName)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		count = 1;
		int totalCount = 0;
		needToBuildAttributes = true;
		
		using (micromineWriter = new MicromineWireframeWriter(fileName))
		{
			micromineWriter.BeginUpdate();
			try
			{
				
				foreach(var tri in inputLayer.Triangulations)
				{
					WriteSolid(tri);
				}
				totalCount = micromineWriter.GetTotalTriangulations();
			}
			catch(Exception e)
			{
				micromineWriter.Rollback();
				throw e;
			}
			micromineWriter.EndUpdate();	
		}
		
		sw.Stop();
		
		Out.WriteLine("writing " +  totalCount + " of " + (count -1) + " took " + sw.ElapsedMilliseconds +" milliseconds " ); 
	}
	
	public void WriteSolid(LayerTriangulation n)
	{
		var triMesh = n.Data;
		
		if (triMesh == null) {
			Console.WriteLine("Error with {0} - no solid or no trianglemesh", n.Handle.ToString());
			return;
		}
		
		
		
		var values = new Dictionary<string, object>();
	
		foreach(var attribute in Project.ActiveProject.Design.Attributes.AllAttributes){

			if(n.AttributeValues[attribute]!=null){
				values[attribute.Name] = n.AttributeValues[attribute];
			}
		}

//			foreach (var kvp in values) {
//			        Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
//			}
		
		
		micromineWriter.Save(count++,n.Handle.ToString(),triMesh, needToBuildAttributes, values);

		
	}


}











//				Console.WriteLine(attribute.Name+" : ");
//				Console.WriteLine(tri.AttributeValues[attribute]);


//			foreach (var kvp in values) {
//			        Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
//			}



//				var values = new Dictionary<string, object>();
//				foreach(var attribute in Project.ActiveProject.Design.Attributes.AllAttributes){
//					values[attribute.FullName] = tri.AttributeValues[attribute];
//				}
