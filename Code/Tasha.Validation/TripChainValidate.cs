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
    [ModuleInformation(
        Description = "A validation module that counts the length of the different trip chains scheduled by TASHA. " +
                        "As an input it takes in the newly scheduled trip chains from TASHA and then counts the length " +
                        "of each trip-chain. The output is a table that shows the lengths of trip chains, followed by " +
                        "the percentage of the total trip chains."
        )]
    public class TripChainValidate : IPostHousehold
    {
        [RunParameter( "Output File", "TripChainCount.csv", "The output file name" )]
        public string OutputFile;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<int, float> NumberOfTrips = [];

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
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripChain in person.TripChains )
                    {
                        int currentNumberOfTrips = tripChain.Trips.Count;

                        if ( NumberOfTrips.ContainsKey( currentNumberOfTrips ) ) // Has this scenario occured previously?
                        {
                            NumberOfTrips[currentNumberOfTrips] += 1; // If it has, add one more occurence to it.
                        }
                        else
                        {
                            NumberOfTrips.Add( currentNumberOfTrips, 1 ); // If it hasn't, create the scenario and give it a value of one occurence at this point.
                        }

                        if ( currentNumberOfTrips == 1 )
                        {
                            throw new XTMFRuntimeException(this, "Household " + household.HouseholdId + " has a trip chain with only one trip. The trip chain belongs to person number " + person.Id );
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            lock (this)
            {
                using StreamWriter writer = new StreamWriter(OutputFile);
                writer.WriteLine("Trips/TripChain, Total, Percentage of all Trips");

                var sum = NumberOfTrips.Sum(v => v.Value);
                foreach (var pair in NumberOfTrips)
                {
                    writer.WriteLine("{0}, {1}, {2}", pair.Key, pair.Value, pair.Value / sum * 100);
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
            return "Currently Validating Trip Chains!";
        }
    }
}