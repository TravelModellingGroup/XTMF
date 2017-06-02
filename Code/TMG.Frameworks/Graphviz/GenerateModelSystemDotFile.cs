using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Graphviz
{
    public class GenerateModelSystemDotFile : ISelfContainedModule
    {

        public string Name
        {

            get
            {
                return "Generate Model System Dot File";
            }
            set
            {

            }
        }



        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(50, 150, 50); }
        }

        private IConfiguration Configuration;

        private List<IModelSystemStructure> _modelSystemStructure;


        [SubModelInformation(Required = true, Description = "The output path of the generated dot file.")]
        public FileLocation OutputPath;

        [RunParameter("Include Self", false, "Whether or not to include this module in output dot file.")]
        public bool IncludeSelf;

        [RunParameter("Include Unused", true, "Whether or not to include module slots that are unassigned / unused.")]
        public bool IncludeUnused;

        [RunParameter("Model System", "", "The name of the model system under the current project to create a dot file for. Leaving this paramter" +
            "blank will write the model system that is the parent of this module.")]
        public string ModelSystemName;

        [RunParameter("Include Descriptions", false, "Flag for including module descriptions in the generated dot file.")]
        public bool IncludeDescriptions;


        [RunParameter("Include Module Type", false, "Flag for including the module type in the generated dot file.")]
        public bool IncludeModuleType;

        [RunParameter("Post-Generate Command", "", "The command to execute following generation of the dot file")]
        public string PostGenerateCommand;





        public bool RuntimeValidation(ref string error)
        {



            if (string.IsNullOrEmpty(ModelSystemName))
            {
                _modelSystemStructure = TMG.Functions.ModelSystemReflection.BuildModelStructureChain(Configuration, this);



            }
            else
            {

                foreach (var projectRepository in Configuration.ProjectRepository)
                {
                    foreach (var modelSystemStructure in projectRepository.ModelSystemStructure)
                    {
                        if (modelSystemStructure.Name.ToLower() == ModelSystemName.ToLower())
                        {
                            _modelSystemStructure = new List<IModelSystemStructure> { modelSystemStructure };
                        }
                    }
                }
            }

            if (_modelSystemStructure == null)
            {
                error = "Unable to find the model system specified.";
                return false;
            }

            return true;

        }

        public void Start()
        {

            Console.WriteLine("Writing model system dot file...");
            WriteDotFile();
            Console.WriteLine("Finished writing model system dot file");
        }


        public GenerateModelSystemDotFile(IConfiguration config)
        {

            this.Configuration = config;

        }

        private void WriteModuleRanks(List<IModelSystemStructure> sameRankModules, System.IO.StreamWriter writer)
        {

        }




        private void WriteMetaModule(IModelSystemStructure module, StreamWriter writer)
        {

            if (module.IsMetaModule)
            {
                writer.WriteLine($"\"{module.Name}_{module.GetHashCode()}\" " +
          $"[shape=box," +
          $"color=darkblue," +
          $"style=filled,fillcolor=lightblue," +
          $"label=<" +
        $"<FONT COLOR=\"DARKBLUE\" POINT-SIZE=\"16\">{module.Name}</FONT>" +
        $"<BR/>" +
        $"<FONT POINT-SIZE=\"12\">{module.Description}</FONT >" +
        $">" +
                $"];");
            }
            if (module.Children != null)
            {

                foreach (var childModule in module.Children)
                {
                    WriteMetaModule(childModule, writer);


                }
            }
        }



        private void WriteModule(IModelSystemStructure module, System.IO.StreamWriter writer)
        {

            if (module.IsMetaModule)
            {
                return;

            }
            writer.WriteLine($"\"{module.Name}_{module.GetHashCode()}\" " +
                $"[shape=box," +
                $"label=<{module.Name}");

            if (IncludeModuleType && module.Type != null)
            {
                writer.Write($"<BR/><FONT POINT-SIZE=\"10\">{module.Type}</FONT>");
            }

            if (IncludeDescriptions && module.Type != null && !string.IsNullOrEmpty(module.Description))
            {
                writer.Write($"<BR/><FONT POINT-SIZE=\"10\">{module.Description}</FONT>");
            }

            writer.Write(">,");

            writer.Write("]\r\n");
            if (module.Children != null)
            {

                foreach (var childModule in module.Children)
                {
                    WriteModule(childModule, writer);


                }
            }


        }

        private void WriteModuleConnections(IModelSystemStructure module, StreamWriter writer)
        {
            if (module.Required && module.Type == null && !IncludeUnused && !module.IsCollection)
            {
                return;
            }

            if (module.IsMetaModule)
            {
                return;
            }
            //only write connections if the module has children
            if (module.Children != null)
            {
                foreach (var child in module.Children)
                {
                    writer.Write($"\"{module.Name}_{module.GetHashCode()}\" -- \"{child.Name}_{child.GetHashCode()}\"\r\n");
                    WriteModuleConnections(child, writer);
                }
            }
        }

        private void WriteDotFile()
        {


            using (System.IO.StreamWriter dotFile =
           new System.IO.StreamWriter(OutputPath.GetFilePath()))
            {
                dotFile.Write("graph xtmf_model_system_graph { \r\n");
                WriteModule(_modelSystemStructure[0], dotFile);
                WriteMetaModule(_modelSystemStructure[0], dotFile);
                WriteModuleConnections(_modelSystemStructure[0], dotFile);
                dotFile.Write("}\r\n");
            }
        }
    }
}
