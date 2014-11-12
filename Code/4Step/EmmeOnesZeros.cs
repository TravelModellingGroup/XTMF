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
using XTMF;
using TMG;

namespace James.UTDM
{
    public class EmmeOnesZeros : IModelSystemTemplate
    {
        [SubModelInformation( Description = "What zone system to create the .311's for", Required = true )]
        public IZoneSystem ZoneSystem;


        [RunParameter("Input Directory", "../../Input", "The directory to read from.")]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            this.ZoneSystem.LoadData();

            var zones = this.ZoneSystem.ZoneArray.GetFlatData();

            this.WriteFile( "Zeros.311", '0', zones );
            this.WriteFile( "Ones.311", '1', zones );

            this.ZoneSystem.UnloadData();
        }

        private void WriteFile(string fileName, char number, IZone[] zones)
        {
            using ( StreamWriter writer = new StreamWriter( fileName ) )
            {
                writer.WriteLine( "t matrices" );
                writer.WriteLine( "a matrix=mf10  AOD      0 auto Demand" );
                StringBuilder originString = new StringBuilder(10);
                for ( int i = 0; i < zones.Length; i++ )
                {
                    int count = 0;
                    originString.Clear();
                    originString.AppendFormat( "{0,7}", zones[i].ZoneNumber );
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        if ( ( count++ ) == 0 )
                        {
                            writer.Write( originString );
                            writer.Write( "{0,7}:{1,5}", zones[j].ZoneNumber, number );
                        }
                        else
                        {
                            writer.Write( "{0,7}:{1,5}", zones[j].ZoneNumber, number );
                            if ( count >= 5 )
                            {
                                count = 0;
                                writer.WriteLine();
                            }
                        }
                    }
                    if(count != 0)
                    {
                        count = 0;
                        writer.WriteLine();
                    }
                }
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 100, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
