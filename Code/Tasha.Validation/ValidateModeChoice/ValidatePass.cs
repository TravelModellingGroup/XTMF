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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation.ValidateModeChoice
{
    public class ValidatePass : IPostHouseholdIteration
    {
        [RunParameter( "Chart Height", 768, "The height of the chart to make." )]
        public int CharHeight;

        [RunParameter( "Chart Width", 1024, "The width of the chart to make." )]
        public int CharWidth;

        [RunParameter( "Output File", "PassengerValidation.csv", "The file where we can store problems" )]
        public string OutputFile;

        [RunParameter( "Passenger Mode", "Passenger", "The name of the passenger mode, leave blank to not processes them specially." )]
        public string PassengerModeName;

        [RootModule]
        public ITashaRuntime Root;

        private ConcurrentDictionary<float, List<float>> Data = new ConcurrentDictionary<float, List<float>>();

        private int PassengerIndex;

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
            get;
            set;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            for ( int i = 0; i < household.Persons.Length; i++ )
            {
                for ( int j = 0; j < household.Persons[i].TripChains.Count; j++ )
                {
                    if ( household.Persons[i].TripChains[j].JointTrip && !household.Persons[i].TripChains[j].JointTripRep )
                    {
                        continue;
                    }

                    for ( int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++ )
                    {
                        var trip = household.Persons[i].TripChains[j].Trips[k];

                        if ( trip.Mode == Root.AllModes[PassengerIndex] )
                        {
                            using StreamWriter writer = new StreamWriter(OutputFile, true);
                            var originalTrip = (ITrip)trip["Driver"];
                            var passengerDistance = Root.ZoneSystem.Distances[trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber];
                            var firstLeg = originalTrip.OriginalZone == trip.OriginalZone ? 0 : Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, trip.OriginalZone.ZoneNumber];
                            var secondLeg = originalTrip.DestinationZone == trip.DestinationZone ? 0 : Root.ZoneSystem.Distances[trip.DestinationZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                            var newDistance = (passengerDistance + firstLeg + secondLeg);

                            if (Data.Keys.Contains(passengerDistance))
                            {
                                Data[passengerDistance].Add(newDistance);
                            }
                            else
                            {
                                Data.TryAdd(passengerDistance, []);
                                Data[passengerDistance].Add(newDistance);
                            }

                            writer.WriteLine("{0}, {1}, {2}, {3}, {4}", household.HouseholdId, household.Persons[i].Id, originalTrip.TripChain.Person.Id, passengerDistance, newDistance);
                        }
                    }
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            PassengerIndex = -1;
            if ( !String.IsNullOrWhiteSpace( PassengerModeName ) )
            {
                for ( int i = 0; i < Root.AllModes.Count; i++ )
                {
                    if ( Root.AllModes[i].ModeName == PassengerModeName )
                    {
                        PassengerIndex = i;
                        break;
                    }
                }
                if ( PassengerIndex <= 0 )
                {
                    error = "In '" + Name + "' we were unable to find any passenger mode with the name '" + PassengerModeName + "'.";
                    return false;
                }
            }
            return true;
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            
        }
    }
}