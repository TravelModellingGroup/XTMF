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

namespace TMG.GTAModel;

public class NetworkData : INetworkData, IDisposable
{
    [RunParameter( "AM End Time", "9:00", typeof( Time ), "The end of the AM peak period." )]
    public Time AMEndTime;

    [RunParameter( "AM Start Time", "6:00", typeof( Time ), "The start of the AM peak period." )]
    public Time AMStartTime;

    [RunParameter( "Base AM Cost Data", "BaseCacheData/AMAutoCosts.311", "The base AM network costs .311/csv file" )]
    public string BaseAMTravelCostData;

    [RunParameter( "Base AM Travel Time Data", "BaseCacheData/AMAutoTimes.311", "The base AM network times .311/csv file" )]
    public string BaseAMTravelTimeData;

    [RunParameter( "Base OP Cost Data", "BaseCacheData/OffpeakAutoCosts.311", "The base Offpeak network costs .311/csv file" )]
    public string BaseOffpeakTravelCostData;

    [RunParameter( "Base OP Travel Time Data", "BaseCacheData/OffpeakAutoTimes.311", "The base Offpeak network times .311/csv file" )]
    public string BaseOffpeakTravelTimeData;

    [RunParameter( "Base PM Cost Data", "BaseCacheData/PMAutoCosts.311", "The base PM network costs .311/csv file" )]
    public string BasePMTravelCostData;

    [RunParameter( "Base PM Travel Time Data", "BaseCacheData/PMAutoTimes.311", "The base PM network times .311/csv file" )]
    public string BasePMTravelTimeData;

    [RunParameter( "Header", true, "When loading CSV data, will it contain a header?" )]
    public bool HeaderBoolean;

    /// <summary>
    /// Allows us to try to get the current iteration data
    /// </summary>
    [DoNotAutomate]
    public IIterativeModel IterativeRoot;

    [RunParameter( "First ODC File", "BaseCacheData/Auto.odc", "The location of the base Network Cache." )]
    public string ODC;

    [RunParameter( "PM End Time", "18:30", typeof( Time ), "The end of the PM peak period." )]
    public Time PMEndTime;

    [RunParameter( "PM Start Time", "15:30", typeof( Time ), "The start of the PM peak period." )]
    public Time PMStartTime;

    [RunParameter( "Rebuild Data", true, "Rebuild the data cache on successive iterations?" )]
    public bool RebuildDataOnSuccessiveLoads;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter( "Updated AM Cost Data", "UpdatedCacheData/AMAutoCosts.311", "The updated AM network costs .311/csv file" )]
    public string UpdatedAMCostData;

    [RunParameter( "Updated AM Travel Time Data", "UpdatedCacheData/AMAutoTimes.311", "The updated AM network times .311/csv file" )]
    public string UpdatedAMTravelTimeData;

    [RunParameter( "Updated ODC File", "UpdatedCacheData/Auto.odc", "The location of the updated Network Cache." )]
    public string UpdatedODC;

    [RunParameter( "Updated OP Data", "UpdatedCacheData/OffpeakAutoCosts.311", "The updated Offpeak network costs .311/csv file" )]
    public string UpdatedOffpeakCostData;

    [RunParameter( "Updated OP Travel Time Data", "UpdatedCacheData/OffpeakAutoTimes.311", "The updated Offpeak network times .311/csv file" )]
    public string UpdatedOffpeakTravelTimeData;

    [RunParameter( "Updated PM Cost Data", "UpdatedCacheData/PMAutoCosts.311", "The updated PM network costs .311/csv file" )]
    public string UpdatedPMCostData;

    [RunParameter( "Updated PM Travel Time Data", "UpdatedCacheData/PMAutoTimes.311", "The updated PM network times .311/csv file" )]
    public string UpdatedPMTravelTimeData;

    [RunParameter( "Year", 2006, "The simulation year.  This number will be attached to the metadata when creating a new cache file." )]
    public int Year;

    private bool AlreadyLoaded;

    private OdCache Data;

    private int DataEntries;

    private int NumberOfZones;

    /// <summary>
    /// Contains all of the data from the cache
    /// </summary>
    private float[] StoredData;

    internal enum AutoDataTypes
    {
        TravelTime = 0,
        CarCost = 1
    }

    [RunParameter( "Network Type", "Auto", "The name of the network data contained in this NetworkData module" )]
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

    public string Name
    {
        get;
        set;
    }

    public INetworkData GiveData()
    {
        return this;
    }

    public bool Loaded
    {
        get { return StoredData != null; }
    }

    public void LoadData()
    {
        if ( IterativeRoot != null )
        {
            AlreadyLoaded = IterativeRoot.CurrentIteration > 0;
        }

        var cacheFile = GetFullPath( AlreadyLoaded ? UpdatedODC : ODC );
        if ( ( AlreadyLoaded & RebuildDataOnSuccessiveLoads ) || !File.Exists( cacheFile ) )
        {
            Generate( cacheFile );
        }
        Data = new OdCache( cacheFile );
        var loadedData = Data.StoreAll();
        StoredData = ProcessLoadedData( loadedData, Data.Types, Data.Times );
        Data.Release();
        Data = null;
        AlreadyLoaded = true;
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        // if we are attached to an iterative model load it in
        IterativeRoot = Root as IIterativeModel;
        return true;
    }

    public float TravelCost(IZone start, IZone end, Time time)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        return TravelCost( zoneArray.GetFlatIndex( start.ZoneNumber ), zoneArray.GetFlatIndex( end.ZoneNumber ), time );
    }

    public float TravelCost(int flatOrigin, int flatDestination, Time time)
    {
        var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
        var timeIndex = GetTimePeriod( time );
        return StoredData[zoneIndex + ( timeIndex + 3 * (int)AutoDataTypes.CarCost )];
    }

    public Time TravelTime(IZone start, IZone end, Time time)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        return TravelTime( zoneArray.GetFlatIndex( start.ZoneNumber ), zoneArray.GetFlatIndex( end.ZoneNumber ), time );
    }

    public Time TravelTime(int flatOrigin, int flatDestination, Time time)
    {
        var zoneIndex = ( flatOrigin * NumberOfZones + flatDestination ) * DataEntries;
        var timeIndex = GetTimePeriod( time );
        return Time.FromMinutes( StoredData[zoneIndex + ( timeIndex + 3 * (int)AutoDataTypes.TravelTime )] );
    }

    public void UnloadData()
    {
        if ( Data != null )
        {
            Data.Release();
            Data = null;
            StoredData = null;
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

    private string FailIfNotExist(string localPath)
    {
        var path = GetFullPath( localPath );
        try
        {
            if ( !File.Exists( path ) )
            {
                throw new XTMFRuntimeException(this, "The file \"" + path + "\" does not exist!" );
            }
        }
        catch ( IOException )
        {
            throw new XTMFRuntimeException(this, "An error occured wile looking for the file \"" + path + "\"!" );
        }
        return path;
    }

    private void Generate(string cacheFile)
    {
        // create the data if it doesn't already exist
        OdMatrixWriter<IZone> creator =
            new(Root.ZoneSystem.ZoneArray, 2, 3)
            {
                Year = Year,
                AdditionalDescription = "Automatically Generated",
                StartTimesHeader = "6:00,15:30,Other",
                EndTimesHeader = "9:00AM,18:30,Other",
                TypeHeader = "TravelTime,Cost",
                Modes = "Auto"
            };
        LoadTimes( creator, AlreadyLoaded ? UpdatedAMTravelTimeData : BaseAMTravelTimeData, 0 );
        LoadTimes( creator, AlreadyLoaded ? UpdatedPMTravelTimeData : BasePMTravelTimeData, 1 );
        LoadTimes( creator, AlreadyLoaded ? UpdatedOffpeakTravelTimeData : BaseOffpeakTravelTimeData, 2 );
        LoadCosts( creator, AlreadyLoaded ? UpdatedAMCostData : BaseAMTravelCostData, 0 );
        LoadCosts( creator, AlreadyLoaded ? UpdatedPMCostData : BasePMTravelCostData, 1 );
        LoadCosts( creator, AlreadyLoaded ? UpdatedOffpeakCostData : BaseOffpeakTravelCostData, 2 );
        creator.Save( cacheFile, false );
        GC.Collect();
    }

    private string GetFullPath(string localPath)
    {
        var fullPath = localPath;
        if ( !Path.IsPathRooted( fullPath ) )
        {
            fullPath = Path.Combine( Root.InputBaseDirectory, fullPath );
        }
        return fullPath;
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
        if ( time >= PMStartTime && time < PMEndTime )
        {
            return 1;
        }
        return 2;
    }

    private void LoadCosts(OdMatrixWriter<IZone> writer, string fileName, int i)
    {
        if ( Path.GetExtension( fileName ) == ".311" )
        {
            writer.LoadEmme2( FailIfNotExist( fileName ), i, (int)AutoDataTypes.CarCost );
        }

        else
        {
            writer.LoadCsvTimes( FailIfNotExist( fileName ), HeaderBoolean, i, (int)AutoDataTypes.CarCost );
        }
    }

    private void LoadTimes(OdMatrixWriter<IZone> writer, string fileName, int i)
    {
        if ( Path.GetExtension( fileName ) == ".311" )
        {
            writer.LoadEmme2( FailIfNotExist( fileName ), i, (int)AutoDataTypes.TravelTime );
        }
        else
        {
            writer.LoadCsvTimes( FailIfNotExist( fileName ), HeaderBoolean, i, (int)AutoDataTypes.TravelTime );
        }
    }

    private float[] ProcessLoadedData(SparseTwinIndex<float[]> loadedData, int types, int times)
    {
        var flatLoadedData = loadedData.GetFlatData();
        var dataEntries = DataEntries = times * types;
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var zones = zoneArray.GetFlatData();
        NumberOfZones = zones.Length;
        var ret = new float[zones.Length * zones.Length * types * times];
        Parallel.For( 0, flatLoadedData.Length, i =>
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
        Dispose( true );
        GC.SuppressFinalize( this );
    }

    protected virtual void Dispose(bool all)
    {
        if ( Data != null )
        {
            Data.Dispose();
            Data = null;
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