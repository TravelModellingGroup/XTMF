using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Testing
{
    /// <summary>
    /// A dummmy / testing module to be used to help simulate estimation and scheduling for the XTMF GUI.
    /// </summary>
    [ModuleInformation(Description =
        @"A dummy module that can be used as a root module in model systems used for testing the GUI.")]
    public class TestRootModule : IModelSystemTemplate
    {
        public string Name { get; set; }
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        [SubModelInformation(Description = "Child Modules",Required = false)]
        public List<ISelfContainedModule> ChildModules { get; set; }

  
        private IConfiguration _configuration;

        public bool RuntimeValidation(ref string error)
        {

            return true;
        }

        public void Start()
        {
            Console.WriteLine("Starting model system");
            ChildModules.ForEach((module) => module.Start());
            return;
        }


        public TestRootModule(IConfiguration configuration)
        {
            this._configuration = configuration;

        }

        [RunParameter("Input Directory", "../../Input", "The input directory for the Model System")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Input Directory", "../../Output", "The output directory for the Model System")]
        public string OutputBaseDirectory { get; set; }


        public bool ExitRequest()
        {

            return true;
        }
    }
}
