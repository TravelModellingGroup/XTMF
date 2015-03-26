/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.DataUtility;

namespace TMG.Emme.NetworkAssignment.PreProcessing
{
    public class AssignBoardingPenalties : IEmmeTool
    {
        private const string ToolNamespace = "tmg.assignment.preprocessing.assign_v4_boarding_penalty";

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if(modeller == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were not given a modeller controller!");
            }
            return modeller.Run(ToolNamespace, GetArguments(modeller));
        }


        public sealed class BoardingPenalty : XTMF.IModule
        {
            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            [RunParameter("Label", "", "Describes what the penalty is being applied for")]
            public string Label;

            [RunParameter("Line Filter", "", "Leave this blank to use a mode filter, otherwise set this equal to an EMME filter string.")]
            public string LineFilter;

            [RunParameter("Mode", 'm', "This parameter is only used if the Line Filter is empty.  Set this to the mode you wish to apply this filter to.")]
            public char Mode;

            [RunParameter("Penalty", 0.0f, "The boarding penalty to apply for this filter.")]
            public float Penalty;

            internal string ReturnFilter(ModellerController controller)
            {
                return Label.Replace('"', '\'') + ":" 
                    + (String.IsNullOrWhiteSpace(LineFilter) ? "modes=" + (Mode == '"' ? '\'' : Mode) : "line=" + LineFilter.Replace('"', '\''))
                    + ": " + controller.ToEmmeFloat(Penalty).ToString();
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The different boarding penalties to apply.")]
        public BoardingPenalty[] BoardingPenalties;

        [RunParameter("Scenario Numbers", "1", typeof(NumberList), "A comma separated list of scenario numbers to execute this against.")]
        public NumberList ScenarioNumbers;

        private string GetArguments(ModellerController controller)
        {
            var scenarioString = string.Join(",", ScenarioNumbers.Select(v => v.ToString()));
            var penaltyString = "\"" + string.Join("\n", BoardingPenalties.Select(b => b.ReturnFilter(controller))) + "\"";
            return scenarioString + " " + penaltyString;
        }
    }

}
