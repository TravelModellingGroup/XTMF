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

namespace TMG.GTAModel
{
    public class NetworkComponentData : ITripComponentData, IDisposable
    {
        [RunParameter( "AM End Time", "9:00", typeof( Time ), "The end of the AM peak period." )]
        public Time AMEndTime;

        [RunParameter( "AM Start Time", "6:00", typeof( Time ), "The start of the AM peak period." )]
        public Time AMStartTime;

        [RunParameter( "Base Boarding-AM", "BaseCacheData/fin_trnbord-AM.311", "The base morning boarding time." )]
        public string BaseBoardingAM;

        [RunParameter( "Base Boarding-OP", "BaseCacheData/fin_trnbord-OP.311", "The base off-peak boarding time." )]
        public string BaseBoardingOP;

        [RunParameter( "Base Boarding-PM", "BaseCacheData/fin_trnbord-PM.311", "The base afternoon boarding time." )]
        public string BaseBoardingPM;

        [RunParameter( "Base ivtt-AM", "BaseCacheData/fin_trnivtt-AM.311", "The base morning in vehicle travel time." )]
        public string BaseIvttAM;

        [RunParameter( "Base ivtt-OP", "BaseCacheData/fin_trnivtt-OP.311", "The base off-peak in vehicle travel time." )]
        public string BaseIvttOP;

        [RunParameter( "Base ivtt-PM", "BaseCacheData/fin_trnivtt-PM.311", "The base afternoon in vehicle travel time." )]
        public string BaseIvttPM;

        [RunParameter( "Base wait-AM", "BaseCacheData/fin_trnwait-AM.311", "The base morning waiting time." )]
        public string BaseWaitAM;

        [RunParameter( "Base wait-OP", "BaseCacheData/fin_trnwait-OP.311", "The base off-peak waiting time." )]
        public string BaseWaitOP;

        [RunParameter( "Base wait-PM", "BaseCacheData/fin_trnwait-PM.311", "The base afternoon waiting time." )]
        public string BaseWaitPM;

        [RunParameter( "Base walk-AM", "BaseCacheData/fin_trnwalk-AM.311", "The base morning walk time." )]
        public string BaseWalkAM;

        [RunParameter( "Base walk-OP", "BaseCacheData/fin_trnwalk-OP.311", "The base off-peak walk time." )]
        public string BaseWalkOP;

        [RunParameter( "Base walk-PM", "BaseCacheData/fin_trnwalk-PM.311", "The base afternoon walk time." )]
        public string BaseWalkPM;

        [RunParameter( "Fares", "BaseTravelData/transitfares.311", "The fare matrix." )]
        public string Fares;

        [DoNotAutomate]
        public IIterativeModel IterativeRoot;

        [RunParameter( "No Walktime Infeasible", false, "If there is 0 walk time then the OD Pair is infeasible!" )]
        public bool NoWalkTimeInfeasible;

        [RunParameter( "First ODC File", "BaseCacheData/Transit.odc", "The location of the base Network Component information." )]
        public string ODC;

        [RunParameter( "PM End Time", "18:30", typeof( Time ), "The end of the PM peak period." )]
        public Time PMEndTime;

        [RunParameter( "PM Start Time", "15:30", typeof( Time ), "The start of the PM peak period." )]
        public Time PMStartTime;

        [RunParameter( "Regenerate", true, "Regenerate the data after the first iteration." )]
        public bool Regenerate;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Updated Boarding-AM", "UpdatedCacheData/fin_trnbord-AM.311", "The Updated morning boarding time." )]
        public string UpdatedBoardingAM;

        [RunParameter( "Updated Boarding-OP", "UpdatedCacheData/fin_trnbord-OP.311", "The Updated off-peak boarding time." )]
        public string UpdatedBoardingOP;

        [RunParameter( "Updated Boarding-PM", "UpdatedCacheData/fin_trnbord-PM.311", "The Updated afternoon boarding time." )]
        public string UpdatedBoardingPM;

        [RunParameter( "Updated ivtt-AM", "UpdatedCacheData/fin_trnivtt-AM.311", "The updated morning in vehicle travel time." )]
        public string UpdatedIvttAM;

        [RunParameter( "Updated ivtt-OP", "UpdatedCacheData/fin_trnivtt-OP.311", "The updated off-peak in vehicle travel time." )]
        public string UpdatedIvttOP;

        [RunParameter( "Updated ivtt-PM", "UpdatedCacheData/fin_trnivtt-PM.311", "The updated afternoon in vehicle travel time." )]
        public string UpdatedIvttPM;

        [RunParameter( "Updated ODC File", "UpdatedCacheData/Transit.odc", "The location of the updated Network Component information." )]
        public string UpdatedODC;

        [RunParameter( "Updated wait-AM", "UpdatedCacheData/fin_trnwait-AM.311", "The updated updated morning waiting time." )]
        public string UpdatedWaitAM;

        [RunParameter( "Updated wait-OP", "UpdatedCacheData/fin_trnwait-OP.311", "The updated off-peak waiting time." )]
        public string UpdatedWaitOP;

        [RunParameter( "Updated wait-PM", "UpdatedCacheData/fin_trnwait-PM.311", "The updated afternoon waiting time." )]
        public string UpdatedWaitPM;

        [RunParameter( "Updated walk-AM", "UpdatedCacheData/fin_trnwalk-AM.311", "The updated morning walk time." )]
        public string UpdatedWalkAM;

        [RunParameter( "Updated walk-OP", "UpdatedCacheData/fin_trnwalk-OP.311", "The updated off-peak walk time." )]
        public string UpdatedWalkOP;

        [RunParameter( "Updated walk-PM", "UpdatedCacheData/fin_trnwalk-PM.311", "The updated afternoon walk time." )]
        public string UpdatedWalkPM;

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
                var timeIndex = GetTimePeriod( time );
                return Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.BoardingTime )] );
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
            ODCCreator2<IZone> creator = new ODCCreator2<IZone>( this.Root.ZoneSystem.ZoneArray, (int)DataTypes.NumberOfDataTypes, 3 );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedIvttAM : this.BaseIvttAM ), 0, (int)DataTypes.TravelTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWaitAM : this.BaseWaitAM ), 0, (int)DataTypes.WaitTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWalkAM : this.BaseWalkAM ), 0, (int)DataTypes.WalkTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedBoardingAM : this.BaseBoardingAM ), 0, (int)DataTypes.BoardingTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedIvttPM : this.BaseIvttPM ), 1, (int)DataTypes.TravelTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWaitPM : this.BaseWaitPM ), 1, (int)DataTypes.WaitTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWalkPM : this.BaseWalkPM ), 1, (int)DataTypes.WalkTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedBoardingPM : this.BaseBoardingPM ), 1, (int)DataTypes.BoardingTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedIvttOP : this.BaseIvttOP ), 2, (int)DataTypes.TravelTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWaitOP : this.BaseWaitOP ), 2, (int)DataTypes.WaitTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedWalkOP : this.BaseWalkOP ), 2, (int)DataTypes.WalkTime );
            creator.LoadEMME2( FailIfNotExist( this.AlreadyLoaded ? this.UpdatedBoardingOP : this.BaseBoardingOP ), 2, (int)DataTypes.BoardingTime );
            if ( !String.IsNullOrWhiteSpace( this.Fares ) )
            {
                creator.LoadEMME2( FailIfNotExist( this.Fares ), 0, (int)DataTypes.Cost );
                creator.LoadEMME2( FailIfNotExist( this.Fares ), 1, (int)DataTypes.Cost );
                creator.LoadEMME2( FailIfNotExist( this.Fares ), 2, (int)DataTypes.Cost );
            }
            creator.Save( GetFullPath( this.AlreadyLoaded ? this.UpdatedODC : this.ODC ), false );
            creator = null;
            GC.Collect();
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            var timePeriod = GetTimePeriod( time );
            if ( UseCache )
            {
                ivtt = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.TravelTime] );
                walk = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.WalkTime] );
                wait = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.WaitTime] );
                boarding = Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.BoardingTime] );
                cost = this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.Cost];
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
                var timeIndex = GetTimePeriod( time );
                ivtt = Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.TravelTime )] );
                walk = Time.FromMinutes( this.StoredData[zoneIndex + ( 3 * (int)DataTypes.WalkTime )] );
                wait = Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.WaitTime )] );
                boarding = Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.BoardingTime )] );
                cost = this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.Cost )];
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
                var timeIndex = GetTimePeriod( time );
                return Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.TravelTime )] );
            }
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        public void LoadData()
        {
            if ( this.Data != null )
            {
                this.Data.Release();
            }
            if ( this.IterativeRoot != null )
            {
                this.AlreadyLoaded = this.Regenerate & ( this.IterativeRoot.CurrentIteration > 0 );
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
                var timeIndex = GetTimePeriod( time );
                return this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.Cost )];
            }
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            if ( UseCache )
            {
                var timePeriod = GetTimePeriod( time );
                return Time.FromMinutes( this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.TravelTime]
                    + this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.WalkTime]
                    + this.Data[origin.ZoneNumber, destination.ZoneNumber, timePeriod, (int)DataTypes.WaitTime] );
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
                var timeIndex = GetTimePeriod( time );
                return Time.FromMinutes(
                    this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.TravelTime )]
                    + this.StoredData[zoneIndex + ( timeIndex + (int)DataTypes.WalkTime )]
                    + this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.WaitTime )] );
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
                var timeIndex = GetTimePeriod( time );
                return Time.FromMinutes( this.StoredData[zoneIndex + ( timeIndex + 3 * (int)DataTypes.WaitTime )] );
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
                return Time.FromMinutes( this.StoredData[zoneIndex + ( 3 * (int)DataTypes.WalkTime )] );
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
                throw new XTMFRuntimeException( "an error occured wile looking for the file \"" + path + "\"!" );
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

        /// <summary>
        /// Gets the time period for travel time
        /// </summary>
        /// <param name="time">The time the trip starts at</param>
        /// <returns>The time period</returns>
        private int GetTimePeriod(Time time)
        {
            if ( time >= AMStartTime && time < AMEndTime )
            {
                return 0;
            }
            else if ( time >= PMStartTime && time < PMEndTime )
            {
                return 1;
            }
            return 2;
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
                    if ( flatLoadedData[i][j] == null ) continue;
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