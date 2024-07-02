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

[ModuleInformation(Description = "Builds a distribution of Auto Ownership by planning district")]
public sealed class AODistributionByPD : Analysis
{

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The location to save the file to.")]
    public FileLocation SaveTo;

    [RunParameter("Max Cars", 4, "The maximum number of cars to report.")]
    public int MaxCars;

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
            for (var j = 0; j <= MaxCars; j++)
            {
                writer.Write($",{observed[i * MaxCars + j]}");
            }
            for (var j = 0; j <= MaxCars; j++)
            {
                writer.Write($",{model[i * MaxCars + j]}");
            }
            for (var j = 0; j <= MaxCars; j++)
            {
                writer.Write($",{model[i * MaxCars + j] - observed[i * MaxCars + j]}");
            }
            writer.WriteLine();
        }
    }

    private void WriteHeader(StreamWriter writer)
    {
        // Write header
        writer.Write("PD");
        for (var i = 0; i <= MaxCars; i++)
        {
            writer.Write($",Observed_{i}");
        }
        for (var i = 0; i <= MaxCars; i++)
        {
            writer.Write($",Model_{i}");
        }
        for (var i = 0; i <= MaxCars; i++)
        {
            writer.Write($",Model_{i}-Observed_{i}");
        }
        writer.WriteLine();
    }

    private float[] GetObserved(ITashaHousehold[] surveyHouseholdsWithTrips, TimePeriod[] timePeriods, int[] pds, int[] zoneToPD, SparseArray<IZone> zones)
    {
        var lockObject = new object();
        var ret = new float[pds.Length * (MaxCars + 1)];
        Parallel.ForEach(surveyHouseholdsWithTrips,
            () => new float[pds.Length * (MaxCars + 1)],
            (household, _, local) =>
        {
            var homeZoneIndex = zones.GetFlatIndex(household.HomeZone.ZoneNumber);
            if(homeZoneIndex < 0)
            {
                throw new XTMFRuntimeException(this, $"Zone {household.HomeZone.ZoneNumber} not found in zone system!");
            }
            var pd = zoneToPD[homeZoneIndex];
            var numberOfVehicles = Math.Min(household.Vehicles.Length, MaxCars);
            local[pd * (MaxCars + 1) + numberOfVehicles] += household.ExpansionFactor;
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
        var ret = new float[pds.Length * (MaxCars + 1)];
        Parallel.ForEach(microsimData.Households,
            () => new float[pds.Length * (MaxCars + 1)],
            (household, _, local) =>
        {
            var homeZoneIndex = zones.GetFlatIndex(household.HomeZone);
            var pd = zoneToPD[homeZoneIndex];
            var numberOfVehicles = Math.Min(household.Vehicles, MaxCars);
            local[pd * (MaxCars + 1) + numberOfVehicles] += household.Weight;
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

    public override bool RuntimeValidation(ref string error)
    {
        if (MaxCars < 0)
        {
            error = "MaxCars must be greater than or equal to 0.";
            return false;
        }
        return base.RuntimeValidation(ref error);
    }

}
