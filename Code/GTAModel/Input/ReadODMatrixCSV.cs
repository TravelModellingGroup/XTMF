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
    public class ReadODMatrixCSV : IReadODData<float>
    {
        [RunParameter( "File Name", "data.csv", typeof( FileFromInputDirectory ), "The file to read in.  If UseInputDirectory is false we will use the run directory instead." )]
        public FileFromInputDirectory FileName;

        [RootModule]
        public ITravelDemandModel Root;

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
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            using ( CsvReader reader = new CsvReader( this.FileName.GetFileName( UseInputDirectory ? this.Root.InputBaseDirectory : "." ) ) )
            {
                ODData<float> point;
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
                    reader.Get( out point.O, 0 );
                    for ( int i = 1; i < length; i++ )
                    {
                        point.D = zones[i - 1].ZoneNumber;
                        reader.Get( out point.Data, i );
                        yield return point;
                    }
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}