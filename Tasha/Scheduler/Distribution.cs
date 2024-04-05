/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using Tasha.Common;
using XTMF;

namespace Tasha.Scheduler
{
    /// <summary>
    /// This class provides access to the distributions
    /// </summary>
    /// <remarks>
    /// I am sorry about the length of this class.
    /// It was required to maintain the abstraction that works very well for mode-choice.
    /// The other option I considered was to include this embedded in the person class.
    /// If I went with that, a person could rewrite the distribution code, but then
    /// everyone would have to write their own distribution code, instead of just
    /// providing new values.
    /// </remarks>
    internal static partial class Distribution
    {
        public static SparseArray<AdultDistributionInformation> AdultDistributions;

        public static SparseArray<DistributionInformation> Distributions;

        public static int NumAdultFrequencies;

        public static int NumberOfFrequencies;

        internal static int GetNumAdultsJointEpisode(ITashaHousehold household, Random random, Activity activity)
        {
            int numEpisodeAdults;
            if (household.NumberOfAdults == 1)
            {
                numEpisodeAdults = 1;
            }
            else
            {
                if (household.NumberOfAdults == 2 && household.NumberOfChildren == 0)
                    numEpisodeAdults = 2;
                else
                    numEpisodeAdults = GetRandomNumberAdults(household, activity, 0, household.NumberOfAdults, random);
            }
            return numEpisodeAdults;
        }

        /// <summary>
        /// Do a frequency random assignment
        /// </summary>
        /// <param name="min">The smallest possible value (>= 0)</param>
        /// <param name="max">The largest possible value (less than or equal to 10)</param>
        /// <param name="random"></param>
        /// <param name="distributionID">Which distro to look for</param>
        /// <param name="workPD"></param>
        /// <param name="generationAdjustments"></param>
        /// <param name="householdPD"></param>
        /// <returns>[min,max] the value selected</returns>
        internal static int GetRandomFrequencyValue(int min, int max, Random random, int distributionID, int householdPD, int workPD,
            GenerationAdjustment[] generationAdjustments)
        {
            if (min == max) return min;
            float rand = (float)random.NextDouble();
            float cdf = 0;
            float total = 0;
            DistributionInformation pdf = Distributions[distributionID];
            float adjustment = GetGenerationAdjustment(generationAdjustments, distributionID, householdPD, workPD);
            if (pdf == null || pdf.Frequency == null)
            {
                throw new XTMFRuntimeException(null, "Unable to load PDF #" + distributionID + " from the Distribution Frequency File!");
            }
            // to start with just add
            for (int i = min; i <= max; i++)
            {
                total += i == 0 ? pdf.Frequency[i] : pdf.Frequency[i] * adjustment;
            }
            rand = rand * total;
            for (int i = min; i <= max; i++)
            {
                // we can just multiply now, faster than division
                cdf += i == 0 ? pdf.Frequency[i] : pdf.Frequency[i] * adjustment;
                if (rand < cdf)
                {
                    return i;
                }
            }
            return 0;
        }

        private static float GetGenerationAdjustment(GenerationAdjustment[] generationAdjustments, int distributionID, int householdPD, int workPD)
        {
            for (int i = 0; i < generationAdjustments.Length; i++)
            {
                if (generationAdjustments[i].DistributionIDs.Contains(distributionID))
                {
                    if (generationAdjustments[i].PlanningDistricts.Contains(householdPD))
                    {
                        if (generationAdjustments[i].WorkPlanningDistrict.Contains(workPD))
                        {
                            return generationAdjustments[i].Factor;
                        }
                    }
                }
            }
            return 1.0f;
        }

        /// <summary>
        /// Needs to be called by every thread that wants to use distributions
        /// </summary>
        internal static void InitializeDistributions()
        {
            NumberOfFrequencies = Scheduler.MaxFrequency + 1;
            int startTimeQuantums = Scheduler.StartTimeQuanta;

            NumAdultFrequencies = Scheduler.NumberOfAdultFrequencies;

            AdultDistributions = new ZoneCache<AdultDistributionInformation>(Scheduler.AdultDistributionsFile,
                LoadAdultDistributionData).StoreAll();

            Distributions = new ZoneCache<DistributionInformation>(Scheduler.FrequencyDistributionsFile,
                delegate (int number, float[] data)
                {
                    DistributionInformation f = new();

                    int current = 0;
                    f.Frequency = new float[NumberOfFrequencies];
                    Array.Copy(data, 0, f.Frequency, 0, NumberOfFrequencies);
                    current += NumberOfFrequencies;
                    f.Durations = new float[startTimeQuantums][];

                    for (int i = 0; i < startTimeQuantums; i++)
                    {
                        f.Durations[i] = new float[startTimeQuantums + 1];
                    }

                    for (int time = 0; time < startTimeQuantums; time++)
                    {
                        for (int dur = 0; dur < startTimeQuantums + 1; dur++)
                        {
                            f.Durations[time][dur] = data[current++];
                        }
                    }
                    f.StartTimeFrequency = new float[startTimeQuantums][];
                    for (int i = 0; i < startTimeQuantums; i++)
                    {
                        f.StartTimeFrequency[i] = new float[NumberOfFrequencies];
                    }

                    for (int fre = 0; fre < NumberOfFrequencies; fre++)
                    {
                        for (int time = 0; time < startTimeQuantums; time++)
                        {
                            f.StartTimeFrequency[time][fre] = data[current++];
                        }
                    }
                    return f;
                }).StoreAll();
        }

        internal static void PrimaryWorkStartTimeAndDuration(ITashaPerson person, out Time startTime, out Time duration)
        {
            throw new NotImplementedException();
        }

        private static AdultDistributionInformation LoadAdultDistributionData(int n, float[] data)
        {
            AdultDistributionInformation adultDistributionInformation = new();
            adultDistributionInformation.Adults = new float[NumAdultFrequencies];
            // now we only load the frequency since we calculate the pdf anyways
            for (int i = 0; i < data.Length; i++)
            {
                adultDistributionInformation.Adults[i] = data[i];
            }

            return adultDistributionInformation;
        }

        public class AdultDistributionInformation
        {
            internal float[] Adults;
        }

        public class DistributionInformation
        {
            internal float[][] Durations;

            /// <summary>
            /// Data[0:10]
            /// </summary>
            internal float[] Frequency;

            internal float[][] StartTimeFrequency;
        }
    }
}