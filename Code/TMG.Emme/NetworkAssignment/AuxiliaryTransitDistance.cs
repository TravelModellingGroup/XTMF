/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Emme.NetworkAssignment
{
    [ModuleInformation(Description = "This tool allows you to get the distance people would need to travel in order to get between zones using only"
        +" auxiliary transit modes.")]
    public class AuxiliaryTransitDistance : IEmmeTool
    {
        private const string ToolName = "tmg.assignment.transit.aux_transit_distance";

        [RunParameter("Scenario Number", 0, "The scenario number in EMME to use.")]
        public int ScenarioNumber;

        [RunParameter("Distance Matrix", 0, "The matrix number to store the distances to.")]
        public int DistanceMatrix;

        [RunParameter("Allowed AuxillaryModes", "wv", "The auxiliary transit modes that are allowed to be used to find the shortest path.")]
        public string Modes;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public bool Execute(Controller controller)
        {
            if(controller is ModellerController mc)
            {
                return mc.Run(this, ToolName, new ModellerControllerParameter[]
                    {
                        new ModellerControllerParameter("xtmf_AssignmentModes", Modes),
                        new ModellerControllerParameter("DistanceSkimMatrixID", DistanceMatrix.ToString()),
                        new ModellerControllerParameter("xtmf_ScenarioId", ScenarioNumber.ToString()),
                        new ModellerControllerParameter("ClassName", "aux_transit_distance")
                    });
            }
            throw new XTMFRuntimeException(this, "The runtime controller for EMME was not a modeller controller!");
        }

        public bool RuntimeValidation(ref string error)
        {
            if(ScenarioNumber <= 0)
            {
                error = "The scenario number must be greater than zero!";
                return false;
            }
            return true;
        }
    }
}
