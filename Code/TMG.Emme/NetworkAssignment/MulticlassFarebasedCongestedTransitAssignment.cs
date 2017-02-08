/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using XTMF;

namespace TMG.Emme.NetworkAssignment
{
    [ModuleInformation(Description = "Executes a multi-class congested transit assignment procedure " +
                    "for GTAModel V4.0. " +
                    "<br><br>Hard-coded assumptions: " +
                    "<ul><li> Boarding penalties are assumed stored in <b>UT3</b></li>" +
                    "<li> The congestion term is stored in <b>US3</b></li>" +
                    "<li> In-vehicle time perception is 1.0</li>" +
                    "<li> Unless specified, all available transit modes will be used.</li>" +
                    "</ul>" +
                    "<font color='red'>This tool is only compatible with Emme 4.2 and later versions</font>",
                    Name = "Multi-Class Transit Assignment")]
    public class MulticlassFareBasedCongestedTransitAssignment : IEmmeTool
    {

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

        [Parameter("Connector Logit Scale", 0.2f, "Scale parameter for logit model at origin connectors.")]
        public float ConnectorLogitScale;

        [RunParameter("Apply Congestion", true, "Set this to false in order to not apply congestion during assignment.")]
        public bool ApplyCongestion;

        private const string ToolName = "tmg.assignment.transit.multi_class_congested_FBTA";

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Description = "The classes for this multi-class assignment.")]
        public Class[] Classes;

        [SubModelInformation(Description = "The TTF's to apply in the assignment.")]
        // ReSharper disable once InconsistentNaming
        public TTFDefinitions[] TTF;

        public bool Execute(Controller controller)
        {
            Progress = 0;
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            string walkPerception = String.Empty;

            // join all walk perceptions into one string
            foreach (var c in Classes)
            {
                walkPerception = string.Join(";", walkPerception, string.Join("::", c.WalkPerceptions.Select(walk => Controller.ToEmmeFloat(walk.WalkValue) + ":" + walk.LineFilter)));

            }
            walkPerception = '\"' + walkPerception.Substring(1, walkPerception.Length - 1) + '\"';

            /*
        def __call__(self, xtmf_ScenarioNumber, xtmf_DemandMatrixString, \

                WalkSpeed, WalkPerceptionString, WalkPerceptionAttributeIdString,
        
            ClassWaitPerceptionString, ClassBoardPerceptionString, ClassFarePerceptionString, \
        \
        HeadwayFractionAttributeId, LinkFareAttributeId, SegmentFareAttributeId, \
        
            EffectiveHeadwayAttributeId, EffectiveHeadwaySlope,  AssignmentPeriod, \
        
            Iterations, NormGap, RelGap, \
        \
        xtmf_InVehicleTimeMatrixString, xtmf_WaitTimeMatrixString, xtmf_WalkTimeMatrixString, xtmf_FareMatrixString, xtmf_CongestionMatrixString, xtmf_PenaltyMatrixString, xtmf_ImpedanceMatrixString \
        
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
                                        ApplyCongestion
                                       );

            var result = "";
            return mc.Run(ToolName, args, (p => Progress = p), ref result);
        }

        private string ProduceMatrixString(Func<Class, int> matrixNumber)
        {

            return "\"" + string.Join(",", Classes.Select(c => "mf" + matrixNumber(c).ToString())) + "\"";
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
                    if (WalkValue <= 0)
                    {
                        error = "In '" + Name + "' walk perception value must be greater than 0.";
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
