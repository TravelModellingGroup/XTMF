﻿/*
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

[ModuleInformation(Description = "Creates a CSV file containing a trip length frequency distribution of all trips by hour.")]
public sealed class TripLengthFrequencyDistributionByHour : Analysis
{

    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RunParameter("Normalize Results", true, "Should the results be normalized (true) or raw counts (false)?")]
    public bool NormalizeResults;

    /// <summary>
    /// The number of hours in a day.
    /// </summary>
    private const int HOURS = 24;

    /// <summary>
    /// The number of time bins we will consider
    /// </summary>
    private const int TIME_BINS = 96;

    /// <summary>
    /// The number of minutes in each time bin.
    /// </summary>
    private const int TIME_BIN_SIZE = 15;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        float[] model = GetModelResults(microsimData);
        float[] observed = GetObservedResults(surveyHouseholdsWithTrips);
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("Hour,Duration(minutes),Observed,Model,Model-Observed");
        for (int i = 0; i < HOURS; i++)
        {
            for (int j = 0; j < TIME_BINS; j++)
            {
                writer.WriteLine($"{i},{j * TIME_BIN_SIZE},{observed[i]},{model[i]},{model[i] - observed[i]}");
            }
        }
    }

    /// <summary>
    /// Retrieves the observed results for trip length frequency distribution.
    /// </summary>
    /// <param name="surveyHouseholdsWithTrips">The array of households with trips.</param>
    /// <returns>An array of floats representing the observed results.</returns>
    private float[] GetObservedResults(ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        object lockObject = new object();
        float[] ret = new float[HOURS * TIME_BINS];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[HOURS * TIME_BINS],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            var tripTime = trip.TripStartTime - trip.ActivityStartTime;
                            var tripBin = GetBin(trip.TripStartTime, tripTime);
                            local[tripBin] += expFactor;
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
            var reciprical = 1.0f / VectorHelper.Sum(ret, 0, ret.Length);
            VectorHelper.Multiply(ret, 0, ret, 0, reciprical, ret.Length);
        }
        return ret;
    }

    /// <summary>
    /// Retrieves the model results for trip length frequency distribution.
    /// </summary>
    /// <param name="microsimData">The microsimulation data.</param>
    /// <returns>An array of floats representing the model results.</returns>
    private float[] GetModelResults(MicrosimData microsimData)
    {
        object lockObject = new object();
        float[] ret = new float[HOURS * TIME_BINS];
        Parallel.ForEach(microsimData.Households,
            () => new float[HOURS * TIME_BINS],
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
                            var tripTime = mode.DepartureTime - mode.ArrivalTime;
                            var tripBin = GetBin(mode.DepartureTime, tripTime);
                            local[tripBin] += expFactor;
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
            var reciprical = 1.0f / VectorHelper.Sum(ret, 0, ret.Length);
            VectorHelper.Multiply(ret, 0, ret, 0, reciprical, ret.Length);
        }
        return ret;
    }

    /// <summary>
    /// Get the bin for the given trip start time and trip time.
    /// </summary>
    /// <param name="tripStartTime">The start time of the trip</param>
    /// <param name="tripTime">The duration of the trip</param>
    /// <returns>The bin to assign the trip to.</returns>
    private static int GetBin(Time tripStartTime, Time tripTime)
    {
        int hourOffset = SafeMod(tripStartTime.Hours, HOURS);
        int timeBinOffset = SafeMod((int)tripTime.ToMinutes() / TIME_BIN_SIZE, TIME_BINS);
        return hourOffset * TIME_BINS + timeBinOffset;
    }

    /// <summary>
    /// Get the bin for the given trip start time and trip time.
    /// </summary>
    /// <param name="tripStartTime">The start time of the trip</param>
    /// <param name="tripTime">The duration of the trip</param>
    /// <returns>The bin to assign the trip to.</returns>
    private static int GetBin(float tripStartTime, float tripTime)
    {
        int hourOffset = SafeMod((int)tripStartTime / 60, HOURS);
        int timeBinOffset = SafeMod((int)tripTime / TIME_BIN_SIZE, TIME_BINS);
        return hourOffset * TIME_BINS + timeBinOffset;
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
