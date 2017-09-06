/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
    public class ExportTransitBoardings : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The Emme scenario from which to extract results.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Report File", Required = true)]
        public FileLocation ReportFile;

        [SubModelInformation(Description = "Line Aggregation File", Required = false)]
        public FileLocation LineAggregationFile;

        [RunParameter("WriteIndividualRoutes", true, "If a line is not included in an aggregation file, should it be written out separately in the repot file?")]
        public bool WriteIndividualRoutes;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string ToolName = "tmg.analysis.transit.export_boardings";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
            var result = "";
            return mc.Run(this, ToolName, new []
            {
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("ReportFile", ReportFile.GetFilePath()),
                new ModellerControllerParameter("LineAggregationFile", LineAggregationFile?.GetFilePath() ?? ""),
                new ModellerControllerParameter("WriteIndividualRoutesFlag", WriteIndividualRoutes.ToString())
            }, (p => Progress = p), ref result);
        }

        public string Name
        {
            get; set;
        }

        public float Progress
        {
            get; private set;
        }

        public Tuple<byte, byte, byte> ProgressColour => _ProgressColour;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
