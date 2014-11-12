using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG.Emme;

namespace James.UTDM
{
    public class TestNewModellerControllerFeatures : IEmmeTool
    {
        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a ModellerController" );

            /*
            bool isTrue = mc.CheckToolExists("TMG2.Common.Editing");
            bool isFalse = mc.CheckToolExists("Garbage namespace");

            isTrue.ToString();
            isFalse.ToString();
            
            throw new NotImplementedException();
             * */

            return true;
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
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
