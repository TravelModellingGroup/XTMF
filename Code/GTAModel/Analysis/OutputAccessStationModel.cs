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
using System.Linq;
using System.Text;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    [ModuleInformation( Description =
        @"This module is designed to output the access station choice data for GTAModel." )]
    public sealed class OutputAccessStationModel : ISelfContainedModule
    {
        [SubModelInformation( Required = true, Description = "The resource containing the access station model data to output." )]
        public IResource AccessStationData;

        [SubModelInformation( Required = true, Description = "The location to save the data." )]
        public FileLocation OutputFile;

        public void Start()
        {
            var data = AccessStationData.AcquireResource<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>();
            var flatData = data.GetFlatData();
            using ( var writer = new StreamWriter( OutputFile ) )
            {
                //output the header
                var validIndex = data.ValidIndexArray();
                writer.WriteLine( "Origin,Destination,AccessStation[0],AccessStation[1],AccessStation[2],AccessStation[3],AccessStation[4],Utility[0],Utility[1],Utility[2],Utility[3],Utility[4],Logsum" );
                for ( int o = 0; o < validIndex.Length; o++ )
                {
                    var row = flatData[o];
                    for ( int d = 0; d < validIndex.Length; d++ )
                    {
                        var result = row[d];
                        // if there is no data, just skip it
                        if ( result == null ) continue;
                        var zones = result.Item1;
                        var utils = result.Item3;
                        writer.Write( validIndex[o] );
                        writer.Write( ',' );
                        writer.Write( validIndex[d] );
                        for ( int i = 0; i < zones.Length; i++ )
                        {
                            writer.Write( ',' );
                            // make sure this zone is used
                            writer.Write( zones[i] == null ? -1 : zones[i].ZoneNumber );
                        }
                        var total = 0.0f;
                        for ( int i = 0; i < zones.Length; i++ )
                        {
                            // make sure this zone is used
                            writer.Write( ',' );
                            var ammount = zones[i] == null ? 0.0f : utils[i];
                            writer.Write( ammount );
                            total += ammount;
                        }
                        writer.Write( ',' );
                        writer.WriteLine( Math.Log( total ) );
                    }
                }
            }
        }

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
            if ( !AccessStationData.CheckResourceType<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>() )
            {
                error = "In '" + Name + "' the access station data is not connecting to a resource of type SparseTwinIndex<Tuple<IZone[], float[]>>!";
                return false;
            }
            return true;
        }
    }
}
