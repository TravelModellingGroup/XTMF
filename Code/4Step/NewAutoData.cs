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
using Datastructure;
using XTMF;
using System.Collections.Concurrent;

namespace James.UTDM
{
    public class NewAutoData : INetworkData, IDisposable
    {
        [RootModule]
        public ITravelDemandModel Root;

        internal enum AutoDataTypes
        {
            TravelTime = 0,
            CarCost = 1
        }

        internal ODCache Data;

        private SparseTwinIndex<float[]> StoredData;

        [RunParameter( "Cache File", "Auto.odc", "The auto network cache file" )]
        public string ODC;

        [RunParameter( "AM Travel Time Data", "BaseCacheFiles/AMAutoTimes.311", "The auto times .311 file" )]
        public string AMTravelTimeOriginalData;

        [RunParameter( "AMTravel Cost Data", "BaseCacheFiles/AMAutoCosts.311", "The auto costs .311 file" )]
        public string AMTravelCostOriginalData;

        [RunParameter( "PM Travel Time Data", "BaseCacheFiles/PMAutoCosts.311", "The auto times .311 file" )]
        public string PMTravelTimeOriginalData;

        [RunParameter( "PMTravel Cost Data", "BaseCacheFiles/PMAutoCosts.311", "The auto costs .311 file" )]
        public string PMTravelCostOriginalData;

        [RunParameter( "Offpeak Travel Time Data", "BaseCacheFiles/OffpeakAutoCosts.311", "The auto times .311 file" )]
        public string OffpeakTravelTimeOriginalData;

        [RunParameter( "Offpeak Cost Data", "BaseCacheFiles/OffpeakAutoCosts.311", "The auto costs .311 file" )]
        public string OffpeakTravelCostOriginalData;

        [RunParameter( "Rebuild Data", true, "Rebuild the data after being unloaded once before." )]
        public bool RebuildDataOnSuccessiveLoads;

        [RunParameter( "Header", true, "Does the file contain a header?" )]
        public bool HeaderBoolean;

        [RunParameter( "Network Type", "Auto", "The name of the network data contained in this NetworkData module" )]
        public string NetworkType
        {
            get;
            set;
        }

        [RunParameter( "Year", 2006, "The simulation year." )]
        public int Year;

        private object LoadingLock = new object();
        private bool AlreadyLoaded = false;
        private static Time SixOClock = new Time( "6:00" );
        private static Time NineOClock = new Time( "9:00" );
        private static Time FifteenThirty = new Time( "15:30" );
        private static Time EighteenThiry = new Time( "18:30" );

        public INetworkData GiveData()
        {
            return this;
        }

        /// <summary>
        /// Gets the time period for travel time
        /// </summary>
        /// <param name="time">The time the trip starts at</param>
        /// <returns>The time period</returns>
        private int GetTimePeriod(Time time)
        {
            if ( time >= SixOClock && time < NineOClock )
            {
                return 0;
            }
            else if ( time >= FifteenThirty && time < EighteenThiry )
            {
                return 1;
            }
            return 2;
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

        public Time TravelTime(IZone start, IZone end, Time time)
        {
            Time tvt;
            var startZone = start.ZoneNumber;
            var endZone = end.ZoneNumber;
            if ( startZone == endZone )
            {
                tvt = Time.FromMinutes( start.InternalDistance / 833 + 5 );
            }
            else
            {
                float[] set = null;
                set = this.StoredData[startZone, endZone];
                if ( set != null )
                {
                    var index = (int)GetTimePeriod( time ) + ( 3 * (int)AutoDataTypes.TravelTime );
                    tvt = Time.FromMinutes( set[index] );
                }
                else
                {
                    throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + start.ZoneNumber + " and " + end.ZoneNumber );
                }
            }
            return tvt;
        }

        public float TravelCost(IZone start, IZone end, Time time)
        {
            var startZone = start.ZoneNumber;
            var endZone = end.ZoneNumber;
            var set = this.StoredData[startZone, endZone];
            if ( set != null )
            {
                return set[(int)GetTimePeriod( time ) + ( 3 * (int)AutoDataTypes.CarCost )];
            }
            else
            {
                throw new XTMFRuntimeException( "Unable to retrieve data from " + this.NetworkType + " between " + start.ZoneNumber + " and " + end.ZoneNumber );
            }
        }

        public void LoadData()
        {
            if ( Data == null )
            {
                var cacheFile = GetFullPath( this.ODC );
                if ( ( this.AlreadyLoaded & this.RebuildDataOnSuccessiveLoads ) || !File.Exists( cacheFile ) )
                {
                    // create the data if it doesn't already exist
                    ODMatrixWriter<IZone> creator =
                        new ODMatrixWriter<IZone>( this.Root.ZoneSystem.ZoneArray, 2, 3 );
                    creator.Year = this.Year;
                    creator.AdditionalDescription = "Automatically Generated";
                    creator.StartTimesHeader = "6:00,15:30,Other";
                    creator.EndTimesHeader = "9:00AM,18:30,Other";
                    creator.TypeHeader = "TravelTime,Cost";
                    creator.Modes = "Auto";
                    LoadTimes( creator, this.AMTravelTimeOriginalData, 0 );
                    LoadTimes( creator, this.PMTravelTimeOriginalData, 1 );
                    LoadTimes( creator, this.OffpeakTravelTimeOriginalData, 2 );
                    LoadCosts( creator, this.AMTravelCostOriginalData, 0 );
                    LoadCosts( creator, this.PMTravelCostOriginalData, 1 );
                    LoadCosts( creator, this.OffpeakTravelCostOriginalData, 2 );
                    creator.Save( cacheFile, false );
                }
                Data = new ODCache( cacheFile );
                this.StoredData = Data.StoreAll();
            }
            this.AlreadyLoaded = true;
        }

        public bool Loaded
        {
            get { return this.StoredData != null; }
        }

        private void LoadTimes(ODMatrixWriter<IZone> writer, string FileName, int i)
        {
            if ( Path.GetExtension( FileName ) == ".311" )
            {
                writer.LoadEMME2( FailIfNotExist( FileName ), i, (int)AutoDataTypes.TravelTime );
            }

            else
            {
                writer.LoadCSVTimes( FailIfNotExist( FileName ), HeaderBoolean, i, (int)AutoDataTypes.TravelTime );
            }
        }

        private void LoadCosts(ODMatrixWriter<IZone> writer, string FileName, int i)
        {
            if ( Path.GetExtension( FileName ) == ".311" )
            {
                writer.LoadEMME2( FailIfNotExist( FileName ), i, (int)AutoDataTypes.CarCost );
            }

            else
            {
                writer.LoadCSVTimes( FailIfNotExist( FileName ), HeaderBoolean, i, (int)AutoDataTypes.CarCost );
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
                throw new XTMFRuntimeException( "An error occured wile looking for the file \"" + path + "\"!" );
            }
            return path;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        public void UnloadData()
        {
            if ( Data != null )
            {
                this.Data.Release();
                this.Data = null;
                this.StoredData = null;
            }
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            return this.StoredData.ContainsIndex( start.ZoneNumber, end.ZoneNumber );
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
