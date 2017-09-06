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
using TMG.Input;
using XTMF;

namespace TMG.Emme
{
    [ModuleInformation(Description = "Exports the network transaction file (e.g., using Module 2.11) for a given scenario.")]
    public class ExtractEmmeNetworkBacthFile : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme Scenario from which to extract the base network.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Network Batch File", Required = true)]
        public FileLocation ExportFile;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string ToolName = "tmg.XTMF_internal.export_network_batch_file";
        private const string OldToolName = "TMG2.XTMF.exportNetworkBatchFile";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");

            var args = string.Join(" ", ScenarioNumber,
                                        ExportFile.GetFilePath());
            var result = "";
            if(mc.CheckToolExists(this, ToolName))
            {

                return mc.Run(this, ToolName, args, (p => Progress = p), ref result);
            }
            else
            {
                return mc.Run(this, OldToolName, args, (p => Progress = p), ref result);
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
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
