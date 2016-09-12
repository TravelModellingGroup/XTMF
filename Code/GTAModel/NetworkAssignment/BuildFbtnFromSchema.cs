/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation(Description = "Generates a hyper-network to support fare-based transit " +
                     "assignment(FBTA), from an XML schema file.Links and segments with negative " +
                     "fare values will be reported to the Logbook for further inspection. " +
                     "For fare schema specification, please consult TMG documentation." +
                     "<br><br><b> Temporary storage requirements:</b> one transit line extra " +
                     "attribute, one node extra attribute.",
        Name = "Build Fare Based Transit Network From Schema")]
    public class BuildFbtnFromSchema : IEmmeTool
    {
        [SubModelInformation(Description = "Fare Schema File", Required = true)]
        public FileLocation SchemaFile;

        [RunParameter("Base Scenario", 0, "The number of the Emme BASE (i.e. non-FBTN-enabled) scenario.")]
        public int BaseScenarioNumber;

        [RunParameter("New Scenario", 0, "The number of the scenario to be created.")]
        public int NewScenarioNumber;

        [RunParameter("Transfer Mode", 't', "The mode ID to assign to new virtual connector links.")]
        public char TransferModeId;

        [Parameter("Segment Fare Attribute", "@sfare", "A TRANSIT SEGMENT extra attribute in which to store the in-line fares.")]
        public string SegmentFareAttribute;

        [Parameter("Link Fare Attribute", "@lfare", "A LINK extra attribute in which to store the transfer and boarding fares.")]
        public string LinkFareAttribute;

        [Parameter("Virtual Node Domain", 100000, "All created virtual nodes will have IDs higher than this number. This tool will never override and existing node.")]
        public int VirtualNodeDomain;

        [RunParameter("StationConnectorFlag", true, "Should we automatically integrate stations with centroid connectors?")]
        public bool StationConnectorFlag;

        [RunParameter("Transfer Mode List", "tu", "A list of the modes eligible to changed to the invalid transit mode in cases of link fare circumvention.")]
        public string ModeList;

        [RunParameter("Invalid Transit Mode", 'n', "The transit mode to assign to invalid transfers.")]
        public char InvalidTransferMode;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        private const string _ToolName = "tmg.network_editing.transit_fare_hypernetworks.generate_hypernetwork_from_schema";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", "\"" + SchemaFile.GetFilePath() + "\"",
                                        BaseScenarioNumber,
                                        NewScenarioNumber,
                                        TransferModeId,
                                        SegmentFareAttribute,
                                        LinkFareAttribute,
                                        VirtualNodeDomain,
                                        StationConnectorFlag,
                                        AddQuotes(ModeList),
                                        InvalidTransferMode);
            var result = "";
            return mc.Run(_ToolName, args, (p => Progress = p), ref result);
        }

        private string AddQuotes(string modeList)
        {
            return "\"" + modeList + "\"";
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
