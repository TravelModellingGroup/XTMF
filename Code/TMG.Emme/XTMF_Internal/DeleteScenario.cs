/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Emme.XTMF_Internal
{
    [ModuleInformation(
        Description = "This tool will allow you to be able to delete a scenario."
        )]
    public class DeleteScenario : IEmmeTool
    {
        [RunParameter("Base Scenario", 1, "The scenario to copy from.")]
        public int Scenario;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }
            return mc.Run("tmg.XTMF_internal.delete_scenario", Scenario.ToString());
        }

        public bool RuntimeValidation(ref string error)
        {
            if (Scenario <= 0)
            {
                error = "The scenario number '" + Scenario
                    + "' is an invalid scenario number!";
                return false;
            }
            return true;
        }
    }

}
