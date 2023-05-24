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
using System.IO;
using System.Text;
using System.Text.Json;
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
    public class BuildFbtnFromSchemaMulticlass : IEmmeTool
    {
        [SubModelInformation(Description = "Fare Schema File", Required = true)]
        public FileLocation BaseSchemaFile;

        [RunParameter("Base Scenario", 0, "The number of the Emme BASE (i.e. non-FBTN-enabled) scenario.")]
        public int BaseScenarioNumber;

        [RunParameter("New Scenario", 0, "The number of the scenario to be created.")]
        public int NewScenarioNumber;

        [RunParameter("Transfer Mode", 't', "The mode ID to assign to new virtual connector links.")]
        public char TransferModeId;

        [Parameter("Virtual Node Domain", 100000, "All created virtual nodes will have IDs higher than this number. This tool will never override and existing node.")]
        public int VirtualNodeDomain;

        [RunParameter("StationConnectorFlag", true, "Should we automatically integrate stations with centroid connectors?")]
        public bool StationConnectorFlag;

        [SubModelInformation(Description = "Fare Class", Required = true)]
        public FareClass[] FareClasses;


        private static Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        private const string ToolName = "tmg.network_editing.transit_fare_hypernetworks.generate_hypernetwork_from_schema_multiclass";

        public bool Execute(Controller controller)
        {
            if (controller is not ModellerController mc)
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = false });
            writer.WriteStartObject();
            writer.WritePropertyName("FareClasses");
            writer.WriteStartArray();

            foreach (var fareClass in FareClasses)
            {
                writer.WriteStartObject();
                writer.WriteString("SchemaFile", fareClass.SchemaFile.GetFilePath());
                writer.WriteString("SegmentFareAttribute", fareClass.SegmentFareAttribute);
                writer.WriteString("LinkFareAttribute", fareClass.LinkFareAttribute);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            stream.Position = 0;
            var sb = System.Text.UTF8Encoding.UTF8.GetString(stream.ToArray());
            var args = string.Join(" ", "\"" + BaseSchemaFile.GetFilePath() + "\"",
                                    BaseScenarioNumber,
                                    NewScenarioNumber,
                                    TransferModeId,
                                    VirtualNodeDomain,
                                    StationConnectorFlag,
                                    "\"" + stream.ToString().Replace("\"", "'") + "\""
                                        );
            var result = "";
            return mc.Run(this, ToolName, args, (p => Progress = p), ref result);
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
            get { return _progressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (FareClasses.Length < 1)
            {
                error = "There must be at least one fare class defined.";
                return false;
            }

            return true;
        }

        [ModuleInformation(Description = "Fare class module used with BuildFbtnFromSchemaMulticlass",
        Name = "FBTN Fare Class")]
        public class FareClass : IModule
        {

            private string _name = "FareClass";

            public string Name { get => _name; set => _name = value; }

            public float Progress => 100;

            public Tuple<byte, byte, byte> ProgressColour => Tuple.Create<byte, byte, byte>(100, 100, 100);

            [SubModelInformation(Description = "Fare Schema File", Required = true)]
            public FileLocation SchemaFile;

            [Parameter("Segment Fare Attribute", "@sfare", "A TRANSIT SEGMENT extra attribute in which to store the in-line fares.")]
            public string SegmentFareAttribute;

            [Parameter("Link Fare Attribute", "@lfare", "A LINK extra attribute in which to store the transfer and boarding fares.")]
            public string LinkFareAttribute;

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }
    }


}
