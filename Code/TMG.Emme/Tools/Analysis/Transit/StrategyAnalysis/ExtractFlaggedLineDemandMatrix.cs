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
using XTMF;

namespace TMG.Emme.Tools.Analysis.Transit.StrategyAnalysis;

[ModuleInformation(Description = "This tool is designed to execute the TMG Toolbox for EMME's tmg.analysis.transit.strategy_analysis.extract_flagged_line_demand_matrix tool.")]
public class ExtractFlaggedLineDemandMatrix : IEmmeTool
{
    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    [RunParameter("Scenario Number", 0, "The number of the scenario to analyze.")]
    public int ScenarioNumber;

    [RunParameter("Store Results To", 0, "The matrix number to store the demand that use the selected lines to.")]
    public int StoreResultsTo;

    [RunParameter("Demand Matrix", 0, "The matrix number that contains the demand of the assignment for the class.")]
    public int DemandMatrixNumber;

    [RunParameter("Flag Attribute", "@lflag", "The name of the attribute that is used to signal which lines/segments to generate the demand for.")]
    public string FlagAttribute;

    [RunParameter("Class Name", "", "The name of the class to run the analysis on.  Leave blank for a single class assignment.")]
    public string ClassName;

    private const string TOOL_NAME = "tmg.analysis.transit.strategy_analysis.extract_flagged_line_demand_matrix";

    public bool Execute(Controller controller)
    {
        if(controller is ModellerController mc)
        {
            mc.Run(this, TOOL_NAME, GetParameters());
        }
        else
        {
            throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
        }
        return true;
    }

    private ModellerControllerParameter[] GetParameters()
    {
        return
        [
            new("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
            new("xtmf_MatrixResultNumber", StoreResultsTo.ToString()),
            new("xtmf_DemandMatrixNumber", DemandMatrixNumber.ToString()),
            new("xtmf_ClassName", ClassName),
            new("xtmf_Attribute", FlagAttribute)
          
        ];
    }

    public bool RuntimeValidation(ref string error)
    {
        if(DemandMatrixNumber <= 0)
        {
            error = "The demand matrix number must be a valid matrix number greater than zero!";
            return false;
        }
        if (ScenarioNumber <= 0)
        {
            error = "The scenario number must be a valid scenario number greater than zero!";
            return false;
        }
        return true;
    }
}
