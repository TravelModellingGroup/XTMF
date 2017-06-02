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
using System.Linq;
using XTMF;
using Tasha.Common;
using TMG.Input;
using TMG;
using System.Threading;
using Datastructure;
using System.IO;

namespace Tasha.DataExtraction
{

    public class ExtractObservedDemandToPD : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = false, Description = "The different time periods to extract.")]
        public TimePeriod[] TimePeriods;

        internal SparseArray<float> PDArray;

        internal ITashaMode[] Modes;

        public sealed class TimePeriod : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [ParentModel]
            public ExtractObservedDemandToPD Parent;

            [RunParameter("Start Time", "6:00", typeof(Time), "The time this period starts.")]
            public Time StartTime;

            [RunParameter("End Time", "9:00", typeof(Time), "The time this period ends (exclusive).")]
            public Time EndTime;

            [SubModelInformation(Required = false, Description = "The activities that will be counted.")]
            public ActivityType[] AllowedActivities;

            [SubModelInformation(Required = true, Description = "The location to save the demand.")]
            public FileLocation SaveTo;

            [RunParameter("Skip PD0", true, "Should we exclude PD0? (Externals)")]
            public bool SkipPDZero;

            /// <summary>
            /// This is the lock for accessing the demand matrix data, please acquire this before changing the values
            /// </summary>
            private SpinLock WriteLock = new SpinLock(false);

            /// <summary>
            /// Please have the write lock before editing these values
            /// </summary>
            private float[][][] Demand;

            public sealed class ActivityType : IModule
            {
                [RunParameter("Activity", Activity.Home, "The activity to represent.")]
                public Activity Activity;
                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }

            private Activity[] ContainedActivities;

            public void Execute(ITashaHousehold household)
            {
                foreach(var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach(var tripChain in person.TripChains)
                    {
                        foreach(var trip in tripChain.Trips)
                        {
                            int pdO, pdD, mode;
                            if(IsContained(trip, out pdO, out pdD, out mode))
                            {
                                bool taken = false;
                                var row = Demand[mode][pdO];
                                WriteLock.Enter(ref taken);
                                row[pdD] += expFactor;
                                if(taken) WriteLock.Exit(true);
                            }
                        }
                    }
                }
            }

            private bool IsContained(ITrip trip, out int pdO, out int pdD, out int mode)
            {
                // check the time periods work and purpose is contained
                var startTime = trip.TripStartTime;
                IZone origin = trip.OriginalZone;
                IZone destination = trip.DestinationZone;
                if(startTime < StartTime || startTime >= EndTime ||
                    Array.IndexOf(ContainedActivities, trip.Purpose) < 0
                    || origin == null || destination == null)
                {
                    pdO = pdD = mode = 0;
                    return false;
                }
                pdO = Parent.PDArray.GetFlatIndex(origin.PlanningDistrict);
                pdD = Parent.PDArray.GetFlatIndex(destination.PlanningDistrict);
                mode = Array.IndexOf(Parent.Modes, trip.Mode ?? (trip["ObservedMode"] as ITashaMode));
                return mode >= 0;
            }

            public void Initialize()
            {
                Demand = Parent.Modes.Select( _ => TMG.Functions.ZoneSystemHelper.CreatePdTwinArray<float>(Root.ZoneSystem.ZoneArray).GetFlatData()).ToArray();
                ContainedActivities = AllowedActivities.Select(module => module.Activity).ToArray();
            }

            public void Save()
            {
                var pdIndexes = Parent.PDArray.ValidIndexArray();
                using (StreamWriter writer = new StreamWriter(SaveTo))
                {
                    //write header
                    writer.Write("OriginPD\\DestinationPD");
                    for(int i = 0; i < pdIndexes.Length; i++)
                    {
                        writer.Write(',');
                        writer.Write(pdIndexes[i]);
                    }
                    writer.WriteLine();
                    //The main body of the loop is going to end the line
                    //write body
                    for(int mode = 0; mode < Demand.Length; mode++)
                    {
                        writer.WriteLine(Parent.Modes[mode].ModeName);
                        for(int i = 0; i < Demand[mode].Length; i++)
                        {
                            if(SkipPDZero)
                            {
                                if(pdIndexes[i] == 0)
                                {
                                    continue;
                                }
                            }
                            writer.Write(pdIndexes[i]);
                            var row = Demand[mode][i];
                            for(int j = 0; j < row.Length; j++)
                            {
                                if(SkipPDZero)
                                {
                                    if(pdIndexes[j] == 0)
                                    {
                                        continue;
                                    }
                                }
                                writer.Write(',');
                                writer.Write(row[j]);
                            }
                            writer.WriteLine();
                        }
                    }
                }
            }


            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                if(StartTime > EndTime)
                {
                    error = "In '" + Name + "' the start time must come before the end time!";
                }
                return true;
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            foreach(var period in TimePeriods)
            {
                period.Execute(household);
            }
        }

        public void IterationFinished(int iteration)
        {
            foreach(var period in TimePeriods)
            {
                period.Save();
            }
        }

        public void IterationStarting(int iteration)
        {
            Modes = Modes ?? Root.AllModes.OrderBy(m => m.ModeName).ToArray();
            PDArray = PDArray ?? TMG.Functions.ZoneSystemHelper.CreatePdArray<float>(Root.ZoneSystem.ZoneArray);
            foreach(var period in TimePeriods)
            {
                period.Initialize();
            }
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
