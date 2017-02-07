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

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation( Description = @"<p>A basic, no-frills Emme transit assignment tool. Saves transit strategies to the databank for future
                        analysis (e.g., extraction of travel times matrix - see tools in TMG.GTAModel.NetworkAnalysis).</p>
                        <p>Boarding penalties are assigned to transit lines, and are assumed to be <em>already defined in <b>UT3</b></em>." )]
    public class BasicTransitAssignment : IEmmeTool
    {

        private const string ToolName = "tmg.assignment.transit.V2_transit_assignment";
        private const string OldToolName = "TMG2.Assignment.TransitAssignment.BasicTransitAssignment";
        private const string NewImportToolName = "tmg.XTMF_internal.import_matrix_batch_file";
        private const string OldImportToolName = "TMG2.XTMF.ImportMatrix";
        [Parameter( "Boarding Time Perception", 1.0f, "The perception factor applied to boarding time." )]
        public float BoardingPerception;

        [RunParameter( "Demand File Name", "", "Optional file name for saving tally exports for debugging. Leave blank to disable this feature." )]
        public string DemandFileName;

        [RunParameter( "Demand Matrix Number", 0, @"The number of the full matrix from which to assign demand (e.g., '9' for 'mf9'). A value of '0' assigns a " +
            "scalar matrix of 0" )]
        public int DemandMatrixNumber;

        [Parameter( "In Vehicle Time Perception", 1.0f, "The perception factor applied to in-vehicle time." )]
        public float InVehiclePerception;

        [RunParameter( "Modes", "bgmswtuvy", "A string of Emme mode characters permitted in the assignment." )]
        public string ModeString;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Scenario", 1, "The number of the Emme network scenario" )]
        public int ScenarioNumber;

        [SubModelInformation( Description = "Optional Tallies for exporting transit demand data. Must be empty for scalar assignment, and vice-versa.", Required = false )]
        public List<IModeAggregationTally> Tallies;

        [Parameter( "Additional Demand Flag", false, "Set to true to add transit volumes resulting from this assignment to any existing transit volumes in the " +
            "databank." )]
        public bool UseAdditionalDemand;

        [Parameter( "Emme 4 Options Flag", false, "Future feature yet to be implemented. Enables new features of the Emme 4 transit assignment procedure." )]
        public bool UseEmme4Options;

        [Parameter( "Headway Factor", 0.5f, "The headway factor applied at stops. Should be fixed as 0.5 to get the average headway between transit routes." )]
        public float WaitFactor;

        [Parameter( "Wait Time Perception", 2.0f, "The perception factor applied to waiting time." )]
        public float WaitPerception;

        [Parameter( "Walk Time Perception", 2.0f, "The perception factor applied to walking time." )]
        public float WalkPerception;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>( 255, 173, 28 );

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
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a ModellerController!" );

            if ( DemandMatrixNumber != 0 )
            {
                PassMatrixIntoEmme( mc );
            }

            //Setup space-delimited args for the Emme tool
            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}",
                ScenarioNumber, DemandMatrixNumber, ModeString, WaitPerception, WalkPerception,
                InVehiclePerception, BoardingPerception, UseAdditionalDemand, WaitFactor, UseEmme4Options );
            string result = null;
            if(mc.CheckToolExists(ToolName))
            {
                return mc.Run(ToolName, sb.ToString(), (p => Progress = p), ref result);
            }
            return mc.Run(OldToolName, sb.ToString(), (p => Progress = p), ref result);

            /*
             * ScenarioNumber, DemandMatrixNumber, ModeString, WaitPerception,
                 WalkPerception, InVehiclePerception, BoardingPerception, UseAdditiveDemand,
                 WaitFactor, UseEM4Options
             */
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            var useTempFile = String.IsNullOrWhiteSpace( DemandFileName );
            string outputFileName = useTempFile ? Path.GetTempFileName() : DemandFileName;
            float[][] tally = new float[numberOfZones][];
            for ( int i = 0; i < numberOfZones; i++ )
            {
                tally[i] = new float[numberOfZones];
            }
            for ( int i = Tallies.Count - 1; i >= 0; i-- )
            {
                Tallies[i].IncludeTally( tally );
            }
            var dir = Path.GetDirectoryName( outputFileName );
            if ( !String.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            using ( StreamWriter writer = new StreamWriter( outputFileName ) )
            {
                writer.WriteLine( "t matrices\r\na matrix=mf{0} name=drvtot default=0 descr=generated", DemandMatrixNumber );
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        ToEmmeFloat( tally[o][d], strBuilder );
                        build.AppendFormat( "{0,-4:G} {1,-4:G} {2}\r\n",
                            convertedO, flatZones[d].ZoneNumber, strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }

            try
            {
                if(mc.CheckToolExists(NewImportToolName))
                {
                    mc.Run(NewImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
                else
                {
                    mc.Run(OldImportToolName, "\"" + Path.GetFullPath(outputFileName) + "\" " + ScenarioNumber);
                }
            }
            finally
            {
                if ( useTempFile )
                {
                    File.Delete( outputFileName );
                }
            }
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <param name="builder"></param>
        /// <returns>A limited precision non scientific number in a string</returns>
        private void ToEmmeFloat(float number, StringBuilder builder)
        {
            builder.Clear();
            builder.Append( (int)number );
            number = number - (int)number;
            if ( number > 0 )
            {
                var integerSize = builder.Length;
                builder.Append( '.' );
                for ( int i = integerSize; i < 4; i++ )
                {
                    number = number * 10;
                    builder.Append( (int)number );
                    number = number - (int)number;
                    if ( number == 0 )
                    {
                        break;
                    }
                }
            }
        }
    }
}