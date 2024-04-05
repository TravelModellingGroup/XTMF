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
using System.Linq;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    public class TripStartTime : IPostHousehold
    {
        [RunParameter( "Output File", "TripStartTimesDir", "The directory that will contain the results" )]
        public string OutputFile;

        [RunParameter( "Real Data?", false, "Are you using this to get the real data?" )]
        public bool RealData;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<Activity, float[]> StartTime = [];
        private string Status = "Initializing!";

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
            get { return new Tuple<byte, byte, byte>( 100, 100, 100 ); }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock ( this )
            {
                float expansionFactor = household.ExpansionFactor;
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripChain in person.TripChains )
                    {
                        foreach ( var trip in tripChain.Trips )
                        {
                            var currentMode = trip.Mode;
                            trip.Mode = Root.AutoMode;
                            var hours = trip.ActivityStartTime.Hours;
                            if ( hours > 28 )
                            {
                                trip.Mode = currentMode;
                                continue;
                            }
                            else
                            {
                                if ( StartTime.ContainsKey( trip.Purpose ) )
                                {
                                    StartTime[trip.Purpose][hours] += expansionFactor;
                                }
                                else
                                {
                                    StartTime.Add( trip.Purpose, new float[29] );
                                    StartTime[trip.Purpose][hours] += expansionFactor;
                                }
                            }
                            trip.Mode = currentMode;
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            lock (this)
            {
                foreach ( var pair in StartTime )
                {
                    string fileName;
                    var sum = pair.Value.Sum();
                    for ( int i = 0; i < pair.Value.Length; i++ )
                    {
                        pair.Value[i] = pair.Value[i] / sum;
                    }
                    if ( RealData )
                    {
                        fileName = Path.Combine( OutputFile, pair.Key + "StartTimesData.csv" );
                    }
                    else
                    {
                        fileName = Path.Combine( OutputFile, pair.Key + "StartTimesTasha.csv" );
                    }
                    var dir = Path.GetDirectoryName( fileName );
                    if ( !Directory.Exists( dir ) )
                    {
                        Directory.CreateDirectory( dir );
                    }
                    using ( StreamWriter writer = new StreamWriter( fileName ) )
                    {
                        writer.WriteLine( "Start Hour, Number of Occurrences" );
                        for ( int i = 0; i < pair.Value.Length; i++ )
                        {
                            writer.WriteLine( "{0}, {1}", i, pair.Value[i] );
                        }
                    }
                }
            }
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
        }

        public override string ToString()
        {
            return Status;
        }
    }
}