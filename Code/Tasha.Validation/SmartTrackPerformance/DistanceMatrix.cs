using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using TMG.Emme;
using Tasha.Common;
using XTMF;


namespace Tasha.Validation.SmartTrackPerformance 
{
    public class DistanceMatrixCalculation : IEmmeTool
    {

        [SubModelInformation(Required = true, Description = "Distance Matrix .CSV name")]
        public FileLocation DistanceMatrix;

        [RunParameter("Scenario Number", 12, "Which scenario number would you like to get the distances from?")]
        public int scenarioNumber;

        private const string _ToolName = "org.emme.Distance";

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
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            var args = string.Join(" ", scenarioNumber, "\"" + DistanceMatrix.GetFilePath() + "\"");

            bool emmeRun;
            emmeRun = mc.Run(_ToolName, args);            

            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
