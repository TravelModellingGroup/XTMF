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
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using XTMF;

namespace TMG.Emme.NetworkAssignment
{

    public class MultiClassRoadAssignment : IEmmeTool
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

        [RunParameter("LinkCost", 0f, "The penalty in minutes per dollar to apply when traversing a link.")]
        public float LinkCost;

        [RunParameter("Run Title", "Multi-class Run", "The name of the run to appear in the logbook.")]
        public string RunTitle;

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

            [RunParameter("VolumeAttribute", "@classVolume", "The name of the attribute to save the volumes into")]
            public string VolumeAttribute;

            [RunParameter("TollAttributeID", "@toll", "The attribute containing the road tolls for this class of vehicle.")]
            public string LinkTollAttributeID;

            [RunParameter("Toll Weight", 0f, "")]
            public float TollWeight;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                if(Mode >= 'a' && Mode <= 'z' || Mode >= 'A' && Mode <= 'Z')
                {
                    return true;
                }
                error = "In '" + Name + "' the Mode '" + Mode + "' is not a feasible mode for multi class assignment!";
                return false;
            }

            public Aggregation[] AdditionalAttributesToAggregate;

            public class Aggregation : IModule
            {
                public string Name { get; set; }

                public float Progress => 0f;

                public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

                [RunParameter("Attribute ID", "", "The attribute to aggregate.")]
                public string AttributeId;

                [RunParameter("Aggregation Matrix", 0, "The matrix number to store the aggregated results into.")]
                public int AggregationMatrix;

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }
        }


        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
            {
                throw new XTMFRuntimeException("TMG.Emme.NetworkAssignment.MultiClassRoadAssignment requires the use of EMME Modeller and will not work through command prompt!");
            }
            /*
             xtmf_ScenarioNumber, Mode_List, xtmf_Demand_String, TimesMatrixId,
                 CostMatrixId, TollsMatrixId, PeakHourFactor, LinkCost,
                 TollWeight, Iterations, rGap, brGap, normGap, PerformanceFlag,
                 RunTitle, LinkTollAttributeId, xtmf_NameString, ResultAttributes
            */
            string ret = null;
            if(!mc.CheckToolExists(ToolName))
            {
                throw new XTMFRuntimeException("There was no tool with the name '" + ToolName + "' available in the EMME databank!");
            }
            return mc.Run(ToolName, GetParameters(), (p) => Progress = p, ref ret);
        }

        private ModellerControllerParameter[] GetParameters()
        {
            return new[]
            {
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("Mode_List", GetClasses()),
                new ModellerControllerParameter("xtmf_Demand_String", GetDemand()),
                new ModellerControllerParameter("TimesMatrixId", GetTimes()),
                new ModellerControllerParameter("CostMatrixId", GetCosts()),
                new ModellerControllerParameter("TollsMatrixId", GetTolls()),
                new ModellerControllerParameter("PeakHourFactor", PeakHourFactor.ToString(CultureInfo.InvariantCulture)),
                new ModellerControllerParameter("LinkCost", LinkCost.ToString(CultureInfo.InvariantCulture)),
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
                new ModellerControllerParameter("xtmf_AggAttributes", GetAttributesFromClass()),
                new ModellerControllerParameter("xtmf_AggAttributes", GetAttributeMatrixIds())
            };
        }

        private string GetAttributesFromClass()
        {
            StringBuilder builder = new StringBuilder();
            var numberOfAttributes = Classes.Length > 0 ? Classes[0].AdditionalAttributesToAggregate.Length : 0;
            for (int i = 0; i < numberOfAttributes; i++)
            {
                for (int j = 0; j < Classes.Length; j++)
                {
                    builder.Append(Classes[j].AdditionalAttributesToAggregate[i].AttributeId);
                    builder.Append(",");
                }
            }
            return builder.ToString();
        }

        private string GetAttributeMatrixIds()
        {
            StringBuilder builder = new StringBuilder();
            var numberOfAttributes = Classes.Length > 0 ? Classes[0].AdditionalAttributesToAggregate.Length : 0;
            for (int i = 0; i < numberOfAttributes; i++)
            {
                for (int j = 0; j < Classes.Length; j++)
                {
                    builder.Append("mf");
                    builder.Append(Classes[j].AdditionalAttributesToAggregate[i].AggregationMatrix);
                    builder.Append(",");
                }
            }
            return builder.ToString();
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
            // Check the 
            return true;
        }
    }

}
