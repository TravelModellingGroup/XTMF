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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class ReadODMatrixCSVSeries : IReadODData<float>
    {
        [RunParameter( "File Name", "data.csv", typeof( FileFromInputDirectory ), "The base file name to read in.  If UseInputDirectory is false we will use the run directory instead." )]
        public FileFromInputDirectory FileName;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Series Size", 0, "The number of files in this series" )]
        public int SeriesSize;

        [RunParameter( "Starting Index", 0, "The index to start from for the series." )]
        public int StartingIndex;

        [RunParameter( "Use Input Directory", false, "Should we use the model system's input directory as a base?" )]
        public bool UseInputDirectory;

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

        public IEnumerable<ODData<float>> Read()
        {
            var sparseZones = Root.ZoneSystem.ZoneArray;
            var zones = sparseZones.GetFlatData();
            var ret = sparseZones.CreateSquareTwinArray<float>();

            LoadData( zones, ret );
            // only after all of the files have been finished will this run
            var flatRet = ret.GetFlatData();
            ODData<float> point;
            for ( int i = 0; i < zones.Length; i++ )
            {
                point.O = zones[i].ZoneNumber;
                var row = flatRet[i];
                for ( int j = 0; j < zones.Length; j++ )
                {
                    point.D = zones[j].ZoneNumber;
                    point.Data = row[j];
                    yield return point;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private string GetFileName(int i)
        {
            return ( FileName.GetFileName( UseInputDirectory ? Root.InputBaseDirectory : "." ) + ( i + StartingIndex ) + ".csv" );
        }

        private void LoadData(IZone[] zones, SparseTwinIndex<float> ret)
        {
            for ( int i = 0; i < SeriesSize; i++ )
            {
                ReadFile( GetFileName( i ), zones, ret.GetFlatData() );
            }
        }

        private void ReadFile(string fileName, IZone[] zones, float[][] matrix)
        {
            using ( CsvReader reader = new CsvReader( fileName ) )
            {
                var rowCount = 0;
                int length;
                // burn header
                reader.LoadLine();
                // now read in data
                while ( !reader.EndOfFile )
                {
                    length = reader.LoadLine();
                    if ( length != zones.Length + 1 )
                    {
                        continue;
                    }
                    if ( rowCount >= matrix.Length )
                    {
                        throw new XTMFRuntimeException( "In '" + Name + "' when reading in the file '" + fileName + "' there were more rows (" + rowCount + ") than zones in the zone system!(" + zones.Length + ")" );
                    }
                    var row = matrix[rowCount++];
                    for ( int i = 0; i < row.Length; i++ )
                    {
                        float temp;
                        reader.Get( out temp, i + 1 );
                        row[i] += temp;
                    }
                }
            }
        }
    }
}