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

namespace Tasha.Validation.ValidateModeChoice
{
    public class GetUtilities : IPostHouseholdIteration
    {
        [RunParameter( "FailedHouseholds", "Failed.csv", "The file where we can store the failed households." )]
        public string FailFile;

        [RunParameter( "Output File", "HouseholdUtilities.csv", "The file where we can store the household utilities." )]
        public string OutputFile;

        [RunParameter( "Passenger Mode", "Passenger", "The name of the passenger mode, leave blank to not processes them specially." )]
        public string PassengerModeName;

        [RootModule]
        public ITashaRuntime Root;

        private ConcurrentDictionary<ITashaHousehold, float[][]> HouseUtilities = new ConcurrentDictionary<ITashaHousehold, float[][]>();
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
            if (success)
            {
                if (!HouseUtilities.TryRemove(household, out float[][] util))
                {
                    return;
                }
                lock (this)
                {
                    int tripChains = 0;
                    foreach (var person in household.Persons)
                    {
                        tripChains += person.TripChains.Count;
                    }
                    var writeHeader = !File.Exists(OutputFile);
                    using (StreamWriter writer = new StreamWriter(OutputFile, true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine("HouseholdID,ouseholdIteration,First Household Utility,Second Household Utility,After Passenger Household Utility, TripChains Count");
                        }

                        for (int i = 0; i < util.Length; i++)
                        {
                            writer.Write(household.HouseholdId);
                            writer.Write(',');
                            writer.Write(i);
                            writer.Write(',');
                            writer.Write(util[i][0]);
                            writer.Write(',');
                            writer.Write(util[i][1]);
                            writer.Write(',');
                            writer.Write(util[i][2]);
                            writer.Write(',');
                            writer.WriteLine(tripChains);
                        }
                    }
                }
            }
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var houseData = (ModeChoiceHouseholdData) household["ModeChoiceData"];
            var resource = (HouseholdResourceAllocator) household["ResourceAllocator"];
            var modes = Root.AllModes;

            float firstPassHouseholdU = 0;
            float secondHouseholdU = 0;
            float passengerU = 0;

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
                    var chosenVehicleType = resource.Resolution[i][j];
                    var bestAssignments = tripChainData.BestPossibleAssignmentForVehicleType;
                    float max = float.NegativeInfinity;
                    for ( int a = 0; a < bestAssignments.Length; a++ )
                    {
                        if ( bestAssignments[a] != null && bestAssignments[a].U > max )
                        {
                            max = bestAssignments[a].U;
                        }
                    }
                    firstPassHouseholdU += max;
                    for ( int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++ )
                    {
                        var tripData = tripChainData.TripData[k];
                        int modeIndex = bestAssignments[chosenVehicleType].PickedModes[k];
                        if ( household.Persons[i].TripChains[j].Trips[k].Mode == modes[PassengerIndex] )
                        {
                            passengerU += tripData.V[PassengerIndex];
                        }
                        else
                        {
                            passengerU += tripData.V[modeIndex] + tripData.Error[modeIndex];
                        }
                        secondHouseholdU += tripData.V[modeIndex] + tripData.Error[modeIndex];
                    }
                }
            }

            bool found;
            if ( !( found = HouseUtilities.TryGetValue( household, out float[][] utilities ) ) )
            {
                utilities = new float[totalHouseholdIterations][];
                for ( int i = 0; i < utilities.Length; i++ )
                {
                    utilities[i] = new float[3];
                }
            }
            utilities[hhldIteration][0] = firstPassHouseholdU;
            utilities[hhldIteration][1] = secondHouseholdU;
            utilities[hhldIteration][2] = passengerU;
            if ( !found )
            {
                HouseUtilities[household] = utilities;
            }
        }

        public void HouseholdStart(ITashaHousehold household, int totalIterations)
        {
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
    }
}