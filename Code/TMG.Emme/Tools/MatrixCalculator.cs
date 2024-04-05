/*
    Copyright 2019 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Emme;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace TMG.Emme.Tools
{
    public class MatrixCalculator : IEmmeTool
    {

        public enum DomainTypes
        {
            Link = 0,
            Node = 1,
            Transit_Line = 2,
            Transit_Segment = 3

        }

        private const string ToolName = "tmg.XTMF_internal.xtmf_matrix_calculator";

        [RunParameter("Scenario Number", "1", "What scenario would you like to run this for?")]
        public int ScenarioNumber;

        [RunParameter("Expression", "", "Matrix calculation expression string")]
        public string Expression;

        [RunParameter("Result", "", "Matrix ID or name | partition ID | node user or extra attribute to store the result")]
        public string Result;

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' we require the use of EMME Modeller in order to execute.");
            }
            modeller.Run(this, ToolName, new[]
            {
              new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
              new ModellerControllerParameter("xtmf_expression", Expression),
              new ModellerControllerParameter("xtmf_result", Result)
            });
            return true;
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

        public Tuple<byte, byte, byte> ProgressColour => new(120, 25, 100);

        public bool RuntimeValidation(ref string error)
        {
            if (Result == null)
            {
                return false;
            }
            return true;
        }

    }
}
