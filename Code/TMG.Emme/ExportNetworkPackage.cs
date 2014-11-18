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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Input;
using XTMF;
using TMG.DataUtility;
using System.IO;

namespace TMG.Emme
{
    public class ExportNetworkPackage : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The Emme scenario to export")]
        public int ScenarioNumber;

        [RunParameter("Attributes to Export", "", "A list of extra attribute IDs to include in the NWP file (including the '@' symbol)")]
        public StringList AttributeIdsToExport;

        [SubModelInformation(Description = "Network Package File", Required = true)]
        public FileLocation ExportFile;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string _ToolName = "TMG2.IO.ExportScenario";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var s = string.Join(",", this.AttributeIdsToExport.ToArray());

            if(string.IsNullOrWhiteSpace(this.AttributeIdsToExport.ToString()))
                s += "\"\"";

            var args = string.Join(" ", this.ScenarioNumber,
                                        "\"" + Path.GetFullPath(this.ExportFile.GetFilePath()) + "\"",
                                        s);

            Console.Write("Export network from scenario " + this.ScenarioNumber.ToString() + " to file " + this.ExportFile.GetFilePath());

            var result = "";
            return mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
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

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
