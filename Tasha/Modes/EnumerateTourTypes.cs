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
using Tasha.Common;
using XTMF;

namespace Tasha.Modes
{
    [ModuleInformation( Name = "Enumerate Tour Types",
        Description = "Analyzes trip chain data to enumerate all tour structures (e.g., HWH, HSH, HWOWMH, etc..)" )]
    public class EnumerateTourTypes : IPostHousehold
    {
        [RunParameter( "Home Anchor Override", "", "The name of the variable used to store an agent's initial activity. If blank, this will default to 'Home'" )]
        public string HomeAnchorOverrideName;

        [RunParameter( "Results File", "tourTypeResults.csv", "The file to save results into." )]
        public string ResultsFile;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Expansion Factor Flag", true, "Set to 'true' to report the weighted sum of observations; 'false' to enumerrate number of observed records." )]
        public bool UseExpansionFactor;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private Dictionary<string, double> data;

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

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock ( this )
            {
                foreach ( var p in household.Persons )
                {
                    if ( p.TripChains.Count < 1 )
                        continue; //Skip people with no trips

                    var sb = new StringBuilder();
                    if ( string.IsNullOrEmpty( this.HomeAnchorOverrideName ) )
                    {
                        sb.Append( Activity.Home.ToString() );
                    }
                    else
                    {
                        var x = p.TripChains[0].GetVariable( this.HomeAnchorOverrideName );
                        if ( x != null ) sb.Append( x.ToString() );
                        else sb.Append( Activity.Home.ToString() );
                    }

                    foreach ( var trip in p.TripChains[0].Trips )
                    {
                        sb.AppendFormat( ",{0}", trip.Purpose.ToString() );
                    }

                    var key = sb.ToString();
                    if ( this.data.ContainsKey( key ) )
                    {
                        if ( this.UseExpansionFactor )
                        {
                            this.data[key] += household.ExpansionFactor;
                        }
                        else
                        {
                            this.data[key]++;
                        }
                    }
                    else
                    {
                        if ( this.UseExpansionFactor )
                        {
                            this.data.Add( key, household.ExpansionFactor );
                        }
                        else
                        {
                            this.data.Add( key, 1 );
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            var path = this.ResultsFile;
            using ( StreamWriter sw = new StreamWriter( path ) )
            {
                sw.WriteLine( "TOUR ENUMERATION" );
                sw.WriteLine();
                sw.WriteLine( "Frequency,[List of activities]" );

                foreach ( var e in this.data )
                {
                    sw.WriteLine( e.Value + "," + e.Key );
                }
            }

            //throw new NotImplementedException();
        }

        public void Load(int maxIterations)
        {
            this.data = new Dictionary<string, double>();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
        }
    }
}