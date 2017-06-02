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
using System.Linq;
using TMG.DataUtility;
using TMG.Input;
using XTMF;
namespace TMG.Emme.Tools
{
    [ModuleInformation(
        Description =
        "This module is designed to provide the functionality of importing scenarios from another database. This tool supports EMME 4.0.8+. "
        + "It imports all functions, but does not import matrices or partitions."
    )]
    public class ImportScenarios : IEmmeTool
    {
        private const string ToolName = "tmg.XTMF_internal.import_from_database";

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario Numbers", "0", typeof(NumberList), "A list of scenarios to copy.")]
        public NumberList ScenarioNumbers;

        [RunParameter("Increment", 0, "This number will be added to each imported scenario number when saving into the current database. "
            + "For example if you import scenarios 1,3,4 and increment was set to 10, you would save them into scenarios 10,13,14.")]
        public int Increment;

        [RunParameter("Overwrite Flag", false, "If true, existing scenarios with the same id will be overwritten.")]
        public bool OverwriteFlag;

        [SubModelInformation(Required = true, Description = "The databank to read the scenarios from.")]
        public FileLocation OtherDatabank;

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' the controller was not of type ModellerController!");
            }
            return mc.Run(ToolName, GetArguments());
        }

        private string GetArguments()
        {
            return string.Join(" ", GetScenarioNumbers(), Increment, AddQuotes(OtherDatabank.GetFilePath()), OverwriteFlag);
        }

        private string GetScenarioNumbers()
        {
            return AddQuotes(string.Join(",", from scenario in ScenarioNumbers
                                              select scenario.ToString()));
        }

        private static string AddQuotes(string toQuote)
        {
            return "\"" + toQuote.Replace("\"", "\\\"") +"\"";
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
