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
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var result = "";

            var outputPath = Path.GetFullPath(Filepath.GetFilePath());
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();

                writer.WritePropertyName("xtmf_exportPath");
                writer.WriteValue(outputPath);

                writer.WritePropertyName("xtmf_transitShapes");
                writer.WriteValue(this.TransitShape.ToString());

                writer.WritePropertyName("xtmf_scenario");
                writer.WriteValue(this.ScenarioNumber);

                writer.WriteEndObject();
            }

            var args = sb.ToString();

            if (mc.CheckToolExists(ToolName))
            {
               return mc.Run(ToolName, args, (p => Progress = p), ref result);
            }
         
            return true;
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
