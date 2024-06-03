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

[ModuleInformation(Description = "Produces a CSV containing the number of activities by type binned in 30-minute increments. The CSV is in the format Time,Purpose,Observed,")]
public class ActivityStartTimesByPurpose : Analysis
{
    /// <summary>
    /// The location to save the report to.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    private static readonly (string Name, string[] Purposes)[] s_PurposeBundlesModel =
    [
        // We don't compute the duration for home activities
        ("Home", ["Home", "ReturnHomeFromWork"]),
        ("Work", ["PrimaryWork", "SecondaryWork", "WorkBasedBusiness", ]),
        ("School", ["School"]),
        ("Other", ["IndividualOther", "JointOther"]),
        ("Market", ["Market", "JointOther"])
    ];

    private static readonly (string Name, Activity[] Purposes)[] s_PurposeBundlesObserved =
    [
        // We don't compute the duration for home activities
        ("Home", [Activity.Home, Activity.ReturnFromWork]),
        ("Work", [Activity.PrimaryWork, Activity.SecondaryWork, Activity.WorkAtHomeBusiness]),
        ("School", [Activity.School]),
        ("Other", [Activity.IndividualOther, Activity.JointOther]),
        ("Market", [Activity.Market, Activity.JointMarket])
    ];

    /// <summary>
    /// Executes the analysis to produce a CSV containing the number of activities by type binned in 30-minute increments.
    /// </summary>
    /// <param name="timePeriods">The time periods to analyze.</param>
    /// <param name="microsimData">The microsimulation data.</param>
    /// <param name="surveyHouseholdsWithTrips">The survey households with trips.</param>
    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        using var streamWriter = new StreamWriter(SaveTo);
        streamWriter.WriteLine("Time,Purpose,Observed,Model,Model-Observed");
        // There are 48 30-minute time intervals from 4:00 AM to 4:00 AM the next day
        for (int j = 0; j < s_PurposeBundlesModel.Length; j++)
        {
            for (int i = 0; i < 48; i++)
            {
                var time = $"{(i + 8) >> 1}:{((i & 1) == 0 ? "00" : "30")}";
                var observed = ComputeObserved(surveyHouseholdsWithTrips, i, s_PurposeBundlesObserved[j].Purposes);
                var model = ComputeModel(microsimData, i, s_PurposeBundlesModel[j].Purposes);
                streamWriter.WriteLine($"{time},{s_PurposeBundlesModel[j].Name},{observed},{model},{model - observed}");
            }
        }

    }

    /// <summary>
    /// Computes the observed number of activities for the given start time interval and purposes.
    /// </summary>
    /// <param name="surveyHouseholdsWithTrips">The survey households with trips.</param>
    /// <param name="startTimeInterval">The start time interval.</param>
    /// <param name="purposes">The purposes to consider.</param>
    /// <returns>The observed number of activities.</returns>
    private static float ComputeObserved(ITashaHousehold[] surveyHouseholdsWithTrips, int startTimeInterval, Activity[] purposes)
    {
        // Offset to start at 4:00 AM and run until 28:00
        startTimeInterval += 8;

        double accumulated = 0;
        object lockObject = new();
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => 0.0,
            (ITashaHousehold household, ParallelLoopState _, double currentAccumulated) =>
            {
                var localAccumulated = 0.0;
                foreach (var person in household.Persons)
                {
                    int count = 0;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            if (trip.ActivityStartTime.ToMinutes() >= startTimeInterval * 30
                                && trip.ActivityStartTime.ToMinutes() < (startTimeInterval + 1) * 30
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

    /// <summary>
    /// Computes the model number of activities for the given start time interval and purposes.
    /// </summary>
    /// <param name="microsimData">The microsimulation data.</param>
    /// <param name="startTimeInterval">The start time interval.</param>
    /// <param name="purposes">The purposes to consider.</param>
    /// <returns>The model number of activities.</returns>
    private static float ComputeModel(MicrosimData microsimData, int startTimeInterval, string[] purposes)
    {
        // Offset to start at 4:00 AM and run until 28:00
        startTimeInterval += 8;

        double accumulated = microsimData.Households.AsParallel()
            .Sum(household =>
            {
                double localAccumulated = 0.0;

                foreach (var person in microsimData.Persons[household.HouseholdID])
                {
                    int count = 0;

                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    foreach (var trip in trips)
                    {
                        // If this trip is not something we are looking to include we can skip
                        if (!purposes.Contains(trip.DestinationPurpose))
                        {
                            continue;
                        }

                        foreach (var mode in microsimData.Modes[(household.HouseholdID, person.PersonID, trip.TripID)])
                        {
                            // Times are in minutes from midnight.
                            if (mode.ArrivalTime >= startTimeInterval * 30
                                && mode.ArrivalTime < (startTimeInterval + 1) * 30)
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
