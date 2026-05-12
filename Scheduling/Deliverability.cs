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
using System.Windows.Forms;
#endregion

public partial class EquipmentMovement
{
	static void WriteMovementToExcel(Case _case, ReportingLevel reportingLvl, List<Equipment> eqs)
    {
		var worksheet = Project.ActiveProject.Documents.GetOrCreateSpreadsheetDocument("Equipment Movement").Spreadsheet.Worksheets.First();
		worksheet.ClearContents();
		worksheet[0,0].TextValue = "Equipment";
		worksheet[0,1].TextValue = "Reporting Period";
		worksheet[0,2].TextValue = "Total Distance";
		var groupedSteps = _case.Schedule.Where(x=>x.Process.Productive && eqs.Contains(x.Equipment)).GroupBy(x=>x.Equipment);
		var rowCount = 1;
		foreach(var eqGroup in groupedSteps)
		{
			var prevCentroid = new Point3D(0,0,0);
			var groupByReportingPeriod = eqGroup.GroupBy(x=> x.GetReportingPeriod(reportingLvl.Name));
			foreach(var grp in groupByReportingPeriod)
			{
				var totalDistance = 0.0;
				foreach(var step in grp)
				{
					var currentCentroid = step.Source.Data.Point3D[step.Process.CentroidField].CloneXY(0);
					var movement = 0.0;
					if (currentCentroid != new Point3D(0,0,0) && prevCentroid != new Point3D(0,0,0))
						movement = currentCentroid.Subtract(prevCentroid).Length;
					
					totalDistance += movement;
					prevCentroid = currentCentroid;
				}
				worksheet[rowCount,0].TextValue = grp.First().Equipment.Name;
				worksheet[rowCount,1].TextValue = grp.First().GetReportingPeriod(reportingLvl.Name).Name;
				worksheet[rowCount,2].DoubleValue = totalDistance;
				rowCount++;
			}
		}
	}
	
	public static void WriteEquipmentMovement()
	{
		var form = OptionsForm.Create("Equipment Movement Options");

        var caseSelector = form.Options.AddCaseSelect("Select Case")
                            .Validators.Add(x => x != null, "Please select case")
                            .RestoreValue("EQUIPMENT-MOVEMENT-CASE", _case => _case.FullName, str => Case.TryGet(str));
		
        var reportingPeriodSelector = form.Options.AddReportingLevelSelect("Reporting Level")
                            .SetCase(caseSelector.Value)
                            .RestoreValue("EQUIPMENT-MOVEMENT-REPORTINGLEVEL", level => level.Name, str => caseSelector.Value.ReportingLevels.Get(str))
                            .Validators.Add(x => x != null, "Please select a reporting level.");
		
		var equipmentSelector = form.Options.AddEquipmentSelect("Equipment",true);
		equipmentSelector.SetCase(caseSelector.Value);
		
		equipmentSelector.RestoreValues("EQUIPMENT-MOVEMENT-EQUIPMENT", 
							eqp => equipmentSelector.IncludeAll ? "< All >" : string.Join(",", equipmentSelector.Equipment.Select(x => x.FullName)),
							str =>
							{
								var @case = caseSelector.Value;
				                if (@case == null || str == null)
				                    return Enumerable.Empty<Equipment>();
								if (str.Equals("< All >"))
								{
									equipmentSelector.IncludeAll = true;
									return Enumerable.Empty<Equipment>();
								}
								else
									return str.Split(',').Select(@case.Equipment.GetEquipment);
							}, true, true);			
		
		caseSelector.ValueChanged += (s, e) =>
        {
            if (caseSelector.Value == null)
            {
                reportingPeriodSelector.SetCase(null);
				equipmentSelector.SetCase(null);
                return;
            }
			equipmentSelector.SetCase(caseSelector.Value);
            reportingPeriodSelector.SetCase(caseSelector.Value);
            var reportingLevel = reportingPeriodSelector.Value;
            if (reportingLevel != null)
                reportingPeriodSelector.Value = caseSelector.Value.ReportingLevels.Get(reportingLevel.Name);
        };
		
		if (form.ShowDialog() != DialogResult.OK)
            return;
		
		var selectedCase = caseSelector.Value;
        var selectedReportingLevel = reportingPeriodSelector.Value;
		var selectedEquipment = equipmentSelector.Values.ToList();

        WriteMovementToExcel(selectedCase, selectedReportingLevel, selectedEquipment);
		
	}	
}

public partial class SinkRate
{

    #region Sink RateSet Up

	static List<ScheduleStep> m_filtered_case = new List<ScheduleStep>();
	static ILookup<Node,ScheduleStep> m_lookup = null;

    #endregion

    #region Sink Rate

    class StepData
    {
        public double roofZ, floorZ, pctCompleted;

        public StepData(double roofZ, double floorZ, double pctCompleted)
        {
            this.roofZ = roofZ;
            this.floorZ = floorZ;
            this.pctCompleted = pctCompleted;
        }
    }

    class Centroids
    {
        public Point3D roof, floor;

        public Centroids(Point3D roof, Point3D floor)
        {
            if (roof == null)
                throw new ArgumentNullException("roof");
            if (floor == null)
                throw new ArgumentNullException("floor");

            this.roof = roof;
            this.floor = floor;
        }
    }
	
	class CentroidFields
	{
		public Field wasteRoof, wasteFloor, coalRoof, coalFloor, dumpRoof, dumpFloor;
		
		public CentroidFields(Field wasteRoof, Field wasteFloor, Field coalRoof, Field coalFloor, Field dumpRoof, Field dumpFloor)
		{
			this.wasteRoof = wasteRoof;
			this.wasteFloor = wasteFloor;
			this.coalRoof = coalRoof;
			this.coalFloor = coalFloor;
			this.dumpRoof = dumpRoof;
			this.dumpFloor = dumpFloor;
		}
	}
	
	class sinkRatePercentageCompleted
	{
		public double sinkRate {get; set;}
		public double percentageCompleted {get;set;}
	}
	
    Dictionary<Node, StepData> m_groupingData = new Dictionary<Node, StepData>();
    Dictionary<Node, List<sinkRatePercentageCompleted>> m_sinkRates = new Dictionary<Node, List<sinkRatePercentageCompleted>>();
	
    void ApplyPeriodsProgression(IEnumerable<ScheduleStep> steps, Func<ScheduleStep, Node> nodeGetr, Func<ScheduleStep, double> completionFn, Func<Node, Centroids> getCentroid)
    {
        var stepsList = steps.ToList();

        var nodeSteps = stepsList.GroupBy(nodeGetr);
        foreach (var stepGrp in nodeSteps)
        {
            Node node = stepGrp.Key;

            var centroids = getCentroid(node);

            if (centroids == null)
                continue;
			double numberOfProcessesForNode = m_lookup[node].Select(x=> x.Process).Distinct().Count();
            double totalPercentageCompleted = stepGrp.Select(completionFn).Sum();
			double percentageCompleted = totalPercentageCompleted/numberOfProcessesForNode;

            StepData data;
            if (m_groupingData.TryGetValue(node, out data))
            {
                data.pctCompleted += percentageCompleted;
            }
            else
            {
                data = new StepData(centroids.roof.Z, centroids.floor.Z, percentageCompleted);
                m_groupingData.Add(node, data);
            }

            data.pctCompleted = Math.Min(1.0, data.pctCompleted);
            data.roofZ = Math.Max(data.roofZ, centroids.roof.Z);
            data.floorZ = Math.Max(data.floorZ, centroids.floor.Z);
        }
    }

    void AddSinkRate(IEnumerable<ScheduleStep> steps, Func<ScheduleStep, Node> nodeGetr, Func<Node, string> grping)
    {
        var blkgrp = steps.Select(nodeGetr).GroupBy(grping);

        foreach (var grp in blkgrp)
        {
            double totalThickness = 0.0;
			
			var hash = grp.ToHashSet();

            foreach (var node in hash)
            {
                StepData data;
				
                if (!m_groupingData.TryGetValue(node, out data))
                    continue;
				
				totalThickness += (data.roofZ - data.floorZ) * data.pctCompleted;
            }

            foreach (var node in hash)
            {
                List<sinkRatePercentageCompleted> sinkRates;
                if (!m_sinkRates.TryGetValue(node, out sinkRates))
                {
                    sinkRates = new List<sinkRatePercentageCompleted>();
                    m_sinkRates.Add(node, sinkRates);
                }
				sinkRatePercentageCompleted toAdd = new sinkRatePercentageCompleted();
				toAdd.sinkRate=totalThickness;
				StepData data;
				if(!m_groupingData.TryGetValue(node, out data))
					toAdd.percentageCompleted = 0.0;
				toAdd.percentageCompleted = data.pctCompleted;
                sinkRates.Add(toAdd);
            }
        }
    }

    void WriteSinkRates(string rateField)
    {
        if (m_sinkRates.Count == 0)
            return;

        Table table = m_sinkRates.First().Key.Table;
        foreach (var node in table.Nodes.Leaves)
            node.Data[rateField] = 0.0;

        foreach (var kvp in m_sinkRates)
			kvp.Key.Data[rateField] = kvp.Value.Where(x=> x.percentageCompleted == kvp.Value.Max(y=> y.percentageCompleted)).ToList().First().sinkRate;

    }

    static void WriteSinkRateToTable(Case _case, List<Process> processes, List<Level> sourceLevels, List<Level> destinationLevels, ReportingLevel reportingLvl, bool sourceSinkCheckBox, Field sinkRateField, bool dumpFillCheckBox, Field fillRateField, CentroidFields centroidFields)
    {
        if (_case == null)
            throw new ArgumentNullException("_case", "Need a valid case");
        if (sourceSinkCheckBox && (sourceLevels == null || sourceLevels.Count == 0))
            throw new ArgumentNullException("sourceLevels", "Need more than zero source levels");
        if (dumpFillCheckBox && (destinationLevels == null || destinationLevels.Count == 0))
            throw new ArgumentNullException("destinationLevels", "Need more than zero destination levels");
        if (reportingLvl == null)
            throw new ArgumentNullException("reportingLvl", "Need a reporting level specified");

        var stepsByPeriod = reportingLvl.ScheduleStepsByReportingPeriod();
		m_lookup = _case.Schedule.Where(x=> processes.Contains(x.Process)).ToLookup(x => x.Source);

        if (sourceSinkCheckBox)
        {
            var instance = new SinkRate();

            Func<ScheduleStep, Node> nodeGetr = s => s.Source;
            foreach (var kvp in stepsByPeriod.OrderBy(x => x.Key))
            {
                var filteredSteps = kvp.Value.Where(x => x.Source != null).Where(x => processes.Contains(x.Process)).ToList();

                instance.ApplyPeriodsProgression(filteredSteps, nodeGetr, s => s.SourcePercentageCompleted, n =>
                {
                    var roofCentroid = n.Data.Point3D[centroidFields.wasteRoof] ?? n.Data.Point3D[centroidFields.coalRoof];
                    var floorCentroid = n.Data.Point3D[centroidFields.coalFloor] ?? n.Data.Point3D[centroidFields.wasteFloor];
                    if (roofCentroid == null || floorCentroid == null)
                        return null;
                    else
                        return new Centroids(roofCentroid, floorCentroid);
                });

                var grpingLevels = sourceLevels.Select(x => x.Name).ToArray();
                instance.AddSinkRate(filteredSteps, nodeGetr, n => string.Intern(n.GetGroupingString(grpingLevels)));
            }

            instance.WriteSinkRates(sinkRateField.FullName);
        }

        if (dumpFillCheckBox)
        {
            var instance = new SinkRate();

            Func<ScheduleStep, Node> nodeGetr = s => s.Destination;
            foreach (var kvp in stepsByPeriod.OrderBy(x => x.Key))
            {
                var filteredSteps = kvp.Value.Where(x => x.Destination != null).ToList();
                instance.ApplyPeriodsProgression(filteredSteps, nodeGetr, d => d.DestinationPercentageFilled, n =>
                {
                    var roofCentroid = n.Data.Point3D[centroidFields.dumpRoof];
                    var floorCentroid = n.Data.Point3D[centroidFields.dumpFloor];
                    if (roofCentroid == null || floorCentroid == null)
                        return null;
                    else
                        return new Centroids(roofCentroid, floorCentroid);
                });

                var grpingLevels = destinationLevels.Select(x => x.Name).ToArray();
                instance.AddSinkRate(filteredSteps, nodeGetr, n => string.Intern(n.GetGroupingString(grpingLevels)));
            }

            instance.WriteSinkRates(fillRateField.FullName);
        }
    }

    public static void WriteSinkRateToTable()
    {
        var form = OptionsForm.Create("Sink Rate Options");

        var caseSelector = form.Options.AddCaseSelect("Select Case")
                            .Validators.Add(x => x != null, "Please select case")
                            .RestoreValue("SINK-RATE-CASE", _case => _case.FullName, str => Case.TryGet(str));

        var processesSelectOption = form.Options.AddProcessSelect("Processes", true, true, false);
        processesSelectOption.SetCase(caseSelector.Value);
        processesSelectOption.RestoreValues("SINK-RATE-PROCESSES",
            x => processesSelectOption.IncludeAll ? "< All >" : string.Join(",", processesSelectOption.Processes.Select(process => process.Name)),
            x =>
            {
                var @case = caseSelector.Value;
                if (@case == null || x == null)
                    return null;

                if (x.Equals("< All >"))
                {
                    processesSelectOption.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(process => @case.Processes.Get(process)).Where(process => process != null);
            });

        var reportingPeriodSelector = form.Options.AddReportingLevelSelect("Reporting Level")
                                    .SetCase(caseSelector.Value)
                                    .RestoreValue("SINK-RATE-REPORTINGLEVEL", level => level.Name, str => caseSelector.Value.ReportingLevels.Get(str))
                                    .Validators.Add(x => x != null, "Please select a reporting level.");

        var sourceSinkRateOption = form.Options.AddCheckBox("Include Sink Rate Analysis").SetChecked(false);
        sourceSinkRateOption.RestoreValue("SINK-RATE-INCLUDESOURCE");

        var sourceTableCase = caseSelector.Value == null ? null : caseSelector.Value.SourceTable;

        var sourceLevelSelector = form.Options.AddLevelSelect("Source Levels", true).SetVisible(sourceSinkRateOption.Value);
        sourceLevelSelector.SetTable(sourceTableCase);
        sourceLevelSelector.RestoreValues("SINK-RATE-SOURCELEVELS",
            x => sourceLevelSelector.IncludeAll ? "<All>" : string.Join(",", sourceLevelSelector.Levels.Select(level => level.Name)),
            x =>
            {
                if (x.Equals("<All>"))
                {
                    sourceLevelSelector.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(level => caseSelector.Value.SourceTable.Levels.Get(level)).Where(level => level != null);
            });
		
		var sourceWasteRoofCentroid = form.Options.AddFieldSelect("Source Waste Roof Centroid").SetVisible(sourceSinkRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		sourceWasteRoofCentroid.SetTable(sourceTableCase);
		sourceWasteRoofCentroid.RestoreValue("SINK-RATE-SOURCE-WASTE-ROOF-CENTROID", f => f.FullName, s => caseSelector.Value.SourceTable.Schema.GetFieldOrThrow(s));
		
		var sourceWasteFloorCentroid = form.Options.AddFieldSelect("Source Waste Floor Select").SetVisible(sourceSinkRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		sourceWasteFloorCentroid.SetTable(sourceTableCase);
		sourceWasteFloorCentroid.RestoreValue("SINK-RATE-SOURCE-WASTE-FLOOR-CENTROID", f => f.FullName, s => caseSelector.Value.SourceTable.Schema.GetFieldOrThrow(s));
		
		var sourceCoalRoofCentroid = form.Options.AddFieldSelect("Source Coal Roof Centroid").SetVisible(sourceSinkRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		sourceCoalRoofCentroid.SetTable(sourceTableCase);
		sourceCoalRoofCentroid.RestoreValue("SINK-RATE-SOURCE-COAL-ROOF-CENTROID", f => f.FullName, s => caseSelector.Value.SourceTable.Schema.GetFieldOrThrow(s));
		
		var sourceCoalFloorCentroid = form.Options.AddFieldSelect("Source Coal Floor Select").SetVisible(sourceSinkRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		sourceCoalFloorCentroid.SetTable(sourceTableCase);
		sourceCoalFloorCentroid.RestoreValue("SINK-RATE-SOURCE-COAL-FLOOR-CENTROID", f => f.FullName, s => caseSelector.Value.SourceTable.Schema.GetFieldOrThrow(s));
		
		var sinkRateFieldSelector = form.Options.AddFieldSelect("Sink Rate Field").SetVisible(sourceSinkRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Double");
		sinkRateFieldSelector.SetTable(sourceTableCase);
		sinkRateFieldSelector.RestoreValue("SINK-RATE-FIELD", f => f.FullName, s => caseSelector.Value.SourceTable.Schema.GetFieldOrThrow(s));
		
        var dumpFillRateOption = form.Options.AddCheckBox("Include Fill Rate Analysis").SetChecked(false);
        dumpFillRateOption.RestoreValue("SINK-RATE-INCLUDEDUMP");

        var destinationTableCase = caseSelector.Value == null ? null : caseSelector.Value.DestinationTable;

        var destinationLevelSelector = form.Options.AddLevelSelect("Destination Levels", true).SetVisible(dumpFillRateOption.Value);
        destinationLevelSelector.SetTable(destinationTableCase);
        destinationLevelSelector.RestoreValues("SINK-RATE-DESTINATIONLEVELS",
            x => destinationLevelSelector.IncludeAll ? "< All >" : string.Join(",", destinationLevelSelector.Levels.Select(level => level.Name)),
            x =>
            {
                if (x.Equals("< All >"))
                {
                    destinationLevelSelector.IncludeAll = true;
                    return null;
                }
                else
                    return x.Split(',').Select(level => caseSelector.Value.DestinationTable.Levels.Get(level)).Where(level => level != null);
            });
		
		var destRoofCentroid = form.Options.AddFieldSelect("Destination Roof Centroid").SetVisible(dumpFillRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		destRoofCentroid.SetTable(destinationTableCase);
		destRoofCentroid.RestoreValue("SINK-RATE-DEST-ROOF-CENTROID", f => f.FullName, s => caseSelector.Value.DestinationTable.Schema.GetFieldOrThrow(s));
		
		var destFloorCentroid = form.Options.AddFieldSelect("Destination Floor Centroid").SetVisible(dumpFillRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Point3D");
		destFloorCentroid.SetTable(destinationTableCase);
		destFloorCentroid.RestoreValue("SINK-RATE-DEST-FLOOR-CENTROID", f => f.FullName, s => caseSelector.Value.DestinationTable.Schema.GetFieldOrThrow(s));
		
		var fillRateFieldSelector = form.Options.AddFieldSelect("Fill Rate Field").SetVisible(dumpFillRateOption.Value).SetDisplayFilter(f => f.DataType.Name == "Double");
		fillRateFieldSelector.SetTable(destinationTableCase);
		fillRateFieldSelector.RestoreValue("FILL-RATE-FIELD", f => f.FullName, s => caseSelector.Value.DestinationTable.Schema.GetFieldOrThrow(s));
		
        caseSelector.ValueChanged += (s, e) =>
        {
            if (caseSelector.Value == null)
            {
                sourceLevelSelector.SetTable(null);
                destinationLevelSelector.SetTable(null);
                processesSelectOption.SetCase(null);
                reportingPeriodSelector.SetCase(null);
                sourceLevelSelector.Levels = new List<Level>();
                destinationLevelSelector.Levels = new List<Level>();
                processesSelectOption.Processes = new List<Process>();
                reportingPeriodSelector.Value = null;
				sourceWasteRoofCentroid.Value = null;
				sourceWasteFloorCentroid.Value = null;
				sourceCoalRoofCentroid.Value = null;
				sourceCoalFloorCentroid.Value = null;
				sinkRateFieldSelector.Value = null;
				fillRateFieldSelector.Value = null;
				destRoofCentroid.Value = null;
				destFloorCentroid.Value = null;
                return;
            }

            sourceLevelSelector.SetTable(caseSelector.Value.SourceTable);
            if (caseSelector.Value.SourceTable == null)
                sourceLevelSelector.Levels = new List<Level>();
            else
            {
                var sourceLevelsInNewCase = sourceLevelSelector.Levels.Select(x => caseSelector.Value.SourceTable.Levels.Get(x.Name)).ToList();
                sourceLevelSelector.Levels = !sourceLevelSelector.IncludeAll && sourceLevelsInNewCase.All(x => x != null) ? sourceLevelsInNewCase : new List<Level>();
            }
			
			sourceWasteRoofCentroid.SetTable(caseSelector.Value.SourceTable);
			if(caseSelector.Value.SourceTable == null)
				sourceWasteRoofCentroid.Value = null;
			
			sourceWasteFloorCentroid.SetTable(caseSelector.Value.SourceTable);
			if(caseSelector.Value.SourceTable == null)
				sourceWasteFloorCentroid.Value = null;
			
			sourceCoalRoofCentroid.SetTable(caseSelector.Value.SourceTable);
			if(caseSelector.Value.SourceTable == null)
				sourceCoalRoofCentroid.Value = null;
			
			sourceCoalFloorCentroid.SetTable(caseSelector.Value.SourceTable);
			if(caseSelector.Value.SourceTable == null)
				sourceCoalFloorCentroid.Value = null;
			
			sinkRateFieldSelector.SetTable(caseSelector.Value.SourceTable);
			if(caseSelector.Value.SourceTable == null)
				sinkRateFieldSelector.Value = null;

            destinationLevelSelector.SetTable(caseSelector.Value.DestinationTable);
            if (caseSelector.Value.DestinationTable == null)
                destinationLevelSelector.Levels = new List<Level>();
            else
            {
                var destinationLevelsInNewCase = destinationLevelSelector.Levels.Select(x => caseSelector.Value.DestinationTable.Levels.Get(x.Name)).ToList();
                destinationLevelSelector.Levels = !destinationLevelSelector.IncludeAll && destinationLevelsInNewCase.All(x => x != null) ? destinationLevelsInNewCase : new List<Level>();
            }
			
			destRoofCentroid.SetTable(caseSelector.Value.DestinationTable);
			if(caseSelector.Value.DestinationTable == null)
				destRoofCentroid.Value = null;
			
			destFloorCentroid.SetTable(caseSelector.Value.DestinationTable);
			if(caseSelector.Value.DestinationTable == null)
				destFloorCentroid.Value = null;
			
			fillRateFieldSelector.SetTable(caseSelector.Value.DestinationTable);
			if(caseSelector.Value.DestinationTable == null)
				fillRateFieldSelector.Value = null;

            processesSelectOption.SetCase(caseSelector.Value);
            var processesInNewCase = processesSelectOption.Processes.Select(x => caseSelector.Value.Processes.Get(x.Name)).ToList();
            processesSelectOption.Processes = !processesSelectOption.IncludeAll && processesInNewCase.All(x => x != null) ? processesInNewCase : new List<Process>();

            reportingPeriodSelector.SetCase(caseSelector.Value);
            var reportingLevel = reportingPeriodSelector.Value;
            if (reportingLevel != null)
                reportingPeriodSelector.Value = caseSelector.Value.ReportingLevels.Get(reportingLevel.Name);
        };

        sourceSinkRateOption.ValueChanged += (s, e) =>
        {
            sourceLevelSelector.SetVisible(sourceSinkRateOption.Value);
			sourceWasteRoofCentroid.SetVisible(sourceSinkRateOption.Value);
			sourceWasteFloorCentroid.SetVisible(sourceSinkRateOption.Value);
			sourceCoalRoofCentroid.SetVisible(sourceSinkRateOption.Value);
			sourceCoalFloorCentroid.SetVisible(sourceSinkRateOption.Value);
			sinkRateFieldSelector.SetVisible(sourceSinkRateOption.Value);
        };

        dumpFillRateOption.ValueChanged += (s, e) =>
        {
            destinationLevelSelector.SetVisible(dumpFillRateOption.Value);
			destFloorCentroid.SetVisible(dumpFillRateOption.Value);
			destRoofCentroid.SetVisible(dumpFillRateOption.Value);
			fillRateFieldSelector.SetVisible(dumpFillRateOption.Value);
        };

        if (form.ShowDialog() != DialogResult.OK)
            return;

        var selectedCase = caseSelector.Value;
        var selectedProcesses = processesSelectOption.IncludeAll ? selectedCase.Processes.Where(x => x.Productive).ToList() : processesSelectOption.Processes.ToList();
        var selectedSourceLevels = sourceLevelSelector.IncludeAll ? selectedCase.SourceTable.Levels.ToList() : sourceLevelSelector.Levels.ToList();
        var selectedDestinationLevels = destinationLevelSelector.IncludeAll ? selectedCase.DestinationTable.Levels.ToList() : destinationLevelSelector.Levels.ToList();
        var selectedReportingLevel = reportingPeriodSelector.Value;
        var sourceSinkCheckBox = sourceSinkRateOption.Value;
		var sinkRateField = sinkRateFieldSelector.Value;
        var dumpFillCheckBox = dumpFillRateOption.Value;
		var fillRateField = fillRateFieldSelector.Value;
		var centroidFields = new CentroidFields(sourceWasteRoofCentroid.Value, sourceWasteFloorCentroid.Value, sourceCoalRoofCentroid.Value, sourceCoalFloorCentroid.Value, destRoofCentroid.Value, destFloorCentroid.Value);

        WriteSinkRateToTable(selectedCase, selectedProcesses, selectedSourceLevels, selectedDestinationLevels, selectedReportingLevel, sourceSinkCheckBox, sinkRateField, dumpFillCheckBox, fillRateField, centroidFields);
    }

    #endregion
}

public partial class TruckCount
{
	static void Calculate(Case _case, DateTime startDate, DateTime endDate, ReportingLevel reportingPeriod)
	{
		var groupedScheduleSteps = _case.Schedule.Where(x => x.Start > startDate && x.End < endDate && x.HaulProfile != null).GroupBy(x => x.GetReportingPeriod(reportingPeriod.Name));
		foreach(var grp in groupedScheduleSteps)
		{
			var productSum = 0.0;
			var weightingSum = 0.0;
			var maxTrucks = 0.0;
			foreach(var step in grp)
			{
				var truckCount = step.HaulageProductivityCalculator.RawTruckCount;
				if (truckCount > maxTrucks)
					maxTrucks = truckCount;
				var operatingHours = step.HaulageProductivityCalculator.RawTruckOperatingTime;
				productSum += truckCount * operatingHours.TotalHours;
				weightingSum += operatingHours.TotalHours;
			}
			var averageTrucks = productSum/weightingSum;
			Console.WriteLine(grp.First().GetReportingPeriod(reportingPeriod.Name));
			Console.WriteLine("Max Trucks: " + maxTrucks);
			Console.WriteLine("Avg Trucks: " + averageTrucks);
		}
	}
	
	public static void WriteTruckCountToOutput()
	{
		var form = OptionsForm.Create("Truck Count Options");

        var caseSelector = form.Options.AddCaseSelect("Select Case")
                            .Validators.Add(x => x != null, "Please select case")
                            .RestoreValue("TRUCK-RATE-CASE", _case => _case.FullName, str => Case.TryGet(str));
		
		var reportingPeriodSelector = form.Options.AddReportingLevelSelect("Reporting Level")
                                    .SetCase(caseSelector.Value)
                                    .RestoreValue("TRUCK-RATE-REPORTINGLEVEL", level => level.Name, str => caseSelector.Value.ReportingLevels.Get(str))
                                    .Validators.Add(x => x != null, "Please select a reporting level.");
		
		var startDateSelector = form.Options.AddDateTime("Start Date")
								.RestoreValue("TRUCK-RATE-START-DATE", date => date.ToString(), str => DateTime.Parse(str))
								.Validators.Add(x => x != null, "Please select Date");
		
		var endDateSelector = form.Options.AddDateTime("End Date")
								.RestoreValue("TRUCK-RATE-END-DATE", date => date.ToString(), str => DateTime.Parse(str))
								.Validators.Add(x => x != null, "Please select Date");
		
		caseSelector.ValueChanged += (s, e) =>
        {
            if (caseSelector.Value == null)
            {
                reportingPeriodSelector.SetCase(null);
                reportingPeriodSelector.Value = null;
                return;
            }
			
            reportingPeriodSelector.SetCase(caseSelector.Value);
            var reportingLevel = reportingPeriodSelector.Value;
            if (reportingLevel != null)
                reportingPeriodSelector.Value = caseSelector.Value.ReportingLevels.Get(reportingLevel.Name);
			
			startDateSelector.Value = caseSelector.Value.ScheduleStart;
			endDateSelector.Value = caseSelector.Value.ScheduleEnd;
        };
		
		if (form.ShowDialog() != DialogResult.OK)
            return;

        var selectedCase = caseSelector.Value;
        var selectedReportingLevel = reportingPeriodSelector.Value;
		var selectedStartDate = startDateSelector.Value;
		var selectedEndDate = endDateSelector.Value;
		
		
		Calculate(selectedCase, selectedStartDate, selectedEndDate, selectedReportingLevel);
	}
}
