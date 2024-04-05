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
using Tasha.XTMFModeChoice;
using XTMF;

namespace Tasha.Validation.ValidateModeChoice
{
    public class HouseholdUtilities : IPostHouseholdIteration
    {
        [RunParameter("Output File", "HouseholdUtilities.csv", "The file where we can store the household utilities.")]
        public string OutputFile;

        private Dictionary<int, float[]> Utilities = [];

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
                lock (this)
                {
                    var writeHeader = !File.Exists(OutputFile);
                    using (StreamWriter writer = new StreamWriter(OutputFile, true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine("HouseholdID,HouseholdIteration,Household Utility");
                        }
                        var util = Utilities[household.HouseholdId];
                        for (int i = 0; i < util.Length; i++)
                        {
                            writer.Write(household.HouseholdId);
                            writer.Write(',');
                            writer.Write(i);
                            writer.Write(',');
                            writer.WriteLine(util[i]);
                        }
                    }
                }
            }
            else
            {
                throw new XTMFRuntimeException(this, "A household was not able to be resolved.");
            }
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var houseData = (ModeChoiceHouseholdData)household["ModeChoiceData"];
            var resource = (HouseholdResourceAllocator)household["ResourceAllocator"];

            float householdU = 0;

            for (int i = 0; i < household.Persons.Length; i++)
            {
                var personData = houseData.PersonData[i];
                for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                {
                    var tripChainData = personData.TripChainData[j];
                    if (tripChainData.TripChain.JointTrip && !tripChainData.TripChain.JointTripRep)
                    {
                        continue;
                    }
                    var chosenVehicleType = resource.Resolution[i][j];
                    var bestChosen = tripChainData.BestPossibleAssignmentForVehicleType[chosenVehicleType];
                    for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                    {
                        var tripData = tripChainData.TripData[k];
                        int modeIndex = bestChosen.PickedModes[k];
                        householdU += tripData.V[modeIndex] + tripData.Error[modeIndex];
                    }
                }
            }

            lock (this)
            {
                if (Utilities.ContainsKey(household.HouseholdId))
                {
                    Utilities[household.HouseholdId][hhldIteration] = householdU;
                }
                else
                {
                    Utilities.Add(household.HouseholdId, new float[totalHouseholdIterations]);
                    Utilities[household.HouseholdId][hhldIteration] = householdU;
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
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
            return true;
        }
    }
}