using System;
using Datastructure;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;
using TMG.Input;


namespace TMG.Emme.NetworkAssignment
{
    public class TransitAssignmentTool : IEmmeTool
    {
        private const string ToolName = "tmg.XTMF_internal.tmg_transit_assignment_tool";

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);



        /*public bool RuntimeValidation(ref string error)
        {
            return true;
        }
        */

        [RunParameter("Scenario Number", 0, "Emme Scenario Number")]
        public int ScenarioNumber;

        [Parameter("Headway Fraction Attribute", "@hfrac", "The ID of the NODE extra attribute in which to store headway fraction. Should have a default value of 0.5.")]
        public string HeadwayFractionAttribute;

        [Parameter("Effective Headway Attribute", "@ehdw", "The name of the attribute to use for the effective headway")]
        public string EffectiveHeadwayAttributeId;

        //-------------------------------------------

        [RunParameter("Effective Headway Slope", 0.5f, "")]
        public float EffectiveHeadwaySlope;

        [Parameter("Walk Speed", 4.0f, "Walking speed in km/hr. Applied to all walk (aux. transit) modes in the Emme scenario.")]
        public float WalkSpeed;

        [RunParameter("Representative Hour Factor", 2.04f, "A multiplier applied to the demand matrix to scale it to match" +
                    " the transit line capacity period. This is similar to the peak hour factor used in auto assignment.")]
        public float RepresentativeHourFactor;

        //-------------------------------------------

        [RunParameter("Iterations", 20, "Convergence criterion: The maximum number of iterations performed by the transit assignment.")]
        public int MaxIterations;

        [RunParameter("Normalized Gap", 0.01f, "Convergence criterion")]
        public float NormalizedGap;

        [RunParameter("Relative Gap", 0.001f, "Convergence criterion")]
        public float RelativeGap;

        [Parameter("Add Congestion to IVTT", true, "Set to TRUE to extract the congestion matrix and add its weighted value to the in vehicle time (IVTT) matrix.")]
        public bool ExtractCongestedInVehicleTimeFlag;

        [Parameter("Allow Walk all Way", false, "Set to TRUE to allow walk all way in the assignment")]
        public bool WalkAllWayFlag;

        [Parameter("Connector Logit Scale", 0.2f, "Scale parameter for logit model at origin connectors.")]
        public float ConnectorLogitScale;

        [Parameter("Logit Scale at Critical Nodes", 1.0f, "This is the scale parameter for the logit model at critical nodes. Set it to 1 to turn it off logit. Set it to 0 to ensure equal proportion on all connected auxiliary transfer links. Critical nodes are defined as the non centroid end of centroid connectors and nodes that have transit lines from more than one agency")]
        public float NodeLogitScale;

        [RunParameter("Apply Congestion", true, "Set this to false in order to not apply congestion during assignment.")]
        public bool ApplyCongestion;

        //[RunParameter("Warm Start", false, "Set this to false in order to not apply congestion during assignment.")]
        //public bool ApplyCongestion;
        [RunParameter("Exclusive ROW TTF Range", "2", typeof(RangeSet), "Set this to the TTF, TTFs or range of TTFs (seperated by commas) that represent going in an exclusive right of way. This is for use in STSU")]
        public RangeSet XRowTTF;

        [SubModelInformation(Description = "The classes for this multi-class assignment.")]
        public Class[] Classes;

        [SubModelInformation(Description = "The TTF's to apply in the assignment.")]
        // ReSharper disable once InconsistentNaming
        public TTFDefinitions[] TTF;

        [SubModelInformation(Description = "Surface Transit Speed Model", Required = false)]
        public SurfaceTransitSpeed[] SurfaceTransitSpeedModel;

        [SubModelInformation(Required = false, Description = "A link to the csv file that will specify iterational information")]
        public FileLocation IterationCSVFile;

        public bool Execute(Controller controller)
        {
            Progress = 0;
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
            }

            string walkPerception = String.Empty;

            // join all walk perceptions into one string
            foreach (var c in Classes)
            {
                walkPerception = string.Join(";", walkPerception, string.Join("::", c.WalkPerceptions.Select(walk => Controller.ToEmmeFloat(walk.WalkValue) + ":" + walk.LineFilter)));

            }
            walkPerception = '\"' + walkPerception.Substring(1, walkPerception.Length - 1) + '\"';

            ModellerControllerParameter[] GetParameters()
            {
                return new[]
                {
                new ModellerControllerParameter("xtmf_ScenarioNumber",ScenarioNumber.ToString()),
                new ModellerControllerParameter("xtmf_DemandMatrixString",ProduceMatrixString(c => c.DemandMatrixNumber)),
                new ModellerControllerParameter("xtmf_NameString", (string.Join(",", from c in Classes
                                                                select c.Name))),
                new ModellerControllerParameter("WalkSpeed",WalkSpeed.ToString()),
                new ModellerControllerParameter("xtmf_WalkPerceptionString",walkPerception),
                new ModellerControllerParameter("xtmf_WalkPerceptionAttributeIdString", string.Join(",", from c in Classes
                                                                select c.WalkPerceptionAttribute)),
                new ModellerControllerParameter("xtmf_ClassWaitPerceptionString", string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.WaitTimePerception))),
                new ModellerControllerParameter("xtmf_ClassBoardPerceptionString", string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.BoardingPerception))),
                new ModellerControllerParameter("xtmf_ClassFarePerceptionString", string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.FarePerception))),
                new ModellerControllerParameter("xtmf_ClassModeList", string.Join(",", from c in Classes
                                                                select c.ModeList)),
                new ModellerControllerParameter("HeadwayFractionAttributeId",HeadwayFractionAttribute),
                new ModellerControllerParameter("xtmf_LinkFareAttributeIdString", string.Join(",", from c in Classes
                                                                 select c.LinkFareAttribute)),
                new ModellerControllerParameter("xtmf_SegmentFareAttributeIdString", string.Join(",", from c in Classes
                                                                 select c.SegmentFareAttribute)),
                new ModellerControllerParameter("EffectiveHeadwayAttributeId",EffectiveHeadwayAttributeId.ToString()),
                new ModellerControllerParameter("EffectiveHeadwaySlope",Controller.ToEmmeFloat(EffectiveHeadwaySlope)),
                new ModellerControllerParameter("AssignmentPeriod",Controller.ToEmmeFloat(RepresentativeHourFactor)),
                new ModellerControllerParameter("Iterations",MaxIterations.ToString()),
                new ModellerControllerParameter("NormGap",Controller.ToEmmeFloat(NormalizedGap)),
                new ModellerControllerParameter("RelGap",Controller.ToEmmeFloat(RelativeGap)),
                new ModellerControllerParameter("xtmf_InVehicleTimeMatrixString",ProduceMatrixString(c => c.InVehicleMatrixNumber)),
                new ModellerControllerParameter("xtmf_WaitTimeMatrixString",ProduceMatrixString(c => c.WaitMatrixNumber)),
                new ModellerControllerParameter("xtmf_WalkTimeMatrixString",ProduceMatrixString(c => c.WalkMatrixNumber)),
                new ModellerControllerParameter("xtmf_FareMatrixString",ProduceMatrixString(c => c.FareMatrixNumber)),
                new ModellerControllerParameter("xtmf_CongestionMatrixString",ProduceMatrixString(c => c.CongestionMatrixNumber)),
                new ModellerControllerParameter("xtmf_PenaltyMatrixString",ProduceMatrixString(c => c.BoardingPenaltyMatrixNumber)),
                new ModellerControllerParameter("xtmf_ImpedanceMatrixString",ProduceMatrixString(c => c.PerceivedTravelTimeMatrixNumber)),
                new ModellerControllerParameter("xtmf_OriginDistributionLogitScale",Controller.ToEmmeFloat(ConnectorLogitScale)),
                new ModellerControllerParameter("CalculateCongestedIvttFlag",ExtractCongestedInVehicleTimeFlag.ToString()),
                new ModellerControllerParameter("CongestionExponentString",string.Join(",", from ttf in TTF
                                                            select ttf.TTFNumber.ToString() + ":"
                                                            + Controller.ToEmmeFloat(ttf.CongestionPerception) + ":"
                                                            + Controller.ToEmmeFloat(ttf.CongestionExponent))),
                new ModellerControllerParameter("xtmf_congestedAssignment", ApplyCongestion.ToString()),
                new ModellerControllerParameter("xtmf_CSVFile", GetFileLocationOrNone(IterationCSVFile)),
                new ModellerControllerParameter("xtmf_SurfaceTransitSpeed", GetSurfaceSpeedModel()),
                new ModellerControllerParameter("xtmf_WalkAllWayFlag", WalkAllWayFlag.ToString()),
                new ModellerControllerParameter("xtmf_XRowTTFRange", XRowTTF.ToString()),
                new ModellerControllerParameter("xtmf_NodeLogitScale",NodeLogitScale.ToString())

                };
            }


            /*def __call__(self, xtmf_ScenarioNumber, xtmf_DemandMatrixString, xtmf_NameString,\
        WalkSpeed, xtmf_WalkPerceptionString, xtmf_WalkPerceptionAttributeIdString, \
        xtmf_ClassWaitPerceptionString, xtmf_ClassBoardPerceptionString, xtmf_ClassFarePerceptionString, xtmf_ClassModeList,\
        HeadwayFractionAttributeId, xtmf_LinkFareAttributeIdString, xtmf_SegmentFareAttributeIdString, \
        EffectiveHeadwayAttributeId, EffectiveHeadwaySlope,  AssignmentPeriod, \
        Iterations, NormGap, RelGap, \
        xtmf_InVehicleTimeMatrixString, xtmf_WaitTimeMatrixString, xtmf_WalkTimeMatrixString, xtmf_FareMatrixString, xtmf_CongestionMatrixString, xtmf_PenaltyMatrixString, xtmf_ImpedanceMatrixString, \
        xtmf_OriginDistributionLogitScale, CalculateCongestedIvttFlag, CongestionExponentString, xtmf_congestedAssignment):
            */

            var args = string.Join(" ", ScenarioNumber,
                                        ProduceMatrixString(c => c.DemandMatrixNumber),
                                        "\"" + (string.Join(",", from c in Classes
                                                                 select c.Name)).Replace('"', '\'') + "\"",
                                        Controller.ToEmmeFloat(WalkSpeed),
                                        walkPerception,
                                        "\"" + (string.Join(",", from c in Classes
                                                                 select c.WalkPerceptionAttribute)).Replace('"', '\'') + "\"",
                                        "\"" + string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.WaitTimePerception)) + "\"",
                                        "\"" + string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.BoardingPerception)) + "\"",
                                        "\"" + string.Join(",", from c in Classes
                                                                select Controller.ToEmmeFloat(c.FarePerception)) + "\"",
                                        "\"" + string.Join(",", from c in Classes
                                                                select c.ModeList) + "\"",
                                        HeadwayFractionAttribute,
                                        "\"" + (string.Join(",", from c in Classes
                                                                 select c.LinkFareAttribute)).Replace('"', '\'') + "\"",
                                        "\"" + (string.Join(",", from c in Classes
                                                                 select c.SegmentFareAttribute)).Replace('"', '\'') + "\"",
                                        EffectiveHeadwayAttributeId,
                                        Controller.ToEmmeFloat(EffectiveHeadwaySlope),
                                        Controller.ToEmmeFloat(RepresentativeHourFactor),
                                        MaxIterations,
                                        Controller.ToEmmeFloat(NormalizedGap),
                                        Controller.ToEmmeFloat(RelativeGap),
                                        ProduceMatrixString(c => c.InVehicleMatrixNumber),
                                        ProduceMatrixString(c => c.WaitMatrixNumber),
                                        ProduceMatrixString(c => c.WalkMatrixNumber),
                                        ProduceMatrixString(c => c.FareMatrixNumber),
                                        ProduceMatrixString(c => c.CongestionMatrixNumber),
                                        ProduceMatrixString(c => c.BoardingPenaltyMatrixNumber),
                                        ProduceMatrixString(c => c.PerceivedTravelTimeMatrixNumber),
                                        Controller.ToEmmeFloat(ConnectorLogitScale),
                                        ExtractCongestedInVehicleTimeFlag,
                                        string.Join(",", from ttf in TTF
                                                         select ttf.TTFNumber.ToString() + ":"
                                                         + Controller.ToEmmeFloat(ttf.CongestionPerception) + ":"
                                                         + Controller.ToEmmeFloat(ttf.CongestionExponent)),
                                        ApplyCongestion,
                                        GetFileLocationOrNone(IterationCSVFile)
                                        );
            if (SurfaceTransitSpeedModel != null)
            {
                args = string.Join(" ", args, "\"" + string.Join(",", from model in SurfaceTransitSpeedModel
                                                                      select Controller.ToEmmeFloat(model.BoardingDuration) + ":"
                                                                      + Controller.ToEmmeFloat(model.AlightingDuration) + ":"
                                                                      + Controller.ToEmmeFloat(model.DefaultDuration) + ":"
                                                                      + Controller.ToEmmeFloat(model.Correlation) + ":"
                                                                      + model.ModeFilterExpression + ":"
                                                                      + model.LineFilterExpression + ":"
                                                                      + Controller.ToEmmeFloat(model.ErowSpeed)
                                                               ) + "\"");
            }
            else
            {
                args = string.Join(" ", args, false.ToString());
            }

            var result = "";


            //return mc.Run(this, ToolName, GetParameters(), (p => Progress = p), ref result);
            return mc.Run(this, ToolName, GetParameters(), (p => Progress = p), ref result);
        }

        private string GetSurfaceSpeedModel()
        {
            string surfaceSpeedModel;
            if (SurfaceTransitSpeedModel != null)
            {
                surfaceSpeedModel = string.Join(",", from model in SurfaceTransitSpeedModel
                                                     select Controller.ToEmmeFloat(model.BoardingDuration) + ":"
                                         + Controller.ToEmmeFloat(model.AlightingDuration) + ":"
                                         + Controller.ToEmmeFloat(model.DefaultDuration) + ":"
                                         + Controller.ToEmmeFloat(model.Correlation) + ":"
                                         + model.ModeFilterExpression + ":"
                                         + model.LineFilterExpression + ":"
                                         + Controller.ToEmmeFloat(model.ErowSpeed));
            }
            else
            {
                surfaceSpeedModel = false.ToString();
            }
            return surfaceSpeedModel;
        }

        private static string GetFileLocationOrNone(FileLocation location)
        {
            return location == null ? "None" : Path.GetFullPath(location.GetFilePath());
        }

        private string ProduceMatrixString(Func<Class, int> matrixNumber)
        {

            return string.Join(",", Classes.Select(c => "mf" + matrixNumber(c).ToString()));
        }


        public class Class : IModule
        {
            [RunParameter("Demand Matrix Number", 1, "The number of the full matrix containing transit demand ODs")]
            public int DemandMatrixNumber;

            [Parameter("Walk Perception Attribute", "@walkp", "The ID of the LINK extra attribute in which to store walk time perception. Should have a default value of 1.0.")]
            public string WalkPerceptionAttribute;

            [Parameter("Link Fare Attribute", "@lfare", "The ID of the LINK extra attribute containing actual fare costs.")]
            public string LinkFareAttribute;

            [Parameter("Segment Fare Attribute", "@sfare", "The ID of the SEGMENT extra attribute containing actual fare costs.")]
            public string SegmentFareAttribute;

            [RunParameter("In-vehicle Times Matrix", 0, "The number of the FULL matrix in which to save in-vehicle travel time. Enter 0 to skip saving this matrix")]
            public int InVehicleMatrixNumber;

            [RunParameter("WalkTimes Matrix", 0, "The number of the FULL matrix in which to save total walk time. Enter 0 to skip saving this matrix")]
            public int WalkMatrixNumber;

            [RunParameter("Wait Times Matrix", 0, "The number of the FULL matrix in which to save total waiting time. Enter 0 to skip saving this matrix")]
            public int WaitMatrixNumber;

            [RunParameter("Fare Matrix", 0, "The number of the FULL matrix in which to save transit fares. Enter 0 to skip saving this matrix")]
            public int FareMatrixNumber;

            [RunParameter("Perceived Travel Time Matrix", 0, "The number of the FULL matrix in which to save the incurred penalties. Enter 0 to skip saving this matrix")]
            public int PerceivedTravelTimeMatrixNumber;

            [RunParameter("Boarding Penalty Matrix", 0, "The number of the FULL matrix in which to save the applied boarding penalties.  Enter 0 to skip this matrix.")]
            public int BoardingPenaltyMatrixNumber;

            [RunParameter("Congestion Matrix", 0, "The number of the FULL matrix in which to save transit congestion. Enter 0 to skip saving this matrix")]
            public int CongestionMatrixNumber;

            [RunParameter("Boarding Penalty Perception", 1.0f, "Perception factor applied to boarding penalty component.")]
            public float BoardingPerception;

            [RunParameter("Fare Perception", 0.0f, "Perception factor applied to path transit fares, in $/hr.")]
            public float FarePerception;

            [RunParameter("Wait Time Perception", 1.0f, "Perception factor applied to wait time component.")]
            public float WaitTimePerception;

            [RunParameter("Modes", "*", "A character array of all the modes applied to this class. \'*\' selects all.")]
            public string ModeList;

            public bool RuntimeValidation(ref string error)
            {
                if (DemandMatrixNumber <= 0)
                {
                    error = "In '" + Name + "' the demand matrix number must be a non-zero positive integer.";
                    return false;
                }
                return true;

            }

            [SubModelInformation(Description = "The classes for this multi-class assignment.")]
            public WalkPerceptionSegment[] WalkPerceptions;

            public sealed class WalkPerceptionSegment : IModule
            {
                [RunParameter("Walk Perception Value", 1.0f, "The walk perception on links.")]
                public float WalkValue;

                [RunParameter("Filter", "i=10000,20000 or j=10000,20000", "The filter expression for links that the perception applies to")]
                public string LineFilter;

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    if (WalkValue < 0)
                    {
                        error = "In '" + Name + "' walk perception value must be greater than or equal 0.";
                        return false;
                    }
                    return true;

                }
            }

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        }

        // ReSharper disable once InconsistentNaming
        public sealed class TTFDefinitions : IModule
        {
            [RunParameter("TTF", 0, "The TTF number to assign to. 1 would mean TTF1.")]
            // ReSharper disable once InconsistentNaming
            public int TTFNumber;

            [RunParameter("Congestion Perception", 0.41f, "The congestion perception to apply to this TTF.")]
            public float CongestionPerception;

            [RunParameter("Congestion Exponent", 1.62f, "The congestion exponent to apply to this TTF.")]
            public float CongestionExponent;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }


        public sealed class SurfaceTransitSpeed : IModule
        {
            //scenario_number, time_period_duration, boarding_duration, alighting_duration, default_duration, correlation)

            [RunParameter("Boarding Duration", 1.9577, "The boarding duration in seconds per passenger to apply.")]
            public float BoardingDuration;

            [RunParameter("Alighting Duration", 1.1219, "The alighting duration in seconds per passenger to apply.")]
            public float AlightingDuration;

            [RunParameter("Default Duration", 7.4331, "The default duration in seconds per stop to apply.")]
            public float DefaultDuration;

            [RunParameter("Transit Auto Correlation", 1, "The multiplier to auto time to use to find transit time.")]
            public float Correlation;

            [RunParameter("Global EROW Speed", 1, "The speed to use in segments that have Exclusive Right of Way for transit and do not have @erow_speed defined. Note that the speed includes accelaration and decelaration time.")]
            public float ErowSpeed;

            [RunParameter("Mode Filter Expression", "bpgq", "The modes that will get surface transit speed updating applied to them. To select all lines, leave this and the line filter blank")]
            public string ModeFilterExpression;

            [RunParameter("Line Filter Expression", "line = ______ xor line = GT____ xor line = TS____ xor line = T5____", "The line filter that will be used to determing which lines will get surface transit speed applied to them. To select all lines, leave this and the line filter blank")]
            public string LineFilterExpression;



            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [RunParameter("Allow Same Walk Time Perception Attribute", false, "Allow the use of the same walk time perception attributes.  This can cause issues as each class will overwrite the previous classes values.  Use carefully.")]
        public bool AllowSameWalkTimePerceptionAttribute;

        public bool RuntimeValidation(ref string error)
        {
            if (ScenarioNumber <= 0)
            {
                error = "In '" + Name + "' the scenario number must be greater than zero!";
                return false;
            }
            if (Classes.Length < 1)
            {
                error = "In '" + Name + "' must define at least one class.";
                return false;
            }

            if (TTF.Length < 1)
            {
                error = "In '" + Name + "' must define at least one TTF.";
                return false;
            }

            var usedMatricies = new List<int>();
            for (int i = 0; i < Classes.Length; i++)
            {
                //check for walk perception attribute duplicates
                if (!AllowSameWalkTimePerceptionAttribute)
                {
                    if (Classes.Any(c => Classes[i] != c && c.WalkPerceptionAttribute == Classes[i].WalkPerceptionAttribute))
                    {
                        error = "In '" + Name + "' the walk perception attribute name '" + Classes[i].WalkPerceptionAttribute + "' is used more than once.";
                        return false;
                    }
                }
                //check for duplicate demand matrices
                if (Classes.Any(c => Classes[i] != c && c.DemandMatrixNumber == Classes[i].DemandMatrixNumber))
                {
                    error = "In '" + Name + "' demand matrix number '" + Classes[i].DemandMatrixNumber + "' is used more than once.";
                    return false;
                }

                // check for duplicates across output matrices
                if (!CheckMatrix(usedMatricies, Classes[i].PerceivedTravelTimeMatrixNumber, ref error)
                || (!CheckMatrix(usedMatricies, Classes[i].CongestionMatrixNumber, ref error))
                || (!CheckMatrix(usedMatricies, Classes[i].FareMatrixNumber, ref error))
                || (!CheckMatrix(usedMatricies, Classes[i].InVehicleMatrixNumber, ref error))
                || (!CheckMatrix(usedMatricies, Classes[i].WalkMatrixNumber, ref error))
                || (!CheckMatrix(usedMatricies, Classes[i].WaitMatrixNumber, ref error)))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CheckMatrix(List<int> alreadyExploredMatrices, int matrixNumber, ref string error)
        {
            //matrices
            if (matrixNumber != 0)
            {
                if (alreadyExploredMatrices.Contains(matrixNumber))
                {
                    error = "In '" + Name + "' output matrix number '" + matrixNumber + "' is used more than once.";
                    return false;
                }
                alreadyExploredMatrices.Add(matrixNumber);
            }
            return true;
        }

    }
}
