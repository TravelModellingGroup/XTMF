using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string _ToolName = "tmg.analysis.transit.export_boardings";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var reportFilePath = "\"" + Path.GetFullPath(ReportFile.GetFilePath()) + "\"";
            var aggregationFilePath = LineAggregationFile == null ? "" :  "\"" + LineAggregationFile.GetFilePath() + "\"";

            var args = string.Join(" ", ScenarioNumber,
                                        reportFilePath,
                                        aggregationFilePath);

            var result = "";
            return mc.Run(_ToolName, args, (p => Progress = p), ref result);
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
