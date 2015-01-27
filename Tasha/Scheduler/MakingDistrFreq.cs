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

namespace Tasha.Scheduler
{
    [ModuleInformation(
        Description = "This module creates the Frequency-Distribution file that is required to run " +
                        "TASHA. As an input it loads household and trip data, and it computes and " +
                        "records the frequency of each specific DistributionID. It calculates the frequency for work trips " +
                        "separately from all other trips. Furthermore, the process also records any zero-frequency " +
                        "situations in which a person is eligible for an activity, but decides not to participate. As an ouput, the " +
                        "module produces a .csv file which has three columns: DistributionID, Frequency, Expansion Factor. " +
                        "This is the necessary format for running TASHA."
        )]
    public class MakingDistFreq : ITashaRuntime
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

        [RunParameter("Output Files", "FrequencyDistribtuion.csv", "The Output File")]
        public string OutputResults;

        [SubModelInformation(Description = "Primary Mode used for travel times", Required = true)]
        public ITashaMode PrimaryMode;

        [RunParameter("ReturnHomeFromWorkMaxEndTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time ReturnHomeFromWorkMaxEndTimeDateTime;

        [SubModelInformation(Description = "Secondary Mode used for travel times", Required = false)]
        public ITashaMode SecondaryMode;

        [RunParameter("SecondaryWorkMinStartTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time SecondaryWorkMinStartTimeDateTime;

        [RunParameter("SecondaryWork Threshold", "19:00", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time SecondaryWorkThresholdDateTime;

        [RunParameter("Start Time Quantums", 96, "The number of different discreet time options")]
        public int StartTimeQuantums;

        private float CompletedIterationPercentage;

        private int CurrentHousehold = 0;

        private float IterationPercentage;

        private float[][] ResultsArray = new float[262][];

        private string Status = "Initializing!";

        [DoNotAutomate]
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

        [RunParameter("JointMarketInflation", 1f, "Inflate observed joint market activities to generate more episodes")]
        public float JointMarketInflation { get; set; }

        [RunParameter("JointOtherInflation", 1f, "Inflate observed joint other activities to generate more episodes")]
        public float JointOtherInflation { get; set; }

        [RunParameter("MarektInflation", 1f, "Inflate observed individual market activities to generate more episodes")]
        public float MarketInflation { get; set; }

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

        [RunParameter("OtherInflation", 1f, "Inflate observed individual other activities to generate more episodes")]
        public float OtherInflation { get; set; }

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

        [RunParameter("ReturnFromWorkInflation", 1f, "Inflate observed return home from work activities to generate more episodes")]
        public float ReturnFromWorkInflation { get; set; }

        [RunParameter("SecondaryWorkInflation", 1f, "Inflate observed secondary work activities to generate more episodes")]
        public float SecondaryWorkInflation { get; set; }

        [DoNotAutomate]
        public List<ISharedMode> SharedModes { get; set; }

        [RunParameter("Start of Day", "4:00", typeof(Time), "The time that Tasha will start at.")]
        public Time StartOfDay { get; set; }

        [SubModelInformation(Description = "A collection of vehicles that are used by the modes", Required = false)]
        public List<IVehicleType> VehicleTypes { get; set; }

        [RunParameter("WorkAtHomeInflation", 1f, "Inflate observed work at home activities to generate more episodes")]
        public float WorkAtHomeInflation { get; set; }

        [RunParameter("WorkBusinessInflation", 1f, "Inflate observed work business activities to generate more episodes")]
        public float WorkBusinessInflation { get; set; }

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
            Status = "Loading Data";
            for(int i = 0; i < ResultsArray.Length; i++)
            {
                ResultsArray[i] = new float[MaxFrequencyLocal + 1];
            }

            ZoneSystem.LoadData();

            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.Load(Iterations);
                }
            }
            IterationPercentage = 1f / Iterations;
            for(int i = 0; i < Iterations; i++)
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

        private static void AddWorkTrip(ITashaPerson person, int[] eventCount, bool[] addZero, ref Time workStartTime, ref Time workEndTime)
        {
            var id = Distribution.GetDistributionID(person, Activity.PrimaryWork);
            if(id != -1)
            {
                if(workStartTime == Time.Zero)
                {
                    CheckForZero(person, id, Activity.PrimaryWork);
                    addZero[id] = true;
                }
                else
                {
                    CheckForZero(person, id, Activity.PrimaryWork);
                    eventCount[id]++;
                }
            }
        }

        private static void ProcessZeroes(ITashaPerson person, Time workStartTime, int[] eventCount, bool[] addZero, int lunches)
        {
            // Non-Joint
            foreach(Activity activity in Enum.GetValues(typeof(Activity)))
            {
                var id = -1;
                if(IsNonPrimaryWorkEpisodeWithoutPrimary(activity, workStartTime)
                    || (activity == Activity.ReturnFromWork && lunches == 0 && workStartTime != Time.Zero)
                    || (activity != Activity.PrimaryWork && activity != Activity.ReturnFromWork))
                {
                    id = Distribution.GetDistributionID(person, activity);
                    if(id != -1 && eventCount[id] == 0)
                    {
                        CheckForZero(person, id, activity);
                        addZero[id] = true;
                    }
                }
            }
            if(person.Household.Persons.Length >= 2)
            {
                // Joint
                var id = Distribution.GetDistributionID(person.Household, Activity.JointOther);
                if(id != -1 && eventCount[id] == 0)
                {
                    CheckForZero(person, id, Activity.JointOther);
                    addZero[id] = true;
                }
                id = Distribution.GetDistributionID(person.Household, Activity.JointMarket);
                if(id != -1 && eventCount[id] == 0)
                {
                    CheckForZero(person, id, Activity.JointMarket);
                    addZero[id] = true;
                }
            }
        }

        private static void CheckForZero(ITashaPerson person, int id, Activity activity)
        {
            if(id == 0)
            {
                //throw new XTMFRuntimeException(GetPersonString(person, activity));
            }
        }

        private static string GetPersonString(ITashaPerson person, Activity activity)
        {
            return person.Household.HouseholdId.ToString() + ":" + person.Id.ToString() + "'" + (char)person.Occupation + ":" + (char)person.EmploymentStatus + "\r\n" + Enum.GetName(typeof(Activity), activity)+"\r\n" + person.Age.ToString();
        }

        private static bool IsNonPrimaryWorkEpisodeWithoutPrimary(Activity activity, Time workStartTime)
        {
            if(workStartTime == Time.Zero)
            {
                switch(activity)
                {
                    case Activity.WorkAtHomeBusiness:
                    case Activity.WorkBasedBusiness:
                    case Activity.SecondaryWork:
                        return true;
                }
            }
            return false;
        }

        private void AssignWorkSchoolEpisodes(ITashaPerson person, out Time workStartTime, out Time workEndTime, Random random)
        {
            var PersonData = person["SData"] as SchedulerPersonData;
            var primaryVehicle = PrimaryMode.RequiresVehicle;
            workStartTime = Time.Zero;
            workEndTime = Time.Zero;
            foreach(var TripChain in person.TripChains)
            {
                bool usePrimary = SecondaryMode == null || primaryVehicle == null || primaryVehicle.CanUse(person);
                // ignore the last trip because by definition it must be to home
                for(int j = 0; j < (TripChain.Trips.Count - 1); j++)
                {
                    var ThisTrip = TripChain.Trips[j];
                    var NextTrip = TripChain.Trips[j + 1];
                    ThisTrip.Mode = NextTrip.Mode = usePrimary ? PrimaryMode : SecondaryMode;

                    var startTime = ThisTrip.DestinationZone == null || ThisTrip.OriginalZone == null ? ThisTrip.TripStartTime : ThisTrip.ActivityStartTime;
                    var endTime = NextTrip.TripStartTime;
                    if(endTime < startTime)
                    {
                        endTime = Time.EndOfDay;
                    }
                    if(endTime < startTime)
                    {
                        startTime = ThisTrip.TripStartTime;
                    }

                    if(ThisTrip.Purpose == Activity.PrimaryWork || ThisTrip.Purpose == Activity.SecondaryWork || ThisTrip.Purpose == Activity.WorkBasedBusiness)
                    {
                        var NewEpisode = new ActivityEpisode(0, new TimeWindow(startTime, endTime), ThisTrip.Purpose, TripChain.Person);
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
                    else if(ThisTrip.Purpose == Activity.School)
                    {
                        var NewEpisode = new ActivityEpisode(0, new TimeWindow(startTime, endTime), ThisTrip.Purpose, TripChain.Person);
                        NewEpisode.Zone = ThisTrip.DestinationZone;
                        PersonData.SchoolSchedule.Schedule.Insert(NewEpisode, random);
                    }
                }
            }
        }

        private void FirstPass(ITashaPerson person, IVehicleType primaryVehicle, int[] eventCount)
        {
            foreach(var tripChain in person.TripChains)
            {
                if(tripChain.JointTrip)
                {
                    foreach(var trip in tripChain.Trips)
                    {
                        int id = -1;
                        if(((trip.Purpose == Activity.JointOther) | (trip.Purpose == Activity.JointMarket)) & tripChain.JointTripRep)
                        {
                            id = Distribution.GetDistributionID(person.Household, trip.Purpose);
                        }
                        else if(tripChain.JointTripRep || !((trip.Purpose == Activity.JointOther) | (trip.Purpose == Activity.JointMarket)))
                        {
                            id = Distribution.GetDistributionID(person, trip.Purpose);
                        }
                        if(id != -1)
                        {
                            CheckForZero(person, id, trip.Purpose);
                            eventCount[id]++;
                        }
                    }
                }
                else
                {
                    foreach(var trip in tripChain.Trips)
                    {
                        if(!IsWorkTrip(trip.Purpose))
                        {
                            var id = Distribution.GetDistributionID(person, trip.Purpose);
                            if(id != -1)
                            {
                                CheckForZero(person, id, trip.Purpose);
                                eventCount[id]++;
                            }
                        }
                    }
                }
            }
        }

        private bool IsWorkTrip(Activity activity)
        {
            switch(activity)
            {
                case Activity.PrimaryWork:
                    return true;
                default:
                    return false;
            }
        }

        private int LunchPass(ITashaPerson person, int[] eventCount, ref Time workStartTime, ref Time workEndTime)
        {
            int lunchCount = 0;
            // Lunch pass
            foreach(var tripChain in person.TripChains)
            {
                foreach(var trip in tripChain.Trips)
                {
                    if((trip.Purpose == Activity.Home) | (trip.Purpose == Activity.ReturnFromWork))
                    {
                        var startTime = trip.OriginalZone == null || trip.DestinationZone == null ? trip.TripStartTime : trip.ActivityStartTime;
                        if(startTime > workStartTime && startTime < workEndTime)
                        {
                            var id = Distribution.GetDistributionID(person, Activity.ReturnFromWork);
                            if(id != -1)
                            {
                                eventCount[id]++;
                                lunchCount++;
                            }
                        }
                    }
                }
            }
            return lunchCount;
        }

        private void SaveResults()
        {
            Status = "Writing Distribution Frequency File";
            Progress = 0;
            using (StreamWriter Writer = new StreamWriter(OutputResults))
            {
                Writer.WriteLine("DistID, Freq, ExpPersons");

                for(int i = 0; i < ResultsArray.Length; i++)
                {
                    for(int j = 0; j < 11; j++)
                    {
                        if(i < 32)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j]);
                        }
                        else if(i >= 32 && i < 40)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * SecondaryWorkInflation);
                        }
                        else if(i >= 40 && i < 72)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * WorkBusinessInflation);
                        }
                        else if(i >= 72 && i < 84)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * WorkAtHomeInflation);
                        }
                        else if(i >= 84 && i < 94)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j]);
                        }
                        else if(i >= 94 && i < 102)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * ReturnFromWorkInflation);
                        }
                        else if(i >= 102 && i < 158)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * OtherInflation);
                        }
                        else if(i >= 158 && i < 182)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * JointOtherInflation);
                        }
                        else if(i >= 182 && i < 238)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * MarketInflation);
                        }
                        else if(i >= 238 && i < 262)
                        {
                            Writer.WriteLine("{0},{1},{2}", i, j, j == 0 ? ResultsArray[i][j] : ResultsArray[i][j] * JointMarketInflation);
                        }
                    }
                    Progress = (float)i / ResultsArray.Length;
                }
            }
        }

        private void Run(int iteration, ITashaHousehold household)
        {
            var persons = household.Persons;
            var eventCount = new int[NumberOfDistributionsLocal];
            var addZero = new bool[NumberOfDistributionsLocal];
            household.CreateHouseholdProjects();
            for(int i = 0; i < persons.Length; i++)
            {
                persons[i].InitializePersonalProjects();
            }
            for(int i = 0; i < persons.Length; i++)
            {
                Time workStartTime;
                Time workEndTime;
                ITashaPerson person = household.Persons[i];
                AssignWorkSchoolEpisodes(persons[i], out workStartTime, out workEndTime, null);
                var primaryVehicle = PrimaryMode.RequiresVehicle;
                for(int j = 0; j < addZero.Length; j++)
                {
                    eventCount[j] = 0;
                    addZero[j] = false;
                }
                FirstPass(person, primaryVehicle, eventCount);
                int lunches = LunchPass(person, eventCount, ref workStartTime, ref workEndTime);
                AddWorkTrip(person, eventCount, addZero, ref workStartTime, ref workEndTime);
                ProcessZeroes(person, workStartTime, eventCount, addZero, lunches);
                // while adding the results back we need to do this in serial
                StoreResults(person.ExpansionFactor, eventCount, addZero);
            }
            System.Threading.Interlocked.Increment(ref CurrentHousehold);
            Progress = ((float)CurrentHousehold / NumberOfHouseholds) / Iterations + CompletedIterationPercentage;
            household.Recycle();
        }

        private void RunIteration(int i)
        {
            foreach(var network in NetworkData)
            {
                network.LoadData();
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

            RunSerial(i);

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

        private void RunSerial(int iteration)
        {
            Status = "Calculating Distributions...";
            Progress = 0;
            var households = HouseholdLoader.ToArray();
            for(int i = 0; i < households.Length; i++)
            {
                ITashaHousehold household = households[i];
                Run(iteration, household);
                Progress = (float)i / households.Length;
            }
            ModifyResults();
            SaveResults();
        }

        public DistributionFactor[] ModificationFactors;


        public sealed class DistributionFactor : XTMF.IModule
        {
            [RunParameter("ID Range", "", typeof(RangeSet), "The range of distributions to apply this factor to non zero frequencies.")]
            public RangeSet IDRange;

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


        private void ModifyResults()
        {
            foreach(var mod in ModificationFactors)
            {
                foreach(var range in mod.IDRange)
                {
                    for(int id = range.Start; id <= range.Stop; id++)
                    {
                        var row = ResultsArray[id];
                        for(int i = 1; i < row.Length; i++)
                        {
                            row[i] *= mod.Factor;
                        }
                    }
                }
            }
        }

        private void SimulateScheduler()
        {
            Scheduler.MaxFrequency = MaxFrequencyLocal;
            Scheduler.NumberOfAdultDistributions = NumberOfAdultDistributionsLocal;
            Scheduler.NumberOfAdultFrequencies = NumberOfAdultFrequenciesLocal;
            Scheduler.NumberOfDistributions = NumberOfDistributionsLocal;
            Scheduler.StartTimeQuanta = StartTimeQuantums;
            Scheduler.FullTimeActivity = FullTimeActivityDateTime;
            Scheduler.MaxPrimeWorkStartTimeForReturnHomeFromWork = MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;
            Scheduler.MinPrimaryWorkDurationForReturnHomeFromWork = MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;
            Scheduler.ReturnHomeFromWorkMaxEndTime = ReturnHomeFromWorkMaxEndTimeDateTime;
            Scheduler.SecondaryWorkThreshold = SecondaryWorkThresholdDateTime;
            Scheduler.SecondaryWorkMinStartTime = SecondaryWorkMinStartTimeDateTime;
            Time.OneQuantum = Time.FromMinutes(15);
            Time.StartOfDay = new Time() { Hours = 4 };
            Time.EndOfDay = new Time() { Hours = 28 };
        }

        private void StoreResults(float expansionFactor, int[] eventCount, bool[] addZero)
        {
            for(int id = 0; id < eventCount.Length; id++)
            {
                var freq = eventCount[id];
                if(freq > 0 | addZero[id])
                {
                    ResultsArray[id][freq >= MaxFrequencyLocal ? MaxFrequencyLocal : freq] += expansionFactor;
                }
            }
        }
    }
}