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

namespace Tasha.Validation.SmartTrackPerformance
{
    [ModuleInformation(
        Description = "A Performance Measure that counts and records " +
                       "the amount of trips created for each trip purpose." +                       
                       "\nNote: The Expanded trips parameter lets the user choose " +
                       "whether or not he/she wants to look at expansion factors or just frequencies. "
        )]
    public class TripPurposeMeasure : IPostHousehold
    {
        [RunParameter("Expanded Trips?", true, "Did you want to look at expanded trips (false = number of non-expanded trips")]
        public bool ExpandedTrips;

        [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
        public FileLocation TripsByPurpose;

        [SubModelInformation(Required = true, Description = "Where do you want to save the Region Results. Must be in .CSV format.")]
        public FileLocation TripsByRegion;

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<Activity, float> PurposeDictionary = new Dictionary<Activity, float>();

        private Dictionary<string, float> RegionTrips = new Dictionary<string, float>();                            

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

        //[RunParameter("Save By PD", false, "Save the data by Region.")]
        //public bool SaveByRegion;

        public void Execute(ITashaHousehold household, int iteration)
        {
            // only run on the last iteration
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
                                        AddTripToDictionary(PurposeDictionary, amountToAddPerTrip, trip);
                                        AddOriginRegion(trip, RegionTrips, amountToAddPerTrip);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddOriginRegion(ITrip trip, Dictionary<string, float> regionDictionary, float occurance)
        {
            string region;

            if (trip.OriginalZone.ZoneNumber < 1000) { region = "City of Toronto"; }
            else if (trip.OriginalZone.ZoneNumber < 2000) { region = "Region of Durham"; }
            else if (trip.OriginalZone.ZoneNumber < 3000) { region = "Region of York "; }
            else if (trip.OriginalZone.ZoneNumber < 4000) { region = "Region of Peel"; }
            else if (trip.OriginalZone.ZoneNumber < 5000) { region = "Region of Halton"; }
            else if (trip.OriginalZone.ZoneNumber < 6000) { region = "City of Hamilton"; }
            else { region = "External Zones"; }

            if (regionDictionary.ContainsKey(region))
            {
                regionDictionary[region] += occurance;
            }
            else
            {
                regionDictionary.Add(region, occurance);
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
            if(iteration == Root.Iterations - 1)
            {
                using (StreamWriter Writer = new StreamWriter(TripsByPurpose))
                {
                    Writer.WriteLine("Trip Purpose, Number of Occurances");
                    foreach (var pair in PurposeDictionary)
                    {
                        Writer.WriteLine("{0}, {1}", pair.Key, pair.Value);
                    }                                                                                    
                }

                using (StreamWriter Writer = new StreamWriter(TripsByRegion))
                {
                    Writer.WriteLine("Origin Region, Number of Occurances");
                    foreach (var pair in RegionTrips)
                    {
                        Writer.WriteLine("{0}, {1}", pair.Key, pair.Value);                            
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