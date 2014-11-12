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
using System.Diagnostics;
using System.IO;
using XTMF;
using TMG;

namespace James.UTDM
{
    public class TimeSparseTwinIndexLookups : IPurpose
    {
        [RootModule]
        public I4StepModel Root;

        [RunParameter( "Purpose Name", "TestSparsePurpose", "The name of the purpose" )]
        public string PurposeName
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The data source we will read from.", Required = true )]
        public INetworkData Data;

        public void Run()
        {

            Data.LoadData();
            var watch = new Stopwatch();
            int iterations = 10;
            var flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            Time time = new Time() { Hours = 7 };
            float denominator = iterations * numberOfZones * numberOfZones;
            watch.Start();
            for ( int it = 0; it < iterations; it++ )
            {
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        this.Data.TravelTime( flatZones[i], flatZones[j], time );
                    }
                }
                this.Progress = it / (float)iterations;
            }
            watch.Stop();
            WriteFile( watch.ElapsedMilliseconds );
            Data.UnloadData();
        }

        private void WriteFile(float p)
        {
            using ( StreamWriter writer = new StreamWriter( "MatrixScanTime.txt" ) )
            {
                writer.WriteLine( "Total lookup time = " + p + "ms." );
            }
        }

        public List<TreeData<float[][]>> Flows
        {
            get;
            set;
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
            get { return new Tuple<byte, byte, byte>( 50, 50, 150 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [DoNotAutomate]
        public IMultiModeSplit ModeSplit
        {
            get
            {
                return null;
            }
            set
            {
                
            }
        }
    }
}
