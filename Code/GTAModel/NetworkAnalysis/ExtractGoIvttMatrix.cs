using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    public class ExtractGoIvttMatrix : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme scenario from which to extract results.")]
        public int ScenarioNumber;

        [RunParameter("Result Matrix", 5, "The number of the full matrix in which to store extracted results. It will be created if it does not already exist.")]
        public int ResultMatrixNumber;

        private const string _ToolName = "TMG2.Analysis.Transit.Strategies.ExtractGoIvttMatrix";

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);


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

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");


            string args = string.Join(" ", this.ScenarioNumber, "mf" + this.ResultMatrixNumber);
            string result = null;
            return mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

    }
}
