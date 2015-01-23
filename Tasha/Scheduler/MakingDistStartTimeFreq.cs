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

        private int CurrentHousehold = 0;

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
        public int Iterations { get; set; }

        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation(Description = "Network Data", Required = false)]
        public IList<TMG.INetworkData> NetworkData { get; set; }

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
        public TMG.IZoneSystem ZoneSystem { get; set; }

        public ITrip CreateTrip(ITripChain chain, TMG.IZone originalZone, TMG.IZone destinationZone, Activity purpose, Time startTime)
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
            this.ResultsArray = new float[this.NumberOfDistributionsLocal][][];
            for(int i = 0; i < ResultsArray.Length; i++)
            {
                this.ResultsArray[i] = new float[this.MaxFrequencyLocal + 1][];
                for(int j = 0; j < ResultsArray[i].Length; j++)
                {
                    this.ResultsArray[i][j] = new float[this.StartTimeQuantums];
                }
            }

            this.Status = "Loading Data";

            this.ZoneSystem.LoadData();

            if(this.PostHousehold != null)
            {
                foreach(var module in this.PostHousehold)
                {
                    module.Load(this.Iterations);
                }
            }

            this.IterationPercentage = 1f / this.Iterations;

            for(int i = 0; i < this.Iterations; i++)
            {
                this.CurrentHousehold = 0;
                this.CompletedIterationPercentage = i * this.IterationPercentage;
                this.HouseholdLoader.LoadData();
                RunIteration(i);
            }

            if(this.PostRun != null)
            {
                foreach(var module in this.PostRun)
                {
                    module.Start();
                }
            }
            this.ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return Status;
        }

        private bool AddStartTime(int[][] startTimeCount, int id, ITrip trip)
        {
            return AddStartTime(startTimeCount, id, (trip.OriginalZone == null || trip.DestinationZone == null ? trip.TripStartTime : trip.ActivityStartTime) );
        }

        private bool AddStartTime(int[][] startTimeCount, int id, Time tripTime)
        {
            int startTime = (int)Math.Round((tripTime.ToMinutes() / 15) - 16, 0);
            if(startTime < 0)
            {
                startTime += this.StartTimeQuantums;
            }
            if(startTime >= this.StartTimeQuantums)
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
                        startTimeCount[id] = new int[this.StartTimeQuantums];
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
            var PersonData = person["SData"] as SchedulerPersonData;

            foreach(var TripChain in person.TripChains)
            {
                for(int j = 0; j < (TripChain.Trips.Count - 1); j++)
                {
                    var ThisTrip = TripChain.Trips[j];
                    var NextTrip = TripChain.Trips[j + 1];
                    ThisTrip.Mode = ThisTrip[ObservedMode] as ITashaMode;
                    NextTrip.Mode = NextTrip[ObservedMode] as ITashaMode;
                    var startTime = ThisTrip.OriginalZone == null || ThisTrip.DestinationZone == null ? ThisTrip.TripStartTime : ThisTrip.ActivityStartTime;
                    var endTime = NextTrip.TripStartTime;
                    var duration = endTime - startTime;
                    if(duration < Time.Zero)
                    {
                        endTime = Time.EndOfDay;
                    }
                    if(endTime < startTime)
                    {
                        startTime = ThisTrip.TripStartTime;
                    }

                    if(ThisTrip.Purpose == Activity.PrimaryWork || ThisTrip.Purpose == Activity.SecondaryWork || ThisTrip.Purpose == Activity.WorkBasedBusiness)
                    {
                        var NewEpisode = new ActivityEpisode(0, new TimeWindow(startTime, endTime), ThisTrip.Purpose, person);
                        NewEpisode.Zone = ThisTrip.DestinationZone;
                        if(workStartTime == Time.Zero || NewEpisode.StartTime < workStartTime)
                        {
                            workStartTime = NewEpisode.StartTime;
                        }
                        if(workEndTime == Time.Zero || NewEpisode.EndTime > workEndTime)
                        {
                            workEndTime = NewEpisode.EndTime;
                        }
                        PersonData.WorkSchedule.Schedule.Insert(NewEpisode, random);
                    }
                    if(ThisTrip.Purpose == Activity.School)
                    {
                        var NewEpisode = new ActivityEpisode(0, new TimeWindow(startTime, endTime), ThisTrip.Purpose, person);
                        NewEpisode.Zone = ThisTrip.DestinationZone;
                        PersonData.SchoolSchedule.Schedule.Insert(NewEpisode, random);
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

        private int LunchPass(ITashaPerson person, int[] eventCount, int[][] startTimeCount, ref Time workStartTime, ref Time workEndTime)
        {
            int lunchCount = 0;
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
                                lunchCount++;
                            }
                        }
                    }
                }
            }
            return lunchCount;
        }

        private void PrintResults()
        {
            this.Status = "Writing Start Time Distribution File...";
            this.Progress = 0;

            using (StreamWriter Writer = new StreamWriter(OutputResults))
            {
                Writer.WriteLine("DistID, Freq, StartTime, ExpPersons");

                for(int i = 0; i < ResultsArray.Length; i++)
                {
                    for(int j = 0; j < this.MaxFrequencyLocal + 1; j++)
                    {
                        for(int t = 0; t < this.StartTimeQuantums; t++)
                        {
                            Writer.WriteLine("{0},{1},{2},{3}", i, j, t, ResultsArray[i][j][t]);
                        }
                    }
                    this.Progress = (float)i / ResultsArray.Length;
                }
            }
        }

        private void Run(int i, ITashaHousehold household)
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
                var eventCount = new int[this.NumberOfDistributionsLocal];
                var startTimeCount = new int[this.NumberOfDistributionsLocal][];
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
                        AddStartTime(startTimeCount, workID, workStartTime );
                    }
                }
                int lunches = LunchPass(person, eventCount, startTimeCount, ref workStartTime, ref workEndTime);
                // if this person is invalid skip adding the data back to the final results
                if(invalidPerson)
                {
                    return;
                }
                StoreResults(person.ExpansionFactor, eventCount, startTimeCount);
            });
            System.Threading.Interlocked.Increment(ref this.CurrentHousehold);
            this.Progress = ((float)this.CurrentHousehold / this.NumberOfHouseholds) / this.Iterations + this.CompletedIterationPercentage;
            household.Recycle();
        }

        private void RunIteration(int i)
        {
            if(this.NetworkData != null)
            {
                System.Threading.Tasks.Parallel.ForEach(this.NetworkData,
                    delegate (INetworkData network)
                {
                    network.LoadData();
                });
            }

            if(this.PostScheduler != null)
            {
                foreach(var module in this.PostScheduler)
                {
                    module.IterationStarting(i);
                }
            }

            if(this.PostHousehold != null)
            {
                foreach(var module in this.PostHousehold)
                {
                    module.IterationStarting(i);
                }
            }

            RunSerial(i);

            if(this.NetworkData != null)
            {
                foreach(var network in this.NetworkData)
                {
                    network.UnloadData();
                }
            }

            if(this.PostScheduler != null)
            {
                foreach(var module in this.PostScheduler)
                {
                    module.IterationFinished(i);
                }
            }

            if(this.PostHousehold != null)
            {
                foreach(var module in this.PostHousehold)
                {
                    module.IterationFinished(i);
                }
            }
            this.HouseholdLoader.Reset();
        }

        private void RunParallel(int iteration)
        {
            var hhlds = this.HouseholdLoader.ToArray();
            System.Threading.Tasks.Parallel.For(0, hhlds.Length,
               delegate (int i)
            {
                ITashaHousehold hhld = hhlds[i];
                this.Run(iteration, hhld);
            }
             );
        }

        private void RunSerial(int iteration)
        {
            this.Status = "Calculating Start Time Distributions";
            this.Progress = 0;
            var households = this.HouseholdLoader.ToArray();
            for(int i = 0; i < households.Length; i++)
            {
                ITashaHousehold household = households[i];
                this.Run(iteration, household);
                this.Progress = (float)i / households.Length;
            }
            PrintResults();
        }

        private void SimulateScheduler()
        {
            Scheduler.MaxFrequency = this.MaxFrequencyLocal;
            Scheduler.NumberOfAdultDistributions = this.NumberOfAdultDistributionsLocal;
            Scheduler.NumberOfAdultFrequencies = this.NumberOfAdultFrequenciesLocal;
            Scheduler.NumberOfDistributions = this.NumberOfDistributionsLocal;
            Scheduler.StartTimeQuanta = this.StartTimeQuantums;
            Scheduler.FullTimeActivity = this.FullTimeActivityDateTime;
            Scheduler.MaxPrimeWorkStartTimeForReturnHomeFromWork = this.MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;
            Scheduler.MinPrimaryWorkDurationForReturnHomeFromWork = this.MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;
            Scheduler.ReturnHomeFromWorkMaxEndTime = this.ReturnHomeFromWorkMaxEndTimeDateTime;
            Scheduler.SecondaryWorkThreshold = this.SecondaryWorkThresholdDateTime;
            Scheduler.SecondaryWorkMinStartTime = this.SecondaryWorkMinStartTimeDateTime;
        }

        private void StoreResults(float expFactor, int[] eventCount, int[][] startTimeCount)
        {
            lock (ResultsArray)
            {
                for(int id = 0; id < eventCount.Length; id++)
                {
                    var freq = eventCount[id] >= this.MaxFrequencyLocal ? this.MaxFrequencyLocal : eventCount[id];
                    if(startTimeCount[id] == null) continue;
                    var startTimeArray = startTimeCount[id];
                    var resultRow = ResultsArray[id][freq];
                    if(Smooth == true)
                    {
                        for(int startTime = 0; startTime < resultRow.Length; startTime++)
                        {
                            resultRow[startTime] += expFactor * startTimeArray[startTime] / 2.0f;
                            resultRow[startTime + 1 < this.StartTimeQuantums ? startTime + 1 : this.StartTimeQuantums - 1] += expFactor * startTimeArray[startTime] / 6.0f;
                            resultRow[startTime + 2 < this.StartTimeQuantums ? startTime + 2 : this.StartTimeQuantums - 1] += expFactor * startTimeArray[startTime] / 12.0f;
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