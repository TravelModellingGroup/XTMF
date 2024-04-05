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
using System.IO;
using Tasha.Common;
using Tasha.XTMFModeChoice;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Tasha.Validation.ValidateModeChoice
{
    public class VKTHousehold : IPostHouseholdIteration
    {
        [RunParameter( "Output File", "VKT.csv", "The name of the output file" )]
        public string OutputFile;

        [RunParameter( "Passenger Mode", "Passenger", "The name of the passenger mode, leave blank to not processes them specially." )]
        public string PassengerModeName;

        [RunParameter( "RideShare Mode", "RideShare", "The name of the passenger mode, leave blank to not processes them specially." )]
        public string RideshareModeName;

        [RootModule]
        public ITashaRuntime Root;

        private int PassengerIndex;
        private int RideShareIndex;

        private ConcurrentDictionary<int, float[]> VKT = new ConcurrentDictionary<int, float[]>();

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
            if ( success )
            {
                lock ( this )
                {
                    var writeHeader = !File.Exists( OutputFile );
                    using StreamWriter writer = new StreamWriter(OutputFile, true);
                    if (writeHeader)
                    {
                        writer.WriteLine("HouseholdID, Iteration, VKT, Number of Vehicles, Average VKT");
                    }

                    var householdVKT = VKT[household.HouseholdId];
                    for (int i = 0; i < householdVKT.Length; i++)
                    {
                        float average;
                        if (householdVKT[i] == 0)
                        {
                            average = 0;
                        }
                        else
                        {
                            average = householdVKT[i] / household.Vehicles.Length;
                        }
                        writer.WriteLine("{0}, {1}, {2}, {3}, {4}", household.HouseholdId, i, householdVKT[i], household.Vehicles.Length, average);
                    }
                }
            }
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var houseData = (ModeChoiceHouseholdData) household["ModeChoiceData"];
            var modes = Root.AllModes;
            float totalVKT = 0;
            if ( household.Vehicles.Length > 0 )
            {
                for ( int i = 0; i < household.Persons.Length; i++ )
                {
                    var personData = houseData.PersonData[i];
                    for ( int j = 0; j < household.Persons[i].TripChains.Count; j++ )
                    {
                        var tripChainData = personData.TripChainData[j];

                        if ( tripChainData.TripChain.JointTrip && !tripChainData.TripChain.JointTripRep )
                        {
                            continue;
                        }

                        for ( int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++ )
                        {
                            var currentTrip = tripChainData.TripChain.Trips[k];

                            if ( currentTrip.Mode.RequiresVehicle != null )
                            {
                                if ( currentTrip.Mode == modes[PassengerIndex] )
                                {
                                    float firstLeg;
                                    float secondLeg;
                                    var originalTrip = (ITrip) currentTrip["Driver"];
                                    var passengerDistance = Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber];
                                    if ( originalTrip.OriginalZone == currentTrip.OriginalZone )
                                    {
                                        firstLeg = 0;
                                    }
                                    else
                                    {
                                        firstLeg = Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, currentTrip.OriginalZone.ZoneNumber];
                                    }

                                    if ( originalTrip.DestinationZone == currentTrip.DestinationZone )
                                    {
                                        secondLeg = 0;
                                    }
                                    else
                                    {
                                        secondLeg = Root.ZoneSystem.Distances[currentTrip.DestinationZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                                    }
                                    // Subtract out the driver's VKT only if the purpose of this trip is not to facilitate passenger
                                    if ( originalTrip.TripChain.Trips.Count > 1 )
                                    {
                                        totalVKT -= Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                                    }
                                    totalVKT += ( passengerDistance + firstLeg + secondLeg );
                                }
                                else if ( currentTrip.Mode == modes[RideShareIndex] )
                                {
                                    totalVKT += Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber] / 2;
                                }
                                else
                                {
                                    totalVKT += Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber];
                                }
                            }
                        }
                    }
                }
            }
            VKT[household.HouseholdId][hhldIteration] = totalVKT;
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
            VKT.TryAdd( household.HouseholdId, new float[householdIterations] );
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            
        }

        public bool RuntimeValidation(ref string error)
        {
            PassengerIndex = -1;
            RideShareIndex = -1;
            if ( !String.IsNullOrWhiteSpace( PassengerModeName ) )
            {
                for ( int i = 0; i < Root.AllModes.Count; i++ )
                {
                    if ( Root.AllModes[i].ModeName == PassengerModeName )
                    {
                        PassengerIndex = i;
                    }
                    if ( Root.AllModes[i].ModeName == RideshareModeName )
                    {
                        RideShareIndex = i;
                    }
                }
                if ( PassengerIndex <= 0 )
                {
                    error = "In '" + Name + "' we were unable to find any passenger mode with the name '" + PassengerModeName + "'.";
                    return false;
                }
                if ( RideShareIndex <= 0 )
                {
                    error = "In '" + Name + "' we were unable to find any RideShare mode with the name '" + RideShareIndex + "'.";
                    return false;
                }
            }
            return true;
        }
    }
}