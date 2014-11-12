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

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"This module provides a way of building a demand matrix and loading it into EMME through the Modeller Bridge.  
This module requires the root module in the model system to be of type ‘I4StepModel’."
        )]
    public class ModeAggregationAssignment : IEmmeTool
    {
        [RunParameter( "Assignment Parameters", "1 31 0 0 None", "The parameters to use for the assignment." )]
        public string AssingmentParameters;

        [RunParameter( "Assignment Tool Name", "TMG.TMG.RoadAssign", "Should be the name of the tool you want for the assignment." )]
        public string AssingmentToolName;

        [RunParameter( "Destination Files", "UpdatedData\\AutoTimes.311,UpdatedData\\AutoCosts.311", "The path relative to the Input Directory to copy the emme output to." )]
        public string DestinationFiles;

        [RunParameter( "Export Matrix Numbers", "12,13", "The list of matrix numbers to read from." )]
        public string ExportMatrixNumbers;

        [Parameter( "Export Matrix Tool Name", "TMG.TMG.ExportMatrix", "(Should never change from \"TMG.TMG.ExportMatrix\")" )]
        public string ExportMatrixToolName;

        [RunParameter( "Factor", 0.42f, "What should the factor for this period be?" )]
        public float Factor;

        [Parameter( "Load Matrix Tool Name", "TMG.TMG.LoadMatrix", "(Should never change from \"TMG.TMG.LoadMatrix\")" )]
        public string LoadMatrixToolName;

        [RunParameter( "Matrix Number", 10, "What is the number of the matrix that we need to store into?" )]
        public int MatrixNumber;

        [RunParameter( "Modes", "Auto", "Ex \"Auto,Taxi,Passenger\" the modes you want to process." )]
        public string ModeNames;

        [RootModule]
        public I4StepModel Root;

        [RunParameter( "Scenario Number", 1, "What scenario number should we be working with?" )]
        public int ScenarioNumber;

        [SubModelInformation( Description = "Tallies used for counting the number of trips between Origin and Destination", Required = false )]
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
            var demandFile = SetupRun();
            // Step 1, run the matrix loading tool
            // Step 2, once all of the demand data has been loaded run the calculation
            // Only do the assignment step if a toolname has been selected
            if ( controller.Run( this.LoadMatrixToolName, String.Concat( this.ScenarioNumber, ' ', '"', Path.GetFullPath( demandFile ), '"' ) ) &&
                String.IsNullOrWhiteSpace( this.AssingmentToolName ) ? true : controller.Run( this.AssingmentToolName, this.AssingmentParameters ) )
            {
                // Now that we are finished with copying the data we can go ahead and delete our demand file from
                // temporary storage.
                try
                {
                    File.Delete( demandFile );
                }
                catch ( IOException )
                {
                }
                // Now that everything has been cleaned up we should go and export the data back out of EMME
                var numbers = this.ExportMatrixNumbers.Split( ',' );
                var destFiles = this.DestinationFiles.Split( ',' );
                if ( numbers.Length != destFiles.Length )
                {
                    throw new XTMFRuntimeException( "The number of matricies exported must be the same as the number of destination file names!" );
                }
                for ( int i = 0; i < numbers.Length; i++ )
                {
                    if ( String.IsNullOrWhiteSpace( numbers[i] ) || String.IsNullOrWhiteSpace( destFiles[i] ) )
                    {
                        continue;
                    }
                    if ( !controller.Run( this.ExportMatrixToolName, String.Concat( this.ScenarioNumber, " mf", numbers[i], " \"", Path.GetFullPath( this.GetPath( destFiles[i] ) ), '"' ) ) )
                    {
                        throw new XTMFRuntimeException( "Unable to export matrix mf" + numbers[i] + " to \"" + this.GetPath( destFiles[i] ) + "\"" );
                    }
                }
                return true;
            }
            return false;
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
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=incr descr=generated", this.MatrixNumber );
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