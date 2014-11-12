using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Emme;
using XTMF;

namespace James.UTDM
{
    public class TestModellerPrint : IEmmeTool
    {

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var ns = "TMG2.XTMF.testPrint";
            var retval = mc.Run(ns, "");

            return retval;
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0.0f; }
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
