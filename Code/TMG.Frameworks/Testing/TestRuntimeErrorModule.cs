using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks
{
    class TestRuntimeErrorModule : ISelfContainedModule
    {
        public string Name { get; set; } = "TestRuntimeErrorModule";
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            throw new GenericRuntimeError();
        }
    }

    class GenericRuntimeError : Exception
    {

    }
}
