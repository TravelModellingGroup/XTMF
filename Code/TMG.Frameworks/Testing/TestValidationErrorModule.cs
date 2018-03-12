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
    public class TestValidationErrorModule : ISelfContainedModule
    {
        public string Name { get; set; } = "TestValidationErrorModule";
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        [SubModelInformation(Required = true)]
        public IModule RequiredSubModule { get; set; }

        public bool RuntimeValidation(ref string error)
        {
            error =  "Generic Validation Error";
            return false;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }

}
