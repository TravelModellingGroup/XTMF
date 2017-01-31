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
using Datastructure;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    public class AssignV4BoardingPenalty : IEmmeTool
    {

        [RunParameter("Scenario", "0", typeof(RangeSet), "The number of the Emme Scenario which to apply boarding penalties.")]
        public RangeSet ScenarioNumbers;

        [RunParameter("Subway Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float SubwayBoardingPenalty;

        [RunParameter("GO Train Boarding Penalty", 1.0f, "Boarding penalty applied to GO train lines")]
        public float GoTrainBoardingPenalty;

        [RunParameter("GO Bus Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float GoBusBoardingPenalty;

        [RunParameter("Streetcar XROW Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float StreetcarXROWBoardingPenalty;

        [RunParameter("Streetcar Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float StreetcarBoardingPenalty;

        [RunParameter("TTC Bus Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float TTCBusBoardingPenalty;

        [RunParameter("YRT Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float YRTBoardingPenalty;

        [RunParameter("VIVA Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float VIVABoardingPenalty;

        [RunParameter("Brampton Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float BramptonBoardingPenalty;

        [RunParameter("ZUM Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float ZUMBoardingPenalty;

        [RunParameter("MiWay Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float MiWayBoardingPenalty;

        [RunParameter("Durham Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float DurhamBoardingPenalty;

        [RunParameter("Halton Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float HaltonBoardingPenalty;

        [RunParameter("Hamilton Boarding Penalty", 1.0f, "Boarding penalty applied to subway lines")]
        public float HSRBoardingPenalty;


        private const string _ToolName = "tmg.assignment.preprocessing.assign_v4_boarding_penalty";
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController");
            }

            /*
            def __call__(self, xtmf_ScenarioNumber,
                 SubwayBoardingPenalty,
                 GoTrainBoardingPenalty,
                 GoBusBoardingPenalty,
                 StreetcarXROWBoardingPenalty,
                 StreetcarBoardingPenalty,
                 TTCBusBoardingPenalty,
                 YRTBoardingPenalty,
                 VIVABoardingPenalty,
                 BramptonBoardingPenalty,
                 ZUMBoardingPenalty,
                 MiWayBoardingPenalty,
                 DurhamBoardingPenalty,
                 HaltonBoardingPenalty,
                 HSRBoardingPenalty
            */

            List<int> scenarioList = new List<int>();
            foreach (var range in ScenarioNumbers)
            {
                for (int i = range.Start; i <= range.Stop; i++)
                {
                    scenarioList.Add(i);
                }
            }
            string scenarios = "\"" + string.Join(",", scenarioList) + "\"";

            var args = string.Join(" ", scenarios,
                                     mc.ToEmmeFloat(SubwayBoardingPenalty),
                                     mc.ToEmmeFloat(GoTrainBoardingPenalty),
                                     mc.ToEmmeFloat(GoBusBoardingPenalty),
                                     mc.ToEmmeFloat(StreetcarXROWBoardingPenalty),
                                     mc.ToEmmeFloat(StreetcarBoardingPenalty),
                                     mc.ToEmmeFloat(TTCBusBoardingPenalty),
                                     mc.ToEmmeFloat(YRTBoardingPenalty),
                                     mc.ToEmmeFloat(VIVABoardingPenalty),
                                     mc.ToEmmeFloat(BramptonBoardingPenalty),
                                     mc.ToEmmeFloat(ZUMBoardingPenalty),
                                     mc.ToEmmeFloat(MiWayBoardingPenalty),
                                     mc.ToEmmeFloat(DurhamBoardingPenalty),
                                     mc.ToEmmeFloat(HaltonBoardingPenalty),
                                     mc.ToEmmeFloat(HSRBoardingPenalty));
            string result = "";
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
            error = Name + " is currently using the now obsolete module TMG.GTAModel.NetworkAssignmentAssignV4BoardingPenalty. " +
                "This module has since been replaced by TMG.Emme.NetworkAssignment.PreProcessing.AssignBoardingPenalties. " +
                "Please contact your model system provider to help you update your model system, or contact TMG.";
            return false;
        }
    }
}
