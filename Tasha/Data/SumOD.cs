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
using XTMF;
using Datastructure;
using TMG.Functions;
namespace Tasha.Data
{

    public class SumOD : IDataSource<float>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private float Data;

        [SubModelInformation(Required = false, Description = "Sum a raw data source, only one can be used.")]
        public IDataSource<SparseTwinIndex<float>> RawDataSource;

        [SubModelInformation(Required = false, Description = "Sum a raw resource, only one can be used.")]
        public IResource ResourceDataSource;

        public float GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            float[][] operateOnMe = ModuleHelper.GetDataFromDatasourceOrResource(RawDataSource, ResourceDataSource, RawDataSource != null).GetFlatData();
            var sum = 0.0f;
            for (int i = 0; i < operateOnMe.Length; i++)
            {
                sum += VectorHelper.Sum(operateOnMe[i], 0, operateOnMe[i].Length);
            }
            Data = sum;
            Loaded = true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return this.EnsureExactlyOneAndOfSameType(RawDataSource, ResourceDataSource, ref error);
        }

        public void UnloadData()
        {
            Data = 0f;
            Loaded = false;
        }
    }

}
