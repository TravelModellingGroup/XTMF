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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class ReadFlatODMatrix : IReadODData<float>
    {
        [RunParameter( "File Name", "Data.bin", "The flat binary file containing a matrix to load in." )]
        public string FileName;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Load from Input Directory", false, "Load from the model system's input directory?  False means use the run directory." )]
        public bool UseInputDirectory;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<ODData<float>> Read()
        {
            IZone[] zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            Stream s = null;
            var f = this.UseInputDirectory ? this.GetFileLocation( this.FileName ) : this.FileName;
            try
            {
                s = File.OpenRead( f );
            }
            catch ( IOException e )
            {
                s.Close();
                throw new XTMFRuntimeException( e.Message );
            }

            if ( s.Length < ( numberOfZones * numberOfZones ) * 4 )
            {
                throw new XTMFRuntimeException( "The file '" + f + "' does not contain enough data to be used as a flat OD matrix!" );
            }
            ODData<float> ret = new ODData<float>();
            // this will close the file at the end of reading it
            using ( BinaryReader reader = new BinaryReader( s ) )
            {
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    ret.O = zones[i].ZoneNumber;
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        ret.D = zones[j].ZoneNumber;
                        ret.Data = reader.ReadSingle();
                        yield return ret;
                    }
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private string GetFileLocation(string fileName)
        {
            var fullPath = fileName;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }
    }
}