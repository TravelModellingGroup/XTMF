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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG;
using XTMF;
using Datastructure;

namespace James.UTDM
{
    public class TransitData : ITripComponentData, IDisposable
    {

        private ODCache Data;
        private SparseTwinIndex<float[]> StoredData;

        private enum DataTypes
        {
            TravelTime,
            WaitTime,
            WalkTime,
            Cost
        }

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "OCD File", "UpdatedCacheFiles/Transit.odc", "The location of the transit information." )]
        public string ODC;

        [RunParameter( "Minimum Age", 8, "The minimum age to be able to use this network while alone." )]
        public int MinAgeAlone;

        [RunParameter( "trnivtt-AM", "BaseTravelData/fin_trnivtt-AM.311", "The morning in vehicle travel time." )]
        public string trnivttAM;

        [RunParameter( "trnivtt-PM", "BaseTravelData/fin_trnivtt-PM.311", "The afternoon in vehicle travel time." )]
        public string trnivttPM;

        [RunParameter( "trnivtt-OP", "BaseTravelData/fin_trnivtt-OP.311", "The off-peak in vehicle travel time." )]
        public string trnivttOP;

        [RunParameter( "trnwait-AM", "BaseTravelData/fin_trnwait-AM.311", "The morning waiting time." )]
        public string fin_trnwaitAM;

        [RunParameter( "trnwait-PM", "BaseTravelData/fin_trnwait-PM.311", "The afternoon waiting time." )]
        public string fin_trnwaitPM;

        [RunParameter( "trnwait-OP", "BaseTravelData/fin_trnwait-OP.311", "The off-peak waiting time." )]
        public string fin_trnwaitOP;

        [RunParameter( "trnwalk-AM", "BaseTravelData/fin_trnwalk-AM.311", "The morning walk time." )]
        public string trnwalkAM;

        [RunParameter( "trnwalk-PM", "BaseTravelData/fin_trnwalk-PM.311", "The afternoon walk time." )]
        public string trnwalkPM;

        [RunParameter( "trnwalk-OP", "BaseTravelData/fin_trnwalk-OP.311", "The off-peak walk time." )]
        public string trnwalkOP;

        [RunParameter( "Transit Fairs", "BaseTravelData/transitfares.311", "The transit fairs matrix." )]
        public string transitfares;

        [RunParameter( "Network Name", "Transit", "The name of this network data." )]
        public string NetworkType
        {
            get;
            set;
        }

        [RunParameter( "Regenerate", true, "Regenerate the data after the first iteration." )]
        public bool Regenerate;

        private bool AlreadyLoaded = false;

        public INetworkData GiveData()
        {
            return this;
        }

        public void LoadData()
        {
            if ( this.Data != null )
            {
                this.Data.Release();
            }
            var cache = GetFullPath( this.ODC );
            if ( ( this.Regenerate && AlreadyLoaded ) || !File.Exists( cache ) )
            {
                this.Generate();
            }
            this.Data = new ODCache( cache );
            this.StoredData = this.Data.StoreAll();
            this.AlreadyLoaded = true;
        }

        public bool Loaded
        {
            get { return this.StoredData != null; }
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

        public void Generate()
        {
            /*
                *  TRANSIT
                *  Structure for now will be:
                *  0: train in vehicle time
                *  1: train wait time
                *  2: train walk time
                *  3: train fare
                */
            ODCCreator creator = new ODCCreator( this.Root.ZoneSystem.ZoneArray.Top + 1, 4, 3, 1 );
            creator.LoadEMME2( FailIfNotExist( this.trnivttAM ), 0, 0 );
            creator.LoadEMME2( FailIfNotExist( this.fin_trnwaitAM ), 0, 1 );
            creator.LoadEMME2( FailIfNotExist( this.trnwalkAM ), 0, 2 );
            creator.LoadEMME2( FailIfNotExist( this.trnivttPM ), 1, 0 );
            creator.LoadEMME2( FailIfNotExist( this.fin_trnwaitPM ), 1, 1 );
            creator.LoadEMME2( FailIfNotExist( this.trnwalkPM ), 1, 2 );
            creator.LoadEMME2( FailIfNotExist( this.trnivttOP ), 2, 0 );
            creator.LoadEMME2( FailIfNotExist( this.fin_trnwaitOP ), 2, 1 );
            creator.LoadEMME2( FailIfNotExist( this.trnwalkOP ), 2, 2 );
            if ( !String.IsNullOrWhiteSpace( this.transitfares ) )
            {
                creator.LoadEMME2( FailIfNotExist( this.transitfares ), 0, 3 );
                creator.LoadEMME2( FailIfNotExist( this.transitfares ), 1, 3 );
                creator.LoadEMME2( FailIfNotExist( this.transitfares ), 2, 3 );
            }
            creator.Save( GetFullPath( this.ODC ), false );
            creator = null;
            GC.Collect();
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

        public string Name
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

        public float TravelCost(IZone start, IZone end, Time time)
        {
            var data = this.StoredData[start.ZoneNumber, end.ZoneNumber];
            if ( data != null )
            {
                return data[(int)DataTypes.Cost];
            }
            return float.PositiveInfinity;
        }

        public Time WalkTime(IZone origin, IZone destination, Time time)
        {
            var data = this.StoredData[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + origin.ZoneNumber + " and " + destination.ZoneNumber );
            }
            var result = data[(int)DataTypes.WalkTime];
            return Time.FromMinutes( result );
        }

        public Time WaitTime(IZone origin, IZone destination, Time time)
        {
            var data = this.StoredData[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + origin.ZoneNumber + " and " + destination.ZoneNumber );
            }
            return Time.FromMinutes( data[(int)DataTypes.WaitTime] );
        }

        public Time InVehicleTravelTime(IZone origin, IZone destination, Time time)
        {
            var data = this.StoredData[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + origin.ZoneNumber + " and " + destination.ZoneNumber );
            }
            return Time.FromMinutes( data[(int)DataTypes.TravelTime] );
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            var data = this.StoredData[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + origin.ZoneNumber + " and " + destination.ZoneNumber );
            }
            return Time.FromMinutes( data[(int)DataTypes.TravelTime] );
        }

        public bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            var timePeriod = 0;
            var data = this.StoredData[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + origin.ZoneNumber + " and " + destination.ZoneNumber );
            }
            ivtt = Time.FromMinutes( data[(int)DataTypes.TravelTime * 3 + timePeriod] );
            walk = Time.FromMinutes( data[(int)DataTypes.WalkTime * 3 + timePeriod] );
            wait = Time.FromMinutes( data[(int)DataTypes.WaitTime * 3 + timePeriod] );
            cost = data[(int)DataTypes.Cost * 3 + timePeriod];
            boarding = Time.Zero;
            return true;
        }

        public ITransitStation Station(IZone stationZone)
        {
            throw new NotImplementedException();
        }

        public int[] ClosestStations(IZone zone)
        {
            throw new NotImplementedException();
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            return this.StoredData.ContainsIndex( start.ZoneNumber, end.ZoneNumber );
        }

        public SparseTwinIndex<float> OD
        {
            get;
            set;
        }

        public Time BoardingTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }


        public Time WalkTime(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public Time WaitTime(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public Time InVehicleTravelTime(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public Time BoardingTime(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost)
        {
            throw new NotImplementedException();
        }


        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public float TravelCost(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
        }

        public bool ValidOD(int flatOrigin, int flatDestination, Time time)
        {
            throw new NotImplementedException();
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
