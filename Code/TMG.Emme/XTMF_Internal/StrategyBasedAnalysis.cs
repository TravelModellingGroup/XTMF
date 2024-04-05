/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;

namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description =
    "This module is designed to invoke the EMME tool from the TMGToolbox \"tmg.XTMF_internal.strategy_based_analysis\"."
    )]
public sealed class StrategyBasedAnalysis : IEmmeTool
{
    [RunParameter("Scenario Number", 0, "The scenario number to get the boardings from.")]
    public int ScenarioNumber;

    [RunParameter("Class Name", "", "The name of the class to analyze, leave blank for single class assignments.")]
    public string ClassName;

    [RunParameter("Demand Matrix", 0, "The matrix number to use for demand.")]
    public int DemandMatrix;

    [RunParameter("SubPathCombinartionOperator", "+", "The operator to use when combining the different paths.")]
    public string SubPathCombinationOperator;

    [RunParameter("Strategy Values Matrix Number", 0, "The matrix number to store the results to for the strategy values, leave zero to ignore.")]
    public int StrategyValuesMatrixNumber;

    [RunParameter("In-Vehicle Trip Component", "", "The attribute or calculation to perform when building the strategy values.  Leave blank to ignore")]
    public string InVehicleTripComponent;

    [RunParameter("Transit Volumes", "", "The transit segment attribute to store the in-vehicle calculation to.  Leave blank to ignore.")]
    public string TransitVolumesAttribute;

    [RunParameter("Aux Transit", "", "The link attribute to use for the auxiliary transit calculation to.  Leave blank to ignore.")]
    public string AuxTransitAttribute;

    [RunParameter("Aux Transit Volumes Attribute", "", "The link attribute to store the auxiliary transit calculation to.  Leave blank to ignore.")]
    public string AuxTransitVolumesAttribute;

    public const string ToolName = "tmg.XTMF_internal.strategy_based_analysis";
    

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController;
        if (mc == null)
        {
            throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
        }
        return mc.Run(this, ToolName,
            [
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("xtmf_ClassName", ClassName.ToString()),
                new ModellerControllerParameter("xtmf_DemandMatrixNumber", DemandMatrix.ToString()),
                new ModellerControllerParameter("xtmf_sub_path_combination_operator", SubPathCombinationOperator),
                new ModellerControllerParameter("xtmf_StrategyValuesMatrixNumber", StrategyValuesMatrixNumber.ToString()),
                new ModellerControllerParameter("xtmf_in_vehicle_trip_component", InVehicleTripComponent),
                new ModellerControllerParameter("xtmf_transit_volumes_attribute", TransitVolumesAttribute),
                new ModellerControllerParameter("xtmf_aux_transit_attribute", AuxTransitAttribute),
                new ModellerControllerParameter("xtmf_aux_transit_volumes_attribute", AuxTransitVolumesAttribute)
            ]);
    }

    public bool RuntimeValidation(ref string error)
    {
        if (ScenarioNumber <= 0)
        {
            error = "The scenario number '" + ScenarioNumber
                + "' is an invalid scenario number!";
            return false;
        }
        if (DemandMatrix <= 0)
        {
            error = "The matrix number '" + DemandMatrix
                + "' is an invalid demand matrix number!";
            return false;
        }
        if (StrategyValuesMatrixNumber < 0)
        {
            error = "The matrix number '" + StrategyValuesMatrixNumber
                + "' to store the results to is an invalid matrix number!";
            return false;
        }
        return true;
    }
}
