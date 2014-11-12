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
using System.Threading.Tasks;
using Datastructure;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Tally
{
    public class StationAccessTally : DirectModeAggregationTally
    {
        [RunParameter( "Count From Origin", true, "Should we be tallying from the origin to the intermediate zone" +
            "\r\nor should we be counting from the intermediate zone to the destination?" )]
        public bool CountFromOrigin;

        [RunParameter( "Count Line Hull", false, "Should we be tallying the line hull (access station to egress station).\r\nThis option takes priority over Count From Origin." )]
        public bool LineHull;

        [RunParameter( "Simulation Time", "7:00AM", typeof( Time ), "The time of day to use for the split." )]
        public Time Time;

        public override void IncludeTally(float[][] currentTally)
        {
            var purposes = this.Root.Purpose;
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            for ( int purp = 0; purp < this.PurposeIndexes.Length; purp++ )
            {
                var purpose = purposes[purp];
                for ( int m = 0; m < this.ModeIndexes.Length; m++ )
                {
                    var data = GetResult( purpose.Flows, this.ModeIndexes[m] );
                    if ( data == null ) continue;
                    var mode = GetMode( this.ModeIndexes[m] ) as IStationCollectionMode;
                    if ( this.LineHull )
                    {
                        ComputeLineHull( currentTally, zoneArray, zones, numberOfZones, m, data );
                    }
                    else
                    {
                        mode.Access = this.CountFromOrigin;
                        if ( this.CountFromOrigin )
                        {
                            ComputeFromOrigin( currentTally, zoneArray, zones, numberOfZones, m, data );
                        }
                        else
                        {
                            ComputeFromDestination( currentTally, zoneArray, zones, numberOfZones, m, data );
                        }
                    }
                }
            }
        }

        private void ComputeFromDestination(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int numberOfZones, int m, float[][] data)
        {
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int j)
                {
                    for ( int i = 0; i < numberOfZones; i++ )
                    {
                        if ( data[i] == null || data[i][j] <= 0f ) continue;
                        var choices = GetStationChoiceSplit( m, zones[i], zones[j] );
                        if ( choices == null ) continue;
                        // check for egress stations first
                        var stationZones = choices.Item2;
                        if ( stationZones == null )
                        {
                            // if there are no egress stations, use the access stations
                            stationZones = choices.Item1;
                        }
                        var splits = choices.Item3;
                        var totalTrips = data[i][j];
                        var totalSplits = 0f;
                        for ( int z = 0; z < stationZones.Length; z++ )
                        {
                            if ( stationZones[z] != null )
                            {
                                totalSplits += splits[z];
                            }
                        }
                        for ( int z = 0; z < stationZones.Length; z++ )
                        {
                            if ( stationZones[z] == null ) break;
                            var flatZoneNumber = zoneArray.GetFlatIndex( stationZones[z].ZoneNumber );
                            if ( currentTally[flatZoneNumber] == null )
                            {
                                lock ( data )
                                {
                                    System.Threading.Thread.MemoryBarrier();
                                    if ( currentTally[flatZoneNumber] == null )
                                    {
                                        currentTally[flatZoneNumber] = new float[numberOfZones];
                                    }
                                }
                            }
                            // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                            currentTally[flatZoneNumber][j] += totalTrips * ( splits[z] / totalSplits );
                        }
                    }
                } );
        }

        private void ComputeFromOrigin(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int numberOfZones, int m, float[][] data)
        {
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    if ( data[i] == null ) return;
                    var tallyRow = currentTally[i];
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        var totalTrips = data[i][j];
                        if ( totalTrips <= 0f ) continue;
                        var choices = GetStationChoiceSplit( m, zones[i], zones[j] );
                        if ( choices == null ) continue;
                        var stationZones = choices.Item1;
                        var splits = choices.Item3;
                        var totalSplits = 0f;
                        for ( int z = 0; z < stationZones.Length; z++ )
                        {
                            if ( stationZones[z] != null )
                            {
                                totalSplits += splits[z];
                            }
                        }
                        for ( int z = 0; z < stationZones.Length; z++ )
                        {
                            if ( stationZones[z] == null ) break;
                            var flatZoneNumber = zoneArray.GetFlatIndex( stationZones[z].ZoneNumber );
                            // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                            tallyRow[flatZoneNumber] += totalTrips * ( splits[z] / totalSplits );
                        }
                    }
                } );
        }

        private void ComputeLineHull(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int numberOfZones, int m, float[][] data)
        {
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                 delegate(int i)
                 {
                     if ( data[i] == null ) return;
                     for ( int j = 0; j < numberOfZones; j++ )
                     {
                         var totalTrips = data[i][j];
                         if ( totalTrips <= 0f )
                         {
                             continue;
                         }
                         var choices = GetStationChoiceSplit( m, zones[i], zones[j] );
                         if ( choices == null )
                         {
                             continue;
                         }
                         var accessStations = choices.Item1;
                         var egressStations = choices.Item2;
                         if ( egressStations == null )
                         {
                             continue;
                         }
                         var splits = choices.Item3;
                         var totalSplits = 0f;
                         for ( int z = 0; z < accessStations.Length; z++ )
                         {
                             if ( accessStations[z] != null )
                             {
                                 totalSplits += splits[z];
                             }
                         }
                         for ( int z = 0; z < accessStations.Length; z++ )
                         {
                             if ( accessStations[z] == null | egressStations[z] == null ) break;
                             var accessZoneNumber = zoneArray.GetFlatIndex( accessStations[z].ZoneNumber );
                             var egressZoneNumber = zoneArray.GetFlatIndex( egressStations[z].ZoneNumber );
                             // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                             currentTally[accessZoneNumber][egressZoneNumber] += totalTrips * ( splits[z] / totalSplits );
                         }
                     }
                 } );
        }

        private Tuple<IZone[], IZone[], float[]> GetStationChoiceSplit(int m, IZone origin, IZone destination)
        {
            // this mode is our base
            var mode = GetMode( this.ModeIndexes[m] );
            var cat = mode as IStationCollectionMode;
            if ( cat == null )
            {
                throw new XTMFRuntimeException( "The mode '" + mode.ModeName
                    + "' is not an TMG.Modes.IStationCollectionMode and can not be used with a StationAccessTally!" );
            }
            return cat.GetSubchoiceSplit( origin, destination, this.Time );
        }
    }
}