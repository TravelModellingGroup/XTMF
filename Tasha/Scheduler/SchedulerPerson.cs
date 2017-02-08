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
    public static class SchedulerPerson
    {
        public static void AddPersonalProjects(this ITashaPerson person, Random random)
        {
            Schedule pSched = ( (SchedulerPersonData)person["SData"] ).Schedule;
            // First is other then is market episodes
            AddPersonProjects( pSched, person.GetOtherProject(), random );
            AddPersonProjects( pSched, person.GetMarketProject(), random );
        }

        public static void InitializePersonalProjects(this ITashaPerson person)
        {
            //Work | School | IndividualOther | IndividualMarket
            SchedulerPersonData data;
            person.Attach( "SData", data = new SchedulerPersonData() );
            ProjectSchedule workSchedule = new ProjectSchedule( person.Household );
            ProjectSchedule schoolSchedule = new ProjectSchedule( person.Household );
            ProjectSchedule otherSchedule = new ProjectSchedule( person.Household );
            ProjectSchedule marketSchedule = new ProjectSchedule( person.Household );
            // We could just call the other methods, but this will run much faster
            data.WorkSchedule = new PersonalProject( workSchedule, person );
            data.SchoolSchedule = new PersonalProject( schoolSchedule, person );
            data.OtherSchedule = new PersonalProject( otherSchedule, person );
            data.MarketSchedule = new PersonalProject( marketSchedule, person );
            data.Schedule = new PersonSchedule( person );
        }

        internal static void AddHouseholdProjects(this ITashaHousehold household, SchedHouseholdData hdata, Random random)
        {
            household.AddHouseholdProjects( hdata.JointOtherProject.Schedule, random );
            household.AddHouseholdProjects( hdata.JointMarketProject.Schedule, random );
        }

        internal static void AddHouseholdProjects(this ITashaHousehold household, Schedule schedule, Random random)
        {
            for ( int i = 0; i < schedule.EpisodeCount; i++ )
            {
                var episode = (Episode)schedule.Episodes[i];
                episode.ContainingSchedule = schedule;
                var people = episode.People;
                if ( people != null )
                {
                    bool first = true;
                    IZone zone = null;
                    foreach ( var person in people )
                    {
                        var pData = person["SData"] as SchedulerPersonData;
                        //var check = pData.Schedule.EpisodeCount > 0;
                        var ep = new ActivityEpisode( 0, new TimeWindow( episode.StartTime, episode.EndTime ) /*window*/,
                            schedule.Episodes[i].ActivityType, person );
                        if ( first )
                        {
                            pData.Schedule.Insert( ep, random );
                            zone = ep.Zone;
                            first = false;
                        }
                        else
                        {
                            pData.Schedule.Insert( ep, zone );
                            pData.Schedule.CheckEpisodeIntegrity();
                        }
                    }
                }
            }
        }

        internal static void AddPersonProjects(Schedule sched, PersonalProject project, Random random)
        {
            sched.Insert( project.Schedule, random );
        }

        /// <summary>
        /// turn household activities into trip chains
        /// </summary>
        /// <param name="household"></param>
        internal static void BuildChains(this ITashaHousehold household, int householdIterations, Time minimumAtHomeTime)
        {
            foreach ( var person in household.Persons )
            {
                BuildPersonChain( person, household.HomeZone, householdIterations, minimumAtHomeTime );
            }
        }

        internal static void CheckAndUpdateLatestWorkingTime(this ITashaPerson person, Time time)
        {
            if ( ( person["SData"] as SchedulerPersonData ).LatestWorkingTime < time )
            {
                ( person["SData"] as SchedulerPersonData ).LatestWorkingTime = time;
            }
        }

        internal static void CleanupSchedules(this ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                var pdata = ( person["SData"] as SchedulerPersonData );
                pdata.Schedule.CleanUp( new Time() { Minutes = 30 } );
            }
        }

        /// <summary>
        /// Used for debugging only.  Counts how many episodes were generated for this person
        /// </summary>
        /// <param name="person">The person we want to _Count their events for</param>
        internal static int CountEpisodes(this ITashaPerson person)
        {
            int count = 0;
            var data = person["SData"] as SchedulerPersonData;
            // We don't include the null episode
            count += data.WorkSchedule.Schedule.NumberOfEpisodes;
            count += data.SchoolSchedule.Schedule.NumberOfEpisodes;
            count += data.MarketSchedule.Schedule.NumberOfEpisodes;
            count += data.OtherSchedule.Schedule.NumberOfEpisodes;
            return count;
        }

        /// <summary>
        /// Generate the schedule for the person
        /// </summary>
        /// <param name="person"></param>
        internal static void GenerateWorkSchoolSchedule(this ITashaPerson person, Random random)
        {
            var data = ( person["SData"] as SchedulerPersonData );
            // Schedule school first if they are a part time student
            if ( person.StudentStatus == StudentStatus.FullTime
                && person.EmploymentStatus == TTSEmploymentStatus.PartTime )
            {
                person.AddSchool( data, random );
                person.AddWork( data, random );
            }
            else
            {
                person.AddWork( data, random );
                person.AddSchool( data, random );
            }
        }

        internal static PersonalProject GetMarketProject(this ITashaPerson person)
        {
            return ( person["SData"] as SchedulerPersonData ).MarketSchedule;
        }

        internal static PersonalProject GetOtherProject(this ITashaPerson person)
        {
            return ( person["SData"] as SchedulerPersonData ).OtherSchedule;
        }

        internal static PersonalProject GetSchoolProject(this ITashaPerson person)
        {
            return ( person["SData"] as SchedulerPersonData ).SchoolSchedule;
        }

        internal static PersonalProject GetWorkProject(this ITashaPerson person)
        {
            return ( person["SData"] as SchedulerPersonData ).WorkSchedule;
        }

        internal static Time GetWorkSchoolEndTime(ITashaPerson person)
        {
            Time workSchoolEndTime = Time.Zero;

            Project workProject = person.GetWorkProject();
            Schedule workSchedule = workProject.Schedule;
            Time workEndTime = workSchedule.GetLastEpisodeEndTime();

            Project schoolProject = person.GetSchoolProject();
            Schedule schoolSchedule = schoolProject.Schedule;
            Time schoolEndTime = schoolSchedule.GetLastEpisodeEndTime();

            if ( workEndTime != Time.Zero && schoolEndTime == Time.Zero )
            {
                workSchoolEndTime = workEndTime;
            }
            else if ( workEndTime == Time.Zero && schoolEndTime != Time.Zero )
            {
                workSchoolEndTime = schoolEndTime;
            }
            else if ( workEndTime != Time.Zero && schoolEndTime != Time.Zero )
            {
                workSchoolEndTime = schoolEndTime > workEndTime ? schoolEndTime : schoolEndTime;
            }
            else
            {
                workSchoolEndTime = Time.Zero;
            }

            return workSchoolEndTime;
        }

        /// <summary>
        /// Funky function directly from Tasha original
        /// </summary>
        /// <param name="person"></param>
        /// <returns></returns>
        internal static PersonWorkSchoolProjectStatus GetWorkSchoolProjectStatus(ITashaPerson person)
        {
            //TashaTime workSchoolStartTime = (person["SData"] as SchedulerPersonData).;
            Time workSchoolStartTime = GetWorkSchoolStartTime( person );
            Time workSchoolEndTime = GetWorkSchoolEndTime( person );

            PersonWorkSchoolProjectStatus workSchoolProjectStatus = PersonWorkSchoolProjectStatus.NoWorkOrSchool;

            if ( workSchoolStartTime <= Time.StartOfDay )
            {
                workSchoolProjectStatus = PersonWorkSchoolProjectStatus.NoWorkOrSchool;
            }
            else if ( ( workSchoolEndTime - workSchoolStartTime ) >= Scheduler.FullTimeActivity )
            {
                if ( workSchoolEndTime <= new Time() { Hours = 14 } ) workSchoolProjectStatus = PersonWorkSchoolProjectStatus.FullTimeNoEveningWorkOrSchool;
                else if ( workSchoolStartTime >= new Time() { Hours = 9 } ) workSchoolProjectStatus = PersonWorkSchoolProjectStatus.FullTimeEveningWorkOrSchool;
                else workSchoolProjectStatus = PersonWorkSchoolProjectStatus.FullTimeDayAndEveningWorkOrSchool;
            }
            else if ( workSchoolEndTime - workSchoolStartTime < Scheduler.FullTimeActivity )
            {
                if ( workSchoolEndTime <= new Time() { Hours = 9 } ) workSchoolProjectStatus = PersonWorkSchoolProjectStatus.PartTimeDay;
                else if ( workSchoolEndTime <= new Time() { Hours = 14 } ) workSchoolProjectStatus = PersonWorkSchoolProjectStatus.PartTimeEvening;
                else workSchoolProjectStatus = PersonWorkSchoolProjectStatus.Other;
            }

            return workSchoolProjectStatus;
        }

        internal static Time GetWorkSchoolStartTime(ITashaPerson person)
        {
            Time workSchoolStartTime = Time.Zero;
            Project workProject = person.GetWorkProject();
            Schedule workSchedule = workProject.Schedule;
            Time workStartTime = workSchedule.GetFirstEpisodeStartTime();
            Project schoolProject = person.GetSchoolProject();
            Schedule schoolSchedule = schoolProject.Schedule;
            Time schoolStartTime = schoolSchedule.GetFirstEpisodeStartTime();

            if ( workStartTime != Time.Zero && schoolStartTime == Time.Zero )
            {
                workSchoolStartTime = workStartTime;
            }
            else if ( workStartTime == Time.Zero && schoolStartTime != Time.Zero )
            {
                workSchoolStartTime = schoolStartTime;
            }
            else if ( workStartTime != Time.Zero && schoolStartTime != Time.Zero )
            {
                workSchoolStartTime = schoolStartTime < workStartTime ? schoolStartTime : workStartTime;
            }
            else
            {
                workSchoolStartTime = Time.Zero;
            }

            return workSchoolStartTime;
        }

        internal static void SetMarketProject(this ITashaPerson person, PersonalProject market)
        {
            ( person["SData"] as SchedulerPersonData ).MarketSchedule = market;
        }

        internal static void SetOtherProject(this ITashaPerson person, PersonalProject other)
        {
            ( person["SData"] as SchedulerPersonData ).OtherSchedule = other;
        }

        internal static void SetSchoolProject(this ITashaPerson person, PersonalProject school)
        {
            ( person["SData"] as SchedulerPersonData ).SchoolSchedule = school;
        }

        internal static void SetWorkProject(this ITashaPerson person, PersonalProject work)
        {
            ( person["SData"] as SchedulerPersonData ).WorkSchedule = work;
        }

        private static bool AddHouseholdPass(Episode ep, ref TimeWindow feasible)
        {
            // Check to find the time that everyone is available at
            foreach ( var person in ep.People )
            {
                // if we are make sure we can go to the event
                var pdata = ( person["SData"] as SchedulerPersonData );
                if ( !pdata.Schedule.CheckEpisodeInsert( ep, ref feasible ) )
                {
                    // if we can not fit it in reject it
                    return false;
                }
            }
            return true;
        }

        private static void AddSchool(this ITashaPerson person, SchedulerPersonData data, Random random)
        {
            data.Schedule.Insert( data.SchoolSchedule.Schedule, random );
        }

        private static void AddWork(this ITashaPerson person, SchedulerPersonData data, Random random)
        {
            data.Schedule.InsertWorkSchedule( data.WorkSchedule.Schedule, random );
        }

        private static void BuildPersonChain(ITashaPerson person, IZone home, int householdIterations, Time minimumAtHomeTime)
        {
            if ( person.TripChains == null )
            {
                throw new XTMFRuntimeException( "A Person's trip chains must be initialized during construction." );
            }
            var pdata = person["SData"] as SchedulerPersonData;
            pdata.Schedule.GenerateTrips( person.Household, householdIterations, minimumAtHomeTime );
        }
    }

    public class SchedulerPersonData
    {
        public Time LatestWorkingTime;
        public PersonalProject MarketSchedule;
        public PersonalProject OtherSchedule;

        /// <summary>
        /// From this schedule we build the trips
        /// by taking the other schedules and inserting them
        /// in order
        /// </summary>
        public PersonSchedule Schedule;

        public PersonalProject SchoolSchedule;
        public PersonalProject WorkSchedule;

        internal SchedulerPersonData()
        {
            LatestWorkingTime = new Time( 04.00f );
        }
    }
}