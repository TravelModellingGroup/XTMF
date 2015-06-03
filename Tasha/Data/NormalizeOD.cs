/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;
namespace Tasha.Data
{
    [ModuleInformation(Description =
        @"This module is designed to normalize the matrix so it sums to 1.")]
    public class NormalizeOD : IDataSource<SparseTwinIndex<float>>
    {
        private SparseTwinIndex<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "The matrix to normalize")]
        public IResource ToNormalize;

        public SparseTwinIndex<float> GiveData()
        {
            return this.Data;
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        public void LoadData()
        {
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var firstRate = this.ToNormalize.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            SparseTwinIndex<float> data;
            data = zoneArray.CreateSquareTwinArray<float>();
            var flatData = data.GetFlatData();
            if(VectorHelper.IsHardwareAccelerated)
            {
                float sum = 0.0f;
                for(int i = 0; i < firstRate.Length; i++)
                {
                    sum += VectorHelper.VectorSum(firstRate[i], 0, firstRate.Length);
                }
                for(int i = 0; i < flatData.Length; i++)
                {
                    VectorHelper.VectorMultiply(flatData[i], 0, firstRate[i], 0, 1.0f / sum, flatData[i].Length );
                }
            }
            else
            {
                float sum = 0.0f;
                for(int i = 0; i < flatData.Length; i++)
                {
                    for(int j = 0; i < flatData[j].Length; i++)
                    {
                        sum += firstRate[i][j];
                    }
                }
                var invSum = 1.0f / sum;
                for(int i = 0; i < flatData.Length; i++)
                {
                    for(int j = 0; j < flatData[i].Length; j++)
                    {
                        flatData[i][j] = firstRate[i][j] * invSum;
                    }
                }
            }
            this.Data = data;
        }

        public void UnloadData()
        {
            this.Data = null;
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
            if(!this.ToNormalize.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + this.Name + "' the first rates resource is not of type SparseTwinIndex<float>!";
                return false;
            }
            return true;
        }
    }
}
