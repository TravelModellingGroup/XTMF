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

using System;
using System.IO;
using System.Threading.Tasks;
using Tasha.Common;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Write a CSV containing the tour sizes of the observed and modelled data.")]
public sealed class TourLengthDistribution : Analysis
{

    [RunParameter("Normalize Results", true, "Should the results be normalized (true) or raw counts (false)?")]
    public bool NormalizeResults;

    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RunParameter("Max Tour Length", 10, "The maximum tour length to report.")]
    public int MaxTourLength;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("TourLength,Observed,Model,Model-Observed");
        var observedTours = LoadObservedTours(surveyHouseholdsWithTrips);
        var modelTours = LoadModelledTours(microsimData);
        for (int i = 0; i < observedTours.Length + 1; i++)
        {
            writer.WriteLine($"{i + 1},{observedTours[i]},{modelTours[i]},{modelTours[i] - observedTours[i]}");
        }
    }

    private float[] LoadObservedTours(ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        float[] ret = new float[MaxTourLength + 1];
        object lockObject = new object();
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[MaxTourLength + 1],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        local[Math.Min(tripChain.Trips.Count, MaxTourLength)] += expFactor;
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

    private float[] LoadModelledTours(MicrosimData microsimData)
    {
        object lockObject = new();
        float[] ret = new float[MaxTourLength + 1];
        Parallel.ForEach(microsimData.Households,
            () => new float[MaxTourLength + 1],
            (household, _, local) =>
            {
                foreach (var person in microsimData.Persons[household.HouseholdID])
                {
                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    var expFactor = person.Weight / microsimData.ModeChoiceIterations;
                    int count = 0;
                    foreach (var trip in trips)
                    {
                        if (IsGoingHomeActivity(trip))
                        {
                            local[Math.Min(count, MaxTourLength)] += expFactor;
                            count = 0;
                        }
                        else
                        {
                            count++;
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
    /// All of the purposes that have the agent returning home.
    /// </summary>
    private static readonly string[] _homePurposes = ["Home", "ReturnHomeFromWork", "ReturnHomeFromSchool"];

    /// <summary>
    /// Test to see if the purposes of the trip is going home.
    /// </summary>
    /// <param name="trip">The trip to check.</param>
    /// <returns>True if at the end of the trip the person has returned home.</returns>
    private static bool IsGoingHomeActivity(MicrosimTrip trip)
    {
        return Array.IndexOf(_homePurposes, trip.DestinationPurpose) != -1;
    }

    public override bool RuntimeValidation(ref string error)
    {
        if (MaxTourLength < 1)
        {
            error = "Max Tour Length must be greater than 0.";
            return false;
        }
        return true;
    }

}
