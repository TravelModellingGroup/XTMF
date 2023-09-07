/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using XTMF;
using System.IO;

namespace TMG.Emme.XTMF_Internal
{
    [ModuleInformation(Description =
        "Copy Attributes Between Scenarios",
        Name = "Copy Attributes Between Scenarios"
        )]

    public class CopyAttribute : IEmmeTool
    {
        public enum DomainTypes
        {
            LINK = 0,
            NODE = 1,
            TRANSIT_LINE = 2,
            TURN = 3,
            TRANSIT_SEGMENT = 4
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }
        [RunParameter("From Scenario Number", 0, "The scenario number to copy from.")]
        public int FromScenarioNumber;

        [RunParameter("To Scenario Number", 0, "The scenario number to copy to.")]
        public int ToScenarioNumber;

        const string ToolName = "tmg.XTMF_internal.copy_attribute";

        [RunParameter("Attribute Domain Type", DomainTypes.LINK, "Domain types: LINK, NODE, TURN, TRANSIT_LINE, TRANSIT_SEGMENT")]
        public DomainTypes Domain;

        [RunParameter("Attribute to copy from", "", "Attribute to copy from eg. @tvph")]
        public string FromAttribute;

        [RunParameter("Attribute to copy to", "", "Attribute to copy to eg. @check_length")]
        public string ToAttribute;

        [RunParameter("Link Selection", "all", "A link selector expression")]
        public string LinkSelection;

        [RunParameter("Node Selection", "all", "A node selector expression")]
        public string NodeSelection;

        [RunParameter("Transit Line Selection", "all", "A transit line selector expression")]
        public string TransitLineSelection;

        [RunParameter("Incoming Link Selection", "all", "A link selector expression to specify the links coming into turns")]
        public string IncomingLinkSelection;

        [RunParameter("Outgoing Link Selection", "all", "A link selector expression to specify the links going out of turns")]
        public string OutgoingLinkSelection;

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "TMG.XTMF_INTERNAL.CopyAttribute requires the use of EMME Modeller and will not work through command prompt!");
            }

            string ret = null;
            if (!mc.CheckToolExists(this, ToolName))
            {
                throw new XTMFRuntimeException(this, "There was no tool with the name '" + ToolName + "' available in the EMME databank!");
            }

            return mc.Run(this, ToolName, GetParameters(), (p) => Progress = p, ref ret);
        }

        private ModellerControllerParameter[] GetParameters()
        {
            return new[]
            {
                new ModellerControllerParameter("to_scenario_number", ToScenarioNumber.ToString()),
                new ModellerControllerParameter("from_scenario_numbers", FromScenarioNumber.ToString()),
                new ModellerControllerParameter("to_attribute", ToAttribute),
                new ModellerControllerParameter("from_attribute", FromAttribute),
                new ModellerControllerParameter("domain", Enum.GetName(Domain)),
                new ModellerControllerParameter("node_selector", NodeSelection),
                new ModellerControllerParameter("link_selector", LinkSelection),
                new ModellerControllerParameter("transit_line_selector", TransitLineSelection),
                new ModellerControllerParameter("incoming_link_selector", IncomingLinkSelection),
                new ModellerControllerParameter("outgoing_link_selector", OutgoingLinkSelection),
            };
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}

