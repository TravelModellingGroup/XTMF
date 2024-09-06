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

[ModuleInformation(Description = "Writes a CSV containing the number of activity episodes for each time period.")]
public sealed class ActivityEpisodesByTimeOfDay : Analysis
{
    [RunParameter("Minimum Age", 11, "The minimum age of a person to compare against.")]
    public int MinimumAge;

    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<IZone> _zones;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        _zones = Root.ZoneSystem.ZoneArray;
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("TimePeriod,Observed,Model,Model-Observed");
        float[] model = GetModelResults(microsimData, timePeriods);
        float[] observed = GetObservedResults(surveyHouseholdsWithTrips, timePeriods);
        for (int i = 0; i < timePeriods.Length; i++)
        {
            var name = timePeriods[i].Name;
            writer.WriteLine($"{name},{observed[i]},{model[i]},{model[i] - observed[i]}");
        }
    }

    private float[] GetObservedResults(ITashaHousehold[] surveyHouseholdsWithTrips, TimePeriod[] timePeriods)
    {
        float[] ret = new float[timePeriods.Length];
        object lockObject = new();
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[timePeriods.Length],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            if (HasInvalidZone(trip))
                            {
                                continue;
                            }
                            int timePeriod = GetTimePeriod(trip.TripStartTime, timePeriods);
                            if (timePeriod >= 0)
                            {
                                local[timePeriod] += expFactor;
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

    private float[] GetModelResults(MicrosimData microsimData, TimePeriod[] timePeriods)
    {
        float[] ret = new float[timePeriods.Length];
        object lockObject = new();
        Parallel.ForEach(microsimData.Households,
            () => new float[timePeriods.Length],
            (household, _, local) =>
            {
                var persons = microsimData.Persons[household.HouseholdID];
                foreach (var person in persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    var expFactor = person.Weight;
                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    foreach (var trip in trips)
                    {
                        var modes = microsimData.Modes[(household.HouseholdID, person.PersonID, trip.TripID)];
                        foreach (var mode in modes)
                        {
                            int timePeriod = GetTimePeriod(mode.DepartureTime, timePeriods);
                            if (timePeriod >= 0)
                            {
                                local[timePeriod] += expFactor / modes.Count;
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

        return ret;
    }

    /// <summary>
    /// Get the time period for the given departure time, -1 if not found.
    /// </summary>
    /// <param name="tripStartTime">The time the trip will leave at.</param>
    /// <param name="timePeriods">The time periods we need to test.</param>
    /// <returns>The index of the time period that the departure time falls into, -1 of none.</returns>
    private static int GetTimePeriod(Time tripStartTime, TimePeriod[] timePeriods)
    {
        for (int i = 0; i < timePeriods.Length; i++)
        {
            if (timePeriods[i].Contains(tripStartTime))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Get the time period for the given departure time, -1 if not found.
    /// </summary>
    /// <param name="departureTime">The time the trip will leave at.</param>
    /// <param name="timePeriods">The time periods we need to test.</param>
    /// <returns>The index of the time period that the departure time falls into, -1 of none.</returns>
    private static int GetTimePeriod(float departureTime, TimePeriod[] timePeriods)
    {
        for (int i = 0; i < timePeriods.Length; i++)
        {
            if (timePeriods[i].Contains(departureTime))
            {
                return i;
            }
        }
        return -1;
    }

}
