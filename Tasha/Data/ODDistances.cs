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
using Datastructure;
using TMG;
using XTMF;
using TMG.Functions;
namespace Tasha.Data
{
    [ModuleInformation(Description = "This module provides an OD matrix with the distances between zonal centroids.")]
    public class ODDistances : IDataSource<SparseTwinIndex<float>>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Convert to KM", false, "Should we divide the distances by 1k to convert to KM?")]
        public bool ConvertToKM;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {

            if (!Root.ZoneSystem.Loaded)
            {
                Root.ZoneSystem.LoadData();
            }

            SparseTwinIndex<float> distances = Root.ZoneSystem.Distances;
            if (!ConvertToKM)
            {
                Data = distances;
            }
            else
            {
                var flatDistances = distances.GetFlatData();
                Data = distances.CreateSimilarArray<float>();
                var local = Data.GetFlatData();
                for (int i = 0; i < local.Length; i++)
                {
                    VectorHelper.Multiply(local[i], 0, flatDistances[i], 0, 0.001f, local.Length);
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
        }
    }

}
