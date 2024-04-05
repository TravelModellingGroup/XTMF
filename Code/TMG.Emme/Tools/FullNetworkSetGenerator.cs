﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.IO;
using System.Linq;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools;


public class FullNetworkSetGenerator : IEmmeTool
{
    private const string ToolNamespace = "tmg.network_editing.full_network_set_generator";

    [RunParameter("Base Scenario Number", 0, "The scenario number for the base network.")]
    public int BaseScenarioNumber;

    [SubModelInformation(Required = false, Description = "A link to the file containing transit service data.")]
    public FileLocation TransitServiceTable;

    [SubModelInformation(Required = false, Description = "A link to the file containing how to aggregate schedules.")]
    public FileLocation TransitAggreggationSelectionTable;

    [SubModelInformation(Required = false, Description = "A link to the file containing how to modify transit schedules.")]
    public FileLocation TransitAlternativeTable;

    [SubModelInformation(Required = false, Description = "A path to the batch edit file.")]
    public FileLocation BatchEditFile;

    [SubModelInformation(Required = false, Description = "Additional files containing how to modify transit schedules. Each will be applied in order.")]
    public FileLocation[] AdditionalTransitAlternativeTable;

    [RunParameter("Default Aggregation", "Naive", typeof(Aggregation), "The default aggregation to apply.")]
    public Aggregation DefaultAggregation;

    [RunParameter("Node Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
    public string NodeFilterAttribute;
    [RunParameter("Stop Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
    public string StopFilterAttribute;
    [RunParameter("Connector Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
    public string ConnectorFilterAttribute;
    [RunParameter("Attribute Aggregator", "", "The formatted string to aggregate attributes.")]
    public string AttributeAggregator;
    [RunParameter("Line Filter Expression", "", "The formatted string to use as an expression to filter lines.  Leave blank to skip prorating transit speeds.")]
    public string LineFilterExpression;
    [RunParameter("Transfer Modes", "tuy", "A string of the transfer mode IDs.")]
    public string TransferModeString;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController;
        if (modeller == null)
        {
            throw new XTMFRuntimeException(this, $"In ${Name}, the controller was not a modeller controller!");
        }
        return modeller.Run(this, ToolNamespace, GetParameters());
    }


    public sealed class TimePeriodScenario : IModule
    {
        [RunParameter("Unclean Description", "Uncleaned Network", 3, "The description for the uncleaned scenario")]
        public string UncleanedDescription;
        [RunParameter("Cleaned Description", "Cleaned Network", 5, "The description for the cleaned scenario")]
        public string CleanedDescription;
        [RunParameter("Uncleaned Scenario Number", 0, 2, "The scenario number for the uncleaned network")]
        public int UncleanedScenarioNumber;
        [RunParameter("Cleaned Scenario Number", 0, 4, "The scenario number for the cleaned network")]
        public int CleanedScenarioNumber;
        [RunParameter("Start Time", "6:00", typeof(Time), 0, "The start time for this scenario")]
        public Time StartTime;
        [RunParameter("End Time", "9:00", typeof(Time), 1, "The end time for this scenario")]
        public Time EndTime;

        [SubModelInformation(Required = false, Description = "The location of the network update file for this time period.")]
        public FileLocation ScenarioNetworkUpdateFile;
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            if(UncleanedScenarioNumber <= 0)
            {
                error = "In '" + Name + "' the uncleaned scenario number is invalid!";
                return false;
            }
            if(EndTime <= StartTime)
            {
                error = "In '" + Name + "' the end time of the scenario is not after its start time!";
                return false;
            }
            return true;
        }
    }

    [SubModelInformation(Required = true, Description = "Time periods to consider.")]
    public TimePeriodScenario[] TimePeriods;

    public enum Aggregation
    {
        Naive,
        Average
    }


    private string GetParameters()
    {
        /*xtmf_ScenarioNumber, CustomScenarioSetString,
             TransitServiceTableFile, AggTypeSelectionFile, AlternativeDataFile, BatchEditFile,
             DefaultAgg, PublishFlag, TransferModesString, OverwriteScenarioFlag, NodeFilterAttributeId,
             StopFilterAttributeId, ConnectorFilterAttributeId, AttributeAggregatorString,
             LineFilterExpression, AdditionalAlternativeDataFiles*/
        // times are in seconds

        return string.Join(" ", BaseScenarioNumber.ToString(),
                                "\"" + GetTimePeriodScenarioParameters() + "\"",
                                GetFileLocationOrNone(TransitServiceTable),
                                GetFileLocationOrNone(TransitAggreggationSelectionTable),
                                GetFileLocationOrNone(TransitAlternativeTable),
                                GetFileLocationOrNone(BatchEditFile),
                                DefaultAggregation == Aggregation.Naive ? "n" : "a",
                                "True",
                                TransferModeString,
                                "True",
                                NodeFilterAttribute,
                                StopFilterAttribute,
                                ConnectorFilterAttribute,
                                "\"" + AttributeAggregator + "\"",
                                "\"" + LineFilterExpression + "\"",
                                (AdditionalTransitAlternativeTable.Length <= 0 ? "None" : string.Join(";", AdditionalTransitAlternativeTable.Select(f => GetFileLocationOrNone(f)).ToArray()))
                                );
    }



    private string GetTimePeriodScenarioParameters()
    {
        return string.Join(",", from period in TimePeriods
                                select string.Format("{0}:{1}:{2}:{3}:{4}:{5}:{6}",
                                period.UncleanedScenarioNumber.ToString(),
                                period.CleanedScenarioNumber.ToString(),
                                period.UncleanedDescription.Replace('\"', '\''),
                                period.CleanedDescription.Replace('\"', '\''),
                                ConvertTimeToSeconds(period.StartTime),
                                ConvertTimeToSeconds(period.EndTime),
                                GetFileLocationOrNone(period.ScenarioNetworkUpdateFile)));
    }

    private static string GetFileLocationOrNone(FileLocation location)
    {
        return location == null ? "None" : "\"" + Path.GetFullPath(location.GetFilePath()) + "\"";
    }

    private string ConvertTimeToSeconds(Time time)
    {
        return ((int)(time.Hours * 100f + time.Seconds)).ToString();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
