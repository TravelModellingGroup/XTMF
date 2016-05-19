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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    /// <summary>
    /// Handles the scheduling of the population
    /// </summary>
    [ModuleInformation(Description = "This module provides the activity scheduling algorithm for TASHA/GTAModelV4.0.")]
    public class Scheduler : ITashaScheduler
    {
        public static string ActivityLevels;

        public static string AdultDistributionsFile;

        /// <summary>
        /// The most amount of times we will try
        /// </summary>
        public static int EpisodeSchedulingAttempts;

        public static string FrequencyDistributionsFile;

        public static Time FullTimeActivity;

        [DoNotAutomate]
        public static Scheduler LocalScheduler;

        [DoNotAutomate]
        public static ILocationChoiceModel LocationChoiceModel;

        public static int MaxFrequency;

        public static Time MaxPrimeWorkStartTimeForReturnHomeFromWork;

        /// <summary>
        /// The smallest age a person can start working at
        /// </summary>
        public static int MinimumWorkingAge;

        public static Time MinPrimaryWorkDurationForReturnHomeFromWork;

        public static int MinWorkingAge;

        public static int NumberOfAdultDistributions;

        public static int NumberOfAdultFrequencies;

        public static int NumberOfDistributions;

        /// <summary>
        /// The number of internal zones that exist within the network
        /// </summary>
        public static int NumInternalZones;

        public static float PercentOverlapAllowed;

        public static int RandomSeed;

        public static Time ReturnHomeFromWorkMaxEndTime;

        /// <summary>
        /// When the afternoon session for school ends
        /// </summary>
        public static Time SchoolAfternoonEnd;

        /// <summary>
        /// What the afternoon session for students starts
        /// </summary>
        public static Time SchoolAfternoonStart;

        /// <summary>
        /// When the morning ends for students
        /// </summary>
        public static Time SchoolMorningEnd;

        /// <summary>
        /// The start of school in the morning
        /// </summary>
        public static Time SchoolMorningStart;

        public static Time SecondaryWorkMinStartTime;

        public static Time SecondaryWorkThreshold;

        /// <summary>
        /// The number of different bins for start times for a given day
        /// </summary>
        public static int StartTimeQuanta;

        /// <summary>
        /// How long a given start time bin is
        /// </summary>
        public static int StartTimeQuantaInterval;

        [DoNotAutomate]
        public static ITashaRuntime Tasha;

        [RunParameter("Activity Levels", "ActivityLevels.zfc", "The location of the activity level file")]
        public string ActivityLevelsLocal;

        [RunParameter("AdultDistributionFile", "AdultDistributions.zfc", "The file containing all of the adult distributions.")]
        public string AdultDistributionsFileLocal;

        [DoNotAutomate]
        public ITashaMode AlternativeTravelMode;

        [RunParameter("Alternative Travel Mode", "Transit", "The mode to use if using a car is not feasible")]
        public string AlternativeTravelModeName;

        [DoNotAutomate]
        public ITashaMode AutoMode;

        [RunParameter("Max Attempts", 10, "The highest number of attempts to schedule an episode")]
        public int EpisodeSchedulingAttemptsLocal;

        [RunParameter("Frequency Distribution File", "FrequencyDistributions.zfc", "The location of the frequency distribution file.")]
        public string FrequencyDistributionsFileLocal;

        [RunParameter("FullTimeActivity", "4:40", typeof(Time), "The minimum amount time for activity to be concerned as full time activity")]
        public Time FullTimeActivityDateTime;

        [SubModelInformation(Description = "The location choice used by scheduler", Required = true)]
        public ILocationChoiceModel LocationChoiceModelLocal;

        [RunParameter("Max Frequency", 10, "The highest frequency number.")]
        public int MaxFrequencyLocal;

        [RunParameter("MaxPrimeWorkStartTimeForReturnHomeFromWork", "12:00", typeof(Time), "The maximum time you can work and still have a return home from work trip.")]
        public Time MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;

        [RunParameter("Minimum Duration", "00:05", typeof(Time), "The minimum time interval")]
        public Time MinimumDurationDateTime;

        [RunParameter("Min Working Age", 11, "The youngest age a person is allowed to start working at.")]
        public int MinimumWorkingAgeLocal;

        [RunParameter("MinPrimaryWorkDurationForReturnHomeFromWork", "2:00", typeof(Time), "The length of time you need to work before you are allowed to make a return home from work trip.")]
        public Time MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;

        [RunParameter("MinWorkingAge", 11, "The youngest a person is allowed to work at.")]
        public int MinWorkingAgeLocal;

        [RunParameter("NumberOfAdultDistributions", 6, "The total number of distributions for adults.")]
        public int NumberOfAdultDistributionsLocal;

        [RunParameter("NumberOfAdultFrequencies", 9, "The total number of frequencies for adults.")]
        public int NumberOfAdultFrequenciesLocal;

        [RunParameter("#OfDistributions", 262, "The number of distributions")]
        public int NumberOfDistributionsLocal;

        [RunParameter("PercentOverlapAllowed", 0.5f, "The amount of an activity that can be overlapped (0 to 1).")]
        public float PercentOverlapAllowedLocal;

        [RunParameter("ReturnHomeFromWorkMaxEndTime", "15:00", typeof(Time), "The latest that a return home from work trip can occur.")]
        public Time ReturnHomeFromWorkMaxEndTimeDateTime;

        [RunParameter("School AfterNoon End", "3:30 PM", typeof(Time), "When does the Afternoon Session of School end? Also, end time for Full Day School event.")]
        public Time SchoolAfternoonEndDateTime;

        [RunParameter("School Afternoon Start", "12:15 PM", typeof(Time), "When does the Afternoon Session of School Start?")]
        public Time SchoolAfternoonStartDateTime;

        [RunParameter("School Morning End", "12:00 PM", typeof(Time), "When does the Morning Session of School end?")]
        public Time SchoolMorningEndDateTime;

        [RunParameter("School Morning Start", "8:45 AM", typeof(Time), "When does school start in the Morning?")]
        public Time SchoolMorningStartDateTime;

        [RunParameter("SecondaryWorkMinStartTime", "15:00", typeof(Time), "The earliest that a secondary work activity episode can occur.")]
        public Time SecondaryWorkMinStartTimeDateTime;

        [RunParameter("SecondaryWork Threshold", "19:00", typeof(Time), "The latest secondary work can start at")]
        public Time SecondaryWorkThresholdDateTime;

        [RunParameter("Minimum At Home Time", "15 minutes", typeof(Time), "The minimum amount of time to spend at home between trips in order to separate chains.")]
        public Time MinimumTimeAtHomeBetweenActivities;

        [RunParameter("#Start Time Quanta", 96, "The number of start time quanta for the distributions")]
        public int StartTimeQuantaLocal;

        [SubModelInformation(Description = "Adjustments to the generation rates to allow for spatial differences.")]
        public GenerationAdjustment[] GenerationRateAdjustments;

        internal static int SchedulingFail = 0;

        internal static int SchedulingSuccess = 0;

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
            get { return new Tuple<byte, byte, byte>(50, 150, 50); }
        }

        [RootModule]
        public ITashaRuntime TashaRuntime { get; set; }

        /// <summary>
        /// Load the data we only need to get once
        /// </summary>
        public void LoadOneTimeLocalData()
        {
            if (this.AlternativeTravelModeName != null && this.AlternativeTravelModeName != String.Empty)
            {
                var allModes = this.TashaRuntime.AllModes;
                bool found = false;
                foreach (var mode in allModes)
                {
                    if (mode.ModeName == this.AlternativeTravelModeName)
                    {
                        this.AlternativeTravelMode = mode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new XTMFRuntimeException("Unable to find the Alternative Travel Mode called " + this.AlternativeTravelModeName);
                }
            }
            Scheduler.LocalScheduler = this;
            Scheduler.Tasha = this.TashaRuntime;
            Scheduler.MinimumWorkingAge = this.MinWorkingAgeLocal;
            Scheduler.EpisodeSchedulingAttempts = this.EpisodeSchedulingAttemptsLocal;
            Scheduler.ActivityLevels = this.GetFullPath(this.ActivityLevelsLocal);
            Scheduler.SchoolMorningStart = this.SchoolMorningStartDateTime;
            Scheduler.SchoolMorningEnd = this.SchoolMorningEndDateTime;
            Scheduler.SchoolAfternoonStart = this.SchoolAfternoonStartDateTime;
            Scheduler.SchoolAfternoonEnd = this.SchoolAfternoonEndDateTime;
            Scheduler.SecondaryWorkThreshold = this.SecondaryWorkThresholdDateTime;
            Scheduler.StartTimeQuanta = this.StartTimeQuantaLocal;
            Scheduler.StartTimeQuantaInterval = (short)((24 * 60) / Scheduler.StartTimeQuanta);
            Time.OneQuantum = this.MinimumDurationDateTime;
            Time.StartOfDay = new Time() { Hours = 4 };
            Time.EndOfDay = new Time() { Hours = 28 };
            Scheduler.PercentOverlapAllowed = this.PercentOverlapAllowedLocal;
            Scheduler.SecondaryWorkMinStartTime = this.SecondaryWorkMinStartTimeDateTime;
            Scheduler.MinPrimaryWorkDurationForReturnHomeFromWork = this.MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;
            Scheduler.MaxPrimeWorkStartTimeForReturnHomeFromWork = this.MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;
            Scheduler.ReturnHomeFromWorkMaxEndTime = this.ReturnHomeFromWorkMaxEndTimeDateTime;
            Scheduler.FullTimeActivity = this.FullTimeActivityDateTime;
            Scheduler.LocationChoiceModel = this.LocationChoiceModelLocal;
            Scheduler.LocationChoiceModel.LoadLocationChoiceCache();
            //Distributions
            Scheduler.AdultDistributionsFile = this.GetFullPath(this.AdultDistributionsFileLocal);
            Scheduler.FrequencyDistributionsFile = this.GetFullPath(this.FrequencyDistributionsFileLocal);
            Scheduler.NumberOfDistributions = this.NumberOfDistributionsLocal;
            Scheduler.NumberOfAdultFrequencies = this.NumberOfAdultFrequenciesLocal;
            Scheduler.NumberOfAdultDistributions = this.NumberOfAdultDistributionsLocal;

            Scheduler.MaxFrequency = this.MaxFrequencyLocal;
            Distribution.TashaRuntime = this.TashaRuntime;
            Distribution.InitializeDistributions();

            HouseholdExtender.TashaRuntime = this.TashaRuntime;
            // references in scheduler
            SchedulerHousehold.TashaRuntime = this.TashaRuntime;
            Schedule.Scheduler = this;
        }

        /// <summary>
        /// Load the data we need to get every iteration
        /// </summary>
        public void LoadPerIterationData()
        {
            // The distributions will ignore multiple calls if it is already setup on the thread            if (this.AlternativeTravelModeName != null)
            LoadLocationChoiceModel();
            LoadDistributions();
            TimePeriod.LoadTimePeriodData();
        }

        [RunParameter("Random Seed", 1234123, "A random seed to base the randomness of the scheduler.")]
        public int Seed;

        [RunParameter("Household Iterations", 100, "The number of household iterations.")]
        public int HouseholdIterations;

        /// <summary>
        /// Schedule the household
        /// </summary>
        /// <param name="h">The household to schedule</param>
        public void Run(ITashaHousehold h)
        {
            Random r = new Random(h.HouseholdId * this.Seed);
            // Setup the data, no random is needed here
            AddProjects(h);
            // Generate the schedules for each type of project
            if (!h.GenerateProjectSchedules(r, GenerationRateAdjustments))
            {
                // If we were not able to generate a schedule
                System.Threading.Interlocked.Increment(ref SchedulingFail);
                return;
            }
            System.Threading.Interlocked.Increment(ref SchedulingSuccess);
            // Now that we have the individual projects, create the individual schedules
            h.GeneratePersonSchedules(r, HouseholdIterations, MinimumTimeAtHomeBetweenActivities);
            JoinTripChains(h);
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        /// <summary>
        /// Get how long it takes to go somewhere
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="tashaTime"></param>
        /// <returns></returns>
        public Time TravelTime(ITashaPerson person, IZone origin, IZone destination, Time tashaTime)
        {
            if (this.AlternativeTravelMode == null || this.TashaRuntime.AutoType.CanUse(person))
            {
                return this.TashaRuntime.AutoMode.TravelTime(origin, destination, tashaTime);
            }
            else
            {
                return this.AlternativeTravelMode.TravelTime(origin, destination, tashaTime);
            }
        }

        internal static void JoinTripChains(ITashaHousehold house)
        {
            int JointTourNumber = 1;
            // we don't need to look at the last person
            for (int person = 0; person < house.Persons.Length - 1; person++)
            {
                foreach (var chain in house.Persons[person].TripChains)
                {
                    if (chain.JointTrip)
                    {
                        continue;
                    }
                    for (int otherPerson = person + 1; otherPerson < house.Persons.Length; otherPerson++)
                    {
                        foreach (var otherChain in house.Persons[otherPerson].TripChains)
                        {
                            if (otherChain.JointTrip)
                            {
                                continue;
                            }
                            if (AreTogether(chain, otherChain))
                            {
                                int tourNum = JointTourNumber;
                                if (!chain.JointTrip)
                                {
                                    ((SchedulerTripChain)chain).JointTripID = ((SchedulerTripChain)otherChain).JointTripID = tourNum;
                                    ((SchedulerTripChain)chain).JointTripRep = true;
                                    JointTourNumber++;
                                }
                                ((SchedulerTripChain)otherChain).JointTripID = chain.JointTripID;
                                ((SchedulerTripChain)otherChain).GetRepTripChain = chain;
                            }
                        }
                    }
                }
            }
        }

        private static void AddProjects(ITashaHousehold h)
        {
            // Get everything read to add projects to the household
            h.CreateHouseholdProjects();
            // Now that is setup, we can focus on the individual people
            foreach (ITashaPerson person in h.Persons)
            {
                person.InitializePersonalProjects();
            }
        }

        private static bool AreTogether(ITripChain f, ITripChain s)
        {
            if (f.Trips.Count != s.Trips.Count) return false;
            var fTrips = f.Trips;
            var sTrips = s.Trips;
            for (int i = 0; i < fTrips.Count; i++)
            {
                if (!AreTogether(fTrips[i], sTrips[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreTogether(ITrip f, ITrip s)
        {
            return (f.TripStartTime == s.TripStartTime)
                 & (f.Purpose == s.Purpose)
                 & (f.Purpose == Activity.JointOther | f.Purpose == Activity.JointMarket | f.Purpose == Activity.Home)
                 & (f.OriginalZone.ZoneNumber == s.OriginalZone.ZoneNumber)
                 & (f.DestinationZone.ZoneNumber == s.DestinationZone.ZoneNumber);
        }

        private string GetFullPath(string localPath)
        {
            if (!System.IO.Path.IsPathRooted(localPath))
            {
                return System.IO.Path.Combine(this.TashaRuntime.InputBaseDirectory, localPath);
            }
            return localPath;
        }

        /// <summary>
        /// Loads all of the scheduler distributions
        /// </summary>
        private void LoadDistributions()
        {
            ActivityDistribution.LoadDistributions(this.ActivityLevelsLocal, this.TashaRuntime.ZoneSystem.ZoneArray);
            Distribution.InitializeDistributions();
        }

        private void LoadLocationChoiceModel()
        {
            // will ignore multiple calls if it is already setup on the thread
            LocationChoiceModel.LoadLocationChoiceCache();
        }
    }
}