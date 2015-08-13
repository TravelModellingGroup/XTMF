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
using System.Linq;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    public class SchedHouseholdData
    {
        /// <summary>
        ///
        /// </summary>
        public Project JointMarketProject;

        /// <summary>
        ///
        /// </summary>
        public Project JointOtherProject;

        public Time LatestWorkingTime;

        /// <summary>
        /// Unused so far
        /// </summary>
        public Project ServeDependent;

        public SchedHouseholdData()
        {
            LatestWorkingTime = Time.StartOfDay;
        }
    }

    /// <summary>
    /// This class adds flexibility to the household class
    /// so that we can deal with the needs of the scheduler
    /// </summary>
    internal static class SchedulerHousehold
    {
        public static ITashaRuntime TashaRuntime;

        public static HouseholdWorkSchoolProjectStatus GetWorkSchoolProjectStatus(ITashaHousehold household)
        {
            bool evening_workschool = false;
            bool morning_workschool = false;
            bool any_workschool = false;

            foreach(var person in household.Persons)
            {
                PersonWorkSchoolProjectStatus workschoolProjectStatus = SchedulerPerson.GetWorkSchoolProjectStatus(person);
                if(workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeDayAndEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.Other)
                    evening_workschool = true;
                if(workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeNoEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeDayAndEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.PartTimeDay ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.PartTimeEvening)
                    morning_workschool = true;
                if(workschoolProjectStatus > 0) any_workschool = true;
            }

            if(!any_workschool) return HouseholdWorkSchoolProjectStatus.NoWorkOrSchool;  // noone in hhld works or attends school today
            else if(!evening_workschool) return HouseholdWorkSchoolProjectStatus.NoEveningWorkOrSchool; //there is work/school, but none after 6:00pm
            else if(!morning_workschool) return HouseholdWorkSchoolProjectStatus.EveningWorkOrSchool; //there is evening work/school, but no work/school before 1:00pm
            else return HouseholdWorkSchoolProjectStatus.DayAndEveningWorkOrSchool; // there is work/school before 1pm and after 6pm
        }

        public static int MaxTripChainSize(this ITashaHousehold household)
        {
            int maxTripChainSize = 0;
            foreach(var person in household.Persons)
            {
                foreach(var tripChain in person.TripChains)
                {
                    maxTripChainSize = Math.Max(tripChain.Trips.Count, maxTripChainSize);
                }
            }
            return maxTripChainSize;
        }

        internal static void CheckAndUpdateLatestWorkingTime(this ITashaHousehold household, Time time)
        {
            if((household["SData"] as SchedHouseholdData).LatestWorkingTime < time)
            {
                (household["SData"] as SchedHouseholdData).LatestWorkingTime = time;
            }
        }

        internal static void CreateHouseholdProjects(this ITashaHousehold household)
        {
            SchedHouseholdData data;
            household.Attach("SData", data = new SchedHouseholdData());
            ProjectSchedule jointOtherSchedule = new ProjectSchedule(household);
            ProjectSchedule jointMarketSchedule = new ProjectSchedule(household);
            Project jointOtherProject = new HouseholdProject(household, jointOtherSchedule);
            Project jointMarketProject = new HouseholdProject(household, jointMarketSchedule);
            data.JointOtherProject = jointOtherProject;
            data.JointMarketProject = jointMarketProject;
        }

        internal static void GeneratePersonSchedules(this ITashaHousehold household, Random random, int householdIterations)
        {
            var data = (household["SData"] as SchedHouseholdData);
            // Generate each person's schedule
            foreach(var person in household.Persons)
            {
                person.GenerateWorkSchoolSchedule(random);
            }
            // Make each person attend the household level projects
            household.AddHouseholdProjects(data, random);

            //Generate other/market schedules
            foreach(var person in household.Persons)
            {
                //person.Generate
                person.AddPersonalProjects(random);
            }

            // Clean up the people's schedules
            household.CleanupSchedules();

            // Add in the trip chains here for each person
            household.BuildChains(householdIterations);
        }

        /// <summary>
        /// Generate all of the project schedules for the household
        /// </summary>
        /// <param name="household"></param>
        /// <returns></returns>
        internal static bool GenerateProjectSchedules(this ITashaHousehold household, Random random)
        {
            return household.GenerateWorkEpisodes(random)
                && household.GenerateSchoolEpisodes(random)
                && household.GenerateJointOtherEpisodes(random)
                && household.GenerateJointMarketEpisodes(random)
                && household.GenerateIndividualOtherEpisodes(random)
                && household.GenerateIndividualMarketEpisodes(random);
        }

        internal static Project GetJointMarketProject(this ITashaHousehold household)
        {
            return (household["SData"] as SchedHouseholdData).JointMarketProject;
        }

        internal static Project GetJointOtherProject(this ITashaHousehold household)
        {
            return (household["SData"] as SchedHouseholdData).JointOtherProject;
        }

        internal static void SetJointMarketProject(this ITashaHousehold household, Project project)
        {
            (household["SData"] as SchedHouseholdData).JointMarketProject = project;
        }

        internal static void SetJointOtherProject(this ITashaHousehold household, Project project)
        {
            (household["SData"] as SchedHouseholdData).JointOtherProject = project;
        }

        private static bool GenerateIndividualMarketEpisodes(this ITashaHousehold household, Random random)
        {
            foreach(var person in household.Persons)
            {
                if(!person.Child)
                {
                    int freq_I = TimeTable.GetFrequency(person, Activity.Market, random);
                    int outerAttempts = 0;
                    for(int j = 0; j < freq_I; ++j)
                    {
                        //Update the trip generation count

                        Time startTime;
                        Time duration;
                        bool success = false;
                        for(int attempt = 0; attempt < Scheduler.EpisodeSchedulingAttempts && !success; attempt++)
                        {
                            if(!TimeTable.GetStartTime(person, Activity.Market, random, out startTime))
                            {
                                continue;
                            }

                            if(!TimeTable.GetDuration(person, Activity.Market, startTime, random, out duration))
                            {
                                continue;
                            }

                            var endTime = startTime + duration;
                            if(endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                            {
                                success = false;
                                continue;
                            }

                            //instantiate a temporary individual market episode on the heap space and store pointer in p_marketEpisode

                            Episode MarketEpisode = new ActivityEpisode(0, new TimeWindow(startTime, startTime + duration), Activity.Market, person);

                            Project workProject = person.GetWorkProject();
                            Schedule workProjSchedule = workProject.Schedule;
                            Project schoolProject = person.GetSchoolProject();
                            Schedule schoolProjSchedule = schoolProject.Schedule;

                            Time overlap = workProjSchedule.CheckOverlap(MarketEpisode) + schoolProjSchedule.CheckOverlap(MarketEpisode);

                            float percentOverlap = overlap / duration;

                            if(percentOverlap < Scheduler.PercentOverlapAllowed || attempt == Scheduler.EpisodeSchedulingAttempts - 1)
                            {
                                Project marketProject = person.GetMarketProject();
                                Schedule marketSchedule = marketProject.Schedule;

                                if(marketSchedule.Insert(MarketEpisode, random))
                                {
                                    //inserted ok
                                    success = true;
                                }
                                else
                                {
                                    success = false;
                                    //didn't work
                                }
                            }
                            else // i.e. too much overlap with the work and school projects
                            {
                                // attempt will be auto incremented so we don't need to worry about this
                            }
                        }
                        if((outerAttempts++) < Scheduler.EpisodeSchedulingAttempts && !success)
                        {
                            j = -1;
                            Project marketProject = person.GetMarketProject();
                            Schedule marketSchedule = marketProject.Schedule;
                            marketSchedule.Clear();
                        }
                    }
                }
            }
            return true;
        }

        private static bool GenerateIndividualOtherEpisodes(this ITashaHousehold household, Random random)
        {
            foreach(var person in household.Persons)
            {
                if(!person.Child)
                {
                    int freqO = TimeTable.GetFrequency(person, Activity.IndividualOther, random);
                    int outerAttempts = 0;

                    Time durationO, startTimeO = Time.Zero;

                    for(int i = 0; i < freqO; i++)
                    {
                        bool success = false;

                        for(int attempt = 0; !success && (attempt < Scheduler.EpisodeSchedulingAttempts); attempt++)
                        {
                            if(!TimeTable.GetStartTime(person, Activity.IndividualOther, freqO, random, out startTimeO))
                            {
                                success = false;
                                continue;
                            }
                            if(!TimeTable.GetDuration(person, Activity.IndividualOther, startTimeO, random, out durationO))
                            {
                                success = false;
                                continue;
                            }

                            var endTime = startTimeO + durationO;
                            if(endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                            {
                                success = false;
                                continue;
                            }

                            Episode otherEpisode;
                            otherEpisode = new ActivityEpisode(0,
                                new TimeWindow(startTimeO, endTime),
                                Activity.IndividualOther, person);
                            Project workProject = person.GetWorkProject();
                            Schedule workProjSchedule = workProject.Schedule;
                            Project schoolProject = person.GetSchoolProject();
                            Schedule schoolProjSchedule = schoolProject.Schedule;

                            Time overlap = workProjSchedule.CheckOverlap(otherEpisode) + schoolProjSchedule.CheckOverlap(otherEpisode);

                            float percentOverlap = overlap / durationO;

                            if(percentOverlap < Scheduler.PercentOverlapAllowed || attempt == Scheduler.EpisodeSchedulingAttempts - 1)
                            {
                                Project otherProject = person.GetOtherProject();
                                Schedule otherSchedule = otherProject.Schedule;

                                if(otherSchedule.Insert(otherEpisode, random))
                                {
                                    //inserted ok
                                    success = true;
                                }
                            }
                        }
                        if((outerAttempts++) < Scheduler.EpisodeSchedulingAttempts && !success)
                        {
                            i = -1;
                            Project otherProject = person.GetOtherProject();
                            Schedule otherSchedule = otherProject.Schedule;
                            otherSchedule.Clear();
                        }
                    }
                }
            }
            return true;
        }

        private static bool GenerateJointMarketEpisodes(this ITashaHousehold household, Random random)
        {
            // initialize available adults
            var availableAdults = new List<ITashaPerson>(household.Persons.Length);
            // We can only do this with households with more than one person
            if(household.Persons.Length >= 2 && household.NumberOfAdults > 0)
            {
                // Figure out how many times this home is going to go on a joint market trip
                int howManyTimes = TimeTable.GetFrequency(household, Activity.JointMarket, random);
                // Processes each of those trips
                for(int i = 0; i < howManyTimes; i++)
                {
                    Time startTime, duration;
                    int numEpisodeAdults = Distribution.GetNumAdultsJointEpisode(household, random,
                                            Activity.JointMarket);
                    bool success = false;
                    // continue to try until either we get it to work or we fail to schedule this episode
                    for(int attempt = 0; !success && attempt < Scheduler.EpisodeSchedulingAttempts; attempt++)
                    {
                        if(!TimeTable.GetStartTime(household, Activity.JointMarket, random, out startTime))
                        {
                            continue;
                        }
                        if(!TimeTable.GetDuration(household, Activity.JointMarket, startTime, random, out duration))
                        {
                            continue;
                        }
                        // Now that we have our start time and duration, compute our end time
                        Time endTime = startTime + duration;
                        if(availableAdults.Count > 0) availableAdults.Clear();
                        Time workSchoolStartTime, workSchoolEndTime;
                        bool available = false;
                        foreach(var person in household.Persons)
                        {
                            workSchoolStartTime = SchedulerPerson.GetWorkSchoolStartTime(person);
                            workSchoolEndTime = SchedulerPerson.GetWorkSchoolEndTime(person);

                            // this person is available if
                            available = (workSchoolStartTime > endTime) | (workSchoolEndTime < startTime) | (workSchoolStartTime == Time.Zero);

                            if(person.Age >= 16 && available)
                            {
                                availableAdults.Add(person);
                            }
                        }

                        if((availableAdults.Count > 0) & (availableAdults.Count >= numEpisodeAdults))
                        {
                            Episode jointMarketEpisode;
                            jointMarketEpisode = new ActivityEpisode(0,
                                new TimeWindow(startTime, endTime), Activity.JointMarket,
                                availableAdults[0]);

                            foreach(ITashaPerson adult in availableAdults)
                            {
                                jointMarketEpisode.AddPerson(adult);
                            }

                            Project jointMarketProject = household.GetJointMarketProject();
                            Schedule jointMarketSchedule = jointMarketProject.Schedule;
                            bool insert = jointMarketSchedule.Insert(jointMarketEpisode, random);

                            success = insert;
                        }
                    }
                }
            }
            return true;
        }

        private static bool GenerateJointOtherEpisodes(this ITashaHousehold household, Random random)
        {
            //make sure there at least 2 people and one adult
            if((household.Persons.Length >= 2) & (household.NumberOfAdults > 0))
            {
                int freqJ = TimeTable.GetFrequency(household, Activity.JointOther, random);

                Time duration, startTime;
                int numEpisodeAdults = Distribution.GetNumAdultsJointEpisode(household, random,
                    Activity.JointOther);

                for(int i = 0; i < freqJ; i++)
                {
                    bool success = false;
                    int attempt = 0;
                    while(!success && attempt < Scheduler.EpisodeSchedulingAttempts)
                    {
                        if(!TimeTable.GetStartTime(household, Activity.JointOther, freqJ, random, out startTime))
                        {
                            success = false;
                            attempt++;
                            continue;
                        }
                        if(!TimeTable.GetDuration(household, Activity.JointOther, startTime, random, out duration))
                        {
                            success = false;
                            attempt++;
                            continue;
                        }

                        if(duration == Time.Zero || startTime == Time.Zero)
                        {
                            success = false;
                            attempt++;
                        }
                        else
                        {
                            Time endTime = startTime + duration;
                            List<ITashaPerson> availableAdults = new List<ITashaPerson>();
                            foreach(ITashaPerson person in household.Persons)
                            {
                                Time workSchoolStartTime = SchedulerPerson.GetWorkSchoolStartTime(person);
                                Time workSchoolEndTime = SchedulerPerson.GetWorkSchoolEndTime(person);
                                bool available = false;
                                if(workSchoolStartTime > endTime ||
                                    workSchoolEndTime < startTime ||
                                    workSchoolStartTime == Time.Zero)
                                    available = true;
                                if(person.Age >= 16 && available)
                                {
                                    availableAdults.Add(person);
                                }
                            }

                            if(availableAdults.Count >= numEpisodeAdults && availableAdults.Count > 0)
                            {
                                Episode jointOtherEpisode;
                                var owner = availableAdults[0];
                                jointOtherEpisode = new ActivityEpisode(0,
                                    new TimeWindow(startTime, endTime), Activity.JointOther, owner);

                                for(int j = 0; j < numEpisodeAdults; j++)
                                {
                                    jointOtherEpisode.AddPerson(availableAdults[j]);
                                }

                                Project jointOtherProject = household.GetJointOtherProject();
                                Schedule jointOtherSchedule = jointOtherProject.Schedule;
                                bool inserted = jointOtherSchedule.Insert(jointOtherEpisode, random);
                                success = true;

                                if(!inserted)
                                {
                                    success = false;
                                    attempt++;
                                }
                            }
                            else
                            {
                                success = false;
                                attempt++;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static bool GenerateSchoolEpisodes(this ITashaHousehold household, Random random)
        {
            foreach(ITashaPerson person in household.Persons)
            {
                if(person.StudentStatus == StudentStatus.FullTime ||
                    person.StudentStatus == StudentStatus.PartTime)
                {
                    if(person.Age >= 11)
                    {
                        var freq = TimeTable.GetFrequency(person, Activity.School, random);
                        //if there is a school activity generated
                        for(int i = 0; i < freq; i++)
                        {
                            bool success = false;
                            short attempt = 0;
                            Time duration = new Time(), startTime = new Time();
                            int maxAttempts = Scheduler.EpisodeSchedulingAttempts;
                            while(!success && (attempt < maxAttempts))
                            {
                                attempt++;
                                //get start time end time and duration
                                if(!TimeTable.GetStartTime(person, Activity.School, random, out startTime))
                                {
                                    continue;
                                }
                                if(!TimeTable.GetDuration(person, Activity.School, startTime, random, out duration))
                                {
                                    continue;
                                }
                                if(duration != Time.Zero) // no valid duration
                                {
                                    var endTime = startTime + duration;
                                    if(endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                                    {
                                        success = false;
                                        continue;
                                    }
                                    //instantiate temporary school episode;
                                    Schedule schoolSchedule;
                                    Episode schoolEpisode;

                                    schoolEpisode =
                                       new ActivityEpisode(0, new TimeWindow(startTime, startTime + duration),
                                           Activity.School, person);
                                    schoolEpisode.Zone = person.SchoolZone != null ? person.SchoolZone : person.Household.HomeZone;
                                    Project schoolProject = person.GetSchoolProject();
                                    schoolSchedule = schoolProject.Schedule;
                                    success = schoolSchedule.Insert(schoolEpisode, random);
                                }
                            }
                        }
                    }
                    else if(person.Age >= 6)
                    {
                        //this child is in kindergarten
                        //generate random number between 0 and 1
                        Time startTime = new Time(), endTime = new Time();
                        startTime = Scheduler.SchoolMorningStart;
                        endTime = Scheduler.SchoolAfternoonEnd;
                        Schedule schoolSchedule;
                        Episode schoolEpisode;

                        schoolEpisode =
                           new ActivityEpisode(0, new TimeWindow(startTime, endTime),
                               Activity.School, person);
                        schoolEpisode.Zone = person.SchoolZone != null ? person.SchoolZone : person.Household.HomeZone;
                        Project schoolProject = person.GetSchoolProject();
                        schoolSchedule = schoolProject.Schedule;
                        schoolSchedule.Insert(schoolEpisode, random);
                    }
                    else if(person.Age == 5)
                    {
                        //this child is in kindergarten
                        //generate random number between 0 and 1
                        int randNum = random.Next(0, 1);
                        Time duration = new Time(), startTime = new Time(), endTime = new Time();
                        if(randNum <= 0.5) //morning shift
                        {
                            //morning shift
                            startTime = Scheduler.SchoolMorningStart;
                            endTime = Scheduler.SchoolMorningEnd;
                            duration = endTime - startTime;
                        }
                        else
                        {
                            //afternoon shift
                            startTime = Scheduler.SchoolAfternoonStart;
                            endTime = Scheduler.SchoolAfternoonEnd;
                        }
                        Schedule schoolSchedule;
                        Episode schoolEpisode;

                        schoolEpisode =
                           new ActivityEpisode(0, new TimeWindow(startTime, endTime),
                               Activity.School, person);
                        schoolEpisode.Zone = person.SchoolZone != null ? person.SchoolZone : person.Household.HomeZone;
                        Project schoolProject = person.GetSchoolProject();
                        schoolSchedule = schoolProject.Schedule;
                        schoolSchedule.Insert(schoolEpisode, random);
                    }
                }
            }
            return true;
        }

        private static void GenerateWorkBusinessEpisode(ITashaPerson person, Schedule workSchedule, Random random)
        {
            int freq_B = TimeTable.GetFrequency(person, Activity.WorkBasedBusiness, random);
            for(int i = 0; i < freq_B; i++)
            {
                Time startTime;
                Time duration;
                for(int attempt = 0; attempt < Scheduler.EpisodeSchedulingAttempts; attempt++)
                {
                    if(!TimeTable.GetStartTime(person, Activity.WorkBasedBusiness, random, out startTime))
                    {
                        continue;
                    }
                    if(!TimeTable.GetDuration(person, Activity.WorkBasedBusiness, startTime, random, out duration))
                    {
                        continue;
                    }
                    var endTime = startTime + duration;
                    if(endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                    {
                        continue;
                    }
                    Episode workEpisode = new ActivityEpisode(System.Threading.Interlocked.Increment(ref Episode.GeneratedEpisodes), new TimeWindow(startTime, startTime + duration),
                        Activity.WorkBasedBusiness, person);
                    workSchedule.Insert(workEpisode, random);
                    break;
                }
            }
        }

        private static bool GenerateWorkEpisodes(this ITashaHousehold household, Random random)
        {
            foreach(ITashaPerson person in household.Persons)
            {
                // people need to be older than "11" to be allowed to work
                if(person.Age < Scheduler.MinimumWorkingAge) continue;
                // Check to see if they are in a regular job type case
                Project workProject = person.GetWorkProject();
                Schedule workSchedule = workProject.Schedule;
                if((person.Occupation == Occupation.NotEmployed) | (person.Occupation == Occupation.Unknown))
                {
                    continue;
                }
                if(((person.EmploymentStatus == TTSEmploymentStatus.FullTime) | (person.EmploymentStatus == TTSEmploymentStatus.PartTime)))
                {
                    if(person.EmploymentZone == null)
                    {
                        //continue;
                        throw new XTMFRuntimeException("There was a person whom had no employment zone however was a full-time/part-time worker!");
                    }
                    //Employment zone doesn't exist so generate our own based on distributions
                    if(person.EmploymentZone.ZoneNumber == TashaRuntime.ZoneSystem.RoamingZoneNumber)
                    {
                        GenerateWorkBusinessEpisode(person, workSchedule, random);
                        continue;
                    }

                    int freq = TimeTable.GetFrequency(person, Activity.PrimaryWork, random);
                    if(freq <= 0)
                    {
                        continue;
                    }
                    Time startTime = Time.Zero;
                    Time duration = Time.Zero;
                    bool success = false;
                    for(int i = 0; i < freq; i++)
                    {
                        for(int attempts = 0; attempts < Scheduler.EpisodeSchedulingAttempts; attempts++)
                        {
                            // If we use an and here the compiler can't prove that we assign to duration
                            if(!TimeTable.GetStartTime(person, Activity.PrimaryWork, freq, random, out startTime))
                            {
                                continue;
                            }

                            if(TimeTable.GetDuration(person, Activity.PrimaryWork, startTime, random, out duration))
                            {
                                // if we got the duration, success lets continue on
                                success = true;
                                break;
                            }
                        }
                        // if we were unable to get a duration
                        if(!success)
                        {
                            // try the next person
                            continue;
                        }
                        Time endTime = startTime + duration;

                        SchedulerHousehold.CheckAndUpdateLatestWorkingTime(person.Household,
                            endTime);

                        Episode primWorkEpisode;

                        primWorkEpisode = new ActivityEpisode(0, new TimeWindow(startTime, endTime),
                                    Activity.PrimaryWork, person);
                        primWorkEpisode.Zone = person.EmploymentZone;
                        if(!workSchedule.Insert(primWorkEpisode, random) && i == 0)
                        {
                            throw new XTMFRuntimeException("Failed to insert the primary work episode into the work project!");
                        }
                        //set up work business activities
                        ProcessWorkBusiness(person, workSchedule, random, primWorkEpisode);
                        //set up secondary work activities
                        ProcessSecondaryWork(person, workSchedule, random, primWorkEpisode);
                        //set up return home from work activities
                        ProcessReturnHomeFromWork(person, workSchedule, random, primWorkEpisode);
                    }
                }
                // Check to see if they work from home
                else if(person.Age >= 19 &&
                    ((person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_FullTime)
                    | (person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_PartTime)))
                {
                    ProcessWorkAtHome(person, workSchedule, random);
                }
                // If they don't work, just continue on
            }
            return true;
        }

        /// <summary>
        /// Return home from work creates a return home from work activity (return home from work may involve
        /// going to lunch at home or going to check up on something in the house during work).
        /// </summary>
        /// <param name="person"></param>
        /// <param name="schedule"></param>
        /// <param name="episode"></param>
        /// <returns></returns>
        private static bool ProcessReturnHomeFromWork(ITashaPerson person, Schedule schedule, Random random, Episode episode)
        {
            int freq = 0;

            //the current work schedule doesn't allow for a return from work activity
            if(episode.StartTime > Scheduler.MaxPrimeWorkStartTimeForReturnHomeFromWork
                || episode.Duration < Scheduler.MinPrimaryWorkDurationForReturnHomeFromWork)
            {
                return false;
            }

            //End time of work to home activity
            Time endTime = episode.EndTime + new Time(0.3f) < Scheduler.ReturnHomeFromWorkMaxEndTime ?
                episode.EndTime + new Time(0.3f) : Scheduler.ReturnHomeFromWorkMaxEndTime;

            freq = TimeTable.GetFrequency(person, Activity.ReturnFromWork, random, 1, episode.StartTime + new Time(0.3f), endTime);

            if(freq == 1)
            {
                IZone homeZone = person.Household.HomeZone;

                Time HalfAnHour = new Time() { Minutes = 30 };

                Time MaxEndTime = ((episode.EndTime - HalfAnHour) < Scheduler.ReturnHomeFromWorkMaxEndTime) ? (episode.EndTime - HalfAnHour) : Scheduler.ReturnHomeFromWorkMaxEndTime;

                Time startTime;
                if(!TimeTable.GetStartTime(person, Activity.ReturnFromWork
                     , freq
                     , episode.StartTime + HalfAnHour
                     , MaxEndTime, random, out startTime))
                {
                    return false;
                }

                Time maxDuration = new Time(Math.Min((Scheduler.ReturnHomeFromWorkMaxEndTime - Time.OneHour).ToFloat(),
                    (episode.EndTime - HalfAnHour - startTime).ToFloat()));

                Time duration;
                if(!TimeTable.GetDuration(person, Activity.ReturnFromWork, startTime, maxDuration, random, out duration))
                {
                    // reject
                    return false;
                }

                Episode returnFromWorkEpisode;
                returnFromWorkEpisode = new ActivityEpisode(0,
                    new TimeWindow(startTime, startTime + duration), Activity.ReturnFromWork,
                    person);
                returnFromWorkEpisode.Zone = homeZone;
                schedule.Insert(returnFromWorkEpisode, random);
            }
            return true;
        }

        /// <summary>
        /// Secondary work creates a secondary work activity for person
        /// </summary>
        /// <param name="person"></param>
        /// <param name="schedule"></param>
        /// <param name="primaryWorkEpisode"></param>
        private static void ProcessSecondaryWork(ITashaPerson person, Schedule schedule, Random random, Episode primaryWorkEpisode)
        {
            //can only work if finish primary work by 7:00PM
            if(primaryWorkEpisode.EndTime < Scheduler.SecondaryWorkThreshold)
            {
                int freq_R = 0;

                //getting earliest possible startTime
                Time HourAfterWork = primaryWorkEpisode.EndTime + Time.OneHour;
                Time MinStartTime = HourAfterWork > Scheduler.SecondaryWorkMinStartTime ? HourAfterWork : Scheduler.SecondaryWorkMinStartTime;

                freq_R = TimeTable.GetFrequency(person, Activity.SecondaryWork, random, 10, MinStartTime, Time.EndOfDay);

                for(int i = 0; i < freq_R; i++)
                {
                    //zone same as work zone
                    IZone zone = primaryWorkEpisode.Zone.ZoneNumber == Scheduler.Tasha.ZoneSystem.RoamingZoneNumber
                        ? Scheduler.LocationChoiceModel.GetLocationHomeBased(Activity.SecondaryWork, person.Household.HomeZone, random)
                        : primaryWorkEpisode.Zone;

                    //getting start time and duration of secondary work
                    Time startTimeR;

                    Time durationR;

                    if(!TimeTable.GetStartTime(person, primaryWorkEpisode.ActivityType, freq_R, MinStartTime, Time.EndOfDay, random, out startTimeR))
                    {
                        //TODO: We might want to reconsider this, skipping instead of just throwing an exception
                        //throw new XTMFRuntimeException("Unable to find a start time for a primary work episode");
                        return;
                    }

                    if(!TimeTable.GetDuration(person, Activity.SecondaryWork, startTimeR, Time.EndOfDay - startTimeR, random, out durationR))
                    {
                        //throw new XTMFRuntimeException("Unable to find a duration for a primary work episode");
                        return;
                    }

                    //inserting secondary work into schedule
                    Episode secondaryWorkEpisode;
                    secondaryWorkEpisode = new ActivityEpisode(0,
                        new TimeWindow(startTimeR, startTimeR + durationR),
                        Activity.SecondaryWork, person);
                    secondaryWorkEpisode.Zone = zone;
                    schedule.Insert(secondaryWorkEpisode, random);
                }
            }
        }

        private static void ProcessWorkAtHome(ITashaPerson person, Schedule workSchedule, Random random)
        {
            int freq_A = TimeTable.GetFrequency(person, Activity.WorkAtHomeBusiness, random);
            Time duration, startTime;
            for(int i = 0; i < freq_A; i++)
            {
                bool success = false;
                short attempt = 0;
                while(!success && (attempt < Scheduler.EpisodeSchedulingAttempts))
                {
                    if(!TimeTable.GetStartTime(person, Activity.WorkAtHomeBusiness, freq_A, random, out startTime))
                    {
                        success = false;
                        attempt++;
                        continue;
                    }
                    if(!TimeTable.GetDuration(person, Activity.WorkAtHomeBusiness,
                        startTime, Time.EndOfDay - startTime, random, out duration))
                    {
                        success = false;
                        attempt++;
                        continue;
                    }

                    Time endTime = startTime + duration;
                    SchedulerHousehold.CheckAndUpdateLatestWorkingTime(person.Household,
                        endTime);
                    Episode wahBusinessEpisode;
                    wahBusinessEpisode = new ActivityEpisode(0,
                        new TimeWindow(startTime, endTime), Activity.WorkAtHomeBusiness, person);
                    if(!workSchedule.Insert(wahBusinessEpisode, random))
                    {
                        success = false;
                        attempt++;
                    }
                    else
                    {
                        success = true;
                    }
                }
            }
        }

        private static void ProcessWorkBusiness(ITashaPerson person, Schedule workSchedule, Random random, Episode primWorkEpisode)
        {
            Time startTimeB;
            Time durationB;
            Time startTime = primWorkEpisode.StartTime;
            Time endTime = primWorkEpisode.EndTime;

            int freq = TimeTable.GetFrequency(person, Activity.WorkBasedBusiness, random, Scheduler.MaxFrequency, startTime, endTime);
            for(int i = 0; i < freq; i++)
            {
                var attempt = 0;
                while(attempt < Scheduler.EpisodeSchedulingAttempts)
                {
                    attempt++;
                    if(!TimeTable.GetStartTime(person, Activity.WorkBasedBusiness, freq,
                        startTime, endTime, random, out startTimeB))
                    {
                        continue;
                    }

                    if(!TimeTable.GetDuration(person, Activity.WorkBasedBusiness, startTimeB, endTime - startTimeB, random, out durationB))
                    {
                        continue;
                    }

                    Episode businessEpisode;
                    businessEpisode = new ActivityEpisode(0,
                        new TimeWindow(startTimeB, startTimeB + durationB), Activity.WorkBasedBusiness, person);
                    if(workSchedule.Insert(businessEpisode, random))
                    {
                        break;
                    }
                }
            }
        }
    }
}