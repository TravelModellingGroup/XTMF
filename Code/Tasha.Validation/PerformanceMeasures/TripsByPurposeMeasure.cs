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
using Datastructure;
using Tasha.Common;
using TMG.Input;
using TMG;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures
{
    [ModuleInformation(
        Description = "A Performance Measure that counts and records " +
                       "the amount of trips created for each trip purpose." +                       
                       "\nNote: The Expanded trips parameter lets the user choose " +
                       "whether or not he/she wants to look at expansion factors or just frequencies. "
        )]
    public class TripsByPurposeMeasure : IPostHousehold
    {        
        [RunParameter("Expanded Trips?", true, "Did you want to look at expanded trips (false = number of non-expanded trips")]
        public bool ExpandedTrips;

        [SubModelInformation(Required = true, Description = "Folder name in Output Directory where you want to save the files")]
        public FileLocation ResultsFolder;        

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<Activity, float[]> PurposeDictionary = new Dictionary<Activity,float[]>();

        private Dictionary<Activity, float> SummaryTripCount = new Dictionary<Activity, float>();                            

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

        public void Execute(ITashaHousehold household, int iteration)
        {
            // only run on the last iteration

            var homeZoneIndex = Root.ZoneSystem.ZoneArray.GetFlatIndex(household.HomeZone.ZoneNumber);
            
            if(iteration == Root.Iterations - 1)
            {
                lock (this)
                {
                    float amountToAddPerTrip;
                    if(ExpandedTrips)
                    {
                        amountToAddPerTrip = household.ExpansionFactor;
                    }
                    else
                    {
                        amountToAddPerTrip = 1;
                    }

                    foreach(var person in household.Persons)
                    {
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

                                        AddTripToDictionary(PurposeDictionary, amountToAddPerTrip, trip, homeZoneIndex);
                                        AddToSummary(trip, SummaryTripCount, amountToAddPerTrip);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddToSummary(ITrip trip, Dictionary<Activity, float> summaryDictionary, float occurance)
        {                        

            if (summaryDictionary.ContainsKey(trip.Purpose))
            {
                summaryDictionary[trip.Purpose] += occurance;
            }
            else
            {
                summaryDictionary.Add(trip.Purpose, occurance);
            }            
        }       

        private void AddTripToDictionary(Dictionary<Activity,float[]> dictionary, float occurance, ITrip trip, int homeZone)
        {
            if(dictionary.ContainsKey(trip.Purpose))
            {
                dictionary[trip.Purpose][homeZone] += occurance;
            }
            else
            {
                dictionary.Add(trip.Purpose, new float[Root.ZoneSystem.ZoneArray.GetFlatData().Length]);
                dictionary[trip.Purpose][homeZone] += occurance;
            }
        }

        public void IterationFinished(int iteration)
        {
            var zoneFlatData = Root.ZoneSystem.ZoneArray.GetFlatData();
            // only run on the last iteration
            if(iteration == Root.Iterations - 1)
            {
                foreach(var purpose in PurposeDictionary.Keys)
                {
                    Directory.CreateDirectory(Path.GetFullPath(ResultsFolder));
                    var filePath = Path.Combine(Path.GetFullPath(ResultsFolder), purpose.ToString() + ".csv");
                    using (StreamWriter Writer = new StreamWriter(filePath))
                    {
                        Writer.WriteLine("Home Zone", "Number of Occurrences");
                        for(int i = 0; i < PurposeDictionary[purpose].Length; i++)
                        {
                            Writer.WriteLine("{0}, {1}", zoneFlatData[i].ZoneNumber, PurposeDictionary[purpose][i]);
                        }
                    }
                }
                
                var summaryFilePath = Path.Combine(Path.GetFullPath(ResultsFolder), "SummaryFile.csv");

                using (StreamWriter Writer = new StreamWriter(summaryFilePath))
                {
                    Writer.WriteLine("Purpose, Number of Occurrences");
                    foreach (var pair in SummaryTripCount)
                    {
                        Writer.WriteLine("{0}, {1}", pair.Key.ToString(), pair.Value);                            
                    }                        
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
            return "Currently Validating Trip Purposes!";
        }
    }
}