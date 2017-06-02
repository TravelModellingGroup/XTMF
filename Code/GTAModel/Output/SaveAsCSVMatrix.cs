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
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Output
{
    public class SaveAsCSVMatrix : ISaveODData<float>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {

    
            if (Root.ZoneSystem == null)
            {
                error = $"No Zone OD data specified or loaded in root demand model {Root}.";
                return false;
            }
            return true;
        }

        public void SaveMatrix(SparseTwinIndex<float> matrix, string fileName)
        {
            SaveData.SaveMatrix( matrix, fileName );
        }

        public void SaveMatrix(float[][] data, string fileName)
        {
            SaveData.SaveMatrix( Root.ZoneSystem.ZoneArray.GetFlatData(), data, fileName );
        }

        public void SaveMatrix(float[] data, string fileName)
        {

      
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            StringBuilder header = null;
            StringBuilder[] zoneLines = new StringBuilder[zones.Length];
            Parallel.Invoke(
                () =>
                {
                    var dir = Path.GetDirectoryName( fileName );
                    if ( !String.IsNullOrWhiteSpace( dir ) )
                    {
                        if ( !Directory.Exists( dir ) )
                        {
                            Directory.CreateDirectory( dir );
                        }
                    }
                },
                () =>
                {
                    header = new StringBuilder();
                    header.Append( "Zones O\\D" );
                    for ( int i = 0; i < zones.Length; i++ )
                    {
                        header.Append( ',' );
                        header.Append( zones[i].ZoneNumber );
                    }
                },
                () =>
                {
                    Parallel.For( 0, zones.Length, i =>
                    {
                        zoneLines[i] = new StringBuilder();
                        zoneLines[i].Append( zones[i].ZoneNumber );
                        int offset = zones.Length * i;
                        for ( int j = 0; j < zones.Length; j++ )
                        {
                            zoneLines[i].Append( ',' );
                            zoneLines[i].Append( data[j + offset] );
                        }
                    } );
                } );
            using ( StreamWriter writer = new StreamWriter( fileName ) )
            {
                writer.WriteLine( header );
                for ( int i = 0; i < zoneLines.Length; i++ )
                {
                    writer.WriteLine( zoneLines[i] );
                }
            }
        }
    }
}