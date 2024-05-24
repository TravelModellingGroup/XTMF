/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG;
using TMG.Input;
using XTMF;
using Datastructure;
using System.Threading.Tasks;

namespace Tasha.Network;

[ModuleInformation(Description =
    @"This module is used for providing multiple time definitions for network data.  Implemented first for GTAModel V4.0.")]
public sealed class V4AutoNetwork : INetworkCompleteData
{
    [RunParameter("No Unload", false, "Don't unload the data between iterations.")]
    public bool NoUnload;

    [RunParameter("Average LoS", true, "Should we average the LoS between iterations?")]
    public bool AverageLoS;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<IZone> ZoneArray;

    [ModuleInformation(Description =
        @"This module defines a time period and store the network data for that time period.  Implemented first for GTAModel V4.0.")]
    public sealed class TimePeriodNetworkData : IModule
    {
        [RunParameter("Start Time", "6:00AM", typeof(Time), "The start time for the period.")]
        public Time StartTime;
        [RunParameter("End Time", "9:00AM", typeof(Time), "The end time for the period, (exclusive).")]
        public Time EndTime;

        private int NumberOfZones;

        private float[] Data;

        [SubModelInformation(Required = true, Description = "Provides Travel Time data.")]
        public IReadODData<float> TravelTimeReader;

        [SubModelInformation(Required = true, Description = "Provides cost data.")]
        public IReadODData<float> CostReader;

        /// <summary>
        /// This value is used to do averaged travel times
        /// </summary>
        int TimesLoaded;

        internal void LoadData(SparseArray<IZone> zoneArray, bool averageLoS)
        {
            var zones = zoneArray.GetFlatData();
            NumberOfZones = zones.Length;
            var dataSize = zones.Length * zones.Length * NumberOfDataTypes;
            // now that we have zones we can build our data
            var data = Data == null || dataSize != Data.Length ? new float[dataSize] : Data;
            //now we need to load in each type
            Parallel.Invoke(
                () => LoadData(data, TravelTimeReader, TravelTimeIndex, zoneArray, TimesLoaded, averageLoS),
                () => LoadData(data, CostReader, CostIndex, zoneArray, TimesLoaded, averageLoS));
            TimesLoaded++;
            // now store it
            Data = data;
        }

        private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset, SparseArray<IZone> zoneArray, int timesLoaded, bool averageLoS)
        {
            if (readODData == null)
            {
                return;
            }
            var zones = zoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            int previousPointO = -1;
            int previousFlatO = -1;
            if (!averageLoS || timesLoaded == 0)
            {
                foreach (var point in readODData.Read())
                {
                    var o = point.O == previousPointO ? previousFlatO : zoneArray.GetFlatIndex(point.O);
                    var d = zoneArray.GetFlatIndex(point.D);
                    if (o >= 0 & d >= 0)
                    {
                        previousPointO = point.O;
                        previousFlatO = o;
                        var index = (o * numberOfZones + d) * NumberOfDataTypes + dataTypeOffset;
                        data[index] = point.Data;
                    }
                }
            }
            else
            {
                var previousFraction = 1.0f / 2.0f;
                var currentFraction = 1.0f / 2.0f;
                foreach (var point in readODData.Read())
                {
                    var o = point.O == previousPointO ? previousFlatO : zoneArray.GetFlatIndex(point.O);
                    var d = zoneArray.GetFlatIndex(point.D);
                    if (o >= 0 & d >= 0)
                    {
                        previousPointO = point.O;
                        previousFlatO = o;
                        var index = (o * numberOfZones + d) * NumberOfDataTypes + dataTypeOffset;
                        data[index] = data[index] * previousFraction + point.Data * currentFraction;
                    }
                }
            }
        }

        internal void UnloadData()
        {

        }

        internal bool GetDataIfInTimePeriod(Time time, int flatO, int flatD, out float travelTime, out float travelCost)
        {
            if (time < StartTime | time >= EndTime)
            {
                travelTime = 0;
                travelCost = 0;
                return false;
            }
            var index = (NumberOfZones * flatO + flatD) * (NumberOfDataTypes);
            if(Data == null)
            {
                throw new XTMFRuntimeException(this, "Network data was accessed before it was loaded!");
            }
            travelTime = Data[index + TravelTimeIndex];
            travelCost = Data[index + CostIndex];
            return true;
        }

        internal bool GetTimePeriodData(Time time, ref float[] data)
        {
            if (time < StartTime | time >= EndTime) return false;
            data = Data;
            return true;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (StartTime >= EndTime)
            {
                error = "In '" + Name + "' the Start Time is greater than or the same to the end time!";
            }
            return true;
        }

        internal void ResetIterations()
        {
            TimesLoaded = 0;
        }
    }

    [SubModelInformation(Required = false, Description = "The data for each time period for this network")]
    public TimePeriodNetworkData[] TimePeriods;


    private const int TravelTimeIndex = 0;
    private const int CostIndex = 1;
    private const int NumberOfDataTypes = 2;


    public string Name
    {
        get;
        set;
    }

    [RunParameter("Network Name", "Auto", "The name of this network data.")]
    public string NetworkType
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>(100, 200, 100); }
    }

    public INetworkData GiveData()
    {
        return this;
    }

    public bool Loaded
    {
        get;
        private set;
    }

    public void LoadData()
    {
        // setup our zones
        var zoneArray = Root.ZoneSystem.ZoneArray;
        ZoneArray = zoneArray;
        if (!Loaded)
        {
            if (Root is IIterativeModel iterationModel)
            {
                if (iterationModel.CurrentIteration == 0)
                {
                    for (int i = 0; i < TimePeriods.Length; i++)
                    {
                        TimePeriods[i].ResetIterations();
                    }
                }
            }
            Parallel.For(0, TimePeriods.Length, i =>
            {
                TimePeriods[i].LoadData(zoneArray, AverageLoS);
            });
            Loaded = true;
        }
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        Loaded = false;
        return true;
    }

    public float TravelCost(IZone start, IZone end, Time time)
    {
        return TravelCost(ZoneArray.GetFlatIndex(start.ZoneNumber), ZoneArray.GetFlatIndex(end.ZoneNumber), time);
    }

    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        return TravelTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
    }

    public bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost)
    {
        if (GetData(ZoneArray.GetFlatIndex(start.ZoneNumber), ZoneArray.GetFlatIndex(end.ZoneNumber), time, out float localIvtt, out cost))
        {
            ivtt = Time.FromMinutes(localIvtt);
            return true;
        }
        ivtt = Time.Zero;
        return false;
    }

    private bool GetData(int flatO, int flatD, Time time, out float travelTime, out float cost)
    {
        for (int i = 0; i < TimePeriods.Length; i++)
        {
            if (TimePeriods[i].GetDataIfInTimePeriod(time, flatO, flatD, out travelTime, out cost))
            {
                return true;
            }
        }
        travelTime = cost = 0f;
        return false;
    }

    public Time TravelTime(int flatOrigin, int flatDestination, Time time)
    {
        GetData(flatOrigin, flatDestination, time, out float travelTime, out float cost);
        return Time.FromMinutes(travelTime);
    }

    public float TravelCost(int flatOrigin, int flatDestination, Time time)
    {
        GetData(flatOrigin, flatDestination, time, out float travelTime, out float cost);
        return cost;
    }

    public bool GetAllData(int start, int end, Time time, out float ivtt, out float cost)
    {
        for (int i = 0; i < TimePeriods.Length; i++)
        {
            if (TimePeriods[i].GetDataIfInTimePeriod(time, start, end, out ivtt, out cost))
            {
                return true;
            }
        }
        ivtt = cost = 0f;
        return false;
    }

    public float[] GetTimePeriodData(Time time)
    {
        float[] data = null;
        for (int i = 0; i < TimePeriods.Length; i++)
        {
            if (TimePeriods[i].GetTimePeriodData(time, ref data))
            {
                return data;
            }
        }
        return data;
    }

    public void UnloadData()
    {
        if (!NoUnload)
        {
            ZoneArray = null;
            Loaded = false;
            for (int i = 0; i < TimePeriods.Length; i++)
            {
                TimePeriods[i].UnloadData();
            }
        }
    }

    public bool ValidOd(IZone start, IZone end, Time time)
    {
        return true;
    }

    public bool ValidOd(int flatOrigin, int flatDestination, Time time)
    {
        return true;
    }
}
