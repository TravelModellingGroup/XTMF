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
using Datastructure;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation(Description = @"Assigns a limited matrix of demand to centroids which represent
                             GO train stations. Can assign a scalar matrix of 0 or a full matrix of
                             demand (constrained by the station selector). Unlike most other transit
                             assignment tools, this tool saves the constrained IVTT and wait times
                             matrices as outputs.")]
    public class Station2StationTransitAssignment : IEmmeTool
    {
        private const string ImportToolName = "tmg.XTMF_internal.import_matrix_batch_file";
        private const string OldImportToolName = "TMG2.XTMF.ImportMatrix";

        [RunParameter("Demand File Name", "", "Optional file name for saving tally exports for debugging. Leave blank to disable this feature.")]
        public string DemandFileName;

        [RunParameter("Demand Matrix Number", 0, "The number of the full matrix from which to assign demand (e.g., '9' for 'mf9'). A value of '0' assigns a " +
            "scalar matrix of 0")]
        public int DemandMatrixNumber;

        [RunParameter("In Vehicle Time Matrix Number", 0, "The number of the full matrix to save the in vehicle times into.")]
        public int InVehicleTimesMatrixNumber;

        [RunParameter("Modes", "bgmswtuvy", "A string of Emme mode characters permitted in the assignment.")]
        public string ModeString;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Scenario", 1, "The number of the Emme network scenario")]
        public int ScenarioNumber;

        [RunParameter("Station Centroids", "7000-8000", typeof(RangeSet), "A Range Set describing which centroids are classified as stations. Each Range in " +
            "the set will be parsed as end-exclusive (e.g., '1-5' includes 1,2,3, and 4).")]
        public RangeSet Stations;

        [SubModelInformation(Description = "Optional Tallies for exporting transit demand data. Must be empty for scalar assignment, and vice-versa.", Required = false)]
        public List<IModeAggregationTally> Tallies;

        [Parameter("Additional Demand Flag", false, "Set to true to add transit volumes resulting from this assignment to any existing transit volumes in the databank.")]
        public bool UseAdditiveDemand;

        [Parameter("Emme 4 Options Flag", false, "Future feature yet to be implemented. Enables new features of the Emme 4 transit assignmnet procedure.")]
        public bool UseEm4Options;

        [Parameter("Headway Factor", 0.5f, "The headway factor applied at stops. Should be fixed as 0.5 to get the average headway between transit routes.")]
        public float WaitFactor;

        [Parameter("Wait Time Perception", 2.0f, "The perception factor applied to waiting time.")]
        public float WaitPerception;

        [RunParameter("Wait Time Matrix Number", 0, "The number of the full matrix to save the waiting times into.")]
        public int WaitTimeMatrixNumber;

        [Parameter("Walk Time Perception", 2.0f, "The perception factor applied to walking time.")]
        public float WalkPerception;

        private Tuple<byte, byte, byte> _progressColour = new(255, 173, 28);

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            private set;
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
            {
                PassMatrixIntoEmme(mc);
            }

            //Setup space-delimited args for the Emme tool
            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                ScenarioNumber, DemandMatrixNumber, WaitTimeMatrixNumber, InVehicleTimesMatrixNumber,
                ConvertStationsToEmmeSelectorString(), ModeString, WaitFactor, WaitPerception,
                WalkPerception, UseAdditiveDemand, UseEm4Options);
            string result = null;
            return mc.Run(this, "tmg.assignment.transit.V2_line_haul", sb.ToString(), (p => Progress = p), ref result);

            /*
             * ScenarioNumber, DemandMatrixNumber, WaitTimeMatrixNumber, InVehicleTimeMatrixNumber, \
                 StationSelectorExpression, ModeString, WaitFactor, WaitPerception, WalkPerception,\
                 UseAdditiveDemand, UseEM4Options
             */
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private string ConvertStationsToEmmeSelectorString()
        {
            //Convert the Stations RangeSet into a string argument which Emme can interpret as zone selector
            StringBuilder stationExpressionBuilder = new();
            stationExpressionBuilder.AppendFormat("{0}-{1}", Stations[0].Start, Stations[0].Stop);
            for (int i = 1; i < Stations.Count; i++)
            {
                stationExpressionBuilder.AppendFormat(";{0}-{1}", Stations[i].Start, Stations[i].Stop);
            }

            return stationExpressionBuilder.ToString();
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
            for (int i = 0; i < Tallies.Count; i++)
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