/*
    Copyright 2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using XTMF;

namespace TMG.Emme.Tools.NetworkEditing;

[ModuleInformation(Name ="Remove Extra Nodes",
    Description = "Use this tool to invoke the TMGToolbox tool tmg.network_editing.remove_extra_nodes"
    )]
public sealed class RemoveExtraNodes : IEmmeTool
{
    private const string ToolNamespace = "tmg.network_editing.remove_extra_nodes";

    [RunParameter("Scenario Number", 0, "The EMME scenario number to target.")]
    public int ScenarioNumber;

    [RunParameter("Node Filter Attribute", "", "Only remove candidate nodes whose attribute value != 0. Leave blank to remove all candidate nodes.")]
    public string NodeFilterAttribute;

    [RunParameter("Stop Filter Attribute", "", "Remove candidate transit stop nodes whose attribute value != 0. Leave blank to preserve all transit stops.")]
    public string StopFilterAttribute;

    [RunParameter("Connector Filter Attribute", "", "Remove centroid connectors attached to candidate nodes whose attribute value != 0. Leave blank to preserve all centroid connectors.")]
    public string ConnectorFilterAttribute;

    [RunParameter("Attribute Aggregation Functions", "", "Please refer to the tool tmg.network_editing.remove_extra_nodes for the format of this string.")]
    public string AttributeAggregationFunctions;

    public bool Execute(Controller controller)
    {
        if (controller is ModellerController modellerController)
        {
            modellerController.Run(this, ToolNamespace, GetParameters());
            return true;
        }
        throw new XTMFRuntimeException(this, "In '" + Name + "' the controller was not for modeller!");
    }

    private ModellerControllerParameter[] GetParameters()
    {
        //def __call__(self, baseScen, nodeFilter, stopFilter, connFilter, attAgg):
        return new ModellerControllerParameter[]
        {
            new("baseScen",ScenarioNumber.ToString()),
            new("NodeFilterAttributeId",NodeFilterAttribute),
            new("StopFilterAttributeId",StopFilterAttribute),
            new("ConnectorFilterAttributeId",ConnectorFilterAttribute),
            new("AttributeAggregatorString",AttributeAggregationFunctions)
        };
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    public bool RuntimeValidation(ref string error)
    {
        if (ScenarioNumber <= 0)
        {
            error = "The scenario number '" + ScenarioNumber
                + "' is an invalid scenario number!";
            return false;
        }
        return true;
    }
}
