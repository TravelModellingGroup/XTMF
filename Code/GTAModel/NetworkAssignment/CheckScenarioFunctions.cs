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
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation( Description = "Loads a scenario and checks that all referenced functions (link VDF, transit segment TTF, " +
                        "and turn TPF) are defined in the emmebank. It will raise an error listing all missing functions." )]
    public class CheckScenarioFunctions : IEmmeTool
    {
        [RunParameter( "Scenario Number", 0, "The number of the Emme scenario." )]
        public int ScenarioNumber;

        private const string ToolName = "tmg.assignment.preprocessing.check_scenario_functions";
        private const string AlternateToolName = "TMG.Assignment.CheckFunctions";

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );

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

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            string result = null;

            var toolName = ToolName;
            if (!mc.CheckToolExists(toolName))
            {
                toolName = AlternateToolName;
            }


            return mc.Run(toolName, ScenarioNumber.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}