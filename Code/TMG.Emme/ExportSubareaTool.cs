using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;
using TMG.Input;


namespace TMG.Emme
{

    public class ExportSubareaTool : IEmmeTool
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario Number", 0, "The scenario number to execute against.")]
        public int ScenarioNumber;

        [SubModelInformation(Required = false, Description = "A link to the shapefile to specify boundary of the subarea")]
        public FileLocation ShapefileLocation;

        [SubModelInformation(Required = true, Description = "A link to the folder to output the subarea database")]
        public FileLocation SubareaOutputFolder;

        [RunParameter("Extract Transit", true, "Whether or not the tool will extract out transit line and demand information from the scenario")]
        public bool ExtractTransit;

        [RunParameter("Subarea Node Attribute","None", "The node attribute that will be used to define the subarea. Can be specified along with a shapefile or by itself. One of the shapefile or an node attribute must be defined")]
        public string SubareaNodeAttribute;

        public string GetAttribute(string at)
        {
            char a = at[0];
            if (at == "None" || at == "" || at == " ")
            {
                return "None";
            }
            if (a is '@')
            {
                return at;
            }
            else
            {
                return "@" + at;
            }
                
        }
        [RunParameter("Starting Node Number", 1, "Starting node range for the gate labels if they arent defined by a link extra attribute parameter")]
        public int StartingNodeNumber;

        [RunParameter("Gate Labels", "None", "If you have a link extra attribute with your defined gate numbers, enter it here")]
        public string GateLabel;

        [SubModelInformation(Required = true, Description = "The SOLA parameters to perform the analysis")]
        public SOLA _SOLA;

        public sealed class SOLA : IModule
        {
            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            [SubModelInformation(Description = "The classes for the SOLA Road Assignment.")]
            public Class[] Classes;

            [RunParameter("Peak Hour Factor", 0f, "A factor to apply to the demand in order to build a representative hour.")]
            public float PeakHourFactor;

            [RunParameter("Iterations", 0, "The maximum number of iterations to run.")]
            public int Iterations;

            [RunParameter("Relative Gap", 0f, "The minimum gap required to terminate the algorithm.")]
            public float RelativeGap;

            [RunParameter("Best Relative Gap", 0f, "The minimum gap required to terminate the algorithm.")]
            public float BestRelativeGap;

            [RunParameter("Normalized Gap", 0f, "The minimum gap required to terminate the algorithm.")]
            public float NormalizedGap;

            [RunParameter("Performance Mode", true, "Set this to false to leave a free core for other work, recommended to leave set to true.")]
            public bool PerformanceMode;

            public sealed class Class : IModule
            {
                [RunParameter("Mode", 'c', "The mode for this class.")]
                public char Mode;

                [RunParameter("Demand Matrix", 0, "The id of the demand matrix to use.")]
                public int DemandMatrixNumber;

                [RunParameter("VolumeAttribute", "@auto_volume1", "The name of the attribute to save the volumes into (or None for no saving).")]
                public string VolumeAttribute;

                [RunParameter("TollAttributeID", "@toll", "The attribute containing the road tolls for this class of vehicle.")]
                public string LinkTollAttributeID;

                [RunParameter("Toll Weight", 0f, "")]
                public float TollWeight;

                [RunParameter("LinkCost", 0f, "The penalty in minutes per dollar to apply when traversing a link.")]
                public float LinkCost;


                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    if (Mode >= 'a' && Mode <= 'z' || Mode >= 'A' && Mode <= 'Z')
                    {
                        error = "In '" + Name + "' the Mode '" + Mode + "' is not a feasible mode for multi class assignment!";
                        return true;
                    }
                    if (TollWeight <= 0.0000001)
                    {
                        error = "In '" + Name + "' the Toll Weight cannot be less than or equal to 0!";
                        return true;
                    }

                    return false;


                }
            }

            public bool RuntimeValidation(ref string error)
            {
                foreach (var c in Classes)
                {
                    if (!c.RuntimeValidation(ref error))
                    {
                        return false;
                    }
                }

                return true;
            }

        }
        const string ToolName = "tmg.input_output.export_subarea_tool";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "TMG.Emme.NetworkAssignment.MultiClassRoadAssignment requires the use of EMME Modeller and will not work through command prompt!");
            }


            string ret = null;
            if (!mc.CheckToolExists(this, ToolName))
            {
                throw new XTMFRuntimeException(this, "There was no tool with the name '" + ToolName + "' available in the EMME databank!");
            }
            return mc.Run(this, ToolName, GetParameters(), (p) => Progress = p, ref ret);
        }

        private ModellerControllerParameter[] GetParameters()
        {
            /*
                def __call__(self, xtmf_ScenarioNumber, xtmf_PeakHourFactor, xtmf_ModeList, xtmf_NameString, xtmf_AutoDemand, xtmf_LinkCosts, xtmf_LinkTollAttributeIds, 
                 xtmf_TollWeights, xtmf_ResultAttributes, xtmf_MaxIterations, xtmf_rGap, xtmf_brGap, xtmf_normGap, xtmf_PerformanceFlag, 
                 xtmf_ExtractTransitFlag, xtmf_SubareaNodeAttribute, xtmf_SubareaFolderPath, xtmf_NodeNumberStarting, xtmf_ShapefileLocation, xtmf_GateLabel):
            */
            return new[]
            {
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("xtmf_PeakHourFactor", _SOLA.PeakHourFactor.ToString()),
                new ModellerControllerParameter("xtmf_ModeList", GetClasses()),
                new ModellerControllerParameter("xtmf_NameString", string.Join(",", _SOLA.Classes.Select(c => c.Name))),
                new ModellerControllerParameter("xtmf_AutoDemand", GetDemand()),
                new ModellerControllerParameter("xtmf_LinkCosts", string.Join(",", _SOLA.Classes.Select(c => c.LinkCost.ToString()))),
                new ModellerControllerParameter("xtmf_LinkTollAttributeIds", string.Join(",", _SOLA.Classes.Select(c => c.LinkTollAttributeID))),
                new ModellerControllerParameter("xtmf_TollWeights", string.Join(",", _SOLA.Classes.Select(c => c.TollWeight.ToString()))),
                new ModellerControllerParameter("xtmf_ResultAttributes", string.Join(",", _SOLA.Classes.Select(c => c.VolumeAttribute))),
                new ModellerControllerParameter("xtmf_MaxIterations", _SOLA.Iterations.ToString()),
                new ModellerControllerParameter("xtmf_rGap", _SOLA.RelativeGap.ToString()),
                new ModellerControllerParameter("xtmf_brGap", _SOLA.BestRelativeGap.ToString()),
                new ModellerControllerParameter("xtmf_normGap", _SOLA.NormalizedGap.ToString()),
                new ModellerControllerParameter("xtmf_PerformanceFlag", _SOLA.PerformanceMode.ToString()),
                new ModellerControllerParameter("xtmf_ExtractTransitFlag", ExtractTransit.ToString()),
                new ModellerControllerParameter("xtmf_SubareaNodeAttribute", GetAttribute(SubareaNodeAttribute)),
                new ModellerControllerParameter("xtmf_SubareaFolderPath", GetFileLocationOrNone(SubareaOutputFolder)),
                new ModellerControllerParameter("xtmf_NodeNumberStarting", StartingNodeNumber.ToString()),
                new ModellerControllerParameter("xtmf_ShapefileLocation", GetFileLocationOrNone(ShapefileLocation)),
                new ModellerControllerParameter("xtmf_GateLabel", GetAttribute(GateLabel))
            };
        }

        private string GetClasses()
        {
            return string.Join(",", _SOLA.Classes.Select(c => c.Mode.ToString()));
        }

        private string GetDemand()
        {
            return string.Join(",", _SOLA.Classes.Select(c => "mf" + c.DemandMatrixNumber.ToString()));
        }
        private static string GetFileLocationOrNone(FileLocation location)
        {
            return location == null ? "None" : Path.GetFullPath(location.GetFilePath());
        }

        public bool RuntimeValidation(ref string error)
        {
            if (StartingNodeNumber < 1)
            {
                return false;
            }
            foreach (var c in _SOLA.Classes)
            {
                if (!c.RuntimeValidation(ref error))
                {
                    return false;
                }
            }

            return true;
        }
    }

}

