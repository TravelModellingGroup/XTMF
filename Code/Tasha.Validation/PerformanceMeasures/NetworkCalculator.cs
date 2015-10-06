using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;
using TMG.Emme;

namespace Tasha.Validation.PerformanceMeasures
{
    public class NetworkCalculator : IEmmeTool
    {
        private const string _ToolName = "tmg.XTMF_internal.xtmf_network_calculator";

        [RunParameter("Scenario Number", "1", "What scenario would you like to run this for?")]
        public int ScenarioNumber;

        [RunParameter("Expression", "", "What is the expression you want to compute?")]
        public string Expression;

        [RunParameter("Node Selection", "all", "What specific nodes would you like to include in the calculation? Default=all")]
        public string Node_Selection;

        [RunParameter("Link Selection", "all", "What specific links would you like to include in the calculation? Default=all")]
        public string Link_Selection;

        [RunParameter("Transit Line Selection", "all", "What specific transit lines would you like to include in the calculation? Default=all")]
        public string Transit_Line_Selection;

        [SubModelInformation(Required = true, Description = "Resource that will store the sum of the network calculation")]
        public IResource SumOfReport;

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were not given a modeller controller!");
            }
            float value;
            string result = null;
            if(modeller.Run(_ToolName, GetParameters(), ref result))
            {
                if (float.TryParse(result, out value))
                {
                    ((ISetableDataSource<float>)SumOfReport.GetDataSource()).SetData(value);
                }
                return true;
            }
            return false;
        }

        private string GetParameters()
        {
            return string.Join(" ", ScenarioNumber, AddQuotes(Expression), AddQuotes(Node_Selection), AddQuotes(Link_Selection), AddQuotes(Transit_Line_Selection));
        }

        private static string AddQuotes(string toQuote)
        {
            return String.Concat("\"", toQuote, "\"");
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

        public bool RuntimeValidation(ref string error)
        {
            if (!SumOfReport.CheckResourceType<float>())
            {
                error = "";
                return false;
            }
            if (!(SumOfReport.GetDataSource() is ISetableDataSource<float>))
            {
                error = "";
                return false;
            }
            return true;
        }

    }
}
