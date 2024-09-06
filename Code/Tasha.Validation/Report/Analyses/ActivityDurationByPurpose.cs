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
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Generate a histogram of activity types by 15 minute periods.")]
public sealed class ActivityDurationByPurpose : Analysis
{
    /// <summary>
    /// The location to save the report to.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [SubModelInformation(Required = true, Description = "The groups of purposes to analyze.")]
    public ActivityGroup[] PurposeGroup;

    [RunParameter("Minimum Age", 11, "The minimum age of a person to compare against.")]
    public int MinimumAge;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<IZone> _zones;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        _zones = Root.ZoneSystem.ZoneArray;
        using var streamWriter = new StreamWriter(SaveTo);
        streamWriter.WriteLine("Duration(Minutes),Purpose,Observed,Model,Model-Observed");

        for (int j = 0; j < PurposeGroup.Length; j++)
        {
            for (int i = 1; i < 96; i++)
            {
                var time = i * 15;
                var observed = ComputeObserved(surveyHouseholdsWithTrips, i, PurposeGroup[j]);
                var model = ComputeModel(microsimData, i * 15.0f, PurposeGroup[j]);
                streamWriter.WriteLine($"{time},{PurposeGroup[j].Name},{observed},{model},{model - observed}");
            }
        }
    }

    private float ComputeObserved(ITashaHousehold[] surveyHouseholdsWithTrips, int duration, ActivityGroup purposes)
    {
        double accumulated = 0;
        object lockObject = new();
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => 0.0,
            (ITashaHousehold household, ParallelLoopState _, double currentAccumulated) =>
            {
                var localAccumulated = 0.0;
                foreach (var person in household.Persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    int count = 0;
                    foreach (var tripChain in person.TripChains)
                    {
                        var trips = tripChain.Trips;
                        for (int i = 0; i < trips.Count - 1; i++)
                        {
                            var trip = trips[i];
                            if (HasInvalidZone(trip))
                            {
                                continue;
                            }
                            var activityDuration = (trips[i + 1].TripStartTime - trip.ActivityStartTime).ToMinutes();
                            if (activityDuration >= duration * 15
                                && activityDuration < (duration + 1) * 15
                                && purposes.Contains(trip.Purpose))
                            {
                                count++;
                            }
                        }
                    }
                    localAccumulated += person.ExpansionFactor * count;
                }
                // combine what we found with what this thread already had.
                return currentAccumulated + localAccumulated;
            },
            localAccumulated => { lock (lockObject) { accumulated += localAccumulated; } }
        );
        return (float)accumulated;
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

    private float ComputeModel(MicrosimData microsimData, float durationInMinutes, ActivityGroup purposes)
    {
        double accumulated = microsimData.Households.AsParallel()
            .Sum(household =>
            {
                double localAccumulated = 0.0;

                foreach (var person in microsimData.Persons[household.HouseholdID])
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    int count = 0;

                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    for (int i = 0; i < trips.Count - 1; i++)
                    {
                        // If this trip is not something we are looking to include we can skip
                        if (!purposes.Contains(trips[i].DestinationPurpose))
                        {
                            continue;
                        }
                        // No home activities where be processed.
                        var currentModes = microsimData.Modes[(household.HouseholdID, person.PersonID, trips[i].TripID)];
                        // All modes will have the same activity start time unless going back home
                        var startTime = currentModes[0].ArrivalTime;
                        var nextModes = microsimData.Modes[(household.HouseholdID, person.PersonID, trips[i].TripID + 1)];
                        // scan the next modes to gather the weighted duration.
                        foreach (var mode in nextModes)
                        {
                            var activityDuration = (mode.DepartureTime - startTime);
                            // Times are in minutes from midnight.
                            if (activityDuration >= durationInMinutes
                                && activityDuration < (durationInMinutes + 15))
                            {
                                count += mode.Weight;
                            }
                        }
                    }
                    localAccumulated += person.Weight * count;
                }

                return localAccumulated;
            });

        return (float)accumulated / microsimData.ModeChoiceIterations;
    }

}
