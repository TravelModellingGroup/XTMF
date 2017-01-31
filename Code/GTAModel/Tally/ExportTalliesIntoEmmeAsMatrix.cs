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
    public class ExportTalliesIntoEmmeAsMatrix : IEmmeTool
    {
        [RunParameter( "Matrix Description", "", "The 40-character description of the matrix" )]
        public string MatrixDescription;

        [RunParameter( "Matrix Id", 10, "The matrix number (id) exported to Emme" )]
        public int MatrixId;

        [RunParameter( "Matrix Name", "", "The 6-character name of the matrix" )]
        public string MatrixName;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Scenario Number", 1, "For some reason is required, but shouldn't make a difference since Emme matrices are usually shared across all scenarios" )]
        public int ScenarioNumber;

        public List<IModeAggregationTally> Tallies;

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

            var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
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
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=\"{1}\" default=0 descr=\"{2}\"", MatrixId, MatrixName, MatrixDescription );
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        ToEmmeFloat( tally[o][d], strBuilder );
                        build.AppendFormat( "{0,-4:G} {1,-4:G} {2,-4:G}\r\n",
                            convertedO, flatZones[d].ZoneNumber, strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }

            string ret = null;
            mc.Run( "TMG2.XTMF.ImportMatrix", "\"" + Path.GetFullPath( outputFileName ) + "\" " + ScenarioNumber,
                ( (p) => { Progress = p; } ), ref ret );

            File.Delete( outputFileName );

            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( Tallies.Count == 0 )
            {
                error = Name + " requires that you have at least one tally in order to work!";
                return false;
            }
            return true;
        }

        private string GetPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( Root.InputBaseDirectory, fullPath );
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