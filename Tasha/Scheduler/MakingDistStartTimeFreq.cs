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
using System.Threading;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    [ModuleInformation(
        Description = "This module creates the Start time - Frequency Distribution file required to run Tasha. " +
                        "As a default, we have 262 different Distribution ID's, each of which have 10 frequencies, and " +
                        "each frequency has 96 different start times. The 96 start time ID's are 15 min intervals to make a 24hr day. " +
                        "The procedure calculates the frequency of a trip at the person level, and then adds the household " +
                        "expansion factor to the appropriate start time ID. The output of the module is a .csv " +
                        "file, which has the following columns: Distribution ID, Frequency, Start Time, Expansion Factor. This " +
                        "format is required for a TASHA run. "
        )]
    public class MakingDistStartTimeFreq : ITashaRuntime
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

        [RunParameter("Estimated Households", 148112, "A Guess at the number of households (for progress)")]
        public int NumberOfHouseholds;

        [RunParameter("Output Files", "FrequencyStartDistribution.csv", "The Output File")]
        public string OutputResults;

        [RunParameter("ReturnHomeFromWorkMaxEndTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time ReturnHomeFromWorkMaxEndTimeDateTime;

        [RunParameter("Observed Mode", "ObservedMode", "The attribute name for the observed mode.")]
        public string ObservedMode;

        [RunParameter("SecondaryWorkMinStartTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time SecondaryWorkMinStartTimeDateTime;

        [RunParameter("SecondaryWork Threshhold", "19:00", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time SecondaryWorkThresholdDateTime;

        [RunParameter("Start Time Quantums", 96, "The number of different discreet time options")]
        public int StartTimeQuantums;

        private float CompletedIterationPercentage;

        private int CurrentHousehold;

        private float IterationPercentage;

        private float[][][] ResultsArray;

        private string Status = "Initializing";

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

        [SubModelInformation(Description = "Network Data", Required = false)]
        public IList<INetworkData> NetworkData { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }

        public string OutputBaseDirectory { get; set; }

        [RunParameter("Parallel", false, "Should we run in Parallel?")]
        public bool Parallel { get; set; }

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

        [RunParameter("Smooth", false, "Do you want to smooth the trip start time?")]
        public bool Smooth { get; set; }

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
            ResultsArray = new float[NumberOfDistributionsLocal][][];
            for(int i = 0; i < ResultsArray.Length; i++)
            {
                ResultsArray[i] = new float[MaxFrequencyLocal + 1][];
                for(int j = 0; j < ResultsArray[i].Length; j++)
                {
                    ResultsArray[i][j] = new float[StartTimeQuantums];
                }
            }

            Status = "Loading Data";

            ZoneSystem.LoadData();

            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.Load(TotalIterations);
                }
            }

            IterationPercentage = 1f / TotalIterations;

            for(int i = 0; i < TotalIterations; i++)
            {
                CurrentHousehold = 0;
                CompletedIterationPercentage = i * IterationPercentage;
                HouseholdLoader.LoadData();
                RunIteration(i);
            }

            if(PostRun != null)
            {
                foreach(var module in PostRun)
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

        private bool AddStartTime(int[][] startTimeCount, int id, ITrip trip)
        {
            return AddStartTime(startTimeCount, id, (trip.OriginalZone == null || trip.DestinationZone == null ? trip.TripStartTime : trip.ActivityStartTime));
        }

        private bool AddStartTime(int[][] startTimeCount, int id, Time tripTime)
        {
            int startTime = (int)Math.Round((tripTime.ToMinutes() / 15) - 16, 0);
            if(startTime < 0)
            {
                startTime += StartTimeQuantums;
            }
            if(startTime >= StartTimeQuantums)
            {
                return false;
            }
            if(startTimeCount[id] == null)
            {
                lock (startTimeCount)
                {
                    Thread.MemoryBarrier();
                    if(startTimeCount[id] == null)
                    {
                        startTimeCount[id] = new int[StartTimeQuantums];
                    }
                    Thread.MemoryBarrier();
                }
            }
            startTimeCount[id][startTime]++;
            return true;
        }

        private void AssignEpisodes(ITashaPerson person, ref Time workStartTime, ref Time workEndTime, Random random)
        {
            person.InitializePersonalProjects();
            var personData = (SchedulerPersonData)person["SData"];

            foreach(var tripChain in person.TripChains)
            {
                for(int j = 0; j < (tripChain.Trips.Count - 1); j++)
                {
                    var thisTrip = tripChain.Trips[j];
                    var nextTrip = tripChain.Trips[j + 1];
                    thisTrip.Mode = thisTrip[ObservedMode] as ITashaMode;
                    nextTrip.Mode = nextTrip[ObservedMode] as ITashaMode;
                    var startTime = thisTrip.OriginalZone == null || thisTrip.DestinationZone == null ? thisTrip.TripStartTime : thisTrip.ActivityStartTime;
                    var endTime = nextTrip.TripStartTime;
                    var duration = endTime - startTime;
                    if(duration < Time.Zero)
                    {
                        endTime = Time.EndOfDay;
                    }
                    if(endTime < startTime)
                    {
                        startTime = thisTrip.TripStartTime;
                    }

                    if(thisTrip.Purpose == Activity.PrimaryWork || thisTrip.Purpose == Activity.SecondaryWork || thisTrip.Purpose == Activity.WorkBasedBusiness)
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), thisTrip.Purpose, person);
                        newEpisode.Zone = thisTrip.DestinationZone;
                        if(thisTrip.Purpose == Activity.PrimaryWork || thisTrip.Purpose == Activity.WorkBasedBusiness)
                        {
                            if(workStartTime == Time.Zero || newEpisode.StartTime < workStartTime)
                            {
                                workStartTime = newEpisode.StartTime;
                            }
                            if(workEndTime == Time.Zero || newEpisode.EndTime > workEndTime)
                            {
                                workEndTime = newEpisode.EndTime;
                            }
                        }
                        personData.WorkSchedule.Schedule.Insert(newEpisode, random);
                    }
                    if(thisTrip.Purpose == Activity.School)
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), thisTrip.Purpose, person);
                        newEpisode.Zone = thisTrip.DestinationZone;
                        personData.SchoolSchedule.Schedule.Insert(newEpisode, random);
                    }
                }
            }
        }

        private int GetID(ITashaPerson person, ITrip trip)
        {
            var id = IsJointTrip(trip) ? (trip.TripChain.JointTripRep ? Distribution.GetDistributionID(person.Household, trip.Purpose) : -1)
                : Distribution.GetDistributionID(person, trip.Purpose);
            return id;
        }

        private void IncreaseID(ref bool invalidPerson, int[] eventCount, int[][] startTimeCount, ITrip trip, int id)
        {
            if(id == -1) return;
            eventCount[id]++;
            if(!AddStartTime(startTimeCount, id, trip))
            {
                invalidPerson = true;
            }
        }

        private bool IsJointTrip(ITrip trip)
        {
            return trip.TripChain.JointTripRep &&
                ((trip.Purpose == Activity.JointOther) | (trip.Purpose == Activity.JointMarket));
        }

        private void LunchPass(ITashaPerson person, int[] eventCount, int[][] startTimeCount, ref Time workStartTime, ref Time workEndTime)
        {
            // Lunch pass
            foreach(var tripChain in person.TripChains)
            {
                foreach(var trip in tripChain.Trips)
                {
                    if((trip.Purpose == Activity.Home) | (trip.Purpose == Activity.ReturnFromWork))
                    {
                        Time activityStartTime = trip.OriginalZone == null || trip.DestinationZone == null ? trip.TripStartTime : trip.ActivityStartTime;
                        if(activityStartTime > workStartTime && activityStartTime < workEndTime)
                        {
                            var id = Distribution.GetDistributionID(person, Activity.ReturnFromWork);
                            if(id != -1)
                            {
                                eventCount[id]++;
                                AddStartTime(startTimeCount, id, trip);
                            }
                        }
                    }
                }
            }
        }

        private void PrintResults()
        {
            Status = "Writing Start Time Distribution File...";
            Progress = 0;

            using (StreamWriter writer = new StreamWriter(OutputResults))
            {
                writer.WriteLine("DistID, Freq, StartTime, ExpPersons");

                for(int i = 0; i < ResultsArray.Length; i++)
                {
                    for(int j = 0; j < MaxFrequencyLocal + 1; j++)
                    {
                        for(int t = 0; t < StartTimeQuantums; t++)
                        {
                            writer.WriteLine("{0},{1},{2},{3}", i, j, t, ResultsArray[i][j][t]);
                        }
                    }
                    Progress = (float)i / ResultsArray.Length;
                }
            }
        }

        private void Run(ITashaHousehold household)
        {
            var persons = household.Persons;
            Time[] workStartTimes = new Time[persons.Length];
            Time[] workEndTimes = new Time[persons.Length];
            for(int p = 0; p < persons.Length; p++)
            {
                AssignEpisodes(persons[p], ref workStartTimes[p], ref workEndTimes[p], null);
            }
            System.Threading.Tasks.Parallel.For(0, persons.Length, delegate (int personNumber)
            {
                ITashaPerson person = household.Persons[personNumber];
                Time workStartTime = workStartTimes[personNumber];
                Time workEndTime = workEndTimes[personNumber];
                bool invalidPerson = false;
                var eventCount = new int[NumberOfDistributionsLocal];
                var startTimeCount = new int[NumberOfDistributionsLocal][];
                foreach(var tripChain in person.TripChains)
                {
                    List<ITrip> trips = tripChain.Trips;
                    for(int t = 0; t < trips.Count; t++)
                    {
                        if(trips[t].Purpose != Activity.PrimaryWork)
                        {
                            IncreaseID(ref invalidPerson, eventCount, startTimeCount, trips[t], GetID(person, trips[t]));
                        }
                    }
                }
                if(workStartTime > Time.Zero)
                {
                    var workID = Distribution.GetDistributionID(person, Activity.PrimaryWork);
                    if(workID >= 0)
                    {
                        eventCount[workID]++;
                        AddStartTime(startTimeCount, workID, workStartTime);
                    }
                }
                LunchPass(person, eventCount, startTimeCount, ref workStartTime, ref workEndTime);
                // if this person is invalid skip adding the data back to the final results
                if(invalidPerson)
                {
                    return;
                }
                StoreResults(person.ExpansionFactor, eventCount, startTimeCount);
            });
            Interlocked.Increment(ref CurrentHousehold);
            Progress = ((float)CurrentHousehold / NumberOfHouseholds) / TotalIterations + CompletedIterationPercentage;
            household.Recycle();
        }

        private void RunIteration(int i)
        {
            if(NetworkData != null)
            {
                System.Threading.Tasks.Parallel.ForEach(NetworkData,
                    delegate (INetworkData network)
                {
                    network.LoadData();
                });
            }

            if(PostScheduler != null)
            {
                foreach(var module in PostScheduler)
                {
                    module.IterationStarting(i);
                }
            }

            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.IterationStarting(i);
                }
            }

            RunSerial();

            if(NetworkData != null)
            {
                foreach(var network in NetworkData)
                {
                    network.UnloadData();
                }
            }

            if(PostScheduler != null)
            {
                foreach(var module in PostScheduler)
                {
                    module.IterationFinished(i);
                }
            }

            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.IterationFinished(i);
                }
            }
            HouseholdLoader.Reset();
        }

        private void RunSerial()
        {
            Status = "Calculating Start Time Distributions";
            Progress = 0;
            var households = HouseholdLoader.ToArray();
            for(int i = 0; i < households.Length; i++)
            {
                ITashaHousehold household = households[i];
                Run(household);
                Progress = (float)i / households.Length;
            }
            ModifyStartTimes();
            PrintResults();
        }

        public DistributionFactor[] ModificationFactors;

        private void ModifyStartTimes()
        {
            foreach(var mod in ModificationFactors)
            {
                foreach(var idRange in mod.IDRange)
                {
                    for(int id = idRange.Start; id <= idRange.Stop; id++)
                    {
                        if (id < 0 || id >= ResultsArray.Length)
                        {
                            throw new XTMFRuntimeException(mod, $"Invalid Distribution ID number {id}.");
                        }
                        var idRow = ResultsArray[id];
                        for(int i = 1; i < idRow.Length; i++)
                        {
                            var startRow = idRow[i];
                            foreach(var startTimeRange in mod.StartTimeRange)
                            {
                                for(int start = startTimeRange.Start; start <= startTimeRange.Stop; start++)
                                {
                                    startRow[start] *= mod.Factor;
                                }
                            }
                        }
                    }
                }
            }
        }


        public sealed class DistributionFactor : IModule
        {
            [RunParameter("ID Range", "", typeof(RangeSet), "The range of distributions to apply this factor to.")]
            public RangeSet IDRange;

            [RunParameter("Time Range", "", typeof(RangeSet), "The range of distribution start times to apply this factor to.")]
            public RangeSet StartTimeRange;

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


        private void StoreResults(float expFactor, int[] eventCount, int[][] startTimeCount)
        {
            lock (ResultsArray)
            {
                for(int id = 0; id < eventCount.Length; id++)
                {
                    var freq = eventCount[id] >= MaxFrequencyLocal ? MaxFrequencyLocal : eventCount[id];
                    if(startTimeCount[id] == null) continue;
                    var startTimeArray = startTimeCount[id];
                    var resultRow = ResultsArray[id][freq];
                    if(Smooth)
                    {
                        for(int startTime = 0; startTime < resultRow.Length; startTime++)
                        {
                            resultRow[startTime] += expFactor * startTimeArray[startTime] / 2.0f;
                            resultRow[startTime + 1 < StartTimeQuantums ? startTime + 1 : StartTimeQuantums - 1] += expFactor * startTimeArray[startTime] / 6.0f;
                            resultRow[startTime + 2 < StartTimeQuantums ? startTime + 2 : StartTimeQuantums - 1] += expFactor * startTimeArray[startTime] / 12.0f;
                            resultRow[startTime - 1 > 0 ? startTime - 1 : 0] += expFactor * startTimeArray[startTime] / 6.0f;
                            resultRow[startTime - 2 > 0 ? startTime - 2 : 0] += expFactor * startTimeArray[startTime] / 12.0f;
                        }
                    }
                    else
                    {
                        for(int startTime = 0; startTime < resultRow.Length; startTime++)
                        {
                            resultRow[startTime] += expFactor * startTimeArray[startTime];
                        }
                    }
                }
            }
        }
    }
}