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
using TMG;

namespace Tasha.Data
{
    [ModuleInformation(
        Description =
@"This module is designed to take in two resources and normalize the input data by row so that it will sum to the row total of the second resource.  The resource that we are going
to normalize to can be either a SparseTwinIndex<float> (matrix) or a SparseArray<float> (vector).  Both resources must be the same size.  The result will be stored in a new matrix
the same size as the input data to normalize."
        )]
    public class NormalizeByRow : IDataSource<SparseTwinIndex<float>>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> Data;

        [SubModelInformation(Required = true, Description = "The data to normalize to.  Either SparseArray<float> or SparseTwinIndex<float>.")]
        public IResource DataToNormalizeTo;

        [SubModelInformation(Required = true, Description = "The data that will be normalized.  Either SparseArray<float> or SparseTwinIndex<float>.")]
        public IResource DataToNormalize;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            // Get totals by row
            var totalToNormalizeTo = GetRowTotalsFromResource(DataToNormalizeTo);
            var inputMatrix = DataToNormalize.AcquireResource<SparseTwinIndex<float>>();
            var ourMatrix = inputMatrix.GetFlatData();
            var ourTotalByRow = GetRowTotalsFromResource(DataToNormalize);
            // create inverse
            VectorHelper.Divide(ourTotalByRow, 0, totalToNormalizeTo, 0, ourTotalByRow, 0, ourTotalByRow.Length);
            // apply inverse
            var data = inputMatrix.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            for (int i = 0; i < ourTotalByRow.Length; i++)
            {
                // if it is infinity or NAN, that means that we had zero elements
                // thusly we can just leave the matrix alone to its default value of zero.
                if (!(float.IsInfinity(ourTotalByRow[i]) || float.IsNaN(ourTotalByRow[i])))
                {
                    VectorHelper.Multiply(flatData[i], 0, ourMatrix[i], 0, ourTotalByRow[i], flatData[i].Length);
                }
            }
            Data = data;
            Loaded = true;
        }

        private float[] GetRowTotalsFromResource(IResource resource)
        {
            float[] totalByRow;
            if (resource.CheckResourceType<SparseArray<float>>())
            {
                totalByRow = resource.AcquireResource<SparseArray<float>>().GetFlatData();
            }
            else
            {
                var matrix = resource.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
                totalByRow = new float[matrix.Length];
                for (int i = 0; i < totalByRow.Length; i++)
                {
                    totalByRow[i] = VectorHelper.Sum(matrix[i], 0, matrix[i].Length);
                }
            }
            return totalByRow;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!DataToNormalize.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the DataToNormalize must be of type SparseTwinIndex<float>!";
                return false;
            }

            if (!(DataToNormalizeTo.CheckResourceType<SparseTwinIndex<float>>() || DataToNormalizeTo.CheckResourceType<SparseArray<float>>()))
            {
                error = "In '" + Name + "' the DataToNormalizeTo must be of type SparseTwinIndex<float> or SparseArray<float>!";
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            Data = null;
        }
    }
}
