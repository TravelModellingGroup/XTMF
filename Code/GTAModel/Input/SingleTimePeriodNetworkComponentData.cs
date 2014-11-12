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
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class SingleTimePeriodNetworkComponentData : ITripComponentData, IDisposable
    {
        [RunParameter( "Base Boarding", "BaseCacheData/trnbord.311", "The base morning boarding time." )]
        public string BaseBoarding;

        [RunParameter( "Base ivtt", "BaseCacheData/trnivtt.311", "The base morning in vehicle travel time." )]
        public string BaseIvtt;

        [RunParameter( "Base wait", "BaseCacheData/trnwait.311", "The base morning waiting time." )]
        public string BaseWait;

        [RunParameter( "Base walk", "BaseCacheData/trnwalk.311", "The base morning walk time." )]
        public string BaseWalk;

        [RunParameter( "Fares", "BaseTravelData/transitfares.311", "The fare matrix." )]
        public string Fares;

        [DoNotAutomate]
        public IIterativeModel IterativeRoot;

        [RunParameter( "No Walktime Infeasible", false, "If there is 0 walk time then the OD Pair is infeasible!" )]
        public bool NoWalkTimeInfeasible;

        [RunParameter( "First ODC File", "BaseCacheData/Transit.odc", "The location of the base Network Component information." )]
        public string ODC;

        [RunParameter( "Regenerate", true, "Regenerate the data after the first iteration." )]
        public bool Regenerate;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Updated Boarding", "UpdatedCacheData/trnbord.311", "The Updated morning boarding time." )]
        public string UpdatedBoarding;

        [RunParameter( "Updated ivtt", "UpdatedCacheData/trnivtt.311", "The updated morning in vehicle travel time." )]
        public string UpdatedIvtt;

        [RunParameter( "Updated ODC File", "UpdatedCacheData/Transit.odc", "The location of the updated Network Component information." )]
        public string UpdatedODC;

        [RunParameter( "Updated wait", "UpdatedCacheData/trnwait.311", "The updated updated morning waiting time." )]
        public string UpdatedWait;

        [RunParameter( "Updated walk", "UpdatedCacheData/trnwalk.311", "The updated morning walk time." )]
        public string UpdatedWalk;

        [RunParameter( "Use Cache", false, "Dynamically load in data from the disk instead of all at once" )]
        public bool UseCache;

        private bool AlreadyLoaded = false;
        private ODCache Data;
        private int DataEntries, NumberOfZones;
        private float[] StoredData;

        private enum DataTypes
        {
            TravelTime,
            WaitTime,
            WalkTime,
            Cost,
            BoardingTime,
            NumberOfDataTypes
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
            if ( UseCache )
            {
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.BoardingTime] );
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return BoardingTime( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time );
            }
        }

        public Time BoardingTime(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return BoardingTime( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                return Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.BoardingTime] );
            }
        }

        public int[] ClosestStations(IZone zone)
        {
            throw new NotImplementedException();
        }

        public void Generate()
        {
            /*
                *  TRANSIT
                *  Structure for now will be:
                *  0: In vehicle time
                *  1: Wait time
                *  2: Walk time
                *  3: Fare
                *  4: Bording Times
                */
            ODCCreator2<IZone> creator = new ODCCreator2<IZone>( this.Root.ZoneSystem.ZoneArray, (int)DataTypes.NumberOfDataTypes, 1 );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedIvtt : this.BaseIvtt ), 0, (int)DataTypes.TravelTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWait : this.BaseWait ), 0, (int)DataTypes.WaitTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWalk : this.BaseWalk ), 0, (int)DataTypes.WalkTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedBoarding : this.BaseBoarding ), 0, (int)DataTypes.BoardingTime );
            if ( !String.IsNullOrWhiteSpace( this.Fares ) )
            {
                creator.LoadEMME2( FailIfNotExist( this.Fares ), 0, (int)DataTypes.Cost );
            }
            creator.Save( GetFullPath( this.AlreadyLoaded ? this.UpdatedODC : this.ODC ), false );
            creator = null;
            GC.Collect();
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            if ( UseCache )
            {
                ivtt = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.TravelTime] );
                walk = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WalkTime] );
                wait = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WaitTime] );
                boarding = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.BoardingTime] );
                cost = this.Data[origin.ZoneNumber, destination.ZoneNumber, (int)DataTypes.Cost];
                return true;
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return GetAllData( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time, out ivtt, out walk, out wait, out boarding, out cost );
            }
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return GetAllData( zones[flatOrigin], zones[flatDestination], time, out ivtt, out walk, out wait, out boarding, out cost );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                ivtt = Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.TravelTime] );
                walk = Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.WalkTime] );
                wait = Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.WaitTime] );
                boarding = Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.BoardingTime] );
                cost = this.StoredData[zoneIndex + (int)DataTypes.Cost];
                return true;
            }
        }

        public INetworkData GiveData()
        {
            return this;
        }

        public Time InVehicleTravelTime(IZone origin, IZone destination, Time time)
        {
            if ( UseCache )
            {
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.TravelTime] );
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return InVehicleTravelTime( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time );
            }
        }

        public Time InVehicleTravelTime(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return InVehicleTravelTime( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                return Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.TravelTime] );
            }
        }

        public bool Loaded
        {
            get { return this.StoredData != null; }
        }

        public void LoadData()
        {
            if ( this.Data != null )
            {
                this.Data.Release();
            }
            if ( this.IterativeRoot != null )
            {
                this.AlreadyLoaded = this.Regenerate | ( this.IterativeRoot.CurrentIteration > 0 );
            }

            var cache = GetFullPath( this.AlreadyLoaded ? this.UpdatedODC : this.ODC );
            if ( ( this.Regenerate && AlreadyLoaded ) || !File.Exists( cache ) )
            {
                this.Generate();
            }
            this.Data = new ODCache( cache, UseCache );
            if ( !UseCache )
            {
                var loadedData = this.Data.StoreAll();
                this.Data.Release();
                this.StoredData = this.ProcessLoadedData( loadedData, Data.Times, Data.Types );
                this.Data = null;
            }
            this.AlreadyLoaded = true;
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
            if ( UseCache )
            {
                return this.Data[start.ZoneNumber, end.ZoneNumber, 0, (int)DataTypes.Cost];
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return TravelCost( zoneArray.GetFlatIndex( start.ZoneNumber ), zoneArray.GetFlatIndex( end.ZoneNumber ), time );
            }
        }

        public float TravelCost(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return TravelCost( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                return this.StoredData[zoneIndex + (int)DataTypes.Cost];
            }
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            if ( UseCache )
            {
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.TravelTime]
                    + this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WalkTime]
                    + this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WaitTime] );
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return TravelTime( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time );
            }
        }

        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return TravelTime( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                return Time.FromMinutes(
                    this.StoredData[zoneIndex + (int)DataTypes.TravelTime]
                    + this.StoredData[zoneIndex + (int)DataTypes.WalkTime]
                    + this.StoredData[zoneIndex + (int)DataTypes.WaitTime] );
            }
        }

        public void UnloadData()
        {
            if ( this.Data != null )
            {
                this.Data.Release();
                this.Data = null;
            }
            this.StoredData = null;
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            if ( this.UseCache && this.Data.ContainsIndex( start.ZoneNumber, end.ZoneNumber )
                && ( !this.NoWalkTimeInfeasible || this.WalkTime( start, end, time ) > Time.Zero ) )
            {
                return true;
            }
            else if ( !this.NoWalkTimeInfeasible || this.WalkTime( start, end, time ) > Time.Zero )
            {
                return true;
            }
            return false;
        }

        public bool ValidOD(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return ValidOD( zoneArray[flatOrigin], zoneArray[flatDestination], time );
            }
            else if ( !this.NoWalkTimeInfeasible || this.WalkTime( flatOrigin, flatDestination, time ) > Time.Zero )
            {
                return true;
            }
            return false;
        }

        public Time WaitTime(IZone origin, IZone destination, Time time)
        {
            if ( UseCache )
            {
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WaitTime] );
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return WaitTime( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time );
            }
        }

        public Time WaitTime(int flatOrigin, int flatDestination, Time time)
        {
            if ( this.UseCache )
            {
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                return WaitTime( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
                return Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.WaitTime] );
            }
        }

        public Time WalkTime(IZone origin, IZone destination, Time time)
        {
            if ( UseCache )
            {
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, 0, (int)DataTypes.WalkTime] );
            }
            else
            {
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                return WalkTime( zoneArray.GetFlatIndex( origin.ZoneNumber ), zoneArray.GetFlatIndex( destination.ZoneNumber ), time );
            }
        }

        public Time WalkTime(int flatOrigin, int flatDestination, Time time)
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            if ( this.UseCache )
            {
                return WalkTime( zones[flatOrigin], zones[flatDestination], time );
            }
            else
            {
                var zoneIndex = ( flatOrigin * zones.Length + flatDestination ) * DataEntries;
                return Time.FromMinutes( this.StoredData[zoneIndex + (int)DataTypes.WalkTime] );
            }
        }

        private string FailIfNotExist(string localPath)
        {
            var path = this.GetFullPath( localPath );
            try
            {
                if ( !File.Exists( path ) )
                {
                    throw new XTMFRuntimeException( "The file \"" + path + "\" does not exist!" );
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "an error occurred wile looking for the file \"" + path + "\"!" );
            }
            return path;
        }

        private string GetFullPath(string localPath)
        {
            if ( !Path.IsPathRooted( localPath ) )
            {
                return Path.Combine( this.Root.InputBaseDirectory, localPath );
            }
            return localPath;
        }

        private float[] ProcessLoadedData(SparseTwinIndex<float[]> loadedData, int types, int times)
        {
            var flatLoadedData = loadedData.GetFlatData();
            var dataEntries = this.DataEntries = times * types;
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            this.NumberOfZones = zones.Length;
            var ret = new float[zones.Length * zones.Length * types * times];
            Parallel.For( 0, flatLoadedData.Length, (int i) =>
            {
                var flatI = zoneArray.GetFlatIndex( loadedData.GetSparseIndex( i ) );
                for ( int j = 0; j < flatLoadedData[i].Length; j++ )
                {
                    if ( flatLoadedData[i][j] == null )
                    {
                        continue;
                    }
                    var flatJ = zoneArray.GetFlatIndex( loadedData.GetSparseIndex( i, j ) );
                    for ( int k = 0; k < flatLoadedData[i][j].Length; k++ )
                    {
                        ret[( flatI * zones.Length + flatJ ) * dataEntries + k] = flatLoadedData[i][j][k];
                    }
                }
            } );
            return ret;
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Data != null )
            {
                this.Data.Dispose();
                this.Data = null;
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