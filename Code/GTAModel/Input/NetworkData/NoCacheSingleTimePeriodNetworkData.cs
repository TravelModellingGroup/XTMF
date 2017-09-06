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
    public class NoCacheSingleTimePeriodNetworkData : INetworkData
    {
        [SubModelInformation(Required = true, Description = "Provides cost data." )]
        public IReadODData<float> CostReader;

        [RunParameter("Regenerate", true, "Regenerate the data after the first iteration." )]
        public bool Regenerate;

        [RunParameter("No Unload", false, "Don't unload the data between iterations.")]
        public bool NoUnload;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "Provides Travel Time data." )]
        public IReadODData<float> TravelTimeReader;

        [RunParameter("Apply Time Blending", false, "Apply a blending function to the travel times in ")]
        public bool ApplyTimeBlending;

        private float[] Data;

        [DoNotAutomate]
        private IIterativeModel IterativeRoot;

        private SparseArray<IZone> ZoneArray;

        private IZone[] Zones;

        private enum DataTypes
        {
            TravelTime = 0,
            Cost = 1,
            NumberOfDataTypes = 2
        }

        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Name", "Auto", "The name of this network data." )]
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
            get { return new Tuple<byte, byte, byte>(100, 200, 100 ); }
        }

        public INetworkData GiveData()
        {
            return this;
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
                var data = Data;
                // now that we have zones we can build our data
                if(data == null)
                {
                    data = new float[Zones.Length * Zones.Length * (int)DataTypes.NumberOfDataTypes];
                }
                //now we need to load in each type
                LoadData(data, TravelTimeReader, (int)DataTypes.TravelTime, Data != null & ApplyTimeBlending);
                LoadData(data, CostReader, (int)DataTypes.Cost, false );
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
            if(ApplyTimeBlending && IterativeRoot == null)
            {
                error = "In '' the option Apply Time Blending is selected however the model system is not an compatible with IIterativeModel!";
                return false;
            }
            return true;
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
            return Time.FromMinutes(Data[zoneIndex + (int)DataTypes.TravelTime]);
        }

        public void UnloadData()
        {
            //just ignore this
        }

        public bool ValidOd(IZone start, IZone end, Time time)
        {
            return true;
        }

        public bool ValidOd(int flatOrigin, int flatDestination, Time time)
        {
            return true;
        }

        private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset, bool applyBlending)
        {
            if(readODData == null )
            {
                return;
            }
            var numberOfZones = Zones.Length;
            var dataTypes = (int)DataTypes.NumberOfDataTypes;

            if(applyBlending)
            {
                var iteration = IterativeRoot.CurrentIteration;
                var previousFraction = 1.0f/ (iteration + 1.0f);
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

        public bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost)
        {
            var o = ZoneArray.GetFlatIndex(start.ZoneNumber);
            var d = ZoneArray.GetFlatIndex(end.ZoneNumber);
            var result = GetAllData(o, d, time, out float fTime, out cost);
            ivtt = Time.FromMinutes(fTime);
            return result;
        }

        public bool GetAllData(int start, int end, Time time, out float ivtt, out float cost)
        {
            var zoneIndex = (start * Zones.Length + end) * (int)DataTypes.NumberOfDataTypes;
            ivtt = Data[zoneIndex + (int)DataTypes.TravelTime];
            cost = Data[zoneIndex + (int)DataTypes.Cost];
            return true;
        }
    }
}