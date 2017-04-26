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
using System.Windows.Forms;
using Datastructure;
using XTMF;
using TMG;
using TMG.Input;
namespace Tasha.Data
{
    [ModuleInformation( Description =
        @"This module is designed to take in information from the reader
and store it into a SparseArray<float> that is of the same type as the model system's zone system.
Data outside of the zones that are defined is trimmed off. Both the Origin and Destination from the Reader will be used." )]
    public class ZoneODInformation : IDataSource<SparseTwinIndex<float>>
    {
        [SubModelInformation( Required = false, Description = "Origin, Destination will be used to store data." )]
        public IReadODData<float> Reader;

        [RootModule]
        public ITravelDemandModel Root;

        private SparseTwinIndex<float> Data;

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
            var data = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            if ( Reader != null )
            {
                foreach ( var point in Reader.Read() )
                {
                    if ( data.ContainsIndex( point.O, point.D ) )
                    {
                        data[point.O, point.D] = point.Data;
                    }
                }
            }
            Data = data;
        }

        public void UnloadData()
        {
            Data = null;
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

            if (Root.ZoneSystem == null)
            {
                error = $"No Zone OD data specified or loaded in root demand model {Root}.";
                return false;
            }
            return true;
        }
    }
}
