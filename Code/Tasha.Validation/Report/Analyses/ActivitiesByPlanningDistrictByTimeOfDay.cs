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
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Write a CSV with the number of activities by the time of day and by planning district." +
    " The resulting CSV has the following columns (TimeOfDay, PD, Work, School, Other, Market).")]
public sealed class ActivitiesByPlanningDistrictByTimeOfDay : Analysis
{

    private static (string Name, string[] Purposes)[] s_PurposeBundlesModel =
    [
        // We don't compute the duration for home activities
        ("Home", ["Home", "ReturnHomeFromWork"]),
        ("Work", ["PrimaryWork", "SecondaryWork", "WorkBasedBusiness", "WorkAAtHomeBusiness" ]),
        ("School", ["School"]),
        ("Other", ["IndividualOther", "JointOther"]),
        ("Market", ["Market", "JointOther"])
    ];

    private static (string Name, Activity[] Purposes)[] s_PurposeBundlesObserved =
    [
        // We don't compute the duration for home activities
        ("Home", [Activity.Home, Activity.ReturnFromWork]),
        ("Work", [Activity.PrimaryWork, Activity.SecondaryWork, Activity.WorkBasedBusiness, Activity.WorkAtHomeBusiness]),
        ("School", [Activity.School]),
        ("Other", [Activity.IndividualOther, Activity.JointOther]),
        ("Market", [Activity.Market, Activity.JointMarket])
    ];

    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter("Minimum Age", 11, "The minimum age of a person to compare against.")]
    public int MinimumAge;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        // Process the zone system to get the planning districts and create indexes for them.
        var zones = Root.ZoneSystem.ZoneArray;
        var zoneToPD = zones.GetFlatData().Select(z => z.PlanningDistrict).ToArray();
        var pds = zoneToPD.Distinct().Order().ToArray();
        // Change the zoneToPD from the sparse PD to the index of the PD
        for (var i = 0; i < zoneToPD.Length; i++)
        {
            zoneToPD[i] = Array.IndexOf(pds, zoneToPD[i]);
        }

        float[] observed = GetObservedResult(surveyHouseholdsWithTrips, timePeriods, zoneToPD, pds.Length, zones);
        float[] model = GetModelResults(microsimData, timePeriods, zoneToPD, pds.Length, zones);

        using var writer = new StreamWriter(SaveTo);
        // Emit the header
        writer.Write("TimeOfDay,PD");
        for (var i = 0; i < s_PurposeBundlesModel.Length; i++)
        {
            writer.Write($",Observed{s_PurposeBundlesModel[i].Name}");
        }
        for (var i = 0; i < s_PurposeBundlesModel.Length; i++)
        {
            writer.Write($",Modelled{s_PurposeBundlesModel[i].Name}");
        }
        for (var i = 0; i < s_PurposeBundlesModel.Length; i++)
        {
            writer.Write($",Modeled-Observed{s_PurposeBundlesModel[i].Name}");
        }
        writer.WriteLine();

        for (var i = 0; i < timePeriods.Length; i++)
        {
            var timePeriod = timePeriods[i];
            for (var j = 0; j < pds.Length; j++)
            {
                writer.Write($"{timePeriod.Name},{pds[j]}");
                for (var k = 0; k < s_PurposeBundlesModel.Length; k++)
                {
                    writer.Write($",{observed[(i * s_PurposeBundlesModel.Length * pds.Length) + (k * pds.Length) + j]}");
                }
                for (var k = 0; k < s_PurposeBundlesModel.Length; k++)
                {
                    writer.Write($",{model[(i * s_PurposeBundlesModel.Length * pds.Length) + (k * pds.Length) + j]}");
                }
                for (var k = 0; k < s_PurposeBundlesModel.Length; k++)
                {
                    writer.Write($",{model[(i * s_PurposeBundlesModel.Length * pds.Length) + (k * pds.Length) + j] 
                        - observed[(i * s_PurposeBundlesModel.Length * pds.Length) + (k * pds.Length) + j]}");
                }
                writer.WriteLine();
            }
        }
    }

    private float[] GetObservedResult(ITashaHousehold[] surveyHouseholdsWithTrips, TimePeriod[] timePeriods, int[] zoneToPD, int pds, SparseArray<IZone> zones)
    {
        object lockObject = new();
        var size = timePeriods.Length * s_PurposeBundlesObserved.Length * pds;
        var ret = new float[size];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[size],
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
                            var pdIndex = zoneToPD[zones.GetFlatIndex(trip.DestinationZone.ZoneNumber)];
                            var purposeIndex = GetPurposeIndex(trip.Purpose, s_PurposeBundlesObserved);
                            if (purposeIndex < 0)
                            {
                                continue;
                            }
                            int timePeriod = GetTimePeriodIndex(trip.TripStartTime, timePeriods);
                            if (timePeriod >= 0)
                            {
                                local[(timePeriod * s_PurposeBundlesModel.Length * pds)
                                    + (purposeIndex * pds)
                                    + pdIndex] += expFactor;
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
                    VectorHelper.Add(ret, 0, ret, 0, local, 0, local.Length);
                }
            });
        return ret;
    }

    private float[] GetModelResults(MicrosimData microsimData, TimePeriod[] timePeriods, int[] zoneToPD, int pds, SparseArray<IZone> zones)
    {
        object lockObject = new();
        var size = timePeriods.Length * s_PurposeBundlesModel.Length * pds;
        var ret = new float[size];
        Parallel.ForEach(microsimData.Households,
            () => new float[size],
            (household, _, local) =>
            {
                var persons = microsimData.Persons[household.HouseholdID];
                foreach (var person in persons)
                {
                    if (person.Age < MinimumAge ||
                    !microsimData.Trips.TryGetValue((person.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    // Lift the normalization of the mode choice iterations out of the inner loops.
                    var expFactor = person.Weight / microsimData.ModeChoiceIterations;
                    foreach (var trip in trips)
                    {
                        var pdIndex = zoneToPD[zones.GetFlatIndex(trip.DestinationZone)];
                        var purposeIndex = GetPurposeIndex(trip.DestinationPurpose, s_PurposeBundlesModel);
                        if (purposeIndex < 0)
                        {
                            continue;
                        }
                        var modes = microsimData.Modes[(trip.HouseholdID, trip.PersonID, trip.TripID)];
                        foreach (var mode in modes)
                        {
                            int timePeriod = GetTimePeriodIndex(mode.DepartureTime, timePeriods);
                            if (timePeriod >= 0)
                            {
                                local[(timePeriod * s_PurposeBundlesModel.Length * pds)
                                    + (purposeIndex * pds)
                                    + pdIndex] += expFactor * mode.Weight;
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
                    VectorHelper.Add(ret, 0, ret, 0, local, 0, local.Length);
                }
            });

        return ret;
    }

    private static int GetTimePeriodIndex(float departureTime, TimePeriod[] timePeriods)
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

    private static int GetTimePeriodIndex(Time tripStartTime, TimePeriod[] timePeriods)
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

    private static int GetPurposeIndex(string destinationPurpose, (string Name, string[] Purposes)[] purposeGroup)
    {
        for (int i = 0; i < purposeGroup.Length; i++)
        {
            for (int j = 0; j < purposeGroup[i].Purposes.Length; j++)
            {
                if (destinationPurpose == purposeGroup[i].Purposes[j])
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static int GetPurposeIndex(Activity purpose, (string Name, Activity[] Purposes)[] purposeGroup)
    {
        for (int i = 0; i < purposeGroup.Length; i++)
        {
            for (int j = 0; j < purposeGroup[i].Purposes.Length; j++)
            {
                if (purpose == purposeGroup[i].Purposes[j])
                {
                    return i;
                }
            }
        }
        return -1;
    }

}
