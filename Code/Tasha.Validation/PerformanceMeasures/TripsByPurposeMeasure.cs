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
    [ModuleInformation(
        Description = "A Performance Measure that counts and records " +
                       "the amount of trips created for each trip purpose." +
                       "\nNote: The Expanded trips parameter lets the user choose " +
                       "whether or not he/she wants to look at expansion factors or just frequencies. "
        )]
    public class TripsByPurposeMeasure : IPostHousehold
    {

        [SubModelInformation(Required = true, Description = "Folder name in Output Directory where you want to save the files")]
        public FileLocation ResultsFolder;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<Activity, float[]> PurposeDictionary = new Dictionary<Activity, float[]>();

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
            if(iteration == Root.TotalIterations - 1)
            {
                foreach(var person in household.Persons)
                {
                    float amountToAddPerTrip = person.ExpansionFactor; 
                    if(person.Age >= MinAge)
                    {
                        foreach(var tripChain in person.TripChains)
                        {
                            foreach(var trip in tripChain.Trips)
                            {
                                IZone originalZone = trip.OriginalZone;
                                IZone destinationZone = trip.DestinationZone;
                                if(OriginZones.Contains(originalZone.ZoneNumber) && DestinationZones.Contains(destinationZone.ZoneNumber))
                                {
                                    var tripStartTime = trip.ActivityStartTime;
                                    if (trip.Mode != null)
                                    {
                                        if (tripStartTime >= StartTime && tripStartTime < EndTime)
                                    {
                                        lock (this)
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
        }
        }

        private void AddToSummary(ITrip trip, Dictionary<Activity, float> summaryDictionary, float occurance)
        {
            float value;
            var purpose = trip.Purpose;
            if(!summaryDictionary.TryGetValue(purpose, out value))
            {
                value = 0;
            }
            summaryDictionary[trip.Purpose] = value + occurance;
        }

        private void AddTripToDictionary(Dictionary<Activity, float[]> dictionary, float occurance, ITrip trip, int homeZone)
        {
            float[] value;
            var purpose = trip.Purpose;
            if(!dictionary.TryGetValue(purpose, out value))
            {
                dictionary.Add(trip.Purpose, (value = new float[Root.ZoneSystem.ZoneArray.GetFlatData().Length]));
            }
            value[homeZone] += occurance;
        }

        public void IterationFinished(int iteration)
        {
            var zoneFlatData = Root.ZoneSystem.ZoneArray.GetFlatData();
            PurposeDictionary.OrderBy(k => k.Key);
            SummaryTripCount.OrderBy(k => k.Key);

            // only run on the last iteration
            if(iteration == Root.TotalIterations - 1)
            {
                Directory.CreateDirectory(Path.GetFullPath(ResultsFolder));
                var filePath = Path.Combine(Path.GetFullPath(ResultsFolder), "PurposeByHomeZone.csv");
                using (StreamWriter Writer = new StreamWriter(filePath))
                {
                    Writer.WriteLine("Purpose,HomeZone,NumberOfOccurrences");
                    foreach(var pair in PurposeDictionary.OrderBy(k => k.Key))
                    {
                        var format = pair.Key.ToString() + ",{0},{1}";
                        for(int i = 0; i < PurposeDictionary[pair.Key].Length; i++)
                        {
                            Writer.WriteLine(format, zoneFlatData[i].ZoneNumber, PurposeDictionary[pair.Key][i]);
                        }
                    }
                }

                var summaryFilePath = Path.Combine(Path.GetFullPath(ResultsFolder), "SummaryFile.csv");

                using (StreamWriter Writer = new StreamWriter(summaryFilePath))
                {
                    Writer.WriteLine("Purpose, Number of Occurrences");
                    foreach(var pair in SummaryTripCount.OrderBy(k => k.Key))
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