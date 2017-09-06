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
using System.Globalization;
using System.Text;
using TMG.Emme;
using TMG.Estimation;
using TMG.Estimation.Utilities;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description= "Produces a report ")]
    public class V4ClienntEstimationSupplementalReport2 : ClientFileAggregation, IEmmeTool
    {
        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Scenario", 0, "The Emme scenario from which to extract results.")]
        public int ScenarioNumber;

        [RunParameter("Partition", "gb", "The ID of a matrix/zone partition defined in the Emme databank.")]
        public string PartitionId;

        [RunParameter("Demand Matrix Number", 15, "The number of the demand matrix used in the transit assignment.")]
        public int DemandMatrixNumber;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string ToolName = "tmg.XTMF_internal.return_matrix_results";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController");
            }

            var args = string.Join(" ", ScenarioNumber, PartitionId, "mf" + DemandMatrixNumber);
            string result = "";
            mc.Run(this, ToolName, args, (p => _Progress = p), ref result);
            var modelResults = _ParsePythonResults(result);

            StringBuilder builder = new StringBuilder();
            foreach (var line in modelResults)
            {
                builder.Append(Root.CurrentTask.Generation);
                builder.Append(' ');
                builder.Append(Root.CurrentTask.Index);
                builder.Append(' ');
                var func = Root.RetrieveValue;
                builder.Append((func == null) ? "null" : func().ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(line);
                builder.AppendLine();
            }

            //now that we have built up the data, send it to the host
            SendToHost(builder.ToString());

            Console.WriteLine("Extracted aggregate matrices from Emme");
            return true;
        }

        private float _Progress;
        override public float Progress
        {
            get { return _Progress; }
        }

        private string[] _ParsePythonResults(string results)
        {
            return results.Split('\n');
        }

        override public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        override public bool RuntimeValidation(ref string error)
        {
            return true;
        }

    }
}
