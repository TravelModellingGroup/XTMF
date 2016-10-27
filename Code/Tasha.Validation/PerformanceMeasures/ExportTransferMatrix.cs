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

namespace TMG.Emme.Tools.Analysis.Traffic
{

    public class ExportOperatorTransferMatrix : IEmmeTool
    {
        private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_operator_transfer_matrix";
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario Number", 1, "The scenario to interact with")]
        public int ScenarioNumber;

        [RunParameter("Export Transfer Matrix Flag", true, "Did you want to export the transfer matrix?")]
        public bool ExportTransferMatrixFlag;

        [RunParameter("Export walk all way matrix flag", false, "Did you want to export the walk all way matrix?")]
        public bool ExportWalkAllWayMatrixFlag;

        [SubModelInformation(Required = false, Description = "The location to save the transfer matrix to")]
        public FileLocation TransferMatrixFile;

        [RunParameter("Aggregation Partition for the Walk all way matrix (None if not required)", "None", "The aggregation partition for the walk all way matrix")]
        public string xtmf_AggregationParition;

        [SubModelInformation(Required = false, Description = "The location to save the walk all way matrix to")]
        public FileLocation WalkAllWayMatrixFile;


        [RunParameter("LineGroupOptionOrAttributeId", "", "Description")]
        public string LineGroupOptionOrAttributeId;


        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we require the use of EMME Modeller in order to execute.");
            }
            return modeller.Run(ToolName, GetParameters());
        }

        private ModellerControllerParameter[] GetParameters()
        {
            return new ModellerControllerParameter[]
            {
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("ExportTransferMatrixFlag", ExportTransferMatrixFlag.ToString()),
                new ModellerControllerParameter("ExportWalkAllWayMatrixFlag",ExportWalkAllWayMatrixFlag.ToString()),
                new ModellerControllerParameter("TransferMatrixFile",TransferMatrixFile.GetFilePath()),
                new ModellerControllerParameter("xtmf_AggregationPartition",xtmf_AggregationParition),
                new ModellerControllerParameter("WalkAllWayExportFile",ExportWalkAllWayMatrixFlag ? WalkAllWayMatrixFile.GetFilePath() : "none" ),
                new ModellerControllerParameter("LineGroupOptionOrAttributeId",LineGroupOptionOrAttributeId.ToString())
            };
        }

        public bool RuntimeValidation(ref string error)
        {            
            return true;
        }

        private bool ErrorIfBlank(string flag, string nameOfAttribute, ref string error)
        {
            if (String.IsNullOrWhiteSpace(flag))
            {
                error = "In '" + Name + "' the attribute '" + nameOfAttribute + "' is not assigned to!";
                return true;
            }
            return false;
        }
    }

}
