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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input.NetworkData
{
    public class NoCacheSingleTimePeriodNetworkComponentData : ITripComponentData
    {
        [SubModelInformation(Required = false, Description = "Provides Boarding data.")]
        public IReadODData<float> BoardingReader;

        [SubModelInformation(Required = false, Description = "Provides fare data.")]
        public IReadODData<float> FaresReader;

        [SubModelInformation(Required = true, Description = "Provides IVTT data.")]
        public IReadODData<float> IvttReader;

        [RunParameter("No Walktime Infeasible", false, "If there is 0 walk time then the OD Pair is infeasible!")]
        public bool NoWalkTimeInfeasible;

        [RunParameter("Regenerate", true, "Regenerate the data after the first iteration.")]
        public bool Regenerate;

        [RunParameter("No Unload", false, "Don't unload the data between iterations.")]
        public bool NoUnload;

        [RunParameter("Apply Time Blending", false, "Apply a blending function to the travel times in ")]
        public bool ApplyTimeBlending;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "Provides Wait data.")]
        public IReadODData<float> WaitReader;

        [SubModelInformation(Required = true, Description = "Provides Walk data.")]
        public IReadODData<float> WalkReader;

        private float[] Data;

        [DoNotAutomate]
        private IIterativeModel IterativeRoot;

        private SparseArray<IZone> ZoneArray;

        private IZone[] Zones;

        private enum DataTypes
        {
            TravelTime = 0,
            WaitTime = 1,
            WalkTime = 2,
            Cost = 3,
            BoardingTime = 4,
            NumberOfDataTypes = 5
        }

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

        public Time BoardingTime(IZone origin, IZone destination, Time time)
        {
            return BoardingTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time BoardingTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(Data[zoneIndex + (int)DataTypes.BoardingTime]);
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

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            ivtt = Time.FromMinutes(Data[zoneIndex + (int)DataTypes.TravelTime]);
            walk = Time.FromMinutes(Data[zoneIndex + (int)DataTypes.WalkTime]);
            wait = Time.FromMinutes(Data[zoneIndex + (int)DataTypes.WaitTime]);
            boarding = Time.FromMinutes(Data[zoneIndex + (int)DataTypes.BoardingTime]);
            cost = Data[zoneIndex + (int)DataTypes.Cost];
            return true;
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
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(Data[zoneIndex + (int)DataTypes.TravelTime]);
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        public void LoadData()
        {
            // setup our zones
            ZoneArray = Root.ZoneSystem.ZoneArray;
            Zones = ZoneArray.GetFlatData();
            if(Data == null || Regenerate)
            {
                // now that we have zones we can build our data
                var data = Data;
                if(data == null)
                {
                    data = new float[Zones.Length * Zones.Length * (int)DataTypes.NumberOfDataTypes];
                }
                //now we need to load in each type
                LoadData(data, IvttReader, (int)DataTypes.TravelTime, Data != null & ApplyTimeBlending);
                LoadData(data, FaresReader, (int)DataTypes.Cost, false);
                LoadData(data, WaitReader, (int)DataTypes.WaitTime, Data != null & ApplyTimeBlending);
                LoadData(data, WalkReader, (int)DataTypes.WalkTime, Data != null & ApplyTimeBlending);
                LoadData(data, BoardingReader, (int)DataTypes.BoardingTime, Data != null & ApplyTimeBlending);
                // now store it
                Data = data;
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
            IterativeRoot = Root as IIterativeModel;
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
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Data[zoneIndex + (int)DataTypes.Cost];
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return TravelTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(
                Data[zoneIndex + (int)DataTypes.TravelTime]
                + Data[zoneIndex + (int)DataTypes.WalkTime]
                + Data[zoneIndex + (int)DataTypes.WaitTime]);
        }

        public void UnloadData()
        {
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            if(!NoWalkTimeInfeasible || WalkTime(start, end, time) > Time.Zero)
            {
                return true;
            }
            return false;
        }

        public bool ValidOD(int flatOrigin, int flatDestination, Time time)
        {
            return (!NoWalkTimeInfeasible || WalkTime(flatOrigin, flatDestination, time) > Time.Zero);
        }

        public Time WaitTime(IZone origin, IZone destination, Time time)
        {
            return WaitTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time WaitTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(Data[zoneIndex + (int)DataTypes.WaitTime]);
        }

        public Time WalkTime(IZone origin, IZone destination, Time time)
        {
            return WalkTime(ZoneArray.GetFlatIndex(origin.ZoneNumber), ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public Time WalkTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = (flatOrigin * Zones.Length + flatDestination) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(Data[zoneIndex + (int)DataTypes.WalkTime]);
        }

        private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset, bool applyTimeBlending)
        {
            if(readODData == null)
            {
                return;
            }
            var numberOfZones = Zones.Length;
            var dataTypes = (int)DataTypes.NumberOfDataTypes;
            if(applyTimeBlending)
            {
                var iteration = IterativeRoot.CurrentIteration;
                var previousFraction = 1.0f / (iteration + 1.0f);
                var currentFraction = iteration / (1.0f + iteration);
                foreach(var point in readODData.Read())
                {
                    var o = ZoneArray.GetFlatIndex(point.O);
                    var d = ZoneArray.GetFlatIndex(point.D);
                    if(o >= 0 & d >= 0)
                    {
                        data[(o * numberOfZones + d) * dataTypes + dataTypeOffset] = data[(o * numberOfZones + d) * dataTypes + dataTypeOffset] * previousFraction + point.Data * currentFraction;
                    }
                }
            }
            else
            {
                foreach(var point in readODData.Read())
                {
                    var o = ZoneArray.GetFlatIndex(point.O);
                    var d = ZoneArray.GetFlatIndex(point.D);
                    if(o >= 0 & d >= 0)
                    {
                        data[(o * numberOfZones + d) * dataTypes + dataTypeOffset] = point.Data;
                    }
                }
            }
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(int start, int end, Time time, out float ivtt, out float cost)
        {
            throw new NotImplementedException();
        }
    }
}