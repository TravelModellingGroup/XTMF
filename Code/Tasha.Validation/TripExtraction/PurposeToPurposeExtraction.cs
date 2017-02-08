/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using Tasha.Common;
using TMG.Input;
using TMG;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures
{
    [ModuleInformation(
        )]
    public class PurposeToPurposeExtraction : IPostHousehold
    {

        [RootModule]
        public ITashaRuntime Root;

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


        public sealed class PurposeTransferGroup : IModule
        {
            public sealed class TimePeriod : IModule
            {
                [RunParameter("StartTime", "6:00AM", typeof(Time), "The time to start recording.")]
                public Time StartTime;

                [RunParameter("EndTime", "9:00AM", typeof(Time), "The time to end recording (exclusive).")]
                public Time EndTime;

                [SubModelInformation(Required = true, Description = "The location to save to.")]
                public FileLocation SaveTo;

                [RunParameter("Save as Binary Matrix", true, "Should we save as a binary EMME matrix?  If false a square CSV will be used.")]
                public bool SaveAsBinaryMatrix;

                [RunParameter("Activity Start Time", false, "Should we be using the activity's start time or the trip's start time?")]
                public bool ActivityStartTime;

                [ParentModel]
                public PurposeTransferGroup Parent;

                SparseTwinIndex<float> Results;

                public bool StoreIfValid(int originIndex, int destinationIndex, ITrip currentTrip, float personExpansionFactor)
                {
                    var startTime = ActivityStartTime ? currentTrip.ActivityStartTime : currentTrip.TripStartTime;
                    if (StartTime <= startTime && startTime < EndTime)
                    {
                        var data = Results.GetFlatData();
                        lock (this)
                        {
                            data[originIndex][destinationIndex] += personExpansionFactor;
                        }
                        return true;
                    }
                    return false;
                }

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }

                internal void Initialize()
                {
                    Results = Parent.ZoneSystem.CreateSquareTwinArray<float>();
                }

                internal void IterationFinished()
                {
                    if (SaveAsBinaryMatrix)
                    {
                        new TMG.Emme.EmmeMatrix(Parent.ZoneSystem, Results.GetFlatData()).Save(SaveTo, false);
                    }
                    else
                    {
                        TMG.Functions.SaveData.SaveMatrix(Results, SaveTo);
                    }
                }
            }

            [SubModelInformation(Required = true, Description = "The different time periods to work with")]
            public TimePeriod[] TimePeriods;

            public class ActivityModule : IModule
            {

                [RunParameter("Activity", "PrimaryWork", typeof(Activity), "The activity to represent.")]
                public Activity Activity;

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }

            [RunParameter("Minimum Age", 11, "The minimum age a person needs top be in order to be recorded.")]
            public int MinimumAge;

            [RunParameter("Origin Zones", "1-9999", typeof(RangeSet), "The origin zones to select for.")]
            public RangeSet OriginZones;

            [RunParameter("Destination Zones", "1-9999", typeof(RangeSet), "The destination zones to select for.")]
            public RangeSet DestinationZones;

            [SubModelInformation(Required = true, Description = "The purposes to record from the origin")]
            public ActivityModule[] OriginPurpose;

            [SubModelInformation(Required = true, Description = "The purposes to record from the destination")]
            public ActivityModule[] DestinationPurpose;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            private SparseArray<IZone> ZoneSystem;

            [RootModule]
            public ITravelDemandModel Root;

            /// <summary>
            /// This must be called when an iteration is starting that we will be recording for.
            /// </summary>
            public void IterationStarting()
            {
                ZoneSystem = Root.ZoneSystem.ZoneArray;
                foreach (var timePeriod in TimePeriods)
                {
                    timePeriod.Initialize();
                }
            }

            /// <summary>
            /// Record the household data if it passes the tests
            /// </summary>
            /// <param name="household">The household to record</param>
            public void RecordHousehold(ITashaHousehold household)
            {
                foreach (var person in household.Persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    var expansionFactor = person.ExpansionFactor;
                    var tripChains = person.TripChains;
                    for (int i = 0; i < tripChains.Count; i++)
                    {
                        var chain = tripChains[i].Trips;
                        for (int j = 0; j < chain.Count; j++)
                        {
                            var previousActivity = j == 0 ? Activity.Home : chain[j - 1].Purpose;
                            var currentActivity = chain[j].Purpose;
                            if (ContainsPurpose(OriginPurpose, previousActivity)
                                && ContainsPurpose(DestinationPurpose, currentActivity))
                            {
                                var originZoneNumber = chain[j].OriginalZone.ZoneNumber;
                                var originIndex = ZoneSystem.GetFlatIndex(originZoneNumber);
                                var destinationZoneNumber = chain[j].DestinationZone.ZoneNumber;
                                var destinationIndex = ZoneSystem.GetFlatIndex(destinationZoneNumber);
                                if (TestZones(originZoneNumber, destinationZoneNumber))
                                {
                                    foreach (var period in TimePeriods)
                                    {
                                        if (period.StoreIfValid(originIndex, destinationIndex, chain[j], expansionFactor))
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Check to see if the given purpose is contained within the set of purposes
            /// </summary>
            /// <param name="setOfPurposes">The set to check</param>
            /// <param name="purpose">The purpose to check for</param>
            /// <returns>True if it is contained, false otherwise.</returns>
            private bool ContainsPurpose(ActivityModule[] setOfPurposes, Activity purpose)
            {
                for (int i = 0; i < setOfPurposes.Length; i++)
                {
                    if(setOfPurposes[i].Activity == purpose)
                    {
                        return true;
                    }
                }
                return false;
            }

            [RunParameter("Require both zones contained", true, "Require that both zones are contained.  Setting this to false will result in recording if at least one is contained.")]
            public bool And;

            /// <summary>
            /// Test to see if the origin and destinations pass the test to be recorded
            /// </summary>
            /// <param name="originZoneNumber">The sparse zone number for the origin of the trip</param>
            /// <param name="destinationZoneNumber">The sparse zone number for the destination of the trip</param>
            /// <returns>True if it passes the spatial test, false otherwise.</returns>
            private bool TestZones(int originZoneNumber, int destinationZoneNumber)
            {
                return And ? OriginZones.Contains(originZoneNumber) && DestinationZones.Contains(destinationZoneNumber)
                    : OriginZones.Contains(originZoneNumber) || DestinationZones.Contains(destinationZoneNumber);
            }

            /// <summary>
            /// This must be called at the end of an iteration that we recorded in order to save the data to disk
            /// </summary>
            public void IterationFinished()
            {
                foreach (var timePeriod in TimePeriods)
                {
                    timePeriod.IterationFinished();
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Description = "The activity groups split trips into.")]
        public PurposeTransferGroup[] PurposeTransfers;


        public void Execute(ITashaHousehold household, int iteration)
        {
            // only run on the last iteration
            if (iteration == Root.TotalIterations - 1)
            {
                foreach (var type in PurposeTransfers)
                {
                    type.RecordHousehold(household);
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            if (iteration == Root.TotalIterations - 1)
            {
                foreach (var type in PurposeTransfers)
                {
                    type.IterationFinished();
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
            if (iteration == Root.TotalIterations - 1)
            {
                foreach (var type in PurposeTransfers)
                {
                    type.IterationStarting();
                }
            }
        }

        public override string ToString()
        {
            return "Currently Extracting Trip Purposes!";
        }
    }
}