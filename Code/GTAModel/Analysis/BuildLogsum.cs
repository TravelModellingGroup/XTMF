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
using TMG.Functions;
using XTMF;
using Datastructure;
using TMG.Input;
using System.Threading.Tasks;

namespace TMG.GTAModel.Analysis
{
    [ModuleInformation( Description =
        @"This module is designed to read in a number of OD data sources that represent utilities and to perform an OD logsum on them.  The result will be a square CSV matrix of the combined values." )]
    public class BuildLogsum : ISelfContainedModule
    {
        [RootModule]
        public ITravelDemandModel Root;

        [ModuleInformation(Description = "An entry contains a data source to read in and add to our total.")]
        public class Entry : IModule
        {
            [SubModelInformation(Required = false, Description = "The data source to read in.")]
            public IReadODData<float> DataSource;

            [RunParameter("Raise To E", true, "Should we raise the read in values and add them after converting them to e^x, or just assume they have already in that format?")]
            public bool RaiseToE;

            public string Name { get; set; }

            public float Progress { get; private set;}

            public Tuple<byte, byte, byte> ProgressColour { get; private set; }

            public void Add(SparseTwinIndex<float> data)
            {
                var flatData = data.GetFlatData();
                if ( this.RaiseToE )
                {
                    foreach ( var entry in this.DataSource.Read() )
                    {
                        var o = data.GetFlatIndex( entry.O );
                        var d = data.GetFlatIndex( entry.D );
                        if ( o >= 0 & d >= 0 )
                        {
                            flatData[o][d] += (float)Math.Exp( entry.Data );
                        }
                    }
                }
                else
                {
                    foreach ( var entry in this.DataSource.Read() )
                    {
                        var o = data.GetFlatIndex( entry.O );
                        var d = data.GetFlatIndex( entry.D );
                        if ( o >= 0 & d >= 0 )
                        {
                            flatData[o][d] += (float)entry.Data;
                        }
                    }
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The data to read in.")]
        public Entry[] Entries;

        [SubModelInformation( Required = true, Description = "The location to save the output matrix (csv)." )]
        public FileLocation OutputFile;

        public void Start()
        {
            var data = this.Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var sources = this.Entries;
            for ( int i = 0; i < sources.Length; i++ )
            {
                sources[i].Add( data );
            }
            LogTheMatrix( data );
            SaveData.SaveMatrix( data, this.OutputFile );
        }

        /// <summary>
        /// Take the log of each data point in the matrix.
        /// </summary>
        /// <param name="data">The data source to do the log on.</param>
        private static void LogTheMatrix(SparseTwinIndex<float> data)
        {
            var flatData = data.GetFlatData();
            Parallel.For( 0, flatData.Length, (int i) =>
            {
                var row = flatData[i];
                if ( row == null ) return;
                for ( int j = 0; j < row.Length; j++ )
                {
                    row[j] = (float)Math.Log( row[j] );
                }
            } );
        }

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

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
