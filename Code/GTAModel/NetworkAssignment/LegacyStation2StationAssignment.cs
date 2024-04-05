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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation(Name = "Legacy Station-to-Station Assignment",
                        Description = "Executes a station-to-station assignment for commuter rail (GO Transit) using optional fare-based impedances."
                        + " Similar to Legacy FBTA, this assignment uses functions to apply fare impedances.\r\n"
                        + " Saves matrix results for in-vehicle times and costs (fares) for feasible trips from station centroids only. Station"
                        + " centroids are hard-coded to NCS11 definitions.")]
    public class LegacyStation2StationAssignment : IEmmeTool
    {
        private const string ToolName = "tmg.assignment.transit.V3_line_haul";
        private const string OldToolName = "TMG2.Assignment.TransitAssignment.LegacyStation2Station";

        private const string ImportToolName = "tmg.XTMF_internal.import_matrix_batch_file";
        private const string OldImportToolName = "TMG2.XTMF.ImportMatrix";

        [RunParameter("Cost Matrix Number", 21, "The full matrix number in which to store the assignment costs. Costs will only be reported for feasible trips from station centroids.")]
        public int CostMatrixNumber;

        [RunParameter("Demand File Name", "", "Optional file name for saving the demand matrix passed from XTMF to EMME. Leave blank to use a temporary file.")]
        public string DemandFileName;

        [RunParameter("Demand Matrix Number", 0, "The matrix number which will store the station OD matrix, if applicable. A value of 0 will assign a"
                        + " scalar matrix of '0'. If the matrix exists already, it will be overwritten.")]
        public int DemandMatrixNumber;

        [Parameter("Fare Perception", 0.0f, "The time-value-of-money for transit, in $/hr. Set to '0' to disable fare-based impedances.")]
        public float FarePerception;

        [RunParameter("Times Matrix Number", 20, "The full matrix number in which to store the assignment in-vehicle times. Times will only be reported for feasible trips from station centroids")]
        public int InVehicleTimeMatrixNumber;

        [RunParameter("Rail Base Fare", 3.55f, "A constant base fare ($) which will be added to all feasible station-to-station ODs")]
        public float RailBaseFare;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Scenario Number", 1, "The desired Emme network scenario. Must exist inside the databank.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Tallies used to create the demand matrix for EMME.", Required = false)]
        public List<IModeAggregationTally> Tallies;

        [RunParameter("Total Travel Time Cutoff", 150.0f, "The total travel time cutoff, in minutes. OD pairs determined feasible will have total in-vehicle times less than this threshold.")]
        public float TotalTimeCutoff;

        [Parameter("Additive Demand", false, "Set to 'true' to add the demand of this assignment to that of a previous assignment.")]
        public bool UseAdditiveDemand;

        [Parameter("Wait Perception", 2.0f, "Waiting time perception factor.")]
        public float WaitPerception;

        [Parameter("Walk Perception", 2.0f, "Walking time perception factor.")]
        public float WalkPerception;

        private static Tuple<byte, byte, byte> _progressColour = new(100, 100, 150);

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
            get { return _progressColour; }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException(this, "Controller is not a modeller controller!");

            if (DemandMatrixNumber != 0)
                PassMatrixIntoEmme(mc);

            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3} {4} {5} {6} {7} {8}",
                ScenarioNumber, DemandMatrixNumber, UseAdditiveDemand, RailBaseFare, WalkPerception,
                WaitPerception, TotalTimeCutoff, CostMatrixNumber, InVehicleTimeMatrixNumber);

            /*
             * ScenarioNumber, DemandMatrixNumber, UseAdditiveDemand, GOBaseFare, WalkPerception,
                 WaitPerception, TotalTimeCutoff, CostMatrixNumber,InVehicleTimeMatrixNumber
             */

            string result = null;
            if (mc.CheckToolExists(this, ToolName))
            {
                return mc.Run(this, ToolName, sb.ToString(), (p => Progress = p), ref result);
            }
            return mc.Run(this, OldToolName, sb.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            if ((Tallies == null || Tallies.Count == 0) && DemandMatrixNumber != 0)
            {
                //There are no Tallies, and a scalar is not being assigned
                error = "No Tallies were found, but a scalar matrix was not selected! Please either add a Tally or change the" +
                    " Demand Matrix Number to 0";

                return false;
            }

            return true;
        }

        private void PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            var useTempFile = String.IsNullOrWhiteSpace(DemandFileName);
            string outputFileName = useTempFile ? Path.GetTempFileName() : DemandFileName;
            float[][] tally = new float[numberOfZones][];
            for (int i = 0; i < numberOfZones; i++)
            {
                tally[i] = new float[numberOfZones];
            }
            for (int i = Tallies.Count - 1; i >= 0; i--)
            {
                Tallies[i].IncludeTally(tally);
            }
            var dir = Path.GetDirectoryName(outputFileName);
            if (!String.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using (StreamWriter writer = new(outputFileName))
            {
                writer.WriteLine("t matrices\r\na matrix=mf{0} name=drvtot default=0 descr=generated", DemandMatrixNumber);
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For(0, numberOfZones, delegate (int o)
               {
                   var build = builders[o] = new StringBuilder();
                   var strBuilder = new StringBuilder(10);
                   var convertedO = flatZones[o].ZoneNumber;
                   for (int d = 0; d < numberOfZones; d++)
                   {
                       Controller.ToEmmeFloat(tally[o][d], strBuilder);
                       build.AppendFormat("{0,-4:G} {1,-4:G} {2}\r\n",
                           convertedO, flatZones[d].ZoneNumber, strBuilder);
                   }
               });
                for (int i = 0; i < numberOfZones; i++)
                {
                    writer.Write(builders[i]);
                }
            }

            try
            {
                if (mc.CheckToolExists(this, ImportToolName))
                {
                    mc.Run(this, ImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
                else
                {
                    mc.Run(this, OldImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
            }
            finally
            {
                if (useTempFile)
                {
                    File.Delete(outputFileName);
                }
            }
        }
    }
}