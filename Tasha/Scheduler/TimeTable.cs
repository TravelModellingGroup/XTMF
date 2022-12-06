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
using Tasha.Common;
using XTMF;
using System;

namespace Tasha.Scheduler
{
    /// <summary>
    /// Interface to Distributions
    /// </summary>
    internal static class TimeTable
    {
        #region person start times

        /// <summary>
        /// Gets a startTime based on the given person and activity, assumes frequency is 1
        /// StartTime is between start and end of day
        /// </summary>
        /// <param name="person">The person</param>
        /// <param name="activity">The activity</param>
        /// <param name="random"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        internal static bool GetStartTime(ITashaPerson person, Activity activity, Random random,
            int householdPD, int workPD, StartTimeAdjustment[] adjustments, out Time startTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(person, activity),
                                   1, 0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, adjustments, out startTime);
        }

        internal static bool GetStartTime(ITashaPerson person, Activity activity, int frequency, Random random,
            int householdPD, int workPD, StartTimeAdjustment[] adjustments, out Time startTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(person, activity),
                                    frequency, 0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, adjustments, out startTime);
        }

        /// <summary>
        /// Gets a startTime between the given startTime and endTime
        /// </summary>
        /// <param name="person"></param>
        /// <param name="activity"></param>
        /// <param name="frequency"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <param name="random"></param>
        /// <param name="returnTime"></param>
        /// <returns></returns>
        internal static bool GetStartTime(ITashaPerson person, Activity activity, int frequency, Time startTime, Time endTime,
            Random random, int householdPD, int workPD, StartTimeAdjustment[] adjustments, out Time returnTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(person, activity),
                                   frequency, Distribution.TimeOfDayToDistribution(startTime), Distribution.TimeOfDayToDistribution(endTime),
                                   random, householdPD, workPD, adjustments, out returnTime);
        }

        #endregion person start times

        #region household start times

        internal static bool GetStartTime(ITashaHousehold household, Activity activity, Random random,
            int householdPD, int workPD, StartTimeAdjustment[] adjustments, out Time startTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(household, activity),
                                    1, 0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, adjustments, out startTime);
        }

        internal static bool GetStartTime(ITashaHousehold household, Activity activity, int frequency, Random random,
            int householdPD, int workPD, StartTimeAdjustment[] adjustments, out Time startTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(household, activity),
                                    frequency, 0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, adjustments, out startTime);
        }

        internal static bool GetStartTime(ITashaHousehold household, Activity activity, int frequency,
            int householdPD, int workPD, Time startTime, Time endTime, Random random, StartTimeAdjustment[] adjustments, out Time ourStartTime)
        {
            return Distribution.GetRandomStartTimeFrequency(Distribution.GetDistributionID(household, activity),
                                    frequency, Distribution.TimeOfDayToDistribution(startTime), Distribution.TimeOfDayToDistribution(endTime),
                                    random, householdPD, workPD, adjustments, out ourStartTime);
        }

        #endregion household start times

        #region person durations

        /// <summary>
        /// Gets a duration from the given startTime based on a person and activity and startTime.
        /// The duration will be less than the length of the day minus the startTime
        /// </summary>
        /// <param name="person"></param>
        /// <param name="activity"></param>
        /// <param name="startTime"></param>
        /// <param name="random"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        internal static bool GetDuration(ITashaPerson person, Activity activity, Time startTime, Random random, out Time duration)
        {
            int length = Scheduler.StartTimeQuanta - 1 - Distribution.TimeOfDayToDistribution(startTime);

            return Distribution.GetRandomStartDurationTimeFrequency(Distribution.GetDistributionID(person, activity), startTime, 0, length, random, out duration);
        }

        /// <summary>
        /// Gets a duration that is not greater than the given max Duration
        /// </summary>
        /// <param name="person"></param>
        /// <param name="activity"></param>
        /// <param name="startTime"></param>
        /// <param name="maxDuration"></param>
        /// <param name="random"></param>
        /// <param name="returnStartTime"></param>
        /// <returns></returns>
        internal static bool GetDuration(ITashaPerson person, Activity activity, Time startTime, Time maxDuration, Random random, out Time returnStartTime)
        {
            return Distribution.GetRandomStartDurationTimeFrequency(Distribution.GetDistributionID(person, activity), startTime, 0, Distribution.DurationToDistribution(maxDuration), random, out returnStartTime);
        }

        #endregion person durations

        #region household durations

        internal static bool GetDuration(ITashaHousehold household, Activity activity, Time startTime, Random random, out Time duration)
        {
            int length = Scheduler.StartTimeQuanta - 1 - Distribution.TimeOfDayToDistribution(startTime);

            return Distribution.GetRandomStartDurationTimeFrequency(Distribution.GetDistributionID(household, activity), startTime, 0, length, random,
                out duration);
        }

        internal static bool GetDuration(ITashaHousehold household, Activity activity, Time startTime, Time maxDuration, Random random, out Time duration)
        {
            return Distribution.GetRandomStartDurationTimeFrequency(Distribution.GetDistributionID(household, activity), startTime, 0,
                Distribution.DurationToDistribution(maxDuration), random, out duration);
        }

        #endregion household durations

        #region person frequency

        public static int GetFrequency(ITashaPerson person, Activity activity, Random random, int householdPD, int workPD, GenerationAdjustment[] generationAdjustments)
        {
            int freq;
            var distID = Distribution.GetDistributionID(person, activity);
            if (distID < 0)
            {
                throw new XTMFRuntimeException(null, "We were unable to get the distribution ID number for a person doing a '" + activity
                    + "' episode The person's householdID was " + person.Household.HouseholdId + ", personID was " + person.Id + ", was " + person.Age +
                    " years old, with employment status '" + person.EmploymentStatus + "' occupation '" + person.Occupation + "' Student Status '"
                    + person.StudentStatus + "'.  Their work zone is '" + (person.EmploymentZone != null ? person.EmploymentZone.ZoneNumber.ToString() : "None")
                    + "' and their school zone is '"
                    + (person.SchoolZone != null ? person.SchoolZone.ZoneNumber.ToString() : "None") + "'.");
            }
            freq = Distribution.GetRandomFrequencyValue(
                0, Distribution.NumberOfFrequencies - 1, random, distID, householdPD, workPD, generationAdjustments);
            return freq;
        }

        public static int GetFrequency(ITashaPerson person, Activity activity, Random random, int maxFrequency,
            Time startTime, Time endTime, int householdPD, int workPD, GenerationAdjustment[] generationAdjustments,
            StartTimeAdjustment[] startTimeAdjustments)
        {
            bool feasibleFreq = false;
            int freq = 0;
            while (!feasibleFreq)
            {
                freq = Distribution.GetRandomFrequencyValue(
                    0, maxFrequency, random, Distribution.GetDistributionID(person, activity), householdPD, workPD, generationAdjustments);
                if (freq == 0)
                {
                    break;
                }
                if (!Distribution.GetRandomStartTimeFrequency(
                    Distribution.GetDistributionID(person, activity), freq,
                    Distribution.TimeOfDayToDistribution(startTime), Distribution.TimeOfDayToDistribution(endTime), random,
                    householdPD, workPD, startTimeAdjustments, out Time duration))
                {
                    //a bad thing happens here
                }
                else if (duration != Time.Zero)
                {
                    feasibleFreq = true;
                }
            }

            return freq;
        }

        #endregion person frequency

        #region household frequency

        public static int GetFrequency(ITashaHousehold household, Activity activity, Random random, int householdPD, int workPD,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            bool feasibleFreq = false;
            int freq = 0;
            while (!feasibleFreq)
            {
                freq = Distribution.GetRandomFrequencyValue(
                    0, Distribution.NumberOfFrequencies - 1, random, Distribution.GetDistributionID(household, activity),
                    householdPD, workPD, generationAdjustments);
                if (freq == 0)
                {
                    feasibleFreq = true;
                }
                if (!Distribution.GetRandomStartTimeFrequency(
                    Distribution.GetDistributionID(household, activity), freq,
                    0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, 
                    startTimeAdjustments, out Time startTime))
                {
                    //a bad thing happens here
                }
                else if (startTime != Time.StartOfDay)
                {
                    feasibleFreq = true;
                }
            }

            return freq;
        }

        public static int GetFrequency(ITashaHousehold household, Activity activity, Random random, int maxFreq, int householdPD, int workPD,
            GenerationAdjustment[] generationAdjustments, StartTimeAdjustment[] startTimeAdjustments)
        {
            bool feasibleFreq = false;
            int freq = maxFreq;
            while (!feasibleFreq)
            {
                freq = Distribution.GetRandomFrequencyValue(
                    0, Distribution.NumberOfFrequencies - 1, random, Distribution.GetDistributionID(household, activity),
                    householdPD, workPD, generationAdjustments);
                if (freq == 0)
                {
                    feasibleFreq = true;
                }
                if (!Distribution.GetRandomStartTimeFrequency(
                    Distribution.GetDistributionID(household, activity), freq,
                    0, Scheduler.StartTimeQuanta - 1, random, householdPD, workPD, startTimeAdjustments, out Time startTime))
                {
                    // a bad thing happens here
                }
                else if (startTime != Time.StartOfDay)
                {
                    feasibleFreq = true;
                }
            }

            return freq;
        }

        #endregion household frequency
    }
}