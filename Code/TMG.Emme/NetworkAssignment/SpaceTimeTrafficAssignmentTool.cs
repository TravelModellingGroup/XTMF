/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using XTMF;

namespace TMG.Emme.NetworkAssignment
{
    [ModuleInformation(Description =
        @"The he Space-time Traffic Assignment Tool or STTA runs a multi-class quasi-dynamic traffic assignment that uses a time-dependent network loading."
        )]

    public class SpaceTimeTrafficAssignmentTool : IEmmeTool
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario Number", 1, "The scenario number to execute against.")]
        public int ScenarioNumber;

        const string ToolName = "tmg.XTMF_internal.space_time_travel_assignment";

        [SubModelInformation(Description = "The classes for this multi-class assignment.")]
        public Class[] Classes;

        [RunParameter("Interval Lengths", "60,60,60", "Defines how the assignment time is split into intervals.")]
        public string IntervalLengths;

        [RunParameter("Start Time", "00:00", "")]
        public string StartTime;

        [RunParameter("Extra Time Interval", 60, "")]
        public float ExtraTimeInterval;

        [RunParameter("Number of Extra Time Intervals", 2, "")]
        public int NumberOfExtraTimeIntervals;

        [RunParameter("Background Traffic", true, "Set this to false to not assign transit vehicles on the roads")]
        public bool BackgroundTraffic;

        [RunParameter("Background Traffic Link Component Extra Attribute", "@tvph", "")]
        public string LinkComponentAttribute;

        [RunParameter("Time Dependent Start Index for Attributes", 1, "Time Dependent Start Indices used to create the alphanumerical attribute name string for attributes in this class.")]
        public int StartIndex;

        [RunParameter("Variable Topology", false, "")]
        public bool VariableTopology;

        [RunParameter("Max Inner Iterations", 15, "")]
        public int InnerIterations;

        [RunParameter("Max Outer Iterations", 5, "")]
        public int OuterIterations;

        [RunParameter("Coarse Relative Gap", 0.01f, "")]
        public float CoarseRGap;

        [RunParameter("Fine Relative Gap", 0.0001f, "")]
        public float FineRGap;

        [RunParameter("Coarse Best Relative Gap", 0.01f, "")]
        public float CoarseBRGap;

        [RunParameter("Fine Best Relative Gap", 0.0001f, "")]
        public float FineBRGap;

        [RunParameter("Normalized Gap", 0.005f, "The minimum gap required to terminate the algorithm.")]
        public float NormalizedGap;

        [RunParameter("Performance Flag", true, "Set this to false to leave a free core for other work, recommended to leave set to true.")]
        public bool PerformanceFlag;

        [RunParameter("Run Title", "Multi-class Run", "The name of the run to appear in the logbook.")]
        public string RunTitle;

        [RunParameter("On Road TTFs", "3-128", typeof(RangeSet), "The Transit Time Functions (TTFs) for transit segments that should be applied to the" +
            " road links to reduce capacity for the buses and streetcars in mixed traffic.")]
        public RangeSet OnRoadTTFs;


        public sealed class Class : IModule
        {
            [RunParameter("Mode", 'c', "The mode for this class.")]
            public char Mode;

            [RunParameter("Demand Matrix", 1000, "The id of the demand matrix to use.")]
            public int DemandMatrixNumber;

            [RunParameter("Time Matrix", 10, "The matrix number to save in vehicle travel times")]
            public int TimeMatrixNumber;

            [RunParameter("Cost Matrix", 0, "The matrix number to save the total cost into.")]
            public int CostMatrixNumber;

            [RunParameter("Toll Matrix", 0, "The matrix to save the toll costs into.")]
            public int TollMatrixNumber;

            [RunParameter("VolumeAttribute", "@auto_volume", "The name of the attribute to save the volumes into (or None for no saving).")]
            public string VolumeAttribute;

            [RunParameter("Time Dependent Start Index for Attributes in this Class", 1, "")]
            public int AttributeStartIndex;

            [RunParameter("TollAttributeID", "@toll", "The attribute containing the road tolls for this class of vehicle.")]
            public string LinkTollAttributeID;

            [RunParameter("Toll Weights", "1,2,3", "")]
            public string TollWeight;

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
                return false;
            }

            public Analysis[] PathAnalyses;

            public class Analysis : IModule
            {
                public string Name { get; set; }

                public float Progress => 0f;

                public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);
                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }
        }


        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "TMG.Emme.NetworkAssignment.SpaceTimeTrafficAssignmentTool requires the use of EMME Modeller and will not work through command prompt!");
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
            return new[]
            {
                new ModellerControllerParameter("ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("IntervalLengths", IntervalLengths.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("StartTime", StartTime.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("ExtraTimeInterval", ExtraTimeInterval.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("NumberOfExtraTimeIntervals", NumberOfExtraTimeIntervals.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("BackgroundTraffic", BackgroundTraffic.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("LinkComponentAttribute", LinkComponentAttribute),
                new ModellerControllerParameter("StartIndex", StartIndex.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("VariableTopology", VariableTopology.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("InnerIterations", InnerIterations.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("OuterIterations", OuterIterations.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("CoarseRGap",CoarseRGap.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("FineRGap",FineRGap.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("CoarseBRGap", CoarseBRGap.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("FineBRGap",FineBRGap.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("NormalizedGap", NormalizedGap.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("PerformanceFlag", PerformanceFlag.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("RunTitle", RunTitle),
                new ModellerControllerParameter("OnRoadTTFRanges", OnRoadTTFs.ToString()),
                new ModellerControllerParameter("Mode", GetClasses()),
                new ModellerControllerParameter("DemandMatrixNumber", GetDemand()),
                new ModellerControllerParameter("TimeMatrixNumber", GetTimes()),
                new ModellerControllerParameter("CostMatrixNumber", GetCosts()),
                new ModellerControllerParameter("TollMatrixNumber", GetTolls()),
                new ModellerControllerParameter("VolumeAttribute", string.Join(",", Classes.Select(c => c.VolumeAttribute))),
                new ModellerControllerParameter("AttributeStartIndex", string.Join(",", Classes.Select(c => c.AttributeStartIndex))),
                new ModellerControllerParameter("LinkTollAttributeID", string.Join(",", Classes.Select(c => c.LinkTollAttributeID))),
                new ModellerControllerParameter("TollWeight", string.Join(",", Classes.Select(c => c.TollWeight.ToString(CultureInfo.InvariantCulture)))),
                new ModellerControllerParameter("LinkCost", string.Join(",", Classes.Select(c => c.LinkCost.ToString(CultureInfo.InvariantCulture)))),
            };
        }

        private string GetTimes()
        {
            return string.Join(",", Classes.Select(c => c.TimeMatrixNumber.ToString()));
        }

        private string GetCosts()
        {
            return string.Join(",", Classes.Select(c => c.CostMatrixNumber.ToString()));
        }

        private string GetTolls()
        {
            return string.Join(",", Classes.Select(c => c.TollMatrixNumber.ToString()));
        }

        private string GetClasses()
        {
            return string.Join(",", Classes.Select(c => c.Mode.ToString()));
        }

        private string GetDemand()
        {
            return string.Join(",", Classes.Select(c => c.DemandMatrixNumber.ToString()));
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach (var c in Classes)
            {
                foreach (var at in c.PathAnalyses)
                {
                    if (!at.RuntimeValidation(ref error))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

}
