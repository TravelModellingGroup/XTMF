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

namespace TMG.Emme.Tools.NetworkEditing
{
    [ModuleInformation(Description =
@"This module provides integration with the TMGToolbox's RemoveExtraLinks tool."
        )]
    public class RemoveExtraLinks : IEmmeTool
    {
        private const string ToolNamespace = "tmg.network_editing.remove_extra_links";

        [RunParameter("Scenario Number", 0, "The EMME scenario number to target.")]
        public int BaseScenario;

        [RunParameter("New Scenario", 0, "The EMME scenario to save into.  If it is the same as scenario number then no copying will occur.")]
        public int NewScenario;

        [RunParameter("Transfer Modes", "tuy", "The modes that we need to analyse for correct transfer behaviour between transit services.")]
        public string TransferModes;

        [RunParameter("New Scenario Name", "Copy with removed links", "This will be applied if the new and base scenarios are not the same.")]
        public string NewScenarioName;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' the controller was not for modeller!");
            }
            return modeller.Run(ToolNamespace, GetArguments());
        }

        private string GetArguments()
        {
            return string.Join(" ", BaseScenario, AddQuotes(TransferModes), NewScenario == BaseScenario, NewScenario, AddQuotes(NewScenarioName));
        }

        private string AddQuotes(string name)
        {
            return $"\"{name.Replace('"', '\'')}\"";
        }

        public bool RuntimeValidation(ref string error)
        {
            if (BaseScenario <= 0)
            {
                error = "The scenario number '" + BaseScenario
                    + "' is an invalid scenario number!";
                return false;
            }

            if (NewScenario <= 0)
            {
                error = "The scenario number '" + NewScenario
                    + "' is an invalid scenario number!";
                return false;
            }
            return true;
        }
    }
}
