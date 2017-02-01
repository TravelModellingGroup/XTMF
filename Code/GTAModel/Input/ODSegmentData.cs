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
using System.Threading;
using Datastructure;
using TMG.GTAModel.DataUtility;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    [ModuleInformation( Description = "This module provides floating point data segmented by origin and destination planning districts." )]
    public class ODSegmentData : IODDataSource<float[]>
    {
        [RunParameter( "Data Elements", "1,2", typeof( NumberList ), "What are the data indexes for segment information?" )]
        public NumberList DataElements;

        [RunParameter( "Reasion Indexes", "0,1,2,3,4,5,6,7", typeof( NumberList ), "What are the indexes for reasons?" )]
        public NumberList ReasonIndexes;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Complete Data", true, "The data contained in the Segment information is a complete set, set this to false if not everything is defined." )]
        public bool SegmentDataIsComplete;

        [SubModelInformation( Description = "Used to load in the data that defines a segment.", Required = true )]
        public IDataLineSource<float[]> SegmentDefinitions;

        [SubModelInformation( Description = "Reads the data payload for a segment.", Required = true )]
        public IDataSource<SparseTriIndex<float>> SegmentInformation;

        private SegmentData[] Segments;

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
            get { return null; }
        }

        public float[] GetDataFrom(int origin, int destination, int reason = 0)
        {
            if ( Segments == null )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( Segments == null )
                    {
                        // load in the data here
                        LoadData();
                        Thread.MemoryBarrier();
                    }
                }
            }
            SegmentData segment;
            if ( !GetSegment( origin, destination, out segment ) )
            {
                return null;
            }
            // GET DATA
            var numberOfDataElements = DataElements.Count;
            float[] data = new float[numberOfDataElements];
            for ( int dataElement = 0; dataElement < numberOfDataElements; dataElement++ )
            {
                data[dataElement] = segment.Data[reason * numberOfDataElements + dataElement];
            }
            return data;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private bool GetSegment(int origin, int destination, out SegmentData ret)
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var originPD = zoneArray[origin].PlanningDistrict;
            var destinationPD = zoneArray[destination].PlanningDistrict;
            ret = default( SegmentData );
            for ( int i = 0; i < Segments.Length; i++ )
            {
                if ( Segments[i].OriginRange.ContainsInclusive( originPD ) )
                {
                    if ( Segments[i].DestinationRange.ContainsInclusive( destinationPD ) )
                    {
                        ret = Segments[i];
                        return true;
                    }
                }
            }
            return false;
        }

        private void IncludeSegmentPayload(SegmentData[] segmentArray)
        {
            SegmentInformation.LoadData();
            var rawData = SegmentInformation.GiveData();
            var ammountOfData = DataElements.Count * ReasonIndexes.Count;
            var numberOfReasons = ReasonIndexes.Count;
            var numberOfData = DataElements.Count;
            for ( int i = 0; i < segmentArray.Length; i++ )
            {
                var data = segmentArray[i].Data = new float[ammountOfData];
                for ( int reason = 0; reason < numberOfReasons; reason++ )
                {
                    for ( int dataElement = 0; dataElement < numberOfData; dataElement++ )
                    {
                        if ( rawData.ContainsIndex( DataElements[dataElement], segmentArray[i].SegmentNumber, ReasonIndexes[reason] ) )
                        {
                            data[reason * numberOfData + dataElement] = rawData[DataElements[dataElement], segmentArray[i].SegmentNumber, ReasonIndexes[reason]];
                        }
                        else if ( SegmentDataIsComplete )
                        {
                            // if the user says all of the data is here but it is not throw an exception
                            SegmentInformation.UnloadData();
                            throw new XTMFRuntimeException( "In '" + Name + "' there was no data for Segment# " + segmentArray[i].SegmentNumber + " @" + ReasonIndexes[reason]
                                + ":" + DataElements[dataElement] + "!  Please check '" + SegmentInformation.Name + "' to make sure it is loading the right data." );
                        }
                    }
                }
            }
            SegmentInformation.UnloadData();
        }

        private void LoadData()
        {
            // Load in the spatial data
            List<SegmentData> data = new List<SegmentData>( 20 );
            var segmentArray = LoadInSegmentData( data );
            // now load in the payloads
            IncludeSegmentPayload( segmentArray );
            Segments = segmentArray;
        }

        private SegmentData[] LoadInSegmentData(List<SegmentData> data)
        {
            foreach ( var line in SegmentDefinitions.Read() )
            {
                data.Add( new SegmentData
                {
                    SegmentNumber = (int)line[0],
                    OriginRange = new Range((int)line[1], (int)line[2]),
                    DestinationRange = new Range((int)line[3], (int)line[4])
                } );
            }
            var segmentArray = data.ToArray();
            return segmentArray;
        }

        private struct SegmentData
        {
            internal float[] Data;
            internal Range DestinationRange;
            internal Range OriginRange;
            internal int SegmentNumber;

            public override string ToString()
            {
                return SegmentNumber + " : " + OriginRange + " -> " + DestinationRange;
            }
        }
    }
}