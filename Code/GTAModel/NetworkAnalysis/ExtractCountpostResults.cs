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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation(Description = "<b>NOT IMPLEMENTED</b><br/>Exports traffic assignment results on links flagged with a countpost number.")]
    public class ExtractCountpostResults : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme scenario")]
        public int ScenarioNumber;

        [RunParameter("Countpost Attribute", "@stn1", "The ID of a link extra attribute containing countpost ids; including the '@' symbol")]
        public string CountpostAttributeId;

        [RunParameter("Alternate Countpost Attribute", "@stn2", "The ID of an alternate link extra attribute containing countpost ids, for when two countposts share a link" + 
            "; including the '@' symbol")]
        public string AlternateCountpostAttributeId;

        [SubModelInformation(Description="Export File", Required=true)]
        public FileLocation ExportFile;

        private const string ToolName = "";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController");
            }
            
            string args = ScenarioNumber + " " + CountpostAttributeId + " " + AlternateCountpostAttributeId + " " + ExportFile.GetFilePath();
            string result = "";

            return mc.Run(this, ToolName, args, (p => Progress = p), ref result);
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

        private static Tuple<byte, byte, byte> _ProgressColour = new( 100, 100, 150 );

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
