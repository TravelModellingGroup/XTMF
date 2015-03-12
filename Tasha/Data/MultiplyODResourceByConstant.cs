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
using Datastructure;
using XTMF;
using TMG.Functions.VectorHelper;
namespace Tasha.Data
{

    public class MultiplyODResourceByConstant : IDataSource<SparseTwinIndex<float>>
    {
        [SubModelInformation(Required = true, Description = "The resource to multiply")]
        public IResource ResourceToMultiply;

        [RunParameter("Factor", 1.0f, "The factor to multiply the rates by in order to produce our results.")]
        public float Factor;

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> Data;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var resource = ResourceToMultiply.AquireResource<SparseTwinIndex<float>>();
            var otherData = resource.GetFlatData();
            var ourResource = resource.CreateSimilarArray<float>();
            var data = ourResource.GetFlatData();
            if(IsHardwareAccelerated)
            {
                for(int i = 0; i < data.Length; i++)
                {
                    VectorMultiply(data[i], 0, otherData[i], 0, Factor, data[i].Length);
                }
            }
            else
            {
                for(int i = 0; i < data.Length; i++)
                {
                    var row = data[i];
                    var otherRow = otherData[i];
                    for(int j = 0; j < row.Length; j++)
                    {
                        row[j] = otherRow[j] * Factor;
                    }
                }
            }
            Data = ourResource;
            Loaded = true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!ResourceToMultiply.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the resource was not of type SparseTwinIndex<float>!";
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Data = null;
            Loaded = false;
        }
    }

}
