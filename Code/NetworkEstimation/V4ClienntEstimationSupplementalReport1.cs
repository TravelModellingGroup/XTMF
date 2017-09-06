using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMG.Emme;
using TMG.Estimation;
using TMG.Estimation.Utilities;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description= "Produces a report of initial and transfer boardings, alongside genome results")]
    public class V4ClienntEstimationSupplementalReport1 : ClientFileAggregation, IEmmeTool
    {
        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Scenario", 0, "The Emme scenario from which to extract results.")]
        public int ScenarioNumber;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string ToolName = "tmg.XTMF_internal.return_boarding_types";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController");
            }
            string result = "";
            mc.Run(this, ToolName, ScenarioNumber.ToString(), (p => _Progress = p), ref result);
            var modelResults = _ParsePythonResults(result);

            StringBuilder builder = new StringBuilder();
            builder.Append(Root.CurrentTask.Generation);
            builder.Append(',');
            builder.Append(Root.CurrentTask.Index);
            builder.Append(',');
            var func = Root.RetrieveValue;
            builder.Append((func == null) ? "null" : func().ToString(CultureInfo.InvariantCulture));
            foreach (var f in modelResults)
            {
                builder.Append(',');
                builder.Append(f);
            }
            foreach (var val in Root.CurrentTask.ParameterValues)
            {
                builder.Append(',');
                builder.Append(val.ToString(CultureInfo.InvariantCulture));
            }
            builder.AppendLine();

            //now that we have built up the data, send it to the host
            SendToHost(builder.ToString());
            Console.WriteLine("Extracted line boardings from Emme.");
            return true;
        }

        private float _Progress;
        override public float Progress
        {
            get { return _Progress; }
        }

        private List<float> _ParsePythonResults(string results)
        {
            var retVal = new List<float>();
            results = results.Trim().Replace("[", "").Replace("]", "");

            foreach (var cell in results.Split(','))
            {
                retVal.Add(float.Parse(cell));
            }

            return retVal;
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
