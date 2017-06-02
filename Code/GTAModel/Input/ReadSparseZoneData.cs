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
using System.IO;
using Datastructure;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Input
{
    [ModuleInformation( Description =
        ""
        )]
    public class ReadSparseZoneData : IDataSource<SparseTwinIndex<float>>
    {
        [RunParameter( "File Name", "Data.csv", "The name of the file to load." )]
        public string FileName;

        [RunParameter( "Header Lines", 1, "The number of lines that represent the header for the file." )]
        public int HeaderLines;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Sparse Space", "0,1,2,3", typeof( NumberList ), "For each column a definition of where it exists in sparse space,"
        + " the first column is always the zone." )]
        public NumberList SparseSpace;

        private SparseTwinIndex<float> Data;

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

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        public void LoadData()
        {
            // First create the data space
            List<int> zoneNumbers = new List<int>();
            var sparseLength = SparseSpace.Count;
            List<float>[] dataColumns = new List<float>[sparseLength];
            LoadInData( zoneNumbers, sparseLength, dataColumns );
            Data = CreateSparseData( zoneNumbers, sparseLength, dataColumns );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Data = null;
        }

        protected string GetInputFileName(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private SparseTwinIndex<float> CreateSparseData(List<int> zoneNumbers, int sparseLength, List<float>[] dataColumns)
        {
            var numberOfZoneEntries = zoneNumbers.Count;
            var entries = numberOfZoneEntries * sparseLength;
            var first = new int[entries];
            var second = new int[entries];
            var data = new float[entries];
            int pos = 0;
            for ( int dataEntry = 0; dataEntry < numberOfZoneEntries; dataEntry++ )
            {
                var zoneNumber = zoneNumbers[dataEntry];
                var dataRow = dataColumns[dataEntry];
                for ( int k = 0; k < sparseLength; k++ )
                {
                    first[pos] = zoneNumber;
                    second[pos] = SparseSpace[k];
                    data[pos] = dataRow[k];
                    pos++;
                }
            }
            return SparseTwinIndex<float>.CreateTwinIndex( first, second, data );
        }

        private void LoadInData(List<int> zoneNumbers, int sparseLength, List<float>[] dataColumns)
        {
            // now that we have the data to load into, open up the file
            using ( CsvReader reader = new CsvReader( GetInputFileName( FileName ) ) )
            {
                // burn all of the header lines
                for ( int headerLine = 0; headerLine < HeaderLines; headerLine++ )
                {
                    reader.LoadLine();
                }
                // now that we are at the data it is time to start building our data
                while ( !reader.EndOfFile )
                {
                    int zone;
                    // read in the next line and make sure that it contains enough information
                    if ( reader.LoadLine() < sparseLength + 1 ) continue;
                    // if we have enough data read it in, the first column is always the 'zone'
                    reader.Get( out zone, 0 );
                    zoneNumbers.Add( zone );
                    // now read in the data for this 'zone'
                    for ( int i = 0; i < sparseLength; i++ )
                    {
                        float dataPoint;
                        reader.Get( out dataPoint, 1 + i );
                        dataColumns[i].Add( dataPoint );
                    }
                }
            }
        }
    }
}