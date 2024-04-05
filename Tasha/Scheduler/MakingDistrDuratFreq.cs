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
using Datastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    [ModuleInformation(
        Description = "This module is used to calculate the Start time - Duration Distribution file. " +
                        "The defaults for the procedure are: 262 Distribution ID's, 96 start times (15 min intervals), and 97 durations. " +
                        "As an input, the procedure takes in the household and trip data to use for counting and recording distributions. " +
                        "Since no frequency counting is necessary, the procedure simply adds expansion factors to the " +
                        "respective durations under each start time. To calculate the duration, we simply take the start time " +
                        "of the following trip and from that subtract the start time of the current one. " +
                        "As an output, the module produces a .csv file with the following columns: Distribution ID, Start Times, Durations. "

        )]
    public class MakingDistDuratFreq : ITashaRuntime
    {
        public static int MaxFrequency;

        public static int NumberOfAdultDistributions;

        public static int NumberOfAdultFrequencies;

        [RunParameter("FullTimeActivity", "4:40", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time FullTimeActivityDateTime;

        [RunParameter("Max Frequency", 10, "The highest frequency number.")]
        public int MaxFrequencyLocal;

        [RunParameter("MaxPrimeWorkStartTimeForReturnHomeFromWork", "12:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;

        [RunParameter("MinPrimaryWorkDurationForReturnHomeFromWork", "2:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;

        [RunParameter("NumberOfAdultDistributions", 6, "The total number of distributions for adults.")]
        public int NumberOfAdultDistributionsLocal;

        [RunParameter("NumberOfAdultFrequencies", 9, "The total number of frequencies for adults.")]
        public int NumberOfAdultFrequenciesLocal;

        [RunParameter("#OfDistributions", 262, "The number of distributions")]
        public int NumberOfDistributionsLocal;

        public int NumberOfHouseholds;

        [RunParameter("Output Files", "StartDurationDistribution.csv", "The Output File")]
        public string OutputResults;

        [RunParameter("ReturnHomeFromWorkMaxEndTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time ReturnHomeFromWorkMaxEndTimeDateTime;

        [RunParameter("SecondaryWorkMinStartTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time SecondaryWorkMinStartTimeDateTime;

        [RunParameter("SecondaryWork Threshold", "19:00", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time SecondaryWorkThresholdDateTime;

        [RunParameter("Start Time Quantums", 96, "The number of different discreet time options")]
        public int StartTimeQuantums;

        private float CompletedIterationPercentage;

        private int CurrentHousehold;

        private float IterationPercentage;

        [RunParameter("Observed Mode", "ObservedMode", "The attribute name for the observed mode.")]
        public string ObservedMode;

        private float[][][] ResultsArray = new float[262][][];

        [RunParameter("Smooth durations", true, "Smooth the observed durations")]
        public bool SmoothDurations;

        private string Status = "Initializing!";

        [RunParameter("Distance Factor", 1000.0f, "The higher this factor the less smoothing will occure.")]
        public float DistanceFactor;

        [SubModelInformation(Required = false, Description = "All of the modes for this analysis.")]
        public List<ITashaMode> AllModes { get; set; }

        [DoNotAutomate]
        public ITashaMode AutoMode { get; set; }

        [SubModelInformation(Description = "The type of vehicle that auto is", Required = true)]
        public IVehicleType AutoType { get; set; }

        [RunParameter("End of Day", "28:00", typeof(Time), "The time that Tasha will end at.")]
        public Time EndOfDay { get; set; }

        [SubModelInformation(Description = "The model that will load our household", Required = true)]
        public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

        [RunParameter("Input Directory", "../../Input", "The directory that the input files will be in.")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Number of Iterations", 1, "How many iterations do you want?")]
        public int TotalIterations { get; set; }

        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation(Description = "Network data", Required = false)]
        public IList<INetworkData> NetworkData { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }

        public string OutputBaseDirectory { get; set; }

        public bool Parallel { get { return false; } set { } }

        [DoNotAutomate]
        public List<IPostHousehold> PostHousehold { get; set; }

        [DoNotAutomate]
        public List<IPostIteration> PostIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PostRun { get; set; }

        [DoNotAutomate]
        public List<IPostScheduler> PostScheduler { get; set; }

        [DoNotAutomate]
        public List<IPreIteration> PreIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PreRun { get; set; }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(32, 76, 169); }
        }

        [RunParameter("Random Seed", 12345, "The seed for the random number generator")]
        public int RandomSeed { get; set; }

        [SubModelInformation(Description = "The available resources for this model system.", Required = false)]
        public List<IResource> Resources { get; set; }

        [DoNotAutomate]
        public List<ISharedMode> SharedModes { get; set; }

        [RunParameter("Start of Day", "4:00", typeof(Time), "The time that Tasha will start at.")]
        public Time StartOfDay { get; set; }

        [SubModelInformation(Description = "A collection of vehicles that are used by the modes", Required = false)]
        public List<IVehicleType> VehicleTypes { get; set; }

        [SubModelInformation(Description = "Zone System", Required = true)]
        public IZoneSystem ZoneSystem { get; set; }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        public bool ExitRequest()
        {
            return false;
        }

        [DoNotAutomate]
        public int GetIndexOfMode(ITashaMode mode)
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            for (int i = 0; i < ResultsArray.Length; i++)
            {
                ResultsArray[i] = new float[StartTimeQuantums][];
                for (int j = 0; j < ResultsArray[i].Length; j++)
                {
                    ResultsArray[i][j] = new float[StartTimeQuantums + 1];
                }
            }

            Status = "Loading Data";

            ZoneSystem.LoadData();

            if (PostHousehold != null)
            {
                foreach (var module in PostHousehold)
                {
                    module.Load(TotalIterations);
                }
            }

            IterationPercentage = 1f / TotalIterations;

            for (int i = 0; i < TotalIterations; i++)
            {
                CurrentHousehold = 0;
                CompletedIterationPercentage = i * IterationPercentage;
                HouseholdLoader.LoadData();
                RunIteration(i);
            }

            if (PostRun != null)
            {
                foreach (var module in PostRun)
                {
                    module.Start();
                }
            }
            ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return Status;
        }

        private void AddStartTimeDuration(int[][][] eventCount, ITashaPerson person, int startTime, int duration, int id)
        {
            if (id != -1)
            {
                // check to see if we have an entry for this activity
                if (eventCount[id] == null)
                {
                    // if we do not lock the array
                    lock (eventCount)
                    {
                        // check to see if it has already been fixed, if not create the array
                        Thread.MemoryBarrier();
                        if (eventCount[id] == null)
                        {
                            eventCount[id] = new int[StartTimeQuantums][];
                        }
                        Thread.MemoryBarrier();
                    }
                }
                if(startTime < 0 || startTime >= eventCount[id].Length)
                {
                    return;
                    //throw new XTMFRuntimeException(this, $"An invalid start time was attempted to be used for id {id} at start time {startTime}!");
                }
                // check to see if we have an array for this start time
                if (eventCount[id][startTime] == null)
                {
                    // basic if lock if for start time
                    lock (eventCount[id])
                    {
                        Thread.MemoryBarrier();
                        if (eventCount[id][startTime] == null)
                        {
                            eventCount[id][startTime] = new int[StartTimeQuantums + 1];
                        }
                        Thread.MemoryBarrier();
                    }
                }
                // add our event to our local _Count
                try
                {
                    Interlocked.Increment(ref eventCount[id][startTime][duration]);
                }
                catch
                {
                    throw new XTMFRuntimeException(this, "An error occured when trying to update the data for hhld#" + person.Household.HouseholdId
                        + " StartTime:" + startTime + " Duration:" + duration + " ID:" + id);
                }
            }
        }

        private void AssignEpisodes(ITashaPerson person, ref Time workStartTime, ref Time workEndTime, Random random)
        {
            person.InitializePersonalProjects();
            var personData = (SchedulerPersonData)person["SData"];

            foreach (var tripChain in person.TripChains)
            {
                for (int j = 0; j < (tripChain.Trips.Count - 1); j++)
                {
                    var thisTrip = tripChain.Trips[j];
                    var nextTrip = tripChain.Trips[j + 1];
                    thisTrip.Mode = thisTrip[ObservedMode] as ITashaMode;
                    nextTrip.Mode = nextTrip[ObservedMode] as ITashaMode;
                    var startTime = thisTrip.OriginalZone == null || thisTrip.DestinationZone == null ? thisTrip.TripStartTime : thisTrip.ActivityStartTime;
                    var endTime = nextTrip.TripStartTime;
                    var duration = endTime - startTime;
                    if (duration < Time.Zero)
                    {
                        endTime = Time.EndOfDay;
                    }
                    if (endTime < startTime)
                    {
                        startTime = thisTrip.TripStartTime;
                    }

                    if (thisTrip.Purpose == Activity.PrimaryWork || thisTrip.Purpose == Activity.SecondaryWork || thisTrip.Purpose == Activity.WorkBasedBusiness)
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), thisTrip.Purpose, person)
                        {
                            Zone = thisTrip.DestinationZone
                        };
                        if (thisTrip.Purpose == Activity.PrimaryWork || thisTrip.Purpose == Activity.WorkBasedBusiness)
                        {
                            if (workStartTime == Time.Zero || newEpisode.StartTime < workStartTime)
                            {
                                workStartTime = newEpisode.StartTime;
                            }
                            if (workEndTime == Time.Zero || newEpisode.EndTime > workEndTime)
                            {
                                workEndTime = newEpisode.EndTime;
                            }
                        }
                        personData.WorkSchedule.Schedule.Insert(newEpisode, random);
                    }
                    if (thisTrip.Purpose == Activity.School)
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), thisTrip.Purpose, person)
                        {
                            Zone = thisTrip.DestinationZone
                        };
                        personData.SchoolSchedule.Schedule.Insert(newEpisode, random);
                    }
                }
            }
        }

        private void FirstPass(int[][][] eventCount, ref bool invalidHousehold, ITashaPerson person)
        {
            foreach (var tripChain in person.TripChains)
            {
                for (int j = 0; j < (tripChain.Trips.Count - 1); j++)
                {
                    var thisTrip = tripChain.Trips[j];
                    if (IsMainWorkTrip(thisTrip.Purpose))
                    {
                    }
                    else
                    {
                        var nextTrip = tripChain.Trips[j + 1];
                        Time thisStartTime = thisTrip.OriginalZone == null || thisTrip.DestinationZone == null ? thisTrip.TripStartTime : thisTrip.ActivityStartTime;
                        int startTime = (int)Math.Round((thisStartTime.ToMinutes() / 15 - 16), 0);
                        var tripDuration = (int)((nextTrip.TripStartTime - thisStartTime).ToMinutes() / 15);
                        if (!PreProcessTimes(person, ref startTime, ref tripDuration))
                        {
                            invalidHousehold = true;
                            return;
                        }
                        var id = GetID(person, thisTrip);
                        // check to see if we have a real distribution id, if so add it in to our _Count
                        AddStartTimeDuration(eventCount, person, startTime, tripDuration, id);
                    }
                }
            }
        }

        private static bool IsMainWorkTrip(Activity purpose)
        {
            // primary only because you can't split a secondary anyways
            return purpose == Activity.PrimaryWork;
        }

        private int GetID(ITashaPerson person, ITrip trip)
        {
            var id = IsJointTrip(trip) ? (trip.TripChain.JointTripRep ? Distribution.GetDistributionID(person.Household, trip.Purpose) : -1)
                : Distribution.GetDistributionID(person, trip.Purpose);
            return id;
        }

        private bool IsJointTrip(ITrip trip)
        {
            return trip.TripChain.JointTripRep &&
                ((trip.Purpose == Activity.JointOther) | (trip.Purpose == Activity.JointMarket));
        }

        private void LunchPass(ITashaPerson person, int[][][] eventCount, Time workStartTime, Time workEndTime)
        {
            // Lunch pass
            int workStartBucket = (int)((workStartTime.ToMinutes() / 15) - 16);
            int workEndBucket = (int)((workEndTime.ToMinutes() / 15) - 16);
            var chains = person.TripChains;
            var tripChains = chains.Count;
            for (int j = 0; j < tripChains - 1; j++)
            {
                var thisTrip = chains[j].Trips[chains[j].Trips.Count - 1];
                var nextTrip = chains[j + 1].Trips[0];
                Time activityStartTime = thisTrip.OriginalZone == null || thisTrip.DestinationZone == null ? thisTrip.TripStartTime : thisTrip.ActivityStartTime;
                int startTime = (int)(activityStartTime.ToMinutes() / 15 - 16);
                var duration = (int)((nextTrip.TripStartTime - activityStartTime).ToMinutes() / 15);
                if (!PreProcessTimes(person, ref startTime, ref duration))
                {
                    return;
                }
                if ((thisTrip.Purpose == Activity.Home) | (thisTrip.Purpose == Activity.ReturnFromWork))
                {
                    if (startTime >= workStartBucket && startTime + duration <= workEndBucket)
                    {
                        var id = Distribution.GetDistributionID(person, Activity.ReturnFromWork);
                        if (id != -1)
                        {
                            AddStartTimeDuration(eventCount, person, startTime, duration, id);
                        }
                    }
                }
            }
        }

        private bool PreProcessTimes(ITashaPerson person, ref int startTime, ref int duration)
        {
            if (startTime < 0)
            {
                startTime += StartTimeQuantums;
            }
            if (startTime >= StartTimeQuantums)
            {
                return false;
            }
            if (duration <= 0)
            {
                duration = 1;
            }
            if (duration > StartTimeQuantums)
            {
                throw new XTMFRuntimeException(this, "There exists a duration longer than a day in hhld#" + person.Household.HouseholdId);
            }
            return true;
        }

        private void PrintResults()
        {
            Status = "Writing Files...";
            Progress = 0;
            using StreamWriter writer = new StreamWriter(OutputResults);
            writer.WriteLine("DistID, StartTime, Duration, ExpPersons");

            for (int i = 0; i < ResultsArray.Length; i++)
            {
                for (int j = 0; j < 96; j++)
                {
                    for (int t = 0; t < 97; t++)
                    {
                        writer.WriteLine("{0},{1},{2},{3}", i, j, t, ResultsArray[i][j][t]);
                    }
                }
                Progress = (float)i / ResultsArray.Length;
            }
        }

        private void Run(ITashaHousehold household)
        {
            int[][][] eventCount = new int[NumberOfDistributionsLocal][][];
            bool invalidHousehold = false;

            var numberOfPeople = household.Persons.Length;
            Time[] workStartTimes = new Time[numberOfPeople];
            Time[] workEndTimes = new Time[numberOfPeople];
            for (int p = 0; p < numberOfPeople; p++)
            {
                AssignEpisodes(household.Persons[p], ref workStartTimes[p], ref workEndTimes[p], null);
            }
            System.Threading.Tasks.Parallel.For(0, numberOfPeople, delegate (int personNumber)
            {
                ITashaPerson person = household.Persons[personNumber];
                Time workStartTime = workStartTimes[personNumber];
                Time workEndTime = workEndTimes[personNumber];
                FirstPass(eventCount, ref invalidHousehold, person);
                AddPimaryWorkEpisode(person, eventCount, workStartTime, workEndTime);
                LunchPass(person, eventCount, workStartTime, workEndTime);
            });


            // check to see if we have an invalid household, if we do we do not add the data!
            if (invalidHousehold)
            {
                return;
            }
            StoreResults(household.ExpansionFactor, eventCount);

            Interlocked.Increment(ref CurrentHousehold);
            Progress = ((float)CurrentHousehold / NumberOfHouseholds) / TotalIterations + CompletedIterationPercentage;
            household.Recycle();
        }

        private void AddPimaryWorkEpisode(ITashaPerson person, int[][][] eventCount, Time workStartTime, Time workEndTime)
        {
            if (workEndTime > Time.Zero)
            {
                var id = Distribution.GetDistributionID(person, Activity.PrimaryWork);
                if (id >= 0)
                {
                    int startTime = (int)(workStartTime.ToMinutes() / 15 - 16);
                    var duration = (int)((workEndTime - workStartTime).ToMinutes() / 15);
                    if (duration >= 0)
                    {
                        AddStartTimeDuration(eventCount, person, startTime, duration, id);
                    }
                }
            }
        }

        private void RunIteration(int i)
        {
            if (NetworkData != null)
            {
                System.Threading.Tasks.Parallel.ForEach(NetworkData,
                    delegate (INetworkData network)
                {
                    network.LoadData();
                });
            }

            if (PostScheduler != null)
            {
                foreach (var module in PostScheduler)
                {
                    module.IterationStarting(i);
                }
            }

            if (PostHousehold != null)
            {
                foreach (var module in PostHousehold)
                {
                    module.IterationStarting(i);
                }
            }

            RunSerial();

            if (NetworkData != null)
            {
                foreach (var network in NetworkData)
                {
                    network.UnloadData();
                }
            }

            if (PostScheduler != null)
            {
                foreach (var module in PostScheduler)
                {
                    module.IterationFinished(i);
                }
            }

            if (PostHousehold != null)
            {
                foreach (var module in PostHousehold)
                {
                    module.IterationFinished(i);
                }
            }
            HouseholdLoader.Reset();
        }

        private void RunSerial()
        {
            Status = "Calculating Duration/Start Time distributions...";
            Progress = 0;

            var households = HouseholdLoader.ToArray();
            NumberOfHouseholds = households.Length;
            for (int i = 0; i < households.Length; i++)
            {
                ITashaHousehold household = households[i];
                Run(household);
                Progress = (float)i / households.Length;
            }

            if (SmoothDurations)
            {
                System.Threading.Tasks.Parallel.For(0, ResultsArray.Length, id =>
                {
                    // for each start time smooth the distributions
                    for (int start = 0; start < ResultsArray[id].Length; start++)
                    {
                        var originalRow = ResultsArray[id][start];
                        var smoothedRow = new float[originalRow.Length];
                        for (int i = 0; i < originalRow.Length; i++)
                        {
                            for (int j = 0; j < smoothedRow.Length; j++)
                            {
                                var distanceFactor = 1.0f / (Math.Abs(i - j) * DistanceFactor + 1.0f);
                                smoothedRow[j] += originalRow[i] * distanceFactor;
                            }
                        }
                        ResultsArray[id][start] = smoothedRow;
                    }
                });
            }
            ModifyDurationsTimes();
            PrintResults();
        }


        public DistributionFactor[] ModificationFactors;

        public sealed class DistributionFactor : IModule
        {
            [RunParameter("ID Range", "", typeof(RangeSet), "The range of distributions to apply this factor to.")]
            public RangeSet IDRange;

            [RunParameter("Start Range", "", typeof(RangeSet), "The range of distribution start times to apply this factor to.")]
            public RangeSet StartTimeRange;

            [RunParameter("Duration Range", "", typeof(RangeSet), "The range of distribution duration times to apply this factor to.")]
            public RangeSet DurationRange;

            [RunParameter("Factor", 1.0f, "The factor to apply to the selected distributions.")]
            public float Factor;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        private void ModifyDurationsTimes()
        {
            foreach (var mod in ModificationFactors)
            {
                foreach (var idRange in mod.IDRange)
                {
                    for (int id = idRange.Start; id <= idRange.Stop; id++)
                    {
                        if (id < 0 || id >= ResultsArray.Length)
                        {
                            throw new XTMFRuntimeException(mod, $"Invalid Distribution ID number {id}.");
                        }
                        var idRow = ResultsArray[id];
                        foreach (var startTimeRange in mod.StartTimeRange)
                        {
                            for (int start = startTimeRange.Start; start <= startTimeRange.Stop; start++)
                            {
                                var startRow = idRow[start];
                                foreach (var durationRange in mod.DurationRange)
                                {
                                    for (int dur = durationRange.Start; dur <= durationRange.Stop; dur++)
                                    {
                                        startRow[dur] *= mod.Factor;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void StoreResults(float expFactor, int[][][] eventCount)
        {
            // now that we have the _Count of all of the events go and add them to the totals
            System.Threading.Tasks.Parallel.For(0, NumberOfDistributionsLocal, delegate (int id)
            {
                // if there is no data for this ID just continue
                if (eventCount[id] == null) return;
                for (int startTime = 0; startTime < StartTimeQuantums; startTime++)
                {
                    // get the data we want to store, if there is none, continue on to the next start time
                    var eventCountStartTimeArray = eventCount[id][startTime];
                    if (eventCountStartTimeArray == null) continue;
                    var resultStartTimeArray = ResultsArray[id][startTime];
                    for (int duration = 0; duration < StartTimeQuantums + 1; duration++)
                    {
                        // we do not need interlocks here since we are parallel in the distributions
                        resultStartTimeArray[duration] += expFactor * eventCountStartTimeArray[duration];
                    }
                }
            });
        }
    }
}