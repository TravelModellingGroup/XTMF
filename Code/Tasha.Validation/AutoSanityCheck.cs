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
using System.IO;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    public class AutoSanityCheck : IPostHousehold, IDisposable
    {
        public int Count;

        [RunParameter( "Results File", "AutoSanityResults.csv", "Where do you want us to store the results" )]
        public string FileName;

        [RootModule]
        public ITashaRuntime Root;

        private StreamWriter Validate1;

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
            get { return new Tuple<byte, byte, byte>( 120, 25, 100 ); }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock ( this )
            {
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripChain in person.TripChains )
                    {
                        foreach ( var trip in tripChain.Trips )
                        {
                            var householdIterations = trip.ModesChosen == null ? 1 : trip.ModesChosen.Length;
                            for ( int i = 0; i < householdIterations; i++ )
                            {
                                if ( trip.ModesChosen[i] == null )
                                {
                                    this.Validate1.Write( "Problem in household #" );
                                    this.Validate1.WriteLine( household.HouseholdId );
                                }
                                else
                                {
                                    if ( trip.ModesChosen[i].ModeName == "Auto" )
                                    {
                                        Count += 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
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
            var exist = File.Exists( FileName );
            if ( !exist )
            {
                this.Validate1 = new StreamWriter( FileName );
            }
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Validate1 != null )
            {
                this.Validate1.Dispose();
                this.Validate1 = null;
            }
        }
    }
}