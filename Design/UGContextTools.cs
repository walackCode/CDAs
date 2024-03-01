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
using PrecisionMining.Spry.Spreadsheets;
using PrecisionMining.Spry.Util.OptionsForm;
#endregion

//Useful Attributes for UG Design
//Create level name attributes
//Create Physicals attributes 
//Create Useful filtering attributes

public partial class SetupUGAttributes
{
	[ContextMenuEntryPoint]
    internal ContextMenuEntryPoint<DesignSelection> UGRightClickTools()
    {
        ContextMenuEntryPoint<DesignSelection> menu = new ContextMenuEntryPoint<DesignSelection>();
        
        menu.Name = x => "Quick Setup UG Attributes";
        menu.Enabled = x => x.Shapes.Count() > 0;
        menu.Visible = x => true;
        menu.Execute = designSelect => 
		{
		
			
			var processAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Levels\Process","", typeof(string));
			var panelAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Levels\Panel","", typeof(string));
			var sequenceAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Levels\Sequence","", typeof(string));
			var roadAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Levels\Road","", typeof(string));
			
			
			var pointAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Point Count","", typeof(double));
			var bearAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Bearing","", typeof(double));

			var lengthAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Length","", typeof(double));
			var widthAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Width","", typeof(double));
			var areaAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Area","", typeof(double));

			var heightAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Height","", typeof(double));
			var volumeAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Volume","", typeof(double));

			var priorityAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Priority","", typeof(string));
			var groupingAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Grouping","", typeof(string));
			var classAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Class","", typeof(string));
			var subClassAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Subclass","", typeof(string));
			var tempStringAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Temp String","", typeof(string));
			var tempDoubleAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Utility\Temp Double","", typeof(double));
			

		};
		return menu;
				
	}
	
	[ContextMenuEntryPoint]
    internal ContextMenuEntryPoint<DesignSelection> WriteFilterAttributes()
    {
        ContextMenuEntryPoint<DesignSelection> menu = new ContextMenuEntryPoint<DesignSelection>();
        
        menu.Name = x => "Write Filter Attributes";
        menu.Enabled = x => x.Shapes.Count() > 0;
        menu.Visible = x => true;
        menu.Execute = designSelect => 
		{
		
			var lengthAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Length","", typeof(double));
			var bearAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Bearing","", typeof(double));
			var pointAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute("Point Count","", typeof(double));

			
			
			if (designSelect.Shapes != null) {
				

					foreach(var shape in designSelect.Shapes)
					{
						
						double length = 0;
						double bearing = 0;
						
						for(int i = 1; i < shape.Count; i++)
						{
							var point1 = shape[i];
							var point2 = shape[i - 1];
							var vector = point1 - point2;
							
							Console.WriteLine("point1 "+point1);
							Console.WriteLine("point2 "+point2);
							
							length += vector.Length;
							bearing += vector.Bearing;
							
							Console.WriteLine("length "+length);
							Console.WriteLine("bearing "+bearing);
						}
						
						shape.AttributeValues[lengthAttribute] = Math.Round(length,2);
						shape.AttributeValues[bearAttribute] = Math.Round(bearing,2);
						shape.AttributeValues[pointAttribute] = shape.Count;
					}
				
			}
		};
		return menu;	
	
	}
	
	    [ContextMenuEntryPoint]
        internal ContextMenuEntryPoint<DesignSelection> SetAreaToAttribute()
        {
            ContextMenuEntryPoint<DesignSelection> menu = new ContextMenuEntryPoint<DesignSelection>();
            
			
			
            menu.Name = x => "Calculate Area of Polygon";
            menu.Enabled = x => x.Shapes.Count() > 0;
            menu.Visible = x => true;
            menu.Execute = designSelect => 
			{

				var areaAttribute = Project.ActiveProject.Design.Attributes.GetOrCreateAttribute(@"Physicals\Area","", typeof(double));
				
				foreach(var shape in designSelect.Shapes){
					
					var polygon = new Polygon2D(shape);
					shape.AttributeValues[areaAttribute] = polygon.Area;
					
				}
					

			};	
			return menu;
			
		}
	
	
}