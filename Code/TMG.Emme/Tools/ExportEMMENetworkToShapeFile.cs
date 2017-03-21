using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools
{


    public enum TransitShape
    {
        LINES,
        SEGMENTS,
        LINES_AND_SEGMENTS
    }


    [ModuleInformation(
       Description =
       "This module calls export_network_shapfile tool of TMG Toolbox."
   )]
    public class ExportEMMENetworkToShapeFile : IEmmeTool
    {



        private const string ToolName = "tmg.input_output.export_network_shapefile";


      


        [RunParameter("Transit Shape","SEGMENTS", "The type of transit shape to export.")]
        public TransitShape TransitShape = TransitShape.SEGMENTS;

        [RunParameter("Scenario", 0, "The number of the Emme scenario to use, if the project has multiple scenarios with different zone systems. Not used otherwise.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Output File Path", Required = true)]
        public FileLocation Filepath;

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            Console.WriteLine("Running Export EMME network shape file.");
            return mc.Run(ToolName,
                new[]
                {
                    new ModellerControllerParameter("xtmf_exportPath", Filepath.GetFilePath()),
                    new ModellerControllerParameter("xtmf_transitShapes", TransitShape.ToString()),
                    new ModellerControllerParameter("xtmf_scenario", ScenarioNumber.ToString()),
                });
        }
    

        public bool RuntimeValidation(ref string error)
        {
            if (string.IsNullOrEmpty(Filepath.GetFilePath()))
            {
                error = "Export path cannot be null or empty.";
                return true;
            }

            return false;
        }

        public string Name { get; set; }
        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour { get; }
    }
}
