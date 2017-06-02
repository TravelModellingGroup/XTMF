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
using System.Linq;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;
using TMG.Emme;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Tasha.Validation.Scheduler
{
    [ModuleInformation(Description = "This module is designed to extract out the purposes of trips, in a given time period, by persons over a given age.  The result is a matrix in the EMME4+ binary format.")]
    public class ExtractTripPurposes : IPostHousehold
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RootModule]
        public ITravelDemandModel Root;

        float[][] Data;

        SparseArray<IZone> ZoneSystem;

        [SubModelInformation(Required = true, Description = "The location to write the binary matrix to.")]
        public FileLocation WriteTo;

        private Activity[] Activities;

        [SubModelInformation(Required = true, Description = "The activities to capture and store")]
        public ActivityLink[] ActivitiesToCapture;

        [RunParameter("Start Time", "6:00", typeof(Time), "The start time to capture for.")]
        public Time StartTime;

        [RunParameter("End Time", "9:00", typeof(Time), "The start time to capture for.")]
        public Time EndTime;

        [RunParameter("Minimum Age", 11, "The youngest a person can be and still be recorded.")]
        public int MinimumAge;

        [RunParameter("Use Trip Start Times", true, "Use the start time of the trip instead of the activity.")]
        public bool UseTripStartTime;


        public class ActivityLink : IModule
        {
            [RunParameter("Activity", "IndividualOther", typeof(Activity), "The activity to filter for.")]
            public Activity Activity;
            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }


        public void Execute(ITashaHousehold household, int iteration)
        {
            ITashaPerson[] persons = household.Persons;
            for(int i = 0; i < persons.Length; i++)
            {
                if(persons[i].Age < MinimumAge)
                {
                    continue;
                }
                var expFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;
                for(int j = 0; j < tripChains.Count; j++)
                {
                    var trips = tripChains[j].Trips;
                    for(int k = 0; k < trips.Count; k++)
                    {
                        if(Array.IndexOf(Activities, trips[k].Purpose) >= 0)
                        {
                            var activityStartTime = UseTripStartTime ? trips[k].TripStartTime : trips[k].ActivityStartTime;
                            if(activityStartTime >= StartTime && activityStartTime < EndTime)
                            {
                                AddToMatrix(trips[k], expFactor);
                            }
                        }
                    }
                }
            }
        }

        SpinLock WriteLock = new SpinLock(false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToMatrix(ITrip trip, float expFactor)
        {
            bool taken = false;
            var o = ZoneSystem.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var d = ZoneSystem.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            var row = Data[o];
            WriteLock.Enter(ref taken);
            Thread.MemoryBarrier();
            row[d] += expFactor;
            if(taken) WriteLock.Exit(true);
        }

        public void IterationFinished(int iteration)
        {
            new EmmeMatrix(ZoneSystem, Data).Save(WriteTo, false);
        }

        public void IterationStarting(int iteration)
        {
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            if(Data == null)
            {
                Data = ZoneSystem.CreateSquareTwinArray<float>().GetFlatData();
            }
            else
            {
                for(int i = 0; i < Data.Length; i++)
                {
                    Array.Clear(Data[i], 0, Data[i].Length);
                }
            }
            Activities = ActivitiesToCapture.Select(a => a.Activity).ToArray();
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
