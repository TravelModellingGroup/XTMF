/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation(Description = "Executes a congested transit assignment procedure " +
                        "for GTAModel V4.0. " +
                        "<br><br>Hard-coded assumptions: " +
                        "<ul><li> Boarding penalties are assumed stored in <b>UT3</b></li>" +
                        "<li> The congestion term is stored in <b>US3</b></li>" +
                        "<li> In-vehicle time perception is 1.0</li>" +
                        "<li> Unless specified, all available transit modes will be used.</li>" +
                        "</ul>" +
                        "<font color='red'>This tool is only compatible with Emme 4 and later versions</font>",
                        Name = "V4 Fare Based Transit Assignment (FBTA)")]
    public class V4FBTA : IEmmeTool
    {

        [RunParameter("Scenario Number", 0, "Emme Scenario Number")]
        public int ScenarioNumber;

        [RunParameter("Demand Matrix Number", 0, "The number of the full matrix containing transit demand ODs")]
        public int DemandMatrixNumber;

        [Parameter("Headway Fraction Attribute", "@hfrac", "The ID of the NODE extra attribute in which to store headway fraction. Should have a default value of 0.5.")]
        public string HeadwayFractionAttribute;

        [Parameter("Walk Perception Attribute", "@walkp", "The ID of the LINK extra attribute in which to store walk time perception. Should have a default value of 1.0.")]
        public string WalkPerceptionAttribute;

        [Parameter("Link Fare Attribute", "@lfare", "The ID of the LINK extra attribute containing actual fare costs.")]
        public string LinkFareAttribute;

        [Parameter("Segment Fare Attribute", "@sfare", "The ID of the SEGMENT extra attribute containing actual fare costs.")]
        public string SegmentFareAttribute;

        [Parameter("Effective Headway Attribute", "@ehdw", "The name of the attribute to use for the effective headway")]
        public string EffectiveHeadwayAttributeId;

        //-------------------------------------------

        [RunParameter("In-vehicle Times Matrix", 0, "The number of the FULL matrix in which to save in-vehicle travel time. Enter 0 to skip saving this matrix")]
        public int InVehicleMatrixNumber;

        [RunParameter("WalkTimes Matrix", 0, "The number of the FULL matrix in which to save total walk time. Enter 0 to skip saving this matrix")]
        public int WalkMatrixNumber;

        [RunParameter("Wait Times Matrix", 0, "The number of the FULL matrix in which to save total waiting time. Enter 0 to skip saving this matrix")]
        public int WaitMatrixNumber;

        [RunParameter("Fare Matrix", 0, "The number of the FULL matrix in which to save transit fares. Enter 0 to skip saving this matrix")]
        public int FareMatrixNumber;

        [RunParameter("Boarding Matrix", 0, "The number of the FULL matrix in which to save the incurred boarding penalties. Enter 0 to skip saving this matrix")]
        public int BoardingMatrixNumber;

        [RunParameter("Congestion Matrix", 0, "The number of the FULL matrix in which to save transit congestion. Enter 0 to skip saving this matrix")]
        public int CongestionMatrixNumber;

        [RunParameter("Impedance Matrix", 0, "The number of the FULL matrix in which to save the perceived travel times. Enter 0 to skip saving this matrix")]
        public int ImpedanceMatrix;

        [RunParameter("Distance Matrix", 0, "The number of the FULL matrix in which to save distances. Enter 0 to skip saving this matrix")]
        public int DistanceMatrixNumber;

        //-------------------------------------------

        [RunParameter("Effective Headway Slope", 0.5f, "")]
        public float EffectiveHeadwaySlope;

        [RunParameter("Wait Time Perception", 1.0f, "Perception factor applied to wait time component.")]
        public float WaitTimePerception;

        [Parameter("Walk Speed", 4.0f, "Walking speed in km/hr. Applied to all walk (aux. transit) modes in the Emme scenario.")]
        public float WalkSpeed;

        [RunParameter("Toronto Walk Perception", 1.0f, "Perception factor applied to Toronto links. Hard-coded to NCS11 node numbers.")]
        public float WalkPerceptionToronto;

        [RunParameter("Non-Toronto Walk Perception", 1.0f, "Perception factor applied to non-Toronto links. Hard-coded to NCS11 node numbers.")]
        public float WalkPerceptionNonToronto;

        [RunParameter("Toronto Access Perception", 1.0f, "Walk perception factor applied to Toronto centroid connectors")]
        public float WalkPerceptionTorontoConnectors;

        [RunParameter("Non-Toronto Access Perception", 1.0f, "Walk perception applied to non-Toronto centroid connectors")]
        public float WalkPerceptionNonTorontoConnectors;

        [RunParameter("PD1 Walk Perception", 1.0f, "Walk perception applied non-connector walk links with type 101")]
        public float WalkPerceptionPD1;

        [RunParameter("Boarding Penalty Perception", 0.0f, "Perception factor applied to boarding penalty component.")]
        public float BoardingPerception;

        [RunParameter("Fare Perception", 1.0f, "Perception factor applied to path transit fares, in $/hr.")]
        public float FarePerception;

        [RunParameter("Representative Hour Factor", 2.04f, "A multiplier applied to the demand matrix to scale it to match" +
                    " the transit line capacity period. This is similar to the peak hour factor used in auto assignment.")]
        public float RepresentativeHourFactor;

        //-------------------------------------------

        [RunParameter("Use Boarding Levels", false, "Use boarding levels to ensure that every path must take a transit vehicle before arriving at their destination.")]
        public bool UseBoardingLevels;

        [RunParameter("Iterations", 20, "Convergence criterion: The maximum number of iterations performed by the transit assignment.")]
        public int MaxIterations;

        [RunParameter("Normalized Gap", 0.01f, "Convergence criterion")]
        public float NormalizedGap;

        [RunParameter("Relative Gap", 0.001f, "Convergence criterion")]
        public float RelativeGap;

        [Parameter("Connector Logit Scale", 0.2f, "Scale parameter for logit model at origin connectors.")]
        public float ConnectorLogitScale;

        [Parameter("Add Congestion to IVTT", false, "Set to TRUE to extract the congestion matrix and add its weighted value to the in vehicle time (IVTT) matrix.")]
        public bool ExtractCongestedInVehicleTimeFlag;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        private const string _ToolName = "tmg.assignment.transit.V4_FBTA";


        public sealed class TTFDefinitions : XTMF.IModule
        {
            [RunParameter("TTF", 0, "The TTF number to assign to. 1 would mean TTF1.")]
            public int TTFNumber;

            [RunParameter("Congestion Perception", 0.0f, "The congestion exponent to apply to this TTF.")]
            public float CongestionPerception;

            [RunParameter("Congestion Exponent", 0.0f, "The congestion exponent to apply to this TTF.")]
            public float CongestionExponent;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Description = "The TTF's to apply in the assignment.")]
        public TTFDefinitions[] TTF;


        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", ScenarioNumber,
                                        DemandMatrixNumber,
                                        mc.ToEmmeFloat(WaitTimePerception),
                                        mc.ToEmmeFloat(WalkSpeed),
                                        mc.ToEmmeFloat(WalkPerceptionToronto),
                                        mc.ToEmmeFloat(WalkPerceptionNonToronto),
                                        mc.ToEmmeFloat(WalkPerceptionTorontoConnectors),
                                        mc.ToEmmeFloat(WalkPerceptionNonTorontoConnectors),
                                        mc.ToEmmeFloat(WalkPerceptionPD1),
                                        WalkPerceptionAttribute,
                                        HeadwayFractionAttribute,
                                        LinkFareAttribute,
                                        SegmentFareAttribute,
                                        EffectiveHeadwayAttributeId,
                                        mc.ToEmmeFloat(EffectiveHeadwaySlope),
                                        mc.ToEmmeFloat(BoardingPerception),
                                        mc.ToEmmeFloat(FarePerception),
                                        mc.ToEmmeFloat(RepresentativeHourFactor),
                                        MaxIterations,
                                        mc.ToEmmeFloat(NormalizedGap),
                                        mc.ToEmmeFloat(RelativeGap),
                                        InVehicleMatrixNumber,
                                        WaitMatrixNumber,
                                        WalkMatrixNumber,
                                        FareMatrixNumber,
                                        CongestionMatrixNumber,
                                        BoardingMatrixNumber,
                                        DistanceMatrixNumber,
                                        mc.ToEmmeFloat(ConnectorLogitScale),
                                        ExtractCongestedInVehicleTimeFlag,
                                        string.Join(",", from ttf in TTF
                                                         select ttf.TTFNumber.ToString() + ":"
                                                         + mc.ToEmmeFloat(ttf.CongestionPerception).ToString() + ":"
                                                         + mc.ToEmmeFloat(ttf.CongestionExponent)),
                                        ImpedanceMatrix,
                                        UseBoardingLevels);

            var result = "";
            return mc.Run(_ToolName, args, (p => Progress = p), ref result);
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
