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
using System.IO;
using TMG.Input;
using XTMF;
namespace TMG.Emme.Tools
{

    public class ExportWorksheetTable : IEmmeTool
    {

        const string ToolName = "tmg.XTMF_internal.export_worksheet_table";

        [RunParameter("Scenario Number", 0, "The scenario number to export.")]
        public int ScenarioNumber;

        [SubModelInformation(Required = true, Description = "The path to the worksheet to load.")]
        public FileLocation WorksheetPath;

        [SubModelInformation(Required = true, Description = "The location to save this worksheet to.")]
        public FileLocation OutputLocation;

        [RunParameter("File Name", "", "Optional parameter to set file names different from default.")]
        public String FileName;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' the controller was not for modeller!");
            }
            return mc.Run(ToolName,GetArguments());
        }

        private string GetArguments()
        {
            return string.Join(" ", ScenarioNumber, AddQuotes(Path.GetFullPath(WorksheetPath)), AddQuotes(Path.GetFullPath(OutputLocation)), AddQuotes(FileName));
        }

        private static string AddQuotes(string toQuote)
        {
            return "\"" + toQuote.Replace("\"", "\\\"") + "\"";
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
