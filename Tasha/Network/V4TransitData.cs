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
using XTMF;
using Datastructure;
using TMG;
using TMG.Input;
using System.Threading.Tasks;
namespace Tasha.Network
{
    [ModuleInformation(Description =
        @"This module is used for providing multiple time definitions for network data.  Implemented first for GTAModel V4.0.")]
    public sealed class V4TransitData : ITripComponentCompleteData
    {
        [RunParameter("No Walktime Infeasible", false, "If there is 0 walk time then the OD Pair is infeasible!")]
        public bool NoWalkTimeInfeasible;

        [RunParameter("No Unload", false, "Don't unload the data between iterations.")]
        public bool NoUnload;

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

            [SubModelInformation(Required = false, Description = "Provides In Vehicle Time data.")]
            public IReadODData<float> IvttReader;

            [SubModelInformation(Required = false, Description = "Provides cost data.")]
            public IReadODData<float> CostReader;

            [SubModelInformation(Required = false, Description = "Provides Wait data.")]
            public IReadODData<float> WaitReader;

            [SubModelInformation(Required = false, Description = "Provides Walk data.")]
            public IReadODData<float> WalkReader;

            [SubModelInformation(Required = false, Description = "Provides Boarding data.")]
            public IReadODData<float> BoardingReader;

            /// <summary>
            /// This value is used to do averaged travel times
            /// </summary>
            int TimesLoaded;

            internal void LoadData(SparseArray<IZone> zoneArray)
            {
                var zones = zoneArray.GetFlatData();
                NumberOfZones = zones.Length;
                var dataSize = zones.Length * zones.Length * NumberOfDataTypes;
                // now that we have zones we can build our data
                var data = Data == null || dataSize != Data.Length ? new float[dataSize] : Data;
                //now we need to load in each type
                Parallel.Invoke(() => LoadData(data, IvttReader, TravelTimeIndex, zoneArray, TimesLoaded),
                () => LoadData(data, CostReader, CostIndex, zoneArray, TimesLoaded),
                () => LoadData(data, WalkReader, WalkTimeIndex, zoneArray, TimesLoaded),
                () => LoadData(data, WaitReader, WaitTimeIndex, zoneArray, TimesLoaded),
                () => LoadData(data, BoardingReader, BoardingTimeIndex, zoneArray, TimesLoaded));
                // increase the number of times that we have been loaded
                TimesLoaded++;
                // now store it
                Data = data;
            }

            private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset, SparseArray<IZone> zoneArray, int timesLoaded)
            {
                if (readODData == null)
                {
                    return;
                }
                var zones = zoneArray.GetFlatData();
                var numberOfZones = zones.Length;
                int previousPointO = -1;
                int previousFlatO = -1;
                if (timesLoaded == 0)
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

            internal bool GetData(Time time, int flatO, int flatD,
                out float travelTime, out float travelCost, out float walkTime, out float waitTime, out float boardingTime)
            {
                var data = Data;
                var index = (NumberOfZones * flatO + flatD) * NumberOfDataTypes;
                if(data == null)
                {
                    throw new XTMFRuntimeException(this, "Network data was accessed before it was loaded!");
                }
                travelTime = data[index + TravelTimeIndex];
                waitTime = data[index + WaitTimeIndex];
                walkTime = data[index + WalkTimeIndex];
                travelCost = data[index + CostIndex];
                boardingTime = data[index + BoardingTimeIndex];
                return true;
            }

            internal bool GetTimePeriodData(Time time, ref float[] data)
            {
                if (time < StartTime | time >= EndTime) return false;
                data = Data;
                return true;
            }

            internal void ResetIterations()
            {
                TimesLoaded = 0;
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
        }


        internal const int TravelTimeIndex = 0;
        internal const int WaitTimeIndex = 1;
        internal const int WalkTimeIndex = 2;
        internal const int CostIndex = 3;
        internal const int BoardingTimeIndex = 4;
        internal const int NumberOfDataTypes = 5;


        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Name", "Transit", "The name of this network data.")]
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

        [SubModelInformation(Required = false, Description = "The data for each time period for this network")]
        public TimePeriodNetworkData[] TimePeriods;

        private void GetData(int flatO, int flatD, Time time, out float travelTime, out float walkTime, out float waitTime,
            out float boardingTime, out float cost)
        {
            var periods = TimePeriods;
            if (periods != null)
            {
                for (int i = 0; i < periods.Length; i++)
                {
                    if (time >= periods[i].StartTime && time < periods[i].EndTime)
                    {
                        if (periods[i].GetData(time, flatO, flatD, out travelTime, out cost, out walkTime, out waitTime, out boardingTime))
                        {
                            return;
                        }
                    }
                }
            }
            travelTime = cost = walkTime = waitTime = boardingTime = 0f;
        }

        public float[] GetTimePeriodData(Time time)
        {
            float[] data = null;
            var timePeriods = TimePeriods;
            if (timePeriods != null)
            {
                for (int i = 0; i < timePeriods.Length; i++)
                {
                    if (timePeriods[i].GetTimePeriodData(time, ref data))
                    {
                        return data;
                    }
                }
            }
            return data;
        }


        public Time BoardingTime(IZone origin, IZone destination, Time time)
        {
            return BoardingTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time BoardingTime(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return Time.FromMinutes(boardingtime);
        }

        public int[] ClosestStations(IZone zone)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            return GetAllData(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time,
                out ivtt, out walk, out wait, out boarding, out cost);
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost)
        {
            return GetAllData(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time,
                out ivtt, out walk, out wait, out boarding, out cost);
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out cost, out float walkTime, out float waitTime, out float boardingtime);
            ivtt = Time.FromMinutes(ivttTime);
            walk = Time.FromMinutes(walkTime);
            wait = Time.FromMinutes(waitTime);
            boarding = Time.FromMinutes(boardingtime);
            return !(NoWalkTimeInfeasible & (walkTime <= 0 & ivttTime <= 0));
        }

        public bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost)
        {
            GetData(ZoneArray.GetFlatIndex(start.ZoneNumber), ZoneArray.GetFlatIndex(end.ZoneNumber), time, out float ivttTime, out cost, out float walkTime, out float waitTime, out float boardingtime);
            ivtt = Time.FromMinutes(ivttTime + walkTime + waitTime);
            return !(NoWalkTimeInfeasible & (walkTime <= 0 & ivttTime <= 0));
        }

        public bool GetAllData(int start, int end, Time time, out float ivtt, out float cost)
        {
            GetData(start, end, time, out float ivttTime, out cost, out float walkTime, out float waitTime, out float boardingtime);
            ivtt = ivttTime + walkTime + waitTime;
            return !(NoWalkTimeInfeasible & (walkTime <= 0 & ivttTime <= 0));
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost)
        {
            GetData(flatOrigin, flatDestination, time, out ivtt, out walk, out wait, out boarding, out cost);
            return !(NoWalkTimeInfeasible & (walk <= 0 & ivtt <= 0));
        }

        public INetworkData GiveData()
        {
            return this;
        }

        public Time InVehicleTravelTime(IZone origin, IZone destination, Time time)
        {
            return InVehicleTravelTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time InVehicleTravelTime(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return Time.FromMinutes(ivttTime);
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
                // since we are doing more CPU work here we can load it in parallel
                Parallel.For(0, TimePeriods.Length, i =>
                {
                    TimePeriods[i].LoadData(zoneArray);
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
            return true;
        }

        public ITransitStation Station(IZone stationZone)
        {
            throw new NotImplementedException();
        }

        public float TravelCost(IZone start, IZone end, Time time)
        {
            return TravelCost(ZoneArray.GetFlatIndex(start.ZoneNumber), ZoneArray.GetFlatIndex(end.ZoneNumber), time);
        }

        public float TravelCost(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return cost;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return TravelTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return Time.FromMinutes(ivttTime + walkTime + waitTime);
        }

        public void UnloadData()
        {
            if (!NoUnload)
            {
                ZoneArray = null;
                for (int i = 0; i < TimePeriods.Length; i++)
                {
                    TimePeriods[i].UnloadData();
                }
                Loaded = false;
            }
        }

        public bool ValidOd(IZone start, IZone end, Time time)
        {
            if (!NoWalkTimeInfeasible || WalkTime(start, end, time) > Time.Zero)
            {
                return true;
            }
            return false;
        }

        public bool ValidOd(int flatOrigin, int flatDestination, Time time)
        {
            return (!NoWalkTimeInfeasible || WalkTime(flatOrigin, flatDestination, time) > Time.Zero);
        }

        public Time WaitTime(IZone origin, IZone destination, Time time)
        {
            return WaitTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time WaitTime(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return Time.FromMinutes(waitTime);
        }

        public Time WalkTime(IZone origin, IZone destination, Time time)
        {
            return WalkTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time WalkTime(int flatOrigin, int flatDestination, Time time)
        {
            GetData(flatOrigin, flatDestination, time, out float ivttTime, out float walkTime, out float waitTime, out float boardingtime, out float cost);
            return Time.FromMinutes(walkTime);
        }
    }
}
