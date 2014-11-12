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
        [SubModelInformation( Required = false, Description = "Provides Boarding data." )]
        public IReadODData<float> BoardingReader;

        [SubModelInformation( Required = false, Description = "Provides fare data." )]
        public IReadODData<float> FaresReader;

        [SubModelInformation( Required = true, Description = "Provides IVTT data." )]
        public IReadODData<float> IvttReader;

        [RunParameter( "No Walktime Infeasible", false, "If there is 0 walk time then the OD Pair is infeasible!" )]
        public bool NoWalkTimeInfeasible;

        [RunParameter( "Regenerate", true, "Regenerate the data after the first iteration." )]
        public bool Regenerate;

        [RunParameter( "No Unload", false, "Don't unload the data between iterations." )]
        public bool NoUnload;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Required = true, Description = "Provides Wait data." )]
        public IReadODData<float> WaitReader;

        [SubModelInformation( Required = true, Description = "Provides Walk data." )]
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

        [RunParameter( "Network Name", "Transit", "The name of this network data." )]
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
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        public Time BoardingTime(IZone origin, IZone destination, Time time)
        {
            return BoardingTime( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time );
        }

        public Time BoardingTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.BoardingTime] );
        }

        public int[] ClosestStations(IZone zone)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            return GetAllData( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time,
                out ivtt, out walk, out wait, out boarding, out cost );
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            ivtt = Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.TravelTime] );
            walk = Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.WalkTime] );
            wait = Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.WaitTime] );
            boarding = Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.BoardingTime] );
            cost = this.Data[zoneIndex + (int)DataTypes.Cost];
            return true;
        }

        public INetworkData GiveData()
        {
            return this;
        }

        public Time InVehicleTravelTime(IZone origin, IZone destination, Time time)
        {
            return InVehicleTravelTime( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time );
        }

        public Time InVehicleTravelTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.TravelTime] );
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        public void LoadData()
        {
            // setup our zones
            this.ZoneArray = this.Root.ZoneSystem.ZoneArray;
            this.Zones = this.ZoneArray.GetFlatData();
            if ( this.Data == null || this.Regenerate )
            {
                // now that we have zones we can build our data
                var data = new float[this.Zones.Length * this.Zones.Length * (int)DataTypes.NumberOfDataTypes];
                //now we need to load in each type
                LoadData( data, this.IvttReader, (int)DataTypes.TravelTime );
                LoadData( data, this.FaresReader, (int)DataTypes.Cost );
                LoadData( data, this.WaitReader, (int)DataTypes.WaitTime );
                LoadData( data, this.WalkReader, (int)DataTypes.WalkTime );
                LoadData( data, this.BoardingReader, (int)DataTypes.BoardingTime );
                // now store it
                this.Data = data;
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
            this.IterativeRoot = this.Root as IIterativeModel;
            return true;
        }

        public ITransitStation Station(IZone stationZone)
        {
            throw new NotImplementedException();
        }

        public float TravelCost(IZone start, IZone end, Time time)
        {
            return TravelCost( this.ZoneArray.GetFlatIndex( start.ZoneNumber ), this.ZoneArray.GetFlatIndex( end.ZoneNumber ), time );
        }

        public float TravelCost(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return this.Data[zoneIndex + (int)DataTypes.Cost];
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return TravelTime( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time );
        }

        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes(
                this.Data[zoneIndex + (int)DataTypes.TravelTime]
                + this.Data[zoneIndex + (int)DataTypes.WalkTime]
                + this.Data[zoneIndex + (int)DataTypes.WaitTime] );
        }

        public void UnloadData()
        {
            if ( !this.NoUnload )
            {
                this.Data = null;
                this.ZoneArray = null;
                this.Zones = null;
            }
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            if ( !this.NoWalkTimeInfeasible || this.WalkTime( start, end, time ) > Time.Zero )
            {
                return true;
            }
            return false;
        }

        public bool ValidOD(int flatOrigin, int flatDestination, Time time)
        {
            return ( !this.NoWalkTimeInfeasible || this.WalkTime( flatOrigin, flatDestination, time ) > Time.Zero );
        }

        public Time WaitTime(IZone origin, IZone destination, Time time)
        {
            return WaitTime( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time );
        }

        public Time WaitTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.WaitTime] );
        }

        public Time WalkTime(IZone origin, IZone destination, Time time)
        {
            return WalkTime( this.ZoneArray.GetFlatIndex( origin.ZoneNumber ), this.ZoneArray.GetFlatIndex( destination.ZoneNumber ), time );
        }

        public Time WalkTime(int flatOrigin, int flatDestination, Time time)
        {
            var zoneIndex = ( flatOrigin * this.Zones.Length + flatDestination ) * (int)DataTypes.NumberOfDataTypes;
            return Time.FromMinutes( this.Data[zoneIndex + (int)DataTypes.WalkTime] );
        }

        private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset)
        {
            if ( readODData == null )
            {
                return;
            }
            var numberOfZones = this.Zones.Length;
            var dataTypes = (int)DataTypes.NumberOfDataTypes;
            foreach ( var point in readODData.Read() )
            {
                var o = this.ZoneArray.GetFlatIndex( point.O );
                var d = this.ZoneArray.GetFlatIndex( point.D );
                if ( o >= 0 & d >= 0 )
                {
                    data[( o * numberOfZones + d ) * dataTypes + dataTypeOffset] = point.Data;
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