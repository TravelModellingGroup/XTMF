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
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    [ModuleInformation(
        Description = "This module is used for validation purposes. It computes and records " +
                        "the start times of activities immediately after the scheduler is finished. " +
                        "This is important as it allows for the analysis of the planned schedule before mode choice occurs."

        )]
    public class StartTimeIPostScheduler : IPostScheduler, IDisposable
    {
        [RunParameter( "Output File", "OutputResult.csv", "The file that will contain the results" )]
        public string OutputFile;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<string, int> StartTime = new Dictionary<string, int>();
        private StreamWriter Writer;

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

        public void Execute(ITashaHousehold household)
        {
            lock ( this )
            {
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripChain in person.TripChains )
                    {
                        foreach ( var trip in tripChain.Trips )
                        {
                            if ( trip.Purpose == Activity.ReturnFromSchool || trip.Purpose == Activity.ReturnFromWork )
                            {
                                string TripTime = trip.ActivityStartTime.ToString();
                                string[] Hours = TripTime.Split( ':' );
                                if ( StartTime.ContainsKey( Hours[0] ) )
                                {
                                    StartTime[Hours[0]] += 1;
                                }
                                else
                                {
                                    StartTime.Add( Hours[0], 1 );
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iterationNumber)
        {
            foreach ( var pair in StartTime )
            {
                Writer.WriteLine( "{0}, {1}", pair.Key, pair.Value );
            }
            this.Dispose( true );
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
            bool exists = File.Exists( OutputFile );
            if ( !exists )
            {
                Writer = new StreamWriter( OutputFile );
            }
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Writer != null )
            {
                this.Writer.Dispose();
                this.Writer = null;
            }
        }
    }
}