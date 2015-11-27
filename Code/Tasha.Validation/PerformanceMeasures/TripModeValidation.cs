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
using System.Linq;
using Datastructure;
using Tasha.Common;
using TMG.Input;
using TMG;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures
{
    public class TripModeValidation : IPostHouseholdIteration
    {
        bool Calculate = false;

        Dictionary<ITashaMode, float> AMModeDictionary = new Dictionary<ITashaMode, float>();        
        Dictionary<ITashaMode, float> MDModeDictionary = new Dictionary<ITashaMode, float>();        
        Dictionary<ITashaMode, float> PMModeDictionary = new Dictionary<ITashaMode, float>();        
        Dictionary<ITashaMode, float> EVModeDictionary = new Dictionary<ITashaMode, float>();        



        [SubModelInformation(Required = true, Description = "Mode Validation Results File in .csv")]
        public FileLocation ResultsFile;

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
                        for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                        {
                            var mode = household.Persons[i].TripChains[j].Trips[k].Mode;
                            var tripStartTime = household.Persons[i].TripChains[j].Trips[k].TripStartTime;
                            if (tripStartTime.Hours >= 6 && tripStartTime.Hours < 9)
                            {
                                lock (this)
                                {
                                    AddToDictionary(AMModeDictionary, mode, toAdd);
                                }
                            }
                            else if (tripStartTime.Hours >= 9 && tripStartTime.Hours < 15)
                            {
                                lock (this)
                                {
                                    AddToDictionary(MDModeDictionary, mode, toAdd);
                                }
                            }
                            else if (tripStartTime.Hours >= 15 && tripStartTime.Hours < 19)
                            {
                                lock (this)
                                {
                                    AddToDictionary(PMModeDictionary, mode, toAdd);
                                }
                            }
                            else
                            {
                                lock (this)
                                {
                                    AddToDictionary(EVModeDictionary, mode, toAdd);
                                }
                            }                            
                        }
                    }
                }
            }            
        }

        private void AddToDictionary(Dictionary<ITashaMode, float> modeDictionary, ITashaMode mode, float toAdd)
        {
            float initialValue;                        

            if(!modeDictionary.TryGetValue(mode, out initialValue ))
            {
                initialValue = 0;
            }
            modeDictionary[mode] = toAdd + initialValue;
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
            if(iteration == totalIterations - 1)
            {
                using (StreamWriter writer = new StreamWriter(ResultsFile))
                {
                    writer.WriteLine("Mode, Trips");
                    foreach(var pair in AMModeDictionary)
                    {
                        writer.WriteLine("AM" + "{0}, {1}", pair.Key.ToString(), pair.Value);
                    }
                    foreach (var pair in MDModeDictionary)
                    {
                        writer.WriteLine("MD" + "{0}, {1}", pair.Key.ToString(), pair.Value);
                    }
                    foreach (var pair in PMModeDictionary)
                    {
                        writer.WriteLine("PM" + "{0}, {1}", pair.Key.ToString(), pair.Value);
                    }
                    foreach (var pair in EVModeDictionary)
                    {
                        writer.WriteLine("EV" + "{0}, {1}", pair.Key.ToString(), pair.Value);
                    }
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
