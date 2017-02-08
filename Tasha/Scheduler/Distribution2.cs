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
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Tasha.Scheduler
{
    internal static partial class Distribution
    {
        public static Time DistributionToDuration(int distributionVal)
        {
            Time time = new Time();

            int totalMinutes = distributionVal * Scheduler.StartTimeQuantaInterval;
            time.Hours = (sbyte)( totalMinutes / 60 );
            time.Minutes = (sbyte)( totalMinutes % 60 );
            return time;
        }

        public static Time DistributionToTashaTime(int distributionVal)
        {
            Time time = new Time();

            int totalMinutes = distributionVal * Scheduler.StartTimeQuantaInterval;
            time.Hours = (sbyte)( totalMinutes / 60 );
            time.Minutes = (sbyte)( totalMinutes % 60 );
            return time;
        }

        public static Time DistributionToTimeOfDay(int distributionVal)
        {
            Time time = new Time();
            int totalMinutes = distributionVal * Scheduler.StartTimeQuantaInterval;
            time.Hours = (sbyte)( totalMinutes / 60 );
            time.Minutes = (sbyte)( totalMinutes % 60 );
            return time + Time.StartOfDay;
        }

        public static int DurationToDistribution(Time time)
        {
            int distribution = ( ( 60 / Scheduler.StartTimeQuantaInterval ) * time.Hours ) + ( time.Minutes / Scheduler.StartTimeQuantaInterval );
            return distribution;
        }

        public static bool GetRandomStartDurationTimeFrequency(int distribution, Time tstart, int min, int max, Random random, out Time startTime)
        {
            int start = TimeOfDayToDistribution( tstart );
            float[][] pdf = Distributions[distribution].Durations;
            if ( start == Scheduler.StartTimeQuanta ) start = 0;
            float rand = (float)random.NextDouble();
            float pdfFactor = 0.0f;
            for ( int i = min; i <= max; ++i )
            {
                pdfFactor += pdf[start][i];
            }
            if ( pdfFactor == 0 )
            {
                startTime = Time.Zero;
                return false;
            }
            rand *= pdfFactor;
            float cdf = 0.0f;
            for ( int i = min; i <= max; ++i )
            {
                cdf += pdf[start][i];
                if ( rand < cdf )
                {
                    if ( i == 0 )
                    {
                        startTime = DistributionToDuration( 1 );
                    }
                    else
                    {
                        startTime = DistributionToDuration( i );
                    }
                    if ( startTime == Time.Zero )
                    {
                        throw new XTMFRuntimeException( "Tried to create a zero duration episode!" );
                    }
                    return true;
                }
            }
            // if we get here, it was the last one but off due to rounding errors
            startTime = DistributionToDuration( max );
            return true;
        }

        public static bool GetRandomStartTimeFrequency(int distribution, int freq, int min, int max, Random random, out Time startTime)
        {
            float[][] pdf = Distributions[distribution].StartTimeFrequency;

            if ( min >= max )
            {
                startTime = DistributionToTimeOfDay( max );
                return true;
            }

            double rand = random.NextDouble();
            float pdfFactor = 0.0f;
            for ( int i = min; i < max; ++i )
            {
                pdfFactor += pdf[i][freq];
            }

            if ( pdfFactor == 0 )
            {
                startTime = Time.Zero;
                return false;
            }
            rand *= pdfFactor;
            float cdf = 0.0f;
            for ( int i = min; i < max; i++ )
            {
                cdf += pdf[i][freq];

                if ( rand < cdf )
                {
                    startTime = DistributionToTimeOfDay( i );
                    if ( startTime == Time.Zero )
                    {
                        throw new XTMFRuntimeException( "Tried to create an episode that starts at time 0!" );
                    }
                    return true;
                }
            }
            // if we get here, it was the last one but off due to rounding errors
            startTime = DistributionToTimeOfDay( max );
            return true;
        }

        /// <summary>
        /// Converts a TashaTime object into a time distribution value. Seconds are ignored
        /// from the TashaTime parameter passed.
        /// </summary>
        /// <param name="time">The time to convert</param>
        /// <returns></returns>
        public static int TashaTimeToDistribution(Time time)
        {
            //time = time - TashaTime.StartOfDay;
            int distribution = ( ( 60 / Scheduler.StartTimeQuantaInterval ) * time.Hours ) + ( time.Minutes / Scheduler.StartTimeQuantaInterval );
            return distribution;
        }

        public static int TimeOfDayToDistribution(Time time)
        {
            time = time - Time.StartOfDay;
            int distribution = ( ( 60 / Scheduler.StartTimeQuantaInterval ) * time.Hours ) + ( time.Minutes / Scheduler.StartTimeQuantaInterval );
            return distribution;
        }

        internal static int GetDistributionID(ITashaHousehold household, Activity activity)
        {
            int baseOffset;
            int childOffset;
            int adultOffset = 0;
            int statusOffset;

            var projectStatus = SchedulerHousehold.GetWorkSchoolProjectStatus( household );

            if ( activity == Activity.JointOther ) baseOffset = 158;
            else baseOffset = 238;
            if ( household.NumberOfChildren > 0 ) childOffset = 0;
            else childOffset = 12;
            if ( household.NumberOfAdults == 1 ) adultOffset = 0;
            else if ( household.NumberOfAdults == 2 ) adultOffset = 1;
            else if ( household.NumberOfAdults >= 3 ) adultOffset = 2;
            if ( projectStatus == HouseholdWorkSchoolProjectStatus.NoWorkOrSchool ) statusOffset = 0;
            else if ( projectStatus == HouseholdWorkSchoolProjectStatus.NoEveningWorkOrSchool ) statusOffset = 1;
            else if ( projectStatus == HouseholdWorkSchoolProjectStatus.EveningWorkOrSchool ) statusOffset = 2;
            else statusOffset = 3; //WorkSchoolProjectStatus.DayAndEveningWorkOrShool

            return ( baseOffset + childOffset ) + ( adultOffset * 4 ) + statusOffset;
        }

        /// <summary>
        /// Returns the distribution ID that this person belongs to
        /// </summary>
        /// <param name="person"></param>
        /// <param name="activity"></param>
        /// <returns></returns>
        internal static int GetDistributionID(ITashaPerson person, Activity activity)
        {
            int baseOffset;
            int ageOffset;
            int occupationOffset;
            int age = person.Age;
            switch ( activity )
            {
                case Activity.School:
                    baseOffset = 84;
                    //Now Calculate the occupation offset
                    switch ( person.StudentStatus )
                    {
                        case StudentStatus.FullTime:
                            occupationOffset = 0;
                            break;

                        case StudentStatus.PartTime:
                            occupationOffset = 1;
                            break;

                        default:
                            return -1;
                    }
                    if ( age < 11 )
                    {
                        return -1;
                    }
                    //Calculate the ageOffset
                    if ( age <= 15 )
                        ageOffset = 0;
                    else if ( age <= 18 )
                        ageOffset = 1;
                    else if ( age <= 25 )
                        ageOffset = 2;
                    else if ( age <= 30 )
                        ageOffset = 3;
                    else
                        ageOffset = 4;
                    return baseOffset + ( ageOffset * 2 ) + occupationOffset;

                case Activity.WorkBasedBusiness:
                    // We store the values from the person to improve performance
                    baseOffset = 40;
                    Occupation occupation = person.Occupation;
                    if ( person.EmploymentStatus != TTSEmploymentStatus.FullTime
                         && person.EmploymentStatus != TTSEmploymentStatus.PartTime )
                    {
                        return -1;
                    }
                    //Calculate the ageOffset
                    if ( age < 11 )
                        return -1;
                    if ( age <= 18 )
                        ageOffset = 0;
                    else if ( age <= 25 )
                        ageOffset = 1;
                    else if ( age <= 64 )
                        ageOffset = 2;
                    else
                        ageOffset = 3;

                    //Now Calculate the occupation offset
                    if ( occupation == Occupation.Office )
                        occupationOffset = 0;
                    else if ( occupation == Occupation.Manufacturing )
                        occupationOffset = 1;
                    else if ( occupation == Occupation.Professional )
                        occupationOffset = 2;
                    else if ( occupation == Occupation.Retail )
                        occupationOffset = 3;
                    else
                        return -1;

                    return baseOffset + ( ageOffset * 8 ) + occupationOffset * 2
                + ( person.EmploymentStatus == TTSEmploymentStatus.FullTime ? 0 : 1 );

                case Activity.PrimaryWork:
                    // We store the values from the person to improve performance
                    if(person.EmploymentZone == null)
                        return -1;
                    occupation = person.Occupation;
                    if ( person.EmploymentStatus != TTSEmploymentStatus.FullTime
                         && person.EmploymentStatus != TTSEmploymentStatus.PartTime )
                    {
                        return -1;
                    }
                    //Calculate the ageOffset
                    if ( age < 11 )
                        return -1;
                    if ( age <= 18 )
                        ageOffset = 0;
                    else if ( age <= 25 )
                        ageOffset = 1;
                    else if ( age <= 64 )
                        ageOffset = 2;
                    else
                        ageOffset = 3;
                    //Now Calculate the occupation offset
                    if ( occupation == Occupation.Office )
                        occupationOffset = 0;
                    else if ( occupation == Occupation.Manufacturing )
                        occupationOffset = 1;
                    else if ( occupation == Occupation.Professional )
                        occupationOffset = 2;
                    else if ( occupation == Occupation.Retail )
                        occupationOffset = 3;
                    else
                        return -1;

                    return ( ageOffset * 8 ) + occupationOffset * 2
                + ( person.EmploymentStatus == TTSEmploymentStatus.FullTime ? 0 : 1 );

                case Activity.SecondaryWork:
                    // We store the values from the person to improve performance
                    baseOffset = 32;
                    occupation = person.Occupation;
                    if ( person.EmploymentZone == null )
                        return -1;
                    if ( person.EmploymentStatus != TTSEmploymentStatus.FullTime
                         && person.EmploymentStatus != TTSEmploymentStatus.PartTime )
                    {
                        return -1;
                    }
                    if ( person.Age < 11 )
                    {
                        return -1;
                    }
                    //Now Calculate the occupation offset
                    if ( occupation == Occupation.Office )
                        occupationOffset = 0;
                    else if ( occupation == Occupation.Manufacturing )
                        occupationOffset = 1;
                    else if ( occupation == Occupation.Professional )
                        occupationOffset = 2;
                    else if ( occupation == Occupation.Retail )
                        occupationOffset = 3;
                    else
                        return -1;

                    // Ok, here we do the math, there are 8 distros per age group [only 1 age group]
                    // Each one is broken into 4 occupation types
                    // Each of them are broken into first, full-time then part-time
                    return
                        baseOffset + occupationOffset * 2
                        + ( person.EmploymentStatus == TTSEmploymentStatus.FullTime ? 0 : 1 );
                case Activity.WorkAtHomeBusiness:
                    // We store the values from the person to improve performance
                    baseOffset = 72;
                    occupationOffset = 0;
                    occupation = person.Occupation;
                    age = person.Age;
                    if ( person.EmploymentStatus != TTSEmploymentStatus.WorkAtHome_FullTime
                         && person.EmploymentStatus != TTSEmploymentStatus.WorkAtHome_PartTime )
                    {
                        return -1;
                    }
                    //Calculate the ageOffset
                    if ( age < 19 )
                        return -1;
                    if ( age <= 25 )
                        ageOffset = 0;
                    else if ( age <= 64 )
                        ageOffset = 1;
                    else
                        ageOffset = 2;

                    //Now Calculate the occupation offset
                    if ( occupation == Occupation.Office )
                        occupationOffset = 0;
                    else if ( occupation == Occupation.Manufacturing )
                        occupationOffset = 1;
                    else if ( occupation == Occupation.Professional )
                        occupationOffset = 2;
                    else if ( occupation == Occupation.Retail )
                        occupationOffset = 3;

                    // Ok, here we do the math, there are 8 distros per age group
                    // Each one is broken into 4 occupation types
                    // Each of them are broken into first, full-time then part-time
                    return baseOffset + ( ageOffset * 4 ) + occupationOffset;

                case Activity.ReturnFromWork:
                    // We store the values from the person to improve performance
                    baseOffset = 94;
                    occupationOffset = 0;
                    occupation = person.Occupation;
                    if ( person.EmploymentStatus != TTSEmploymentStatus.FullTime
                        && person.EmploymentStatus != TTSEmploymentStatus.PartTime )
                    {
                        return -1;
                    }
                    //Now Calculate the occupation offset
                    if ( occupation == Occupation.Office )
                        occupationOffset = 0;
                    else if ( occupation == Occupation.Manufacturing )
                        occupationOffset = 1;
                    else if ( occupation == Occupation.Professional )
                        occupationOffset = 2;
                    else if ( occupation == Occupation.Retail )
                        occupationOffset = 3;

                    // Ok, here we do the math, there are 8 distros per age group [only 1 age group]
                    // Each one is broken into 4 occupation types
                    // Each of them are broken into first, full-time then part-time
                    return
                        baseOffset + occupationOffset * 2 + ( person.EmploymentStatus == TTSEmploymentStatus.FullTime ? 0 : 1 );

                case Activity.IndividualOther:
                    baseOffset = 102;
                    //||[0,6] ||==7
                    PersonWorkSchoolProjectStatus workProjestStatus = SchedulerPerson.GetWorkSchoolProjectStatus( person );
                    age = person.Age;

                    //Calculate the ageOffset
                    if ( age < 11 )
                        return -1;
                    if ( age < 16 )
                        ageOffset = 0;
                    else if ( age < 25 )
                        ageOffset = 1;
                    else if ( age < 65 )
                        ageOffset = 2;
                    else
                        ageOffset = 3;
                    //
                    return
                        baseOffset + ( ageOffset * 14 ) + ( person.Female ? 7 : 0 ) + (int)workProjestStatus;
                case Activity.Market:
                    baseOffset = 182;
                    //||[0,6] ||==7
                    workProjestStatus = SchedulerPerson.GetWorkSchoolProjectStatus( person );
                    age = person.Age;

                    //Calculate the ageOffset
                    if ( age < 11 )
                        return -1;
                    if ( age < 16 )
                        ageOffset = 0;
                    else if ( age < 25 )
                        ageOffset = 1;
                    else if ( age < 65 )
                        ageOffset = 2;
                    else
                        ageOffset = 3;
                    // Ok, here we do the math, there are 8 distros per age group
                    // Each one is broken into 4 occupation types
                    // Each of them are broken into first, full-time then part-time
                    return
                        baseOffset + ( ageOffset * 14 ) + ( person.Female ? 7 : 0 ) + (int)workProjestStatus;
                default:
                    return -1;
            }
        }

        internal static int GetRandomNumberAdults(ITashaHousehold household, Activity activity, int min, int max, Random random)
        {
            int distID;
            switch ( activity )
            {
                case Activity.JointOther:
                    if ( household.NumberOfChildren > 0 )
                    {
                        if ( household.NumberOfAdults == 2 )
                        {
                            distID = 0;
                        }
                        else
                        {
                            if ( household.NumberOfAdults >= 3 )
                            {
                                distID = 1;
                            }
                            else
                            {
                                //error
                                throw new XTMFRuntimeException( "One adult, at least one child." );
                            }
                        }
                    }
                    else //no children
                    {
                        if ( household.NumberOfAdults >= 3 )
                        {
                            distID = 2;
                        }
                        else
                        {
                            //error
                            throw new XTMFRuntimeException( "error" );
                        }
                    }
                    break;

                case Activity.JointMarket:
                    if ( household.NumberOfChildren > 0 )
                    {
                        if ( household.NumberOfAdults == 2 )
                        {
                            distID = 3;
                        }
                        else
                        {
                            if ( household.NumberOfAdults >= 3 )
                            {
                                distID = 4;
                            }
                            else
                            {
                                //error
                                throw new XTMFRuntimeException( "error" );
                            }
                        }
                    }
                    else //no children
                    {
                        if ( household.NumberOfAdults >= 3 )
                        {
                            distID = 5;
                        }
                        else
                        {
                            //error
                            throw new XTMFRuntimeException( "error" );
                        }
                    }
                    break;

                default:
                    return 0;
            }
            return GetRandomAdultFrequency( distID, min, max, random );
        }

        private static int GetRandomAdultFrequency(int distid, int min, int max, Random random)
        {
            double randNum = random.NextDouble();
            float pdfFactor = 0.0f;
            var data = AdultDistributions[distid];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to load the adult frequency distribution!" );
            }
            var maxAdults = data.Adults.Length;
            if ( max > maxAdults )
            {
                max = maxAdults;
            }
            for ( int i = min; i < max; i++ )
            {
                pdfFactor += data.Adults[i];
            }
            float adjustedCDF = 0.0f;
            for ( int i = min; i < max; i++ )
            {
                adjustedCDF += data.Adults[i] / pdfFactor;
                if ( randNum < adjustedCDF )
                {
                    return i;
                }
            }
            return 0;
        }
    }
}