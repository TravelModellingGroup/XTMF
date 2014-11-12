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
using System.Threading.Tasks;
using XTMF;
using TMG;
using TMG.Functions;
using Datastructure;

namespace James.UTDM
{
    public class CalculateDistances : IModelSystemTemplate
    {

        [SubModelInformation( Description = "Zone System", Required = true )]
        public IZoneSystem ZoneSystem;

        [RunParameter( "Output File", "ZoneDistances.csv", "The location that we will save the zone distances into." )]
        public string OutputFile;

        public void Start()
        {
            this.ZoneSystem.LoadData();
            var zoneArray = this.ZoneSystem.ZoneArray;
            var validIndexes = zoneArray.ValidIndexies().ToArray();
            var numberOfZones = validIndexes.Length;
            float[] ZoneX = new float[numberOfZones];
            float[] ZoneY = new float[numberOfZones];
            var distances = zoneArray.CreateSquareTwinArray<float>();
            float increment = 0.9f / numberOfZones;
            Parallel.For( 0, numberOfZones,
                delegate(int i)
                {
                    var zone = zoneArray[validIndexes[i]];
                    ZoneX[i] = zone.X;
                    ZoneY[i] = zone.Y;
                } );
            Parallel.For( 0, numberOfZones,
                delegate(int i)
                {
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        distances[validIndexes[i], validIndexes[j]] = (float)Math.Sqrt( Math.Abs( ZoneX[i] - ZoneX[j] ) + Math.Abs( ZoneY[i] - ZoneY[j] ) );
                    }
                    this.Progress += increment;
                } );
            this.Progress = 0.9f;
            using ( StreamWriter writer = new StreamWriter( OutputFile ) )
            {
                writer.WriteLine( "Origin,Destination,Distance" );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        writer.Write( validIndexes[i] );
                        writer.Write( ',' );
                        writer.Write( validIndexes[j] );
                        writer.Write( ',' );
                        writer.WriteLine( distances[validIndexes[i], validIndexes[j]] );
                    }
                }
            }
            this.Progress = 1;
            this.ZoneSystem.UnloadData();
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

        private static Tuple<byte, byte, byte> ProgressColourT = new Tuple<byte, byte, byte>( 100, 200, 100 );
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return ProgressColourT; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [RunParameter( "Input Directory", "../../Input", "The base input directory" )]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        public bool ExitRequest()
        {
            return false;
        }
    }
}
