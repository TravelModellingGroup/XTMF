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

[ModuleInformation(Description = "Write a CSV containing the mode share by PD leaving a given origin.")]
public sealed class ModesByOriginByTimeOfDay : Analysis
{

    [SubModelInformation(Required = true, Description = "The location to save the report to.")]
    public FileLocation SaveTo;

    [RunParameter("Normalize Results", true, "Should the results be normalized (true) or raw counts (false)?")]
    public bool NormalizeResults;

    [RunParameter("Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode.")]
    public string ObservedModeAttachment;

    public ModeGroup[] ModeGroups;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter("Minimum Age", 11, "The minimum age of a person to compare against.")]
    public int MinimumAge;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        // Process the zone system to get the planning districts and create indexes for them.
        var zones = Root.ZoneSystem.ZoneArray;
        int[] zoneToPD = zones.GetFlatData().Select(z => z.PlanningDistrict).ToArray();
        var pds = zoneToPD.Distinct().Order().ToArray();
        // Change the zoneToPD from the sparse PD to the index of the PD
        for (var i = 0; i < zoneToPD.Length; i++)
        {
            zoneToPD[i] = Array.IndexOf(pds, zoneToPD[i]);
        }
        using var writer = new StreamWriter(SaveTo);
        float[] observed = GetObserved(surveyHouseholdsWithTrips, timePeriods, pds, zoneToPD, zones);
        float[] model = GetModel(microsimData, timePeriods, pds, zoneToPD, zones);

        // Write Header
        writer.Write("TimePeriod,PD");
        for (var i = 0; i < ModeGroups.Length; i++)
        {
            writer.Write($",Observed_{ModeGroups[i].Name}");
        }
        for (var i = 0; i < ModeGroups.Length; i++)
        {
            writer.Write($",Model_{ModeGroups[i].Name}");
        }
        for (var i = 0; i < ModeGroups.Length; i++)
        {
            writer.Write($",Model-Observed_{ModeGroups[i].Name}");
        }
        writer.WriteLine();

        for (var i = 0; i < timePeriods.Length; i++)
        {
            for (var j = 0; j < pds.Length; j++)
            {
                writer.Write($"{timePeriods[i].Name},{pds[j]}");
                for (var k = 0; k < ModeGroups.Length; k++)
                {
                    writer.Write($",{observed[i * pds.Length * ModeGroups.Length + j * ModeGroups.Length + k]}");
                }
                for (var k = 0; k < ModeGroups.Length; k++)
                {
                    writer.Write($",{model[i * pds.Length * ModeGroups.Length + j * ModeGroups.Length + k]}");
                }
                for (var k = 0; k < ModeGroups.Length; k++)
                {
                    writer.Write($",{model[i * pds.Length * ModeGroups.Length + j * ModeGroups.Length + k] - observed[i * pds.Length * ModeGroups.Length + j * ModeGroups.Length + k]}");
                }
                writer.WriteLine();
            }
        }
    }

    private float[] GetObserved(ITashaHousehold[] surveyHouseholdsWithTrips, TimePeriod[] timePeriods, int[] pds, int[] zoneToPd, SparseArray<IZone> zones)
    {
        object lockObject = new();
        float[] ret = new float[timePeriods.Length * pds.Length * ModeGroups.Length];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[timePeriods.Length * pds.Length * ModeGroups.Length],
            (household, _, local) =>
            {
                foreach (var person in household.Persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    var expansionFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            var zoneIndex = zones.GetFlatIndex(trip.OriginalZone.ZoneNumber);
                            if (zoneIndex < 0)
                            {
                                continue;
                            }

                            var time = trip.TripStartTime;
                            var mode = trip[ObservedModeAttachment] as ITashaMode;
                            var timeIndex = timePeriods.GetIndex(time);
                            var modeIndex = ModeGroups.GetIndex(mode);
                            if ((timeIndex < 0) | (modeIndex < 0))
                            {
                                continue;
                            }
                            var pdIndex = zoneToPd[zoneIndex];
                            var index = timeIndex * pds.Length * ModeGroups.Length + pdIndex * ModeGroups.Length + modeIndex;
                            local[index] += person.ExpansionFactor;
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
            }
            );

        if (NormalizeResults)
        {
            NormalizeModes(ret, timePeriods, pds, ModeGroups);
        }
        return ret;
    }

    private float[] GetModel(MicrosimData microsimData, TimePeriod[] timePeriods, int[] pds, int[] zoneToPd, SparseArray<IZone> zones)
    {
        object lockObject = new();
        float[] ret = new float[timePeriods.Length * pds.Length * ModeGroups.Length];
        Parallel.ForEach(microsimData.Households,
            () => new float[timePeriods.Length * pds.Length * ModeGroups.Length],
            (household, _, local) =>
            {
                var persons = microsimData.Persons[household.HouseholdID];
                foreach (var person in persons)
                {
                    if (person.Age < MinimumAge)
                    {
                        continue;
                    }
                    if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                    {
                        continue;
                    }
                    var expFactor = person.Weight / microsimData.ModeChoiceIterations;
                    foreach (var trip in trips)
                    {
                        if (!microsimData.Modes.TryGetValue((trip.HouseholdID, trip.PersonID, trip.TripID), out var modes))
                        {
                            continue;
                        }
                        foreach (var mode in modes)
                        {
                            var time = mode.DepartureTime;
                            var timeIndex = timePeriods.GetIndex(time);
                            if (timeIndex < 0)
                            {
                                continue;
                            }
                            var zoneIndex = zones.GetFlatIndex(trip.OriginZone);
                            if (zoneIndex < 0)
                            {
                                throw new XTMFRuntimeException(this, $"We found an invalid zone {trip.OriginZone}!");
                            }
                            var pdIndex = zoneToPd[zoneIndex];
                            var modeIndex = ModeGroups.GetIndex(mode);
                            if (modeIndex < 0)
                            {
                                continue;
                            }
                            var index = timeIndex * pds.Length * ModeGroups.Length + pdIndex * ModeGroups.Length + modeIndex;
                            local[index] += mode.Weight * expFactor;
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
        if (NormalizeResults)
        {
            NormalizeModes(ret, timePeriods, pds, ModeGroups);
        }
        return ret;
    }

    private static void NormalizeModes(float[] ret, TimePeriod[] timePeriods, int[] pds, ModeGroup[] modeGroups)
    {
        // The number of modes is going to be too small to get a value out of vectorizing it.
        for (int i = 0; i < timePeriods.Length; i++)
        {
            for (int j = 0; j < pds.Length; j++)
            {
                float temp = 0.0f;
                for (int k = 0; k < modeGroups.Length; k++)
                {
                    var index = i * pds.Length * modeGroups.Length
                        + j * modeGroups.Length
                        + k;
                    temp += ret[index];
                }
                temp = 1 / temp;
                // clear it if we have a NaN or infinity
                temp = float.IsFinite(temp) ? temp : 0.0f;
                for (int k = 0; k < modeGroups.Length; k++)
                {
                    var index = i * pds.Length * modeGroups.Length
                        + j * modeGroups.Length
                        + k;
                    ret[index] *= temp;
                }
            }
        }
    }

}
