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
using System.IO;
using TMG.Input;
using TMG.Emme;
using Tasha.Common;
using XTMF;


namespace Tasha.Validation.PerformanceMeasures
{
    public class DistanceMatrixCalculation : IEmmeTool
    {
        private const string _ToolName = "tmg.input_output.export_distance_matrix";

        [SubModelInformation(Required = true, Description = "Where to save the distance matrices.")]
        public FileLocation ResultsFile;

        [RunParameter("Cost per km", 0.153, "Per km cost for auto assignment used in this model run")]
        public float CostPerKm;

        [SubModelInformation(Required = true, Description = "The different boarding penalties to apply.")]
        public DistancesPerTimePeriod[] TimePeriodToConsider;

        public sealed class DistancesPerTimePeriod : XTMF.IModule
        {
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

            [RunParameter("Time Period Label", "AM", "Which time period is this?")]
            public string TimePeriodLabel;

            [RunParameter("Scenario Number", 12, "Appropriate scenario corresponding to this time period")]
            public int ScenarioNumber;

            internal string ReturnFilter(ModellerController controller)
            {
                return TimePeriodLabel.Replace('"', '\'') + ":" + ScenarioNumber.ToString();                                
            }

            public Tuple<byte, byte, byte> ProgressColour
            {
                get { return new Tuple<byte, byte, byte>(120, 25, 100); }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
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
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool Execute(Controller controller)
        {

            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were not given a modeler controller!");
            }
            var fullPathToDirectory = Path.GetFullPath(Path.GetDirectoryName(ResultsFile));

            string timePeriods = "";

            foreach(var timePeriod in TimePeriodToConsider)
            {
                timePeriods += timePeriod.ReturnFilter(mc) + ",";
            }

            var args = string.Join(" ", timePeriods, "\"" + fullPathToDirectory + "\"" , CostPerKm.ToString());

            return mc.Run(_ToolName, null);

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
