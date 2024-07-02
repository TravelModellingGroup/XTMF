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

[ModuleInformation(Description = "Builds a distribution of Driver Licenses by Age Group by planning district")]
public sealed class DriverLicenseByAgeByPD : Analysis
{

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The age groups to analyze.")]
    public AgeRange[] AgeRanges;

    [SubModelInformation(Required = true, Description = "The location to save the file to.")]
    public FileLocation SaveTo;

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

        float[] observed = GetObserved(surveyHouseholdsWithTrips, timePeriods, pds, zoneToPD, zones);
        float[] model = GetModel(microsimData, timePeriods, pds, zoneToPD, zones);

        using StreamWriter writer = new(SaveTo);
        WriteHeader(writer);
        WriteRecords(pds, observed, model, writer);
    }

    private void WriteRecords(int[] pds, float[] observed, float[] model, StreamWriter writer)
    {
        for (var i = 0; i < pds.Length; i++)
        {
            writer.Write($"{pds[i]}");
            for (var j = 0; j <= AgeRanges.Length; j++)
            {
                var index = (i * AgeRanges.Length + j) * 2;
                writer.Write($",{observed[index]}");
            }
            for (var j = 0; j <= AgeRanges.Length; j++)
            {
                var index = (i * AgeRanges.Length + j) * 2;
                writer.Write($",{model[index]}");
            }
            for (var j = 0; j <= AgeRanges.Length; j++)
            {
                var index = (i * AgeRanges.Length + j) * 2;
                writer.Write(index);
            }
            writer.WriteLine();
        }
    }

    private void WriteHeader(StreamWriter writer)
    {
        // Write header
        writer.Write("PD");
        for (var i = 0; i < AgeRanges.Length; i++)
        {
            writer.Write($",Observed_{i}");
        }
        for (var i = 0; i < AgeRanges.Length; i++)
        {
            writer.Write($",Model_{i}");
        }
        for (var i = 0; i < AgeRanges.Length; i++)
        {
            writer.Write($",Model_{i}-Observed_{i}");
        }
        writer.WriteLine();
    }

    private float[] GetObserved(ITashaHousehold[] surveyHouseholdsWithTrips, TimePeriod[] timePeriods, int[] pds, int[] zoneToPD, SparseArray<IZone> zones)
    {
        var lockObject = new object();
        var ret = new float[pds.Length * AgeRanges.Length * 2];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[pds.Length * AgeRanges.Length * 2],
            (household, _, local) =>
            {
                var homeZoneIndex = zones.GetFlatIndex(household.HomeZone.ZoneNumber);
                if (homeZoneIndex < 0)
                {
                    throw new XTMFRuntimeException(this, $"Zone {household.HomeZone.ZoneNumber} not found in zone system!");
                }
                var pd = zoneToPD[homeZoneIndex];
                foreach (var person in household.Persons)
                {
                    var ageRangeIndex = AgeRanges.GetIndex(person.Age) * 2;
                    var licenseIndex = person.Licence ? 1 : 0;
                    if (ageRangeIndex >= 0)
                    {
                        local[pd * (AgeRanges.Length * 2) + ageRangeIndex + licenseIndex] += household.ExpansionFactor;
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

    private float[] GetModel(MicrosimData microsimData, TimePeriod[] timePeriods, int[] pds, int[] zoneToPD, SparseArray<IZone> zones)
    {
        var lockObject = new object();
        var ret = new float[pds.Length * AgeRanges.Length * 2];
        Parallel.ForEach(microsimData.Households,
            () => new float[pds.Length * AgeRanges.Length * 2],
            (household, _, local) =>
            {
                var homeZoneIndex = zones.GetFlatIndex(household.HomeZone);
                var pd = zoneToPD[homeZoneIndex];
                var persons = microsimData.Persons[household.HouseholdID];
                foreach (var person in persons)
                {
                    var ageRangeIndex = AgeRanges.GetIndex(person.Age);
                    if (ageRangeIndex >= 0)
                    {
                        local[pd * AgeRanges.Length + ageRangeIndex] += household.Weight;
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

}
