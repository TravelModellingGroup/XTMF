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
using Tasha.Common;
using TMG;
using TMG.Functions;
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
            bool eveningWorkschool = false;
            bool morningWorkschool = false;
            bool anyWorkschool = false;

            foreach (var person in household.Persons)
            {
                PersonWorkSchoolProjectStatus workschoolProjectStatus = SchedulerPerson.GetWorkSchoolProjectStatus(person);
                if (workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeDayAndEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.Other)
                    eveningWorkschool = true;
                if (workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeNoEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.FullTimeDayAndEveningWorkOrSchool ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.PartTimeDay ||
                    workschoolProjectStatus == PersonWorkSchoolProjectStatus.PartTimeEvening)
                    morningWorkschool = true;
                if (workschoolProjectStatus > 0) anyWorkschool = true;
            }

            if (!anyWorkschool) return HouseholdWorkSchoolProjectStatus.NoWorkOrSchool;  // noone in hhld works or attends school today
            if (!eveningWorkschool) return HouseholdWorkSchoolProjectStatus.NoEveningWorkOrSchool; //there is work/school, but none after 6:00pm
            if (!morningWorkschool) return HouseholdWorkSchoolProjectStatus.EveningWorkOrSchool; //there is evening work/school, but no work/school before 1:00pm
            return HouseholdWorkSchoolProjectStatus.DayAndEveningWorkOrSchool; // there is work/school before 1pm and after 6pm
        }

        public static int MaxTripChainSize(this ITashaHousehold household)
        {
            int maxTripChainSize = 0;
            foreach (var person in household.Persons)
            {
                foreach (var tripChain in person.TripChains)
                {
                    maxTripChainSize = Math.Max(tripChain.Trips.Count, maxTripChainSize);
                }
            }
            return maxTripChainSize;
        }

        internal static void CheckAndUpdateLatestWorkingTime(this ITashaHousehold household, Time time)
        {
            if (((SchedHouseholdData)household["SData"]).LatestWorkingTime < time)
            {
                ((SchedHouseholdData)household["SData"]).LatestWorkingTime = time;
            }
        }

        internal static void CreateHouseholdProjects(this ITashaHousehold household)
        {
            SchedHouseholdData data;
            household.Attach("SData", data = new SchedHouseholdData());
            ProjectSchedule jointOtherSchedule = new ProjectSchedule();
            ProjectSchedule jointMarketSchedule = new ProjectSchedule();
            Project jointOtherProject = new HouseholdProject(household, jointOtherSchedule);
            Project jointMarketProject = new HouseholdProject(household, jointMarketSchedule);
            data.JointOtherProject = jointOtherProject;
            data.JointMarketProject = jointMarketProject;
        }

        internal static void GeneratePersonSchedules(this ITashaHousehold household, Random random, int householdIterations, Time minimumAtHomeTime)
        {
            var data = (household["SData"] as SchedHouseholdData);
            // Generate each person's schedule
            foreach (var person in household.Persons)
            {
                person.GenerateWorkSchoolSchedule(random);
            }
            // Make each person attend the household level projects
            household.AddHouseholdProjects(data, random);

            //Generate other/market schedules
            foreach (var person in household.Persons)
            {
                //person.Generate
                person.AddPersonalProjects(random);
            }

            // Clean up the people's schedules
            household.CleanupSchedules();

            // Add in the trip chains here for each person
            household.BuildChains(householdIterations, minimumAtHomeTime);
        }

        /// <summary>
        /// Generate all of the project schedules for the household
        /// </summary>
        /// <param name="household"></param>
        /// <param name="random"></param>
        /// <param name="generationRateAdjustments"></param>
        /// <returns></returns>
        internal static bool GenerateProjectSchedules(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationRateAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            return household.GenerateWorkEpisodes(random, generationRateAdjustments, startTimeAdjustments)
                && household.GenerateSchoolEpisodes(random, generationRateAdjustments, startTimeAdjustments)
                && household.GenerateJointOtherEpisodes(random, generationRateAdjustments, startTimeAdjustments)
                && household.GenerateJointMarketEpisodes(random, generationRateAdjustments, startTimeAdjustments)
                && household.GenerateIndividualOtherEpisodes(random, generationRateAdjustments, startTimeAdjustments)
                && household.GenerateIndividualMarketEpisodes(random, generationRateAdjustments, startTimeAdjustments);
        }

        internal static Project GetJointMarketProject(this ITashaHousehold household)
        {
            return ((SchedHouseholdData) household["SData"]).JointMarketProject;
        }

        internal static Project GetJointOtherProject(this ITashaHousehold household)
        {
            return ((SchedHouseholdData) household["SData"]).JointOtherProject;
        }

        internal static void SetJointMarketProject(this ITashaHousehold household, Project project)
        {
            ((SchedHouseholdData) household["SData"]).JointMarketProject = project;
        }

        internal static void SetJointOtherProject(this ITashaHousehold household, Project project)
        {
            ((SchedHouseholdData) household["SData"]).JointOtherProject = project;
        }

        private static bool GenerateIndividualMarketEpisodes(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            int householdPD = household.HomeZone.PlanningDistrict;
            foreach (var person in household.Persons)
            {
                if (!person.Child)
                {
                    var empZone = person.EmploymentZone;
                    int workPD = empZone == null ? 0 : empZone.PlanningDistrict;
                    int freqI = TimeTable.GetFrequency(person, Activity.Market, random, householdPD, workPD, generationAdjustments);
                    int outerAttempts = 0;
                    for (int j = 0; j < freqI; ++j)
                    {
                        bool success = false;
                        for (int attempt = 0; attempt < Scheduler.EpisodeSchedulingAttempts && !success; attempt++)
                        {
                            if (!TimeTable.GetStartTime(person, Activity.Market, random, householdPD, workPD, startTimeAdjustments, out Time startTime))
                            {
                                continue;
                            }

                            if (!TimeTable.GetDuration(person, Activity.Market, startTime, random, out Time duration))
                            {
                                continue;
                            }

                            var endTime = startTime + duration;
                            if (endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                            {
                                continue;
                            }

                            //instantiate a temporary individual market episode on the heap space and store pointer in p_marketEpisode

                            var window = new TimeWindow(startTime, startTime + duration);
                            Episode marketEpisode = new ActivityEpisode(window, Activity.Market, person);
                            Project workProject = person.GetWorkProject();
                            Schedule workProjSchedule = workProject.Schedule;
                            Project schoolProject = person.GetSchoolProject();
                            Schedule schoolProjSchedule = schoolProject.Schedule;

                            Time overlap = workProjSchedule.CheckOverlap(marketEpisode) + schoolProjSchedule.CheckOverlap(marketEpisode);

                            float percentOverlap = overlap / duration;

                            if (percentOverlap < Scheduler.PercentOverlapAllowed || attempt == Scheduler.EpisodeSchedulingAttempts - 1)
                            {
                                Project marketProject = person.GetMarketProject();
                                Schedule marketSchedule = marketProject.Schedule;

                                if (marketSchedule.Insert(marketEpisode, random))
                                {
                                    //inserted ok
                                    success = true;
                                }
                            }
                        }
                        if ((outerAttempts++) < Scheduler.EpisodeSchedulingAttempts && !success)
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

        private static bool GenerateIndividualOtherEpisodes(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            int householdPD = household.HomeZone.PlanningDistrict;
            foreach (var person in household.Persons)
            {
                if (!person.Child)
                {
                    var empZone = person.EmploymentZone;
                    int workPD = empZone == null ? 0 : empZone.PlanningDistrict;
                    int freqO = TimeTable.GetFrequency(person, Activity.IndividualOther, random, householdPD, workPD, generationAdjustments);
                    int outerAttempts = 0;
                    for (int i = 0; i < freqO; i++)
                    {
                        bool success = false;

                        for (int attempt = 0; !success && (attempt < Scheduler.EpisodeSchedulingAttempts); attempt++)
                        {
                            if (!TimeTable.GetStartTime(person, Activity.IndividualOther, freqO, random, householdPD, workPD,
                                startTimeAdjustments, out Time startTimeO))
                            {
                                continue;
                            }
                            if (!TimeTable.GetDuration(person, Activity.IndividualOther, startTimeO, random, out Time durationO))
                            {
                                continue;
                            }

                            var endTime = startTimeO + durationO;
                            if (endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                            {
                                continue;
                            }

                            Episode otherEpisode;
                            otherEpisode = new ActivityEpisode(new TimeWindow(startTimeO, endTime),
                                Activity.IndividualOther, person);
                            Project workProject = person.GetWorkProject();
                            Schedule workProjSchedule = workProject.Schedule;
                            Project schoolProject = person.GetSchoolProject();
                            Schedule schoolProjSchedule = schoolProject.Schedule;

                            Time overlap = workProjSchedule.CheckOverlap(otherEpisode) + schoolProjSchedule.CheckOverlap(otherEpisode);

                            float percentOverlap = overlap / durationO;

                            if (percentOverlap < Scheduler.PercentOverlapAllowed || attempt == Scheduler.EpisodeSchedulingAttempts - 1)
                            {
                                Project otherProject = person.GetOtherProject();
                                Schedule otherSchedule = otherProject.Schedule;

                                if (otherSchedule.Insert(otherEpisode, random))
                                {
                                    //inserted ok
                                    success = true;
                                }
                            }
                        }
                        if ((outerAttempts++) < Scheduler.EpisodeSchedulingAttempts && !success)
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

        private static bool GenerateJointMarketEpisodes(this ITashaHousehold household, Random random, 
            GenerationAdjustment[] generationAdjustment, StartTimeAdjustment[] startTimeAdjustments)
        {
            var householdPD = household.HomeZone.PlanningDistrict;
            // initialize available adults
            var availableAdults = new List<ITashaPerson>(household.Persons.Length);
            // We can only do this with households with more than one person
            if (household.Persons.Length >= 2 && household.NumberOfAdults > 0)
            {
                // Figure out how many times this home is going to go on a joint market trip
                int howManyTimes = TimeTable.GetFrequency(household, Activity.JointMarket, random, householdPD,
                    0, generationAdjustment, startTimeAdjustments);
                // Processes each of those trips
                for (int i = 0; i < howManyTimes; i++)
                {
                    int numEpisodeAdults = Distribution.GetNumAdultsJointEpisode(household, random,
                                            Activity.JointMarket);
                    bool success = false;
                    // continue to try until either we get it to work or we fail to schedule this episode
                    for (int attempt = 0; !success && attempt < Scheduler.EpisodeSchedulingAttempts; attempt++)
                    {
                        if (!TimeTable.GetStartTime(household, Activity.JointMarket, random, householdPD, 0, startTimeAdjustments, out Time startTime))
                        {
                            continue;
                        }
                        if (!TimeTable.GetDuration(household, Activity.JointMarket, startTime, random, out Time duration))
                        {
                            continue;
                        }
                        // Now that we have our start time and duration, compute our end time
                        Time endTime = startTime + duration;
                        if (availableAdults.Count > 0) availableAdults.Clear();
                        Time workSchoolStartTime, workSchoolEndTime;
                        bool available;
                        foreach (var person in household.Persons)
                        {
                            workSchoolStartTime = SchedulerPerson.GetWorkSchoolStartTime(person);
                            workSchoolEndTime = SchedulerPerson.GetWorkSchoolEndTime(person);

                            // this person is available if
                            available = (workSchoolStartTime > endTime) | (workSchoolEndTime < startTime) | (workSchoolStartTime == Time.Zero);

                            if (person.Age >= 16 && available)
                            {
                                availableAdults.Add(person);
                            }
                        }

                        if ((availableAdults.Count > 0) & (availableAdults.Count >= numEpisodeAdults))
                        {
                            Episode jointMarketEpisode;
                            jointMarketEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), Activity.JointMarket,
                                availableAdults[0]);

                            foreach (ITashaPerson adult in availableAdults)
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

        private static bool GenerateJointOtherEpisodes(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            var householdPD = household.HomeZone.PlanningDistrict;
            //make sure there at least 2 people and one adult
            if ((household.Persons.Length >= 2) & (household.NumberOfAdults > 0))
            {
                int freqJ = TimeTable.GetFrequency(household, Activity.JointOther, random, householdPD,
                    0, generationAdjustments, startTimeAdjustments);
                int numEpisodeAdults = Distribution.GetNumAdultsJointEpisode(household, random,
                    Activity.JointOther);

                for (int i = 0; i < freqJ; i++)
                {
                    bool success = false;
                    int attempt = 0;
                    while (!success && attempt < Scheduler.EpisodeSchedulingAttempts)
                    {
                        if (!TimeTable.GetStartTime(household, Activity.JointOther, freqJ, random, householdPD, 0, startTimeAdjustments, out Time startTime))
                        {
                            attempt++;
                            continue;
                        }
                        if (!TimeTable.GetDuration(household, Activity.JointOther, startTime, random, out Time duration))
                        {
                            attempt++;
                            continue;
                        }

                        if (duration == Time.Zero || startTime == Time.Zero)
                        {
                            attempt++;
                        }
                        else
                        {
                            Time endTime = startTime + duration;
                            List<ITashaPerson> availableAdults = new List<ITashaPerson>();
                            foreach (ITashaPerson person in household.Persons)
                            {
                                Time workSchoolStartTime = SchedulerPerson.GetWorkSchoolStartTime(person);
                                Time workSchoolEndTime = SchedulerPerson.GetWorkSchoolEndTime(person);
                                bool available = false;
                                if (workSchoolStartTime > endTime ||
                                    workSchoolEndTime < startTime ||
                                    workSchoolStartTime == Time.Zero)
                                    available = true;
                                if (person.Age >= 16 && available)
                                {
                                    availableAdults.Add(person);
                                }
                            }

                            if (availableAdults.Count >= numEpisodeAdults && availableAdults.Count > 0)
                            {
                                Episode jointOtherEpisode;
                                var owner = availableAdults[0];
                                jointOtherEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), Activity.JointOther, owner);

                                for (int j = 0; j < numEpisodeAdults; j++)
                                {
                                    jointOtherEpisode.AddPerson(availableAdults[j]);
                                }

                                Project jointOtherProject = household.GetJointOtherProject();
                                Schedule jointOtherSchedule = jointOtherProject.Schedule;
                                bool inserted = jointOtherSchedule.Insert(jointOtherEpisode, random);
                                success = true;

                                if (!inserted)
                                {
                                    success = false;
                                    attempt++;
                                }
                            }
                            else
                            {
                                attempt++;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static bool GenerateSchoolEpisodes(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            var householdPD = household.HomeZone.PlanningDistrict;
            foreach (ITashaPerson person in household.Persons)
            {
                if (person.StudentStatus == StudentStatus.FullTime ||
                    person.StudentStatus == StudentStatus.PartTime)
                {
                    if (person.Age >= 11)
                    {
                        var empZone = person.EmploymentZone;
                        int workPD = empZone == null ? 0 : empZone.PlanningDistrict;
                        var freq = TimeTable.GetFrequency(person, Activity.School, random, householdPD, workPD, generationAdjustments);
                        //if there is a school activity generated
                        for (int i = 0; i < freq; i++)
                        {
                            bool success = false;
                            short attempt = 0;
                            int maxAttempts = Scheduler.EpisodeSchedulingAttempts;
                            while (!success && (attempt < maxAttempts))
                            {
                                attempt++;
                                //get start time end time and duration
                                if (!TimeTable.GetStartTime(person, Activity.School, random, householdPD, workPD, startTimeAdjustments, out Time startTime))
                                {
                                    continue;
                                }
                                if (!TimeTable.GetDuration(person, Activity.School, startTime, random, out Time duration))
                                {
                                    continue;
                                }
                                if (duration != Time.Zero) // no valid duration
                                {
                                    var endTime = startTime + duration;
                                    if (endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                                    {
                                        continue;
                                    }
                                    //instantiate temporary school episode;
                                    Schedule schoolSchedule;
                                    Episode schoolEpisode;

                                    schoolEpisode =
                                       new ActivityEpisode(new TimeWindow(startTime, startTime + duration),
                                           Activity.School, person)
                                       {
                                           Zone = person.SchoolZone ?? person.Household.HomeZone
                                       };
                                    Project schoolProject = person.GetSchoolProject();
                                    schoolSchedule = schoolProject.Schedule;
                                    success = schoolSchedule.Insert(schoolEpisode, random);
                                }
                            }
                        }
                    }
                    else if (person.Age >= 6)
                    {
                        //this child is in kindergarten
                        //generate random number between 0 and 1
                        Time startTime, endTime;
                        startTime = Scheduler.SchoolMorningStart;
                        endTime = Scheduler.SchoolAfternoonEnd;
                        Schedule schoolSchedule;
                        Episode schoolEpisode;

                        schoolEpisode =
                           new ActivityEpisode(new TimeWindow(startTime, endTime),
                               Activity.School, person)
                           {
                               Zone = person.SchoolZone ?? person.Household.HomeZone
                           };
                        Project schoolProject = person.GetSchoolProject();
                        schoolSchedule = schoolProject.Schedule;
                        schoolSchedule.Insert(schoolEpisode, random);
                    }
                    else if (person.Age == 5)
                    {
                        //this child is in kindergarten
                        //generate random number between 0 and 1
                        int randNum = random.Next(0, 1);
                        Time startTime, endTime;
                        if (randNum <= 0.5) //morning shift
                        {
                            //morning shift
                            startTime = Scheduler.SchoolMorningStart;
                            endTime = Scheduler.SchoolMorningEnd;
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
                           new ActivityEpisode(new TimeWindow(startTime, endTime),
                               Activity.School, person)
                           {
                               Zone = person.SchoolZone ?? person.Household.HomeZone
                           };
                        Project schoolProject = person.GetSchoolProject();
                        schoolSchedule = schoolProject.Schedule;
                        schoolSchedule.Insert(schoolEpisode, random);
                    }
                }
            }
            return true;
        }

        private static void GenerateWorkBusinessEpisode(ITashaPerson person, Schedule workSchedule, Random random, int householdPD,
            int workPD, GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            int freqB = TimeTable.GetFrequency(person, Activity.WorkBasedBusiness, random, householdPD, workPD, generationAdjustments);
            for (int i = 0; i < freqB; i++)
            {
                for (int attempt = 0; attempt < Scheduler.EpisodeSchedulingAttempts; attempt++)
                {
                    if (!TimeTable.GetStartTime(person, Activity.WorkBasedBusiness, random, householdPD, workPD, startTimeAdjustments, out Time startTime))
                    {
                        continue;
                    }
                    if (!TimeTable.GetDuration(person, Activity.WorkBasedBusiness, startTime, random, out Time duration))
                    {
                        continue;
                    }
                    var endTime = startTime + duration;
                    if (endTime > Time.EndOfDay + TashaRuntime.EndOfDay + Time.OneQuantum)
                    {
                        continue;
                    }
                    Episode workEpisode = new ActivityEpisode(new TimeWindow(startTime, startTime + duration),
                        Activity.WorkBasedBusiness, person);
                    workSchedule.Insert(workEpisode, random);
                    break;
                }
            }
        }

        private static bool GenerateWorkEpisodes(this ITashaHousehold household, Random random,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            var householdPD = household.HomeZone.PlanningDistrict;
            foreach (ITashaPerson person in household.Persons)
            {
                // people need to be older than "11" to be allowed to work
                if (person.Age < Scheduler.MinimumWorkingAge) continue;
                // Check to see if they are in a regular job type case
                Project workProject = person.GetWorkProject();
                Schedule workSchedule = workProject.Schedule;
                if ((person.Occupation == Occupation.NotEmployed) | (person.Occupation == Occupation.Unknown))
                {
                    continue;
                }
                if (((person.EmploymentStatus == TTSEmploymentStatus.FullTime) | (person.EmploymentStatus == TTSEmploymentStatus.PartTime)))
                {
                    
                    var empZone = person.EmploymentZone;
                    int workPD = empZone == null ? 0 : empZone.PlanningDistrict;
                    bool isTelecommuter = Scheduler.GetTelecommuter(person);
                    if(isTelecommuter)
                    {
                        empZone = household.HomeZone;
                    }
                    if (person.EmploymentZone == null)
                    {
                        //continue;
                        throw new XTMFRuntimeException(TashaRuntime, "There was a person whom had no employment zone however was a full-time/part-time worker!");
                    }
                    //Employment zone doesn't exist so generate our own based on distributions
                    if (person.EmploymentZone.ZoneNumber == TashaRuntime.ZoneSystem.RoamingZoneNumber)
                    {
                        GenerateWorkBusinessEpisode(person, workSchedule, random, householdPD, workPD,
                            generationAdjustments, startTimeAdjustments);
                        continue;
                    }

                    // If the person is telecommuting they have already made the choice to generate the activity episode.
                    int freq = isTelecommuter ? 1 : TimeTable.GetFrequency(person, Activity.PrimaryWork, random, householdPD, workPD, generationAdjustments);
                    if (freq <= 0)
                    {
                        continue;
                    }
                    Time startTime = Time.Zero;
                    Time duration = Time.Zero;
                    bool success = false;
                    for (int i = 0; i < freq; i++)
                    {
                        for (int attempts = 0; attempts < Scheduler.EpisodeSchedulingAttempts; attempts++)
                        {
                            // If we use an and here the compiler can't prove that we assign to duration
                            if (!TimeTable.GetStartTime(person, Activity.PrimaryWork, freq, random, householdPD, workPD, startTimeAdjustments, out startTime))
                            {
                                continue;
                            }

                            if (TimeTable.GetDuration(person, Activity.PrimaryWork, startTime, random, out duration))
                            {
                                // if we got the duration, success lets continue on
                                success = true;
                                break;
                            }
                        }
                        // if we were unable to get a duration
                        if (!success)
                        {
                            // try the next person
                            continue;
                        }
                        Time endTime = startTime + duration;

                        CheckAndUpdateLatestWorkingTime(person.Household,
                            endTime);

                        Episode primWorkEpisode;

                        primWorkEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime),
                                    Activity.PrimaryWork, person)
                        {
                            Zone = person.EmploymentZone
                        };
                        if (!workSchedule.Insert(primWorkEpisode, random) && i == 0)
                        {
                            throw new XTMFRuntimeException(TashaRuntime, "Failed to insert the primary work episode into the work project!");
                        }
                        //set up work business activities
                        ProcessWorkBusiness(person, workSchedule, random, primWorkEpisode, householdPD, workPD,
                            generationAdjustments, startTimeAdjustments);
                        //set up secondary work activities
                        ProcessSecondaryWork(person, workSchedule, random, primWorkEpisode, householdPD, workPD,
                            generationAdjustments, startTimeAdjustments);
                        //set up return home from work activities
                        ProcessReturnHomeFromWork(person, workSchedule, random, primWorkEpisode, householdPD, workPD,
                            generationAdjustments, startTimeAdjustments);
                    }
                }
                // Check to see if they work from home
                else if (person.Age >= 19 &&
                    ((person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_FullTime)
                    | (person.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_PartTime)))
                {
                    ProcessWorkAtHome(person, workSchedule, random, householdPD, householdPD, generationAdjustments, startTimeAdjustments);
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
        /// <param name="random"></param>
        /// <param name="episode"></param>
        /// <param name="householdPD"></param>
        /// <param name="workPD"></param>
        /// <param name="generationAdjustments"></param>
        /// <returns></returns>
        private static void ProcessReturnHomeFromWork(ITashaPerson person, Schedule schedule, Random random,
            Episode episode, int householdPD, int workPD, GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            //the current work schedule doesn't allow for a return from work activity
            if (episode.StartTime > Scheduler.MaxPrimeWorkStartTimeForReturnHomeFromWork
                || episode.Duration < Scheduler.MinPrimaryWorkDurationForReturnHomeFromWork)
            {
                return;
            }

            //End time of work to home activity
            Time endTime = episode.EndTime + new Time(0.3f) < Scheduler.ReturnHomeFromWorkMaxEndTime ?
                episode.EndTime + new Time(0.3f) : Scheduler.ReturnHomeFromWorkMaxEndTime;

            var freq = TimeTable.GetFrequency(person, Activity.ReturnFromWork, random, 1, episode.StartTime + new Time(0.3f), endTime,
                householdPD, workPD, generationAdjustments, startTimeAdjustments);

            if (freq == 1)
            {
                IZone homeZone = person.Household.HomeZone;

                Time halfAnHour = new Time() { Minutes = 30 };

                Time maxEndTime = ((episode.EndTime - halfAnHour) < Scheduler.ReturnHomeFromWorkMaxEndTime) ? (episode.EndTime - halfAnHour) : Scheduler.ReturnHomeFromWorkMaxEndTime;

                if (!TimeTable.GetStartTime(person, Activity.ReturnFromWork
                     , freq
                     , episode.StartTime + halfAnHour
                     , maxEndTime, random, householdPD, workPD, startTimeAdjustments, out Time startTime))
                {
                    return;
                }

                Time maxDuration = new Time(Math.Min((Scheduler.ReturnHomeFromWorkMaxEndTime - Time.OneHour).ToFloat(),
                    (episode.EndTime - halfAnHour - startTime).ToFloat()));

                if (!TimeTable.GetDuration(person, Activity.ReturnFromWork, startTime, maxDuration, random, out Time duration))
                {
                    // reject
                    return;
                }

                Episode returnFromWorkEpisode;
                returnFromWorkEpisode = new ActivityEpisode(new TimeWindow(startTime, startTime + duration), Activity.ReturnFromWork,
                    person)
                {
                    Zone = homeZone
                };
                schedule.Insert(returnFromWorkEpisode, random);
            }
        }

        /// <summary>
        /// Secondary work creates a secondary work activity for person
        /// </summary>
        /// <param name="person"></param>
        /// <param name="schedule"></param>
        /// <param name="random"></param>
        /// <param name="primaryWorkEpisode"></param>
        /// <param name="householdPD"></param>
        /// <param name="workPD"></param>
        /// <param name="generationAdjustments"></param>
        private static void ProcessSecondaryWork(ITashaPerson person, Schedule schedule, Random random, Episode primaryWorkEpisode, int householdPD,
            int workPD, GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            ArgumentNullException.ThrowIfNull(random);
            //can only work if finish primary work by 7:00PM
            if (primaryWorkEpisode.EndTime < Scheduler.SecondaryWorkThreshold)
            {
                int freqR;

                //getting earliest possible startTime
                Time hourAfterWork = primaryWorkEpisode.EndTime + Time.OneHour;
                Time minStartTime = hourAfterWork > Scheduler.SecondaryWorkMinStartTime ? hourAfterWork : Scheduler.SecondaryWorkMinStartTime;

                freqR = TimeTable.GetFrequency(person, Activity.SecondaryWork, random, 10, minStartTime, Time.EndOfDay,
                    householdPD, workPD, generationAdjustments, startTimeAdjustments);

                for (int i = 0; i < freqR; i++)
                {
                    //zone same as work zone
                    IZone zone = primaryWorkEpisode.Zone.ZoneNumber == Scheduler.Tasha.ZoneSystem.RoamingZoneNumber
                        ? Scheduler.LocationChoiceModel.GetLocationHomeBased(Activity.SecondaryWork, person.Household.HomeZone, random)
                        : primaryWorkEpisode.Zone;


                    if (!TimeTable.GetStartTime(person, primaryWorkEpisode.ActivityType, freqR, minStartTime, Time.EndOfDay,
                        random, householdPD, workPD, startTimeAdjustments, out Time startTimeR))
                    {
                        //TODO: We might want to reconsider this, skipping instead of just throwing an exception
                        //throw new XTMFRuntimeException("Unable to find a start time for a primary work episode");
                        return;
                    }

                    if (!TimeTable.GetDuration(person, Activity.SecondaryWork, startTimeR, Time.EndOfDay - startTimeR, random, out Time durationR))
                    {
                        //throw new XTMFRuntimeException("Unable to find a duration for a primary work episode");
                        return;
                    }

                    //inserting secondary work into schedule
                    Episode secondaryWorkEpisode;
                    secondaryWorkEpisode = new ActivityEpisode(new TimeWindow(startTimeR, startTimeR + durationR),
                        Activity.SecondaryWork, person)
                    {
                        Zone = zone
                    };
                    schedule.Insert(secondaryWorkEpisode, random);
                }
            }
        }

        private static void ProcessWorkAtHome(ITashaPerson person, Schedule workSchedule, Random random, int householdPD,
            int workPD, GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            int freqA = TimeTable.GetFrequency(person, Activity.WorkAtHomeBusiness, random, householdPD,
                workPD, generationAdjustments);
            for (int i = 0; i < freqA; i++)
            {
                bool success = false;
                short attempt = 0;
                while (!success && (attempt < Scheduler.EpisodeSchedulingAttempts))
                {
                    if (!TimeTable.GetStartTime(person, Activity.WorkAtHomeBusiness, freqA, random,
                        householdPD, workPD, startTimeAdjustments, out Time startTime))
                    {
                        attempt++;
                        continue;
                    }
                    if (!TimeTable.GetDuration(person, Activity.WorkAtHomeBusiness,
                        startTime, Time.EndOfDay - startTime, random, out Time duration))
                    {
                        attempt++;
                        continue;
                    }

                    Time endTime = startTime + duration;
                    CheckAndUpdateLatestWorkingTime(person.Household,
                        endTime);
                    Episode wahBusinessEpisode;
                    wahBusinessEpisode = new ActivityEpisode(new TimeWindow(startTime, endTime), Activity.WorkAtHomeBusiness, person);
                    if (!workSchedule.Insert(wahBusinessEpisode, random))
                    {
                        attempt++;
                    }
                    else
                    {
                        success = true;
                    }
                }
            }
        }

        private static void ProcessWorkBusiness(ITashaPerson person, Schedule workSchedule, Random random, Episode primWorkEpisode,
            int householdPD, int workPD, GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            Time startTime = primWorkEpisode.StartTime;
            Time endTime = primWorkEpisode.EndTime;

            int freq = TimeTable.GetFrequency(person, Activity.WorkBasedBusiness, random, Scheduler.MaxFrequency, startTime, endTime,
                householdPD, workPD, generationAdjustments, startTimeAdjustments);
            for (int i = 0; i < freq; i++)
            {
                var attempt = 0;
                while (attempt < Scheduler.EpisodeSchedulingAttempts)
                {
                    attempt++;
                    if (!TimeTable.GetStartTime(person, Activity.WorkBasedBusiness, freq,
                        startTime, endTime, random, householdPD, workPD, startTimeAdjustments, out Time startTimeB))
                    {
                        continue;
                    }

                    if (!TimeTable.GetDuration(person, Activity.WorkBasedBusiness, startTimeB, endTime - startTimeB, random, out Time durationB))
                    {
                        continue;
                    }

                    Episode businessEpisode;
                    businessEpisode = new ActivityEpisode(new TimeWindow(startTimeB, startTimeB + durationB), Activity.WorkBasedBusiness, person);
                    if (workSchedule.Insert(businessEpisode, random))
                    {
                        break;
                    }
                }
            }
        }
    }
}