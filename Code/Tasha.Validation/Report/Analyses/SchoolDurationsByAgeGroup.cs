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

using Datastructure;
using System.IO;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Generates school start times by given age groups in a CSV File.")]
public sealed class SchoolDurationsTimeByAgeGroup : Analysis
{
    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [SubModelInformation(Required = true, Description = "The age groups to report on.")]
    public AgeRange[] AgeRanges;

    private const int HOURS = 24;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<IZone> _zones;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData,
        ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        _zones = Root.ZoneSystem.ZoneArray;
        float[] observed = GetObservedResults(surveyHouseholdsWithTrips);
        float[] model = GetModelResults(microsimData);
        using var writer = new StreamWriter(SaveTo);

        writer.WriteLine("AgeGroup,Hours,Observed,Model,Model-Observed");
        for (int i = 0; i < AgeRanges.Length; i++)
        {
            for (int j = 0; j < HOURS; j++)
            {
                var ageGroup = AgeRanges[i].Name;
                var hour = j;
                var observedValue = observed[i * HOURS + j];
                var modelValue = model[i * HOURS + j];
                writer.WriteLine($"{ageGroup},{hour},{observedValue},{modelValue},{modelValue - observedValue}");
            }
        }
    }

    private float[] GetModelResults(MicrosimData microsimData)
    {
        object lockObject = new();
        var ret = new float[AgeRanges.Length * HOURS];
        Parallel.ForEach(microsimData.Households,
            () => new float[AgeRanges.Length * HOURS],
            (household, _, local) =>
            {
                if(!microsimData.Persons.TryGetValue(household.HouseholdID, out var persons))
                {
                    return local;
                }
                foreach (var person in persons)
                {
                    var ageOffset = AgeRanges.GetIndex(person.Age) * HOURS;
                    if (ageOffset < 0
                    || !microsimData.Trips.TryGetValue((person.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    var expFactor = person.Weight / microsimData.ModeChoiceIterations;
                    for (int i = 0; i < trips.Count - 1; i++)
                    {
                        MicrosimTrip trip = trips[i];
                        if (trip.DestinationPurpose != "School"
                            || !microsimData.Modes.TryGetValue((trip.HouseholdID, trip.PersonID, trip.TripID), out var modes))
                        {
                            continue;
                        }
                        if(!microsimData.Modes.TryGetValue((trip.HouseholdID, trip.PersonID, trip.TripID + 1), out var nextModes))
                        {
                            continue;
                        }

                        // Compute the average next trip start time
                        float expectedNextStartTime = 0;
                        foreach(var mode in nextModes)
                        {
                            expectedNextStartTime += mode.DepartureTime;
                        }
                        expectedNextStartTime /= nextModes.Count;
                        var duration = modes[0].ArrivalTime - expectedNextStartTime;
                        var hourOffset = SafeMod((int)(duration / 60), HOURS);
                        local[ageOffset + hourOffset] += expFactor;
                    }
                }
                return local;
            },
            (local) =>
            {
                lock(lockObject)
                {
                    VectorHelper.Add(ret, 0, ret, 0, local, 0, ret.Length);
                }
            }
        );
        return ret;
    }

    private float[] GetObservedResults(ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        object lockObject = new();
        var ret = new float[AgeRanges.Length * HOURS];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[AgeRanges.Length * HOURS],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    var ageOffset = AgeRanges.GetIndex(person.Age) * HOURS;
                    if (ageOffset < 0)
                    {
                        continue;
                    }
                    var expansionFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        var trips = tripChain.Trips;
                        for (int i = 0; i < trips.Count - 1; i++)
                        {
                            if (trips[i].Purpose != Activity.School
                                || HasInvalidZone(trips[i]))
                            {
                                continue;
                            }
                            var duration = trips[i + 1].TripStartTime - trips[i].ActivityStartTime;
                            var hourOffset = SafeMod((int)(duration.ToMinutes() / 60), HOURS);
                            local[ageOffset + hourOffset] += expansionFactor;
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
            }
        );
        return ret;
    }

    private bool HasInvalidZone(ITrip trip)
    {
        var origin = trip.OriginalZone;
        var destination = trip.DestinationZone;
        return origin == null
            || destination == null
            || _zones.GetFlatIndex(origin.ZoneNumber) < 0
            || _zones.GetFlatIndex(destination.ZoneNumber) < 0;
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
