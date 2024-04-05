/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures
{
    public class TripModeValidation : IPostHouseholdIteration
    {
        bool Calculate;

        Dictionary<string, float> AMModeDictionary = [];
        Dictionary<string, float> MDModeDictionary = [];
        Dictionary<string, float> PMModeDictionary = [];
        Dictionary<string, float> EVModeDictionary = [];

        [SubModelInformation(Required = true, Description = "Mode Validation Results File in .csv")]
        public FileLocation ResultsFile;

        [RunParameter("Minimum Age", 11, "The minimum age a person must be in order to be recorded.")]
        public int MinAge;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            if (Calculate)
            {
                for (int i = 0; i < household.Persons.Length; i++)
                {
                    float toAdd = household.Persons[i].ExpansionFactor / totalHouseholdIterations;

                    for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                    {
                        var person = household.Persons[i];
                        if(person.Age < MinAge)
                        {
                            continue;
                        }
                        for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                        {
                            var trip = person.TripChains[j].Trips[k];
                            var mode = trip.Mode.ModeName;
                            var tripStartTime = trip.TripStartTime.Hours;
                            if (tripStartTime >= 6 && tripStartTime < 9)
                            {
                                AddToDictionary(AMModeDictionary, mode, toAdd);
                            }
                            else if (tripStartTime >= 9 && tripStartTime < 15)
                            {
                                AddToDictionary(MDModeDictionary, mode, toAdd);
                            }
                            else if (tripStartTime >= 15 && tripStartTime < 19)
                            {
                                AddToDictionary(PMModeDictionary, mode, toAdd);
                            }
                            else
                            {
                                AddToDictionary(EVModeDictionary, mode, toAdd);
                            }
                        }
                    }
                }
            }
        }

        private void AddToDictionary(Dictionary<string, float> modeDictionary, string mode, float toAdd)
        {
            lock (this)
            {
                if (!modeDictionary.TryGetValue(mode, out float initialValue))
                {
                    initialValue = 0;
                }
                modeDictionary[mode] = toAdd + initialValue;
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            Calculate = iteration == totalIterations - 1;
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            if (iteration == totalIterations - 1)
            {
                using StreamWriter writer = new(ResultsFile);
                writer.WriteLine("Mode, Trips");
                foreach (var pair in AMModeDictionary)
                {
                    writer.WriteLine("AM" + "{0}, {1}", pair.Key, pair.Value);
                }
                foreach (var pair in MDModeDictionary)
                {
                    writer.WriteLine("MD" + "{0}, {1}", pair.Key, pair.Value);
                }
                foreach (var pair in PMModeDictionary)
                {
                    writer.WriteLine("PM" + "{0}, {1}", pair.Key, pair.Value);
                }
                foreach (var pair in EVModeDictionary)
                {
                    writer.WriteLine("EV" + "{0}, {1}", pair.Key, pair.Value);
                }
            }

            AMModeDictionary.Clear();
            MDModeDictionary.Clear();
            PMModeDictionary.Clear();
            EVModeDictionary.Clear();
        }

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
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
