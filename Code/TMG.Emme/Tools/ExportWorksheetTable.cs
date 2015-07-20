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
using TMG.Input;
using XTMF;
namespace TMG.Emme.Tools
{

    public class ExportWorksheetTable : IEmmeTool
    {

        const string ToolName = "tmg.XTMF_internal.export_worksheet_table";

        [RunParameter("Scenario Number", 0, "The scenario number to export.")]
        public int ScenarioNumber;


        public class TableExport : XTMF.IModule
        {
            [SubModelInformation(Required = true, Description = "The location to save this worksheet to.")]
            public FileLocation OutputLocation;

            [RunParameter("Worksheet Path", "", "The path to save the worksheet to.")]
            public string WorksheetPath;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Description = "The worksheets to export.")]
        public TableExport[] ToExport;


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
            return string.Join(" ", ScenarioNumber, BuildList(e => e.WorksheetPath), BuildList(e => e.OutputLocation));
        }

        private string BuildList(Func<TableExport, string> getValue)
        {
            return AddQuotes(string.Join(",", from export in ToExport
                                              select getValue(export)));
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
