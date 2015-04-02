/*
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools
{

    public class FullNetworkSetGenerator : IEmmeTool
    {
        private const string ToolNamespace = "tmg.network_editing.full_network_set_generator";

        [RunParameter("Base Scenario Number", 0, "The scenario number for the base network.")]
        public int BaseScenarioNumber;

        [SubModelInformation(Required = true, Description = "A link to the file containing transit service data.")]
        public FileLocation TransitServiceTable;

        [SubModelInformation(Required = true, Description = "A link to the file containing how to aggregate schedules.")]
        public FileLocation TransitAggreggationSelectionTable;

        [SubModelInformation(Required = true, Description = "A link to the file containing how to modify transit schedules.")]
        public FileLocation TransitAlternativeTable;

        [SubModelInformation(Required = true, Description = "A path to the batch edit file.")]
        public FileLocation BatchEditFile;

        [RunParameter("Default Aggregation", "Naive", typeof(Aggregation), "The default aggregation to apply.")]
        public Aggregation DefaultAggregation;

        [RunParameter("Node Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
        public string NodeFilterAttribute;
        [RunParameter("Stop Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
        public string StopFilterAttribute;
        [RunParameter("Connector Filter Attribute", "@attributeName", "The name of the attribute to use as a filter.")]
        public string ConnectorFilterAttribute;
        [RunParameter("Attribute Aggregator", "", "The formated string to aggregate attributes.")]
        public string AttributeAggregator;
        [RunParameter("Line Filter Expression", "", "The formated string to use as an expression to filter lines.")]
        public string LineFilterExpression;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            return modeller.Run(ToolNamespace, GetParameters());
        }


        public sealed class TimePeriodScenario : IModule
        {
            [RunParameter("Unclean Description", "Uncleaned Network", "The description for the uncleaned scenario")]
            public string UncleanedDescription;
            [RunParameter("Cleaned Description", "Cleaned Network", "The description for the cleaned scenario")]
            public string CleanedDescription;
            [RunParameter("Uncleaned Scenario Number", 0, "The scenario number for the uncleaned network")]
            public int UncleanedScenarioNumber;
            [RunParameter("Cleaned Scenario Number", 0, "The scenario number for the cleaned network")]
            public int CleanedScenarioNumber;
            [RunParameter("Start Time", "6:00", typeof(Time), "The start time for this scenario")]
            public Time StartTime;
            [RunParameter("End Time", "9:00", typeof(Time), "The end time for this scenario")]
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
                if(CleanedScenarioNumber <= 0)
                {
                    error = "In '" + Name + "' the cleaned scenario number is invalid!";
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

        public TimePeriodScenario[] TimePeriods;

        public enum Aggregation
        {
            Naive,
            Average
        }


        private string GetParameters()
        {
            /*xtmf_ScenarioNumber, Scen1UnNumber, Scen1UnDescription, Scen1Number,
                 Scen1Description, Scen1Start, Scen1End, 
                 Scen2UnNumber, Scen2UnDescription, Scen2Number, 
                 Scen2Description, Scen2Start, Scen2End,
                 Scen3UnNumber, Scen3UnDescription, Scen3Number, 
                 Scen3Description, Scen3Start, Scen3End,
                 Scen4UnNumber, Scen4UnDescription, Scen4Number, 
                 Scen4Description, Scen4Start, Scen4End,
                 Scen5UnNumber, Scen5UnDescription, Scen5Number, 
                 Scen5Description, Scen5Start, Scen5End,
                 TransitServiceTableFile, AggTypeSelectionFile, AlternativeDataFile,BatchEditFile,
                 DefaultAgg, PublishFlag, OverwriteScenarioFlag, NodeFilterAttributeId,
                 StopFilterAttributeId, ConnectorFilterAttributeId, AttributeAggregatorString,
                 LineFilterExpression*/
            // times are in seconds
            return string.Join(" ", BaseScenarioNumber.ToString(),
                                    GetTimePeriodScenarioParameters(),
                                    "\"" + Path.GetFullPath(TransitServiceTable.GetFilePath()) + "\"",
                                    "\"" + Path.GetFullPath(TransitAggreggationSelectionTable.GetFilePath()) + "\"",
                                    "\"" + Path.GetFullPath(TransitAlternativeTable.GetFilePath()) + "\"",
                                    "\"" + Path.GetFullPath(BatchEditFile.GetFilePath()) + "\"",
                                    DefaultAggregation == Aggregation.Naive ? "n" : "a",
                                    "True",
                                    "True",
                                    NodeFilterAttribute,
                                    StopFilterAttribute,
                                    ConnectorFilterAttribute,
                                    "\"" + AttributeAggregator + "\"",
                                    "\"" + LineFilterExpression + "\""
                                    );
        }

        private string GetTimePeriodScenarioParameters()
        {
            return string.Join(" ", from period in TimePeriods
                                    select string.Format("{0} \"{1}\" {2} \"{3}\" {4} {5} \"{6}\"",
                                    period.UncleanedScenarioNumber.ToString(),
                                    period.UncleanedDescription.Replace('\"', '\''),
                                    period.CleanedScenarioNumber.ToString(),
                                    period.UncleanedDescription.Replace('\"', '\''),
                                    ConvertTimeToSeconds(period.StartTime),
                                    ConvertTimeToSeconds(period.EndTime),
                                    period.ScenarioNetworkUpdateFile == null? "None" : Path.GetFullPath(period.ScenarioNetworkUpdateFile)));
        }

        private string ConvertTimeToSeconds(Time time)
        {
            return (time.Hours * 3600 + time.Minutes * 60 + time.Seconds).ToString();
        }

        public bool RuntimeValidation(ref string error)
        {
            if(TimePeriods.Length != 5)
            {
                error = "In '" + Name + "' you are required to have 5 timer periods at the moment!";
                return false;
            }
            return true;
        }
    }

}
