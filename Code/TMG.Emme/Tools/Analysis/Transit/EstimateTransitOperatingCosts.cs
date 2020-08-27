/*
    Copyright 2020 Travel Modelling Group, Department of Civil Engineering, University of Toronto
    
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

/*
    Estimate transit operating costs
    
    Author: Peter Lai
    Version: 1.0.0
    Date: 2020-08-27
*/

using System;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools.Analysis.Transit
{

    [ModuleInformation(
        Description =
        "This tool calls the estimate_transit_operating_costs tool of TMGToolbox. " +
        "Computes estimated route-by-route transit operating costs."
    )]

    public class EstimateTransitOperatingCosts : IEmmeTool
    {

        private const string ToolName = "tmg.analysis.transit.estimate_transit_operating_costs";

        // Specify module parameters and required files
        [RunParameter("Scenario Number", 0, "The Emme scenario number to perform the cost computation for.")]
        public int ScenarioNumber;

        [SubModelInformation(Required = true, Description = "The path to the transit service table file for the corresponding network that provides trip departure and arrival schedules.")]
        public FileLocation ServiceTableFile;

        [SubModelInformation(Required = true, Description = "The path to the cost parameters file for the network.")]
        public FileLocation CostParamsFile;

        [SubModelInformation(Required = true, Description = "Report output file path.")]
        public FileLocation ReportFile;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
            }

            modeller.Run(this, ToolName, new[]
                {
                    new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                    new ModellerControllerParameter("ServiceTableFile", ServiceTableFile.GetFilePath()),
                    new ModellerControllerParameter("CostParamsFile", CostParamsFile.GetFilePath()),
                    new ModellerControllerParameter("ReportFile", ReportFile.GetFilePath())
                }
            );

            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (ScenarioNumber <= 0)
            {
                error = "The scenario number '" + ScenarioNumber
                    + "' is an invalid scenario number!";
                return false;
            }

            if (ServiceTableFile.IsPathEmpty())
            {
                error = "Service table path cannot be null or empty.";
                return false;
            }

            if (CostParamsFile.IsPathEmpty())
            {
                error = "Cost parameters path cannot be null or empty.";
                return false;
            }

            if (ReportFile.IsPathEmpty())
            {
                error = "Output report path cannot be null or empty.";
                return false;
            }

            return true;
        }
    }

}

