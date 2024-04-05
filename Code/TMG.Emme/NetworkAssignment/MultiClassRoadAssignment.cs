/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Emme.NetworkAssignment;


public class MultiClassRoadAssignmentTool : IEmmeTool
{

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [RunParameter("Scenario Number", 0, "The scenario number to execute against.")]
    public int ScenarioNumber;

    const string ToolName = "tmg.XTMF_internal.multi_class_road_assignment";

    [SubModelInformation(Description = "The classes for this multi-class assignment.")]
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

    [RunParameter("Run Title", "Multi-class Run", "The name of the run to appear in the logbook.")]
    public string RunTitle;

    [RunParameter("Background Transit", true, "Set this to false to not assign transit vehicles on the roads")]
    public bool BackgroundTransit;

    [RunParameter("On Road TTFs", "3-128", typeof(RangeSet), "The Transit Time Functions (TTFs) for transit segments that should be applied to the" +
        " road links to reduce capacity for the buses and streetcars in mixed traffic.")]
    public RangeSet OnRoadTTFs;


    public sealed class Class : IModule
    {
        [RunParameter("Mode", 'c', "The mode for this class.")]
        public char Mode;

        [RunParameter("Demand Matrix", 0, "The id of the demand matrix to use.")]
        public int DemandMatrixNumber;

        [RunParameter("Time Matrix", 0, "The matrix number to save in vehicle travel times")]
        public int TimeMatrix;

        [RunParameter("Cost Matrix", 0, "The matrix number to save the total cost into.")]
        public int CostMatrix;

        [RunParameter("Toll Matrix", 0, "The matrix to save the toll costs into.")]
        public int TollMatrix;

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

        public Analysis[] PathAnalyses;

        public class Analysis : IModule
        {
            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

            [RunParameter("Attribute ID", "", "The attribute to use for analysis.")]
            public string AttributeId;

            [RunParameter("Aggregation Matrix", 0, "The matrix number to store the results into.")]
            public int AggregationMatrix;

            [RunParameter("Operator", "+", "The operator to use to aggregate the matrix. Example:'+' for emissions, 'max' for select link analysis")]
            public string AggregationOperator;

            [RunParameter("Lower Bound for Path Selector", "None", "The number to use for the lower bound in path selection, or None if using all paths")]
            public string LowerBound;

            [RunParameter("Upper Bound for Path Selector", "None", "The number to use for the upper bound in path selection, or None if using all paths")]
            public string UpperBound;

            public enum Selection
            {
                ALL,
                SELECTED
            }
            [RunParameter("Paths to Select", "ALL", typeof(Selection), "The paths that will be used for analysis")]
            public Selection PathSelection;

            [RunParameter("Multiply Path Proportion By Analyzed Demand", true, "Choose whether to multiply the path proportion by the analyzed demand")]
            public bool MultiplyPathByDemand;
            [RunParameter("Multiply Path Proportion By Path Value", true, "Choose whether to multiply the path proportion by the path value")]
            public bool MultiplyPathByValue;

            public bool RuntimeValidation(ref string error)
            {
                if (String.IsNullOrWhiteSpace(AttributeId))
                {
                    error = $"In {Name} the attribute ID was not valid!";
                    return false;
                }
                if (AggregationMatrix <= 0)
                {
                    error = $"In {Name} the aggregation matrix number was invalid!";
                    return false;
                }

                return true;
            }
        }
    }


    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController ?? throw new XTMFRuntimeException(this, "TMG.Emme.NetworkAssignment.MultiClassRoadAssignment requires the use of EMME Modeller and will not work through command prompt!");
        /*
         xtmf_ScenarioNumber, Mode_List, xtmf_Demand_String, TimesMatrixId,
             CostMatrixId, TollsMatrixId, PeakHourFactor, LinkCost,
             TollWeight, Iterations, rGap, brGap, normGap, PerformanceFlag,
             RunTitle, LinkTollAttributeId, xtmf_NameString, ResultAttributes, xtmf_AggAttributes, xtmf_aggAttributesMatrixId
        */
        string ret = null;
        if (!mc.CheckToolExists(this, ToolName))
        {
            throw new XTMFRuntimeException(this, "There was no tool with the name '" + ToolName + "' available in the EMME databank!");
        }
        return mc.Run(this, ToolName, GetParameters(), (p) => Progress = p, ref ret);
    }

    private ModellerControllerParameter[] GetParameters()
    {
        return
        [
            new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
            new ModellerControllerParameter("Mode_List", GetClasses()),
            new ModellerControllerParameter("xtmf_Demand_String", GetDemand()),
            new ModellerControllerParameter("TimesMatrixId", GetTimes()),
            new ModellerControllerParameter("CostMatrixId", GetCosts()),
            new ModellerControllerParameter("TollsMatrixId", GetTolls()),
            new ModellerControllerParameter("PeakHourFactor", PeakHourFactor.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("LinkCost", string.Join(",", Classes.Select(c => c.LinkCost.ToString(CultureInfo.InvariantCulture)))),
            new ModellerControllerParameter("TollWeight", string.Join(",", Classes.Select(c => c.TollWeight.ToString(CultureInfo.InvariantCulture)))),
            new ModellerControllerParameter("Iterations", Iterations.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("rGap", RelativeGap.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("brGap", BestRelativeGap.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("normGap", NormalizedGap.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("PerformanceFlag", PerformanceMode.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("RunTitle", RunTitle),
            new ModellerControllerParameter("LinkTollAttributeId", string.Join(",", Classes.Select(c => c.LinkTollAttributeID))),
            new ModellerControllerParameter("xtmf_NameString", string.Join(",", Classes.Select(c => c.Name))),
            new ModellerControllerParameter("ResultAttributes", string.Join(",", Classes.Select(c => c.VolumeAttribute))),
            new ModellerControllerParameter("xtmf_AnalysisAttributes", GetAttributesFromClass()),
            new ModellerControllerParameter("xtmf_AnalysisAttributesMatrixId", GetAttributeMatrixIds()),
            new ModellerControllerParameter("xtmf_AggregationOperator", GetAggregationOperator()),
            new ModellerControllerParameter("xtmf_LowerBound", GetLowerBound()),
            new ModellerControllerParameter("xtmf_UpperBound", GetUpperBound()),
            new ModellerControllerParameter("xtmf_PathSelection", GetPathSelection()),
            new ModellerControllerParameter("xtmf_MultiplyPathPropByDemand", GetPathMultiplyDemand()),
            new ModellerControllerParameter("xtmf_MultiplyPathPropByValue", GetPathMultiplyValue()),
            new ModellerControllerParameter("xtmf_BackgroundTransit", BackgroundTransit.ToString()),
            new ModellerControllerParameter("OnRoadTTFRanges", OnRoadTTFs.ToString())
        ];
    }

    private string GetAttributesFromClass()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.AttributeId));
    }

    private string GetAttributeMatrixIds()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select "mf" + at.AggregationMatrix));
    }

    private string GetAggregationOperator()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.AggregationOperator));
    }

    private string GetLowerBound()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.LowerBound));
    }

    private string GetUpperBound()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.UpperBound));
    }

    private string GetPathSelection()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.PathSelection));
    }

    private string GetPathMultiplyDemand()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.MultiplyPathByDemand));
    }

    private string GetPathMultiplyValue()
    {
        return string.Join("|", from c in Classes
                                select string.Join(",", from at in c.PathAnalyses
                                                        select at.MultiplyPathByValue));
    }

    private string GetTimes()
    {
        return string.Join(",", Classes.Select(c => "mf" + c.TimeMatrix.ToString()));
    }

    private string GetCosts()
    {
        return string.Join(",", Classes.Select(c => "mf" + c.CostMatrix.ToString()));
    }

    private string GetTolls()
    {
        return string.Join(",", Classes.Select(c => "mf" + c.TollMatrix.ToString()));
    }

    private string GetClasses()
    {
        return string.Join(",", Classes.Select(c => c.Mode.ToString()));
    }

    private string GetDemand()
    {
        return string.Join(",", Classes.Select(c => "mf" + c.DemandMatrixNumber.ToString()));
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
