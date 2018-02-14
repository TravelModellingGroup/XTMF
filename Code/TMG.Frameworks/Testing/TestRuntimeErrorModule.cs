using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Testing
{
    [ModuleInformation(Description =
        @"A dummy module that can be used as a root module in model systems used for testing the GUI.")]
    public class TestRuntimeErrorModule : ISelfContainedModule
    {
        private float _progress = 0;
        public string Name { get; set; }
        public float Progress { get => _progress; }
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

    public class GenericRuntimeError : Exception
    {

    }
}
