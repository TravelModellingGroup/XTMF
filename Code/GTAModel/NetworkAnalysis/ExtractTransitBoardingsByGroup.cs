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
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation(Description= "Extracts transit line boardings by line group, by mode and by operator, and saves the results" +
                                    " in a CSV file. The following line groups are hard-coded: <ul>" +
                                    "<li value='TTC Subways' />" +
                                    "<li value='TTC Streetcars' />" +
                                    "<li value='TTC Buses' />" +
                                    "<li value='GO Trains' />" +
                                    "<li value='GO Buses' />" +
                                    "<li value='Durham Buses' />" +
                                    "<li value='YRT Buses (incl. VIVA)' />" +
                                    "<li value='VIVA Buses only' />" +
                                    "<li value='Mississauag Buses' />" +
                                    "<li value='Brampton Buses (incl. ZUM)' />" +
                                    "<li value='ZUM Buses only' />" +
                                    "<li value='Halton Buses' />" +
                                    "<li value='Hamilton Buses' /> </ul>")]
    public class ExtractTransitBoardingsByGroup : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The Emme scenario from which to extract results.")]
        public int ScenarioNumber;

        [SubModelInformation(Description= "Export File", Required= true)]
        public FileLocation ExportFile;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string _ToolName = "TMG2.Analysis.Transit.ExportLineGroups";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", this.ScenarioNumber,
                                        this.ExportFile.GetFilePath());

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
