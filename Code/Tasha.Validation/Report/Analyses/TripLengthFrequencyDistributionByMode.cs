/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.IO;
using System.Threading.Tasks;
using Tasha.Common;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Write a CSV containing the trip length frequency distribution by mode groups.")]
public sealed class TripLengthFrequencyDistributionByMode : Analysis
{
    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RunParameter("Normalize Results", true, "Should the results be normalized (true) or raw counts (false)?")]
    public bool NormalizeResults;

    [RunParameter("Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode.")]
    public string ObservedModeAttachment;

    /// <summary>
    /// The number of time bins we will consider
    /// </summary>
    private const int TIME_BINS = 96;

    /// <summary>
    /// The number of minutes in each time bin.
    /// </summary>
    private const int TIME_BIN_SIZE = 15;

    [SubModelInformation(Required = true, Description = "The groups of modes to analyze.")]
    public ModeGroup[] ModeGroups;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("Mode,Duration(minutes),Observed,Model,Model-Observed");
        // There are 48 30-minute time intervals from 4:00 AM to 4:00 AM the next day
        for (int j = 0; j < ModeGroups.Length; j++)
        {
            var observed = GetObservedResults(surveyHouseholdsWithTrips, ModeGroups[j]);
            var model = GetModelResults(microsimData, ModeGroups[j]);
            for (int i = 0; i < TIME_BINS; i++)
            {
                writer.WriteLine($"{ModeGroups[j].Name},{i * TIME_BIN_SIZE},{observed[i]},{model[i]},{model[i] - observed[i]}");
            }
        }
    }

    /// <summary>
    /// Retrieves the observed results for trip length frequency distribution.
    /// </summary>
    /// <param name="surveyHouseholdsWithTrips">The array of households with trips.</param>
    /// <param name="group">The group of modes to analyze.</param>
    /// <returns>An array of floats representing the observed results.</returns>
    private float[] GetObservedResults(ITashaHousehold[] surveyHouseholdsWithTrips, ModeGroup group)
    {
        object lockObject = new();
        float[] ret = new float[TIME_BINS];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[TIME_BINS],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            if (group.Contains((trip[ObservedModeAttachment] as ITashaMode)))
                            {
                                var tripTime = trip.TripStartTime - trip.ActivityStartTime;
                                var tripBin = GetBin(tripTime);
                                local[tripBin] += expFactor;
                            }
                        }
                    }
                }
                return local;
            },
            (local) =>
            {
                lock (lockObject)
                {
                    VectorHelper.Add(ret, 0, ret, 0, local, 0, ret.Length);
                }
            });

        if (NormalizeResults)
        {
            // Normalize the resulting vector
            var reciprocal = 1.0f / VectorHelper.Sum(ret, 0, ret.Length);
            VectorHelper.Multiply(ret, 0, ret, 0, reciprocal, ret.Length);
        }
        return ret;
    }

    /// <summary>
    /// Retrieves the model results for trip length frequency distribution.
    /// </summary>
    /// <param name="microsimData">The microsimulation data.</param>
    /// <param name="group">The group of modes to analyze.</param>
    /// <returns>An array of floats representing the model results.</returns>
    private float[] GetModelResults(MicrosimData microsimData, ModeGroup group)
    {
        object lockObject = new();
        float[] ret = new float[TIME_BINS];
        Parallel.ForEach(microsimData.Households,
            () => new float[TIME_BINS],
            (household, _, local) =>
            {
                foreach (var person in microsimData.Persons[household.HouseholdID])
                {
                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    var expFactor = person.Weight / microsimData.ModeChoiceIterations;
                    foreach (var trip in trips)
                    {
                        foreach (var mode in microsimData.Modes[(household.HouseholdID, person.PersonID, trip.TripID)])
                        {
                            if (group.Contains(mode.Mode))
                            {
                                var tripTime = mode.DepartureTime - mode.ArrivalTime;
                                var tripBin = GetBin(tripTime);
                                local[tripBin] += expFactor;
                            }
                        }
                    }
                }
                return local;
            },
            (local) =>
            {
                lock (lockObject)
                {
                    VectorHelper.Add(ret, 0, ret, 0, local, 0, ret.Length);
                }
            });

        if (NormalizeResults)
        {
            // Normalize the resulting vector
            var reciprocal = 1.0f / VectorHelper.Sum(ret, 0, ret.Length);
            VectorHelper.Multiply(ret, 0, ret, 0, reciprocal, ret.Length);
        }
        return ret;
    }

    /// <summary>
    /// Get the bin for the given trip start time and trip time.
    /// </summary>
    /// <param name="tripStartTime">The start time of the trip</param>
    /// <param name="tripTime">The duration of the trip</param>
    /// <returns>The bin to assign the trip to.</returns>
    private static int GetBin(Time tripTime)
    {
        int timeBinOffset = SafeMod((int)tripTime.ToMinutes() / TIME_BIN_SIZE, TIME_BINS);
        return timeBinOffset;
    }

    /// <summary>
    /// Get the bin for the given trip start time and trip time.
    /// </summary>
    /// <param name="tripStartTime">The start time of the trip</param>
    /// <param name="tripTime">The duration of the trip</param>
    /// <returns>The bin to assign the trip to.</returns>
    private static int GetBin(float tripTime)
    {
        int timeBinOffset = SafeMod((int)tripTime / TIME_BIN_SIZE, TIME_BINS);
        return timeBinOffset;
    }

    /// <summary>
    /// Provides a modulo operation that works correctly for negative numbers.
    /// </summary>
    /// <param name="number">The numerator</param>
    /// <param name="divisor">The denominator.</param>
    /// <returns>The modulo of the number, if negative it wraps around back into the positive domain.</returns>
    private static int SafeMod(int numerator, int denominator)
    {
        return ((numerator %= denominator) < 0) ? numerator + denominator : numerator;
    }

}
