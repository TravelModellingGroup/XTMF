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
    [ModuleInformation(Description= "Calculates a per-km link toll attribute in Emnme, assuming a two-zone system")]
    public class Calc407Tolls : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme scenario")]
        public int ScenarioNumber;

        [RunParameter("Result Attribute Id", "@toll", "The ID of the link extra attribute to save the toll results into, including the '@' character.")]
        public string ResultAttributeId;

        [RunParameter("Toll Attribute Id", "@tzone", "The ID of the link extra attribute containing the integer toll zone data. Ordinal 0 is not tolled, "+
            "ordinal 1 is the light zone, and ordinal 2 is the regular zone.")]
        public string TollZoneAttributeId;

        [RunParameter("Light Zone Price", 0.2323f, "The unit toll cost per km within the light zone ($/km)")]
        public float LightZoneToll;

        [RunParameter("Regular Zone Price", 0.2487f, "The unit toll cost per km within the regular zone ($/km)")]
        public float RegularZoneToll;

        private const string ToolName = "tmg.assignment.preprocessing.calc_407ETR_tolls";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController");
            }

            string args = ScenarioNumber + " " + ResultAttributeId + " " + TollZoneAttributeId + " " + LightZoneToll + " " + RegularZoneToll;
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

        private static Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _progressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
