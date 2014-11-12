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
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using XTMF;
using TMG;
using Datastructure;


namespace James.UTDM
{
    public class BasicPurpose : IPurpose
    {
        [SubModelInformation( Description = "The model that will do generation", Required = true )]
        public IGeneration Generation { get; set; }

        [SubModelInformation( Description = "The model that will do distribution", Required = true )]
        public IDistribution Distribution { get; set; }

        [SubModelInformation( Description = "The model that will do mode split", Required = true )]
        public IMultiModeSplit ModeSplit { get; set; }

        [RunParameter( "Purpose Name", "Work", "The name to use for this purpose, information saved here will appear in a directory with the same name" )]
        public string PurposeName { get; set; }

        [RunParameter( "Save Flows", false, "Save the flows to disk?" )]
        public bool SaveFlows;

        private int Step;

        [RootModule]
        public I4StepModel Root;

        public void Run()
        {
            SparseArray<float> production, attraction;
            Stopwatch watch = new Stopwatch();
            if ( !Directory.Exists( this.PurposeName ) )
            {
                Directory.CreateDirectory( this.PurposeName );
            }
            using ( StreamWriter timerWriter = new StreamWriter( this.PurposeName + "/PerformanceTimes.txt", true ) )
            {
                Step = 0;
                watch.Start();
                var zoneArray = Root.ZoneSystem.ZoneArray;
                production = zoneArray.CreateSimilarArray<float>();
                attraction = zoneArray.CreateSimilarArray<float>();

                foreach ( var zone in zoneArray.ValidIndexies() )
                {
                    production[zone] = zoneArray[zone].Population;
                    attraction[zone] = zoneArray[zone].Employment;
                }
                this.Generation.Generate( production, attraction );
                watch.Stop();
                timerWriter.WriteLine( String.Format( "Generations = {0}ms", watch.ElapsedMilliseconds ) );
                watch.Restart();
                Step = 1;
                var flows = this.Distribution.Distribute( production, attraction );
                watch.Stop();
                timerWriter.WriteLine( String.Format( "Flows = {0}ms", watch.ElapsedMilliseconds ) );
                if ( this.SaveFlows )
                {
                    watch.Restart();
                    SaveMatrix( flows, this.PurposeName + "/Flows.csv" );
                    watch.Stop();
                    timerWriter.WriteLine( String.Format( "Save Flows = {0}ms", watch.ElapsedMilliseconds ) );
                }
                Step = 2;
                watch.Restart();
                var split = this.ModeSplit.ModeSplit( flows );
                watch.Stop();
                timerWriter.WriteLine( String.Format( "Mode Split = {0}ms", watch.ElapsedMilliseconds ) );
                try
                {
                    for ( int i = 0; i < split.Count; i++ )
                    {
                        this.WriteModeSplit( split[i], this.Root.Modes[i], this.PurposeName );
                    }
                }
                catch ( AggregateException aggex )
                {
                    throw new XTMFRuntimeException( aggex.InnerException.Message );
                }
            }
        }

        private void WriteModeSplit(TreeData<float[][]> split, IModeChoiceNode modeNode, string directoryName)
        {
            if ( !Directory.Exists( directoryName ) )
            {
                Directory.CreateDirectory( directoryName );
            }
            using ( StreamWriter writer = new StreamWriter( Path.Combine( directoryName, modeNode.ModeName + ".csv" ) ) )
            {
                var header = true;
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                for(int i = 0; i < zones.Length; i++)
                {
                    var first = true;
                    if ( header )
                    {
                        header = false;
                        for(int j = 0; j < zones.Length; j++)
                        {
                            if ( first )
                            {
                                first = false;
                                writer.Write( "Zones O\\D," );
                                writer.Write( zones[j].ZoneNumber );
                            }
                            else
                            {
                                writer.Write( ',' );
                                writer.Write( j );
                            }
                        }
                        writer.WriteLine();
                        first = true;
                    }
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        var s = split.Result[i][j];
                        if ( first )
                        {
                            first = false;
                            writer.Write( i );
                            writer.Write( ',' );
                            writer.Write( s );
                        }
                        else
                        {
                            writer.Write( ',' );
                            writer.Write( s );
                        }
                    }
                    writer.WriteLine();
                }
            }
            if ( split.Children != null )
            {
                for ( int i = 0; i < split.Children.Length; i++ )
                {
                    WriteModeSplit( split.Children[i], ( (IModeCategory)modeNode ).Children[i], directoryName );
                }
            }
        }

        private static void SaveMatrix(SparseTwinIndex<float> matrix, string fileName)
        {
            using ( StreamWriter writer = new StreamWriter( fileName ) )
            {
                var header = true;
                foreach ( var i in matrix.ValidIndexes() )
                {
                    var first = true;
                    if ( header )
                    {
                        header = false;
                        foreach ( var j in matrix.ValidIndexes( i ) )
                        {
                            if ( first )
                            {
                                first = false;
                                writer.Write( "Zones O\\D," );
                                writer.Write( j );
                            }
                            else
                            {
                                writer.Write( ',' );
                                writer.Write( j );
                            }
                        }
                        writer.WriteLine();
                        first = true;
                    }
                    foreach ( var j in matrix.ValidIndexes( i ) )
                    {
                        if ( first )
                        {
                            first = false;
                            writer.Write( i );
                            writer.Write( ',' );
                            writer.Write( matrix[i, j] );
                        }
                        else
                        {
                            writer.Write( "," );
                            writer.Write( matrix[i, j] );
                        }
                    }
                    writer.WriteLine();
                }
            }
        }

        public List<TreeData<float[][]>> Flows
        {
            get;
            set;
        }

        public string Name { get; set; }


        public float Progress
        {
            get
            {
                float progress = 0;
                switch ( Step )
                {
                    case 0:
                        progress = 1 / 3f;
                        break;
                    case 1:
                        progress = ( 1 / 3f ) + this.Distribution.Progress * ( 1 / 3f );
                        break;
                    case 2:
                        progress = ( 2 / 3f ) + this.ModeSplit.Progress * ( 1 / 3f );
                        break;
                }
                return progress;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
