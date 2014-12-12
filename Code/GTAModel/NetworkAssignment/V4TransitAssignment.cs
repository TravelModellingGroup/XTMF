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
    [ModuleInformation(Description= "Executes a congested transit assignment procedure "+
                        "for GTAModel V4.0. "+
                        "<br><br>Hard-coded assumptions: "+
                        "<ul><li> Boarding penalties are assumed stored in <b>UT3</b></li>"+
                        "<li> The congestion term is stored in <b>US3</b></li>"+
                        "<li> In-vehicle time perception is 1.0</li>"+
                        "<li> Unless specified, all available transit modes will be used.</li>"+
                        "</ul>"+
                        "<font color='red'>This tool is only compatible with Emme 4 and later versions</font>")]
    public class V4TransitAssignment : IEmmeTool
    {

        [RunParameter("Scenario Number", 0, "Emme Scenario Number")]
        public int ScenarioNumber;

        [RunParameter("Demand Matrix Number", 0, "The number of the full matrix containing transit demand ODs")]
        public int DemandMatrixNumber;

        [Parameter("Headway Fraction Attribute", "@hfrac", "The ID of the NODE extra attribute in which to store headway fraction. Should have a default value of 0.5.")]
        public string HeadwayFractionAttribute;

        [Parameter("Walk Perception Attribute", "@walkp", "The ID of the LINK extra attribute in which to store walk time perception. Should have a default value of 1.0.")]
        public string WalkPerceptionAttribute;

        //-------------------------------------------

        [RunParameter("GO Train Headway Fraction", 0.5f, "The headway fraction applied to GO Rail nodes (98000 <= i < 99000) only. Normally, the headway fraction is set to 0.5 "
                        + "which represents a uniform arrival of passengers at a stop, given an average wait time of 1/2 (0.5 *) the headway. GO Trains run on very infrequent "
                        + "schedules, so passengers try to time their arrival to coincide with the train's departure and thus experience less waiting time.")]
        public float GoTrainHeadwayFraction;

        [RunParameter("Wait Time Perception", 1.0f, "Perception factor applied to wait time component.")]
        public float WaitTimePerception;

        [RunParameter("Toronto Walk Perception", 1.0f, "Perception factor applied to Toronto links & connectors. Hard-coded to NCS11 node numbers.")]
        public float WalkPerceptionToronto;

        [RunParameter("Non-Toronto Walk Perception", 1.0f, "Perception factor applied to non-Toronto links & connectors. Hard-coded to NCS11 node numbers.")]
        public float WalkPerceptionNonToronto;

        [RunParameter("Boarding Penalty Perception", 0.0f, "Perception factor applied to boarding penalty component.")]
        public float BoardingPerception;

        [RunParameter("Congestion Penalty Perception", 0.15f, "Perception factor applied ton congestion penalty component.")]
        public float CongestionPerception;

        [RunParameter("Representative Hour Factor", 2.57f, "A multiplier applied to the demand matrix to scale it to match" +
                    " the transit line capacity period. This is similar to the peak hour factor used in auto assignment.")]
        public float RepresentativeHourFactor;

        //-------------------------------------------

        [RunParameter("Iterations", 20, "Convergence criterion: The maximum number of iterations performed by the transit assignment.")]
        public int MaxIterations;

        [RunParameter("Normalized Gap", 0.01f, "Convergence criterion")]
        public float NormalizedGap;

        [RunParameter("Relative Gap", 0.001f, "Convergence criterion")]
        public float RelativeGap;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        private const string _ToolName = "tmg.assignment.transit.V4_FBTA";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", this.ScenarioNumber, 
                                        this.DemandMatrixNumber,
                                        mc.ToEmmeFloat(this.GoTrainHeadwayFraction),
                                        mc.ToEmmeFloat(this.WaitTimePerception),
                                        mc.ToEmmeFloat(this.WalkPerceptionToronto),
                                        mc.ToEmmeFloat(this.WalkPerceptionNonToronto),
                                        this.WalkPerceptionAttribute,
                                        this.HeadwayFractionAttribute,
                                        mc.ToEmmeFloat(this.BoardingPerception), 
                                        mc.ToEmmeFloat(this.CongestionPerception),
                                        this.RepresentativeHourFactor, 
                                        this.MaxIterations, 
                                        this.NormalizedGap,
                                        this.RelativeGap);

            /*
            
            def __call__(self, xtmf_ScenarioNumber, xtmf_DemandMatrixNumber, GoTrainHeadwayFraction, WaitPerception,
                 WalkPerceptionToronto, WalkPerceptionNonToronto, 
                 WalkAttributeId, HeadwayFractionAttributeId, BoardPerception, CongestionPerception, 
                 AssignmentPeriod, Iterations, NormGap, RelGap
            */

            var result = "";
            return mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
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
