using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation(Description = "Extracts real (raw) transit in-vehicle times matrix from " +
                         "any type of Extended Transit Assignment.In particular, this tool " +
                         "auto - detects if a congested or capacitated assignment has been run " +
                         "and compensates for the additional crowding term.")]
    class ExtractTrueTransitIvttMatrix : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme scenario with transit assignment results.")]
        public int ScenarioNumber;

        [RunParameter("Result Matrix Number", 1, "The number of the full matrix in which to store the results.")]
        public int MatrixNumber;

        private const string _ToolName = "TMG2.Analysis.Transit.Strategies.ExtractRawIvttMatrix";

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", this.ScenarioNumber, "mf" + this.MatrixNumber);

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
