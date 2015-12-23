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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Validation
{
    [ModuleInformation(
        Description = "A validation module which takes in Household data and then counts and records " +
                        "the amount of trips created for each trip purpose. This is an important validation " +
                        "step as one needs to make sure that the distribution of trips between the different " +
                        "trip purposes is logical. \nNote: The Expanded trips parameter lets the user choose " +
                        "whether or not he/she wants to look at expansion factors or just frequencies. "
        )]
    public class TripPurposeValidate : IPostHousehold
    {
        [RunParameter("Expanded Trips?", true, "Did you want to look at expanded trips (false = number of non-expanded trips")]
        public bool ExpandedTrips;

        [RunParameter("Results File", "TripPurposeValidate.csv", "Where do you want us to store the results")]
        public string FileName;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<Activity, float> PurposeDictionary;

        private SparseTwinIndex<Dictionary<Activity, float>> ODPurposeDictionary;

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

        [RunParameter("StartTime", "6:00AM", typeof(Time), "The time to start recording.")]
        public Time StartTime;

        [RunParameter("EndTime", "9:00AM", typeof(Time), "The time to end recording (exclusive).")]
        public Time EndTime;

        [RunParameter("Min Age", 11, "The minimum age to record the purposes for.")]
        public int MinAge;

        [RunParameter("Origin Zones", "1-9999", typeof(RangeSet), "The origin zones to select for.")]
        public RangeSet OriginZones;

        [RunParameter("Destination Zones", "1-9999", typeof(RangeSet), "The destination zones to select for.")]
        public RangeSet DestinationZones;

        [RunParameter("Save as Zone OD", false, "Save as OD Pair")]
        public bool SaveOD;

        public void Execute(ITashaHousehold household, int iteration)
        {
            // only run on the last iteration
            if(iteration == Root.TotalIterations - 1)
            {
                lock (this)
                {
                    float amountToAddPerTrip;

                    foreach(var person in household.Persons)
                    {
                        if (ExpandedTrips)
                        {
                            amountToAddPerTrip = person.ExpansionFactor;
                        }
                        else
                        {
                            amountToAddPerTrip = 1;
                        }

                        if(person.Age < MinAge)
                        {
                            continue;
                        }
                        foreach(var tripChain in person.TripChains)
                        {
                            foreach(var trip in tripChain.Trips)
                            {
                                IZone originalZone = trip.OriginalZone;
                                IZone destinationZone = trip.DestinationZone;
                                if(OriginZones.Contains(originalZone.ZoneNumber) && DestinationZones.Contains(destinationZone.ZoneNumber))
                                {
                                    var tripStartTime = trip.TripStartTime;
                                    if(tripStartTime >= StartTime && tripStartTime < EndTime)
                                    {
                                        if(SaveOD)
                                        {
                                            var dictionary = ODPurposeDictionary[originalZone.ZoneNumber, destinationZone.ZoneNumber];
                                            if(dictionary == null)
                                            {
                                                ODPurposeDictionary[originalZone.ZoneNumber, destinationZone.ZoneNumber] = dictionary = new Dictionary<Activity, float>();
                                            }
                                            AddTripToDictionary(dictionary, amountToAddPerTrip, trip);
                                            
                                        }
                                        else
                                        {
                                            AddTripToDictionary(PurposeDictionary, amountToAddPerTrip, trip);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddTripToDictionary(Dictionary<Activity,float> dictionary, float occurance, ITrip trip)
        {
            if(dictionary.ContainsKey(trip.Purpose))
            {
                dictionary[trip.Purpose] += occurance;
            }
            else
            {
                dictionary.Add(trip.Purpose, occurance);
            }
        }

        public void IterationFinished(int iteration)
        {
            // only run on the last iteration
            if(iteration == Root.TotalIterations - 1)
            {
                var dir = Path.GetDirectoryName(FileName);
                if(dir != null)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(dir);
                    if(!dirInfo.Exists)
                    {
                        dirInfo.Create();
                    }
                }
                using (StreamWriter Writer = new StreamWriter(FileName))
                {
                    if(SaveOD)
                    {
                        Writer.WriteLine("OriginZone,DestinationZone,TripPurpose,NumberOfOccurrences");
                        foreach(var origin in ODPurposeDictionary.ValidIndexes())
                        {
                            var originStr = origin.ToString();
                            foreach(var destintation in ODPurposeDictionary.ValidIndexes(origin))
                            {
                                var destStr = destintation.ToString();
                                var dictionary = ODPurposeDictionary[origin, destintation];
                                if(dictionary != null)
                                {
                                    foreach(var pair in dictionary)
                                    {
                                        Writer.WriteLine("{0},{1},{2},{3}", originStr, destStr, pair.Key, pair.Value);
                                   } 
                                }
                            }
                        }
                    }
                    else
                    {
                        Writer.WriteLine("Trip Purpose, Number of Occurrences");
                        foreach(var pair in PurposeDictionary)
                        {
                            Writer.WriteLine("{0}, {1}", pair.Key, pair.Value);
                        }
                    }
                }

                PurposeDictionary.Clear();
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
            if(iteration == Root.TotalIterations - 1)
            {
                if(SaveOD)
                {
                    ODPurposeDictionary = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<Dictionary<Activity, float>>();
                }
                else
                {
                    PurposeDictionary = new Dictionary<Activity, float>();
                }
            }
        }

        public override string ToString()
        {
            return "Currently Validating Trip Purposes!";
        }
    }
}