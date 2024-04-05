/*
    Copyright 2021-2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Common;

[ModuleInformation(Description = "Parking Model for GTAModel V4.2+.  All costs are in dollars, all times are in minutes-from-midnight.")]
public class ParkingModel : IParkingCost
{

    [RootModule]
    public ITravelDemandModel Root;

    public bool Loaded { get; set; }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    private struct TimePeriodData
    {
        internal float StartOfPeriod;
        internal float Hourly;
        internal float Max;
    }

    private struct ParkingInformation
    {
        internal TimePeriodData Daily;
        internal TimePeriodData Nightly;
        internal float FullDayMax;
    }
    
    private SparseArray<IZone> _zoneSystem;
    private ParkingInformation[] _parkingInformation;

    [SubModelInformation(Required = true, Description = "CSV of (Zone,StartOfDay,DailyHourly,DailyMax,StartOfNight,NightlyHourly,NightlyMax,FullDayMax)")]
    public FileLocation ParkingData;

    public float ComputeParkingCost(Time parkingStart, Time parkingEnd, IZone zone)
    {
        // If you don't actually spend any time there, there is no cost.
        if (parkingStart == parkingEnd)
        {
            return 0f;
        }
        var zoneIndex = _zoneSystem.GetFlatIndex(zone.ZoneNumber);
        if (zoneIndex >= 0)
        {
            return ComputeParkingCost(parkingStart, parkingEnd, zoneIndex);
        }
        else
        {
            return 0f;
        }
    }

    public float ComputeParkingCost(Time parkingStart, Time parkingEnd, int flatZone)
    {
        // If you don't actually spend any time there, there is no cost.
        if(parkingStart == parkingEnd)
        {
            return 0f;
        }
        var ps = parkingStart.ToMinutes();
        var pe = parkingEnd.ToMinutes();
        ref var zoneData = ref _parkingInformation[flatZone];
        float cost = 0.0f;
        /*
         * Parking has 3 components, time spent before the daily, during the daily, and during the nightly
         */
        if (ps <= zoneData.Daily.StartOfPeriod)
        {
            var duration = Math.Min(zoneData.Daily.StartOfPeriod, pe) - ps;
            cost = Math.Min(duration * zoneData.Nightly.Hourly, zoneData.Nightly.Max);
        }
        if (ps <= zoneData.Nightly.StartOfPeriod)
        {
            var duration = Math.Min(pe, zoneData.Nightly.StartOfPeriod) - Math.Max(ps, zoneData.Daily.StartOfPeriod);
            cost += Math.Min(duration * zoneData.Daily.Hourly, zoneData.Daily.Max);
        }
        if (pe >= zoneData.Nightly.StartOfPeriod)
        {
            var duration = pe - Math.Max(ps, zoneData.Nightly.StartOfPeriod);
            cost += Math.Min(duration * zoneData.Nightly.Hourly, zoneData.Nightly.Max);
        }
        return Math.Min(cost, zoneData.FullDayMax);
    }

    public void LoadData()
    {
        _zoneSystem = Root.ZoneSystem.ZoneArray;
        _parkingInformation = new ParkingInformation[_zoneSystem.Count];
        try
        {
            using CsvReader reader = new(ParkingData);
            reader.LoadLine();
            while (reader.LoadLine(out var columns))
            {
                if (columns >= 8)
                {
                    ParkingInformation data;
                    reader.Get(out int zoneNumber, 0);
                    var index = _zoneSystem.GetFlatIndex(zoneNumber);
                    if (index < 0)
                    {
                        throw new XTMFRuntimeException(this, $"Unknown zone number {zoneNumber} when reading parking data!");
                    }
                    reader.Get(out data.Daily.StartOfPeriod, 1);
                    reader.Get(out data.Daily.Hourly, 2);
                    reader.Get(out data.Daily.Max, 3);
                    reader.Get(out data.Nightly.StartOfPeriod, 4);
                    reader.Get(out data.Nightly.Hourly, 5);
                    reader.Get(out data.Nightly.Max, 6);
                    reader.Get(out data.FullDayMax, 7);

                    // Convert from hourly costs to per minute costs
                    data.Daily.Hourly /= 60;
                    data.Nightly.Hourly /= 60;

                    _parkingInformation[index] = data;
                }
            }
        }
        catch (IOException ex)
        {
            throw new XTMFRuntimeException(this, ex);
        }
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        _zoneSystem = null;
        _parkingInformation = null;
        Loaded = false;
    }

    public IParkingCost GiveData()
    {
        return this;
    }
}
