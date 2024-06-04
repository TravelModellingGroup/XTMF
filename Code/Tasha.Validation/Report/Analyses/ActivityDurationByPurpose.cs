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
using System.Linq;
using System.Threading.Tasks;
using Tasha.Common;
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

    private static readonly (string Name, string[] Purposes)[] s_PurposeBundlesModel =
    [
        // We don't compute the duration for home activities
        //("Home", ["Home", "ReturnHomeFromWork"]),
        ("Work", ["PrimaryWork", "SecondaryWork", "WorkBasedBusiness", "WorkAAtHomeBusiness" ]),
        ("School", ["School"]),
        ("Other", ["IndividualOther", "JointOther"]),
        ("Market", ["Market", "JointOther"])
    ];

    private static readonly (string Name, Activity[] Purposes)[] s_PurposeBundlesObserved =
    [
        // We don't compute the duration for home activities
        //("Home", [Activity.Home, Activity.ReturnFromWork]),
        ("Work", [Activity.PrimaryWork, Activity.SecondaryWork, Activity.WorkBasedBusiness, Activity.WorkAtHomeBusiness]),
        ("School", [Activity.School]),
        ("Other", [Activity.IndividualOther, Activity.JointOther]),
        ("Market", [Activity.Market, Activity.JointMarket])
    ];

    [RunParameter("Minimum Age", 11, "The minimum age of a person to compare against.")]
    public int MinimumAge;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        using var streamWriter = new StreamWriter(SaveTo);
        streamWriter.WriteLine("Duration,Purpose,Observed,Model,Model-Observed");

        static string GetMinutes(int i) =>
            (i & 0x3) switch
            {
                0 => "00",
                1 => "15",
                2 => "30",
                3 => "45",
                _ => "00"
            };

        for (int j = 0; j < s_PurposeBundlesModel.Length; j++)
        {
            for (int i = 1; i < 96; i++)
            {
                var time = $"{i >> 2}:{GetMinutes(i)}";
                var observed = ComputeObserved(surveyHouseholdsWithTrips, i, s_PurposeBundlesObserved[j].Purposes);
                var model = ComputeModel(microsimData, i * 15.0f, s_PurposeBundlesModel[j].Purposes);
                streamWriter.WriteLine($"{time},{s_PurposeBundlesModel[j].Name},{observed},{model},{model - observed}");
            }
        }
    }

    private float ComputeObserved(ITashaHousehold[] surveyHouseholdsWithTrips, int duration, Activity[] purposes)
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

    private float ComputeModel(MicrosimData microsimData, float durationInMinutes, string[] purposes)
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
