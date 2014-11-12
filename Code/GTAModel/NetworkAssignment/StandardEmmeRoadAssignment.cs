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

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation( Description = "Executes a standard Emme road assignment, collecting link costs on a per-kilometer basis." )]
    public class StandardEmmeRoadAssignment : IEmmeTool
    {
        [RunParameter( "Best Relative Gap", 0.01f, "(%) Emme Traffic Assignment parameter" )]
        public float BestRelativeGap;

        [RunParameter( "Travel Cost Matrix Number", 13, "The matrix number which will store the auto travel costs matrix. If the matrix exists already, it will be overwritten." )]
        public int CostMatrixNumber;

        [RunParameter( "Demand Matrix Number", 10, "The matrix number which will store th auto OD matrix. If the matrix exists already, it will be overwritten." )]
        public int DemandMatrixNumber;

        [RunParameter( "Peak Hour Factor", 0.42f, "Converts the modelling period into a one-hour assignment period." )]
        public float Factor;

        [RunParameter( "Link Unit Cost", 0.138f, "The link unit cost in $/km." )]
        public float GasCost;

        [RunParameter( "Max Iterations", 100, "Emme Traffic Assignment parameter" )]
        public int MaxIterations;

        [RunParameter( "Normalized Gap", 0.01f, "Emme Traffic Assignment parameter" )]
        public float NormalizedGap;

        [RunParameter( "Relative Gap", 0.0f, "Emme Traffic Assignment parameter" )]
        public float RelativeGap;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Scenario Number", 1, "The desired Emme network scenario. Must exist inside the databank." )]
        public int ScenarioNumber;

        [SubModelInformation( Description = "Tallies used for counting the number of trips between Origin and Destination", Required = false )]
        public List<IModeAggregationTally> Tallies;

        [RunParameter( "Travel Time Matrix Number", 12, "The matrix number which will store the auto travel times matrix. If the matrix exists already, it will be overwritten." )]
        public int TravelTimeMatrixNumber;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );

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
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            PassMatrixIntoEmme( mc );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}",
                ScenarioNumber, DemandMatrixNumber, TravelTimeMatrixNumber, CostMatrixNumber, Factor,
                GasCost, MaxIterations, RelativeGap, BestRelativeGap, NormalizedGap );

            return mc.Run( "TMG2.Assignment.RoadAssignment.GTAModelRoadAssignment", sb.ToString() );
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.Tallies.Count == 0 )
            {
                error = this.Name + " requires that you have at least one tally in order to work!";
                return false;
            }
            return true;
        }

        private string GetPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private float[][] GetResult(TreeData<float[][]> node, int modeIndex, ref int current)
        {
            if ( modeIndex == current )
            {
                return node.Result;
            }
            current++;
            if ( node.Children != null )
            {
                for ( int i = 0; i < node.Children.Length; i++ )
                {
                    float[][] temp = GetResult( node.Children[i], modeIndex, ref current );
                    if ( temp != null )
                    {
                        return temp;
                    }
                }
            }
            return null;
        }

        private void PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            string outputFileName = Path.GetTempFileName();
            float[][] tally = new float[numberOfZones][];
            for ( int i = 0; i < numberOfZones; i++ )
            {
                tally[i] = new float[numberOfZones];
            }
            for ( int i = Tallies.Count - 1; i >= 0; i-- )
            {
                Tallies[i].IncludeTally( tally );
            }
            using ( StreamWriter writer = new StreamWriter( outputFileName ) )
            {
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=0 descr=generated", this.DemandMatrixNumber );
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        this.ToEmmeFloat( tally[o][d], strBuilder );
                        build.AppendFormat( "{0,-4:G} {1,-4:G} {2,-4:G}\r\n",
                            convertedO, flatZones[d].ZoneNumber, strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }

            mc.Run( "TMG2.XTMF.ImportMatrix", "\"" + outputFileName + "\" " + ScenarioNumber );

            File.Delete( outputFileName );
        }

        private string SetupRun()
        {
            var flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            string outputFileName = Path.GetTempFileName();
            float[][] tally = new float[numberOfZones][];
            for ( int i = 0; i < numberOfZones; i++ )
            {
                tally[i] = new float[numberOfZones];
            }
            for ( int i = Tallies.Count - 1; i >= 0; i-- )
            {
                Tallies[i].IncludeTally( tally );
            }
            using ( StreamWriter writer = new StreamWriter( outputFileName ) )
            {
                // We need to know what the head should look like.
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=incr descr=generated", this.DemandMatrixNumber );
                // Now that the header is in place we can start to generate all of the instructions
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        this.ToEmmeFloat( tally[o][d], strBuilder );
                        build.AppendFormat( "{0,-4:G} {1,-4:G} {2,-4:G}\r\n",
                            convertedO, flatZones[d].ZoneNumber, strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }
            return outputFileName;
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
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