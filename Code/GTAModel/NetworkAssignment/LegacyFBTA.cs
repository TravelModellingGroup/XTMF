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
using System.Linq;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation(Name = "Legacy Fare-Based Transit Assignment",
                        Description = @"Executes a <b>fare-based transit assignment</b> (FBTA) as described
                            in the GTAModel Version 3 documentation: Using special functions
                            on centroid connectors and transit time functions. This requires
                            a compatible network, which can currently only be created by
                            macro_EditNetwork.mac.<br><br>
                            This Tool can also be used to execute a more standard tranist
                            assignment procedure by using a fare perception of '0'.
                            This Tool executes an Extended Transit Assignment, which allows
                            for subsequent analyses; such as those that can be found in
                            TMG2.Assignment.TransitAnalysis.")]
    public class LegacyFBTA : IEmmeTool
    {
        private const string _ToolName = "tmg.assignment.transit.V3_FBTA";
        private const string _OldToolName = "TMG2.Assignment.TransitAssignment.LegacyFBTA";
        private const string _ImportToolName = "tmg.XTMF_internal.import_matrix_batch_file";
        private const string _OldImportToolName = "TMG2.XTMF.ImportMatrix";
        [Parameter("Boarding Parameter", 1.0f, "The perception factor for boarding penalties.")]
        public float BoardingPerception;

        [RunParameter("Demand File Name", "", "Should we save the demand after tallying?  If so what should we name the file? (Blank will use a temporary file)")]
        public string DemandFileName;

        [RunParameter("Demand Matrix Number", 0, "The number of the full matrix to save the transit demand in. If it already exists, it will be overwritten. Set to 0 to use a scalar matrix of '0'")]
        public int DemandMatrixNumber;

        [Parameter("Fare Perception", 0.0f, "The time-value-of-money for converting fares to generalized cost. Set to 0.0 to disable fare-based impedance.")]
        public float FarePerception;

        [Parameter("In-vehicle Perception", 1.0f, "The perception factor for in-vehicle time.")]
        public float InVehiclePerception;

        [RunParameter("Modes", "blmstuvwy", "The string of modes to assign.")]
        public string Modes;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Scenario Number", 0, "The number of the scenario in which to run the assignment")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Optional Tallies for exporting transit demand data. Must be empty for scalar assignment, and vice-versa.", Required = false)]
        public List<IModeAggregationTally> Tallies;

        [Parameter("Use Additive Demand", false, "Set to true to add this assignment's volumes to those of a previous assignment. The default setting of false indicates a new assignment.")]
        public bool UseAdditiveDemand;

        [Parameter("Wait Factor", 0.5f, "The wait time factor used to estimate waiting time at stops. This should never change from 0.5")]
        public float WaitFactor;

        [Parameter("Wait Perception", 2.0f, "The perception factor for waiting time.")]
        public float WaitPerception;

        [Parameter("Walk Perception", 2.0f, "The perception factor for walking (auxiliary transit) time.")]
        public float WalkPerception;

        [Parameter("Walk Speed", 6.0f, "Walking speed, in km/hr")]
        public float WalkSpeed;

        [RunParameter("EMME Demand Matrix Name", "Demand", "A name to attach to the matrix.")]
        public string DemandMatrixName;

        /*
        ScenarioNumber, DemandMatrixNumber, Modes, WalkSpeed, WaitPerception, \
                 WalkPerception, InVehiclePerception, BoardingPerception, FarePerception, \
                 UseAdditiveDemand, WaitFactor
        */
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

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

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
                throw new XTMFRuntimeException("Controller is not a modeller controller!");

            if(this.DemandMatrixNumber != 0)
            {
                // if false then there were no records saved and we need to skip the assignment.
                if(!PassMatrixIntoEmme(mc))
                {
                    return true;
                }
            }

            //Setup space-delimited args for the Emme tool
            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                ScenarioNumber, DemandMatrixNumber, Modes, WalkSpeed, WaitPerception, WalkPerception,
                InVehiclePerception, BoardingPerception, FarePerception, UseAdditiveDemand, WaitFactor);
            string result = null;
            if(mc.CheckToolExists(_ToolName))
            {
                return mc.Run(_ToolName, sb.ToString(), (p => this.Progress = p), ref result);
            }
            else
            {
                return mc.Run(_OldToolName, sb.ToString(), (p => this.Progress = p), ref result);
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if((this.Tallies == null || this.Tallies.Count == 0) && this.DemandMatrixNumber != 0)
            {
                //There are no Tallies, and a scalar is not being assigned
                error = "No Tallies were found, but a scalar matrix was not selected! Please either add a Tally or change the" +
                    " Demand Matrix Number to 0";
                return false;
            }
            return true;
        }

        private float[][] GetResult(TreeData<float[][]> node, int modeIndex, ref int current)
        {
            if(modeIndex == current)
            {
                return node.Result;
            }
            current++;
            if(node.Children != null)
            {
                for(int i = 0; i < node.Children.Length; i++)
                {
                    float[][] temp = GetResult(node.Children[i], modeIndex, ref current);
                    if(temp != null)
                    {
                        return temp;
                    }
                }
            }
            return null;
        }

        private bool PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            var useTempFile = String.IsNullOrWhiteSpace(this.DemandFileName);
            string outputFileName = useTempFile ? Path.GetTempFileName() : this.DemandFileName;
            float[][] tally = new float[numberOfZones][];
            for(int i = 0; i < numberOfZones; i++)
            {
                tally[i] = new float[numberOfZones];
            }
            for(int i = Tallies.Count - 1; i >= 0; i--)
            {
                Tallies[i].IncludeTally(tally);
            }
            var dir = Path.GetDirectoryName(outputFileName);
            if(!String.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using (StreamWriter writer = new StreamWriter(outputFileName))
            {
                writer.WriteLine("t matrices\r\na matrix=mf{0} name={1} default=0 descr=generated", this.DemandMatrixNumber, DemandMatrixName.Replace(" ", ""));
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                bool any = false;
                Parallel.For(0, numberOfZones, delegate (int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder(10);
                    var convertedO = flatZones[o].ZoneNumber;
                    var localAny = false;
                    for(int d = 0; d < numberOfZones; d++)
                    {
                        var result = tally[o][d];
                        if(result > 0)
                        {
                            localAny = true;
                            this.ToEmmeFloat(result, strBuilder);
                            build.AppendFormat("{0,-4:G} {1,-4:G} {2,-4:G}\r\n",
                                convertedO, flatZones[d].ZoneNumber, strBuilder);
                        }
                    }
                    if(localAny)
                    {
                        any = true;
                    }
                });
                if(!any)
                {
                    return false;
                }
                for(int i = 0; i < numberOfZones; i++)
                {
                    writer.Write(builders[i]);
                }
            }

            try
            {
                if(mc.CheckToolExists(_ImportToolName))
                {
                    mc.Run(_ImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
                else
                {
                    mc.Run(_OldImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
            }
            finally
            {
                if(useTempFile)
                {
                    File.Delete(outputFileName);
                }
            }
            return true;
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        private void ToEmmeFloat(float number, StringBuilder builder)
        {
            builder.Clear();
            builder.Append((int)number);
            number = number - (int)number;
            if(number > 0)
            {
                var integerSize = builder.Length;
                builder.Append('.');
                for(int i = integerSize; i < 4; i++)
                {
                    number = number * 10;
                    builder.Append((int)number);
                    number = number - (int)number;
                    if(number == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}