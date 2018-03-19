/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using XTMF;
using TMG.Input;
using TMG.Functions;

namespace TMG.Emme.Utilities
{
    // ReSharper disable once InconsistentNaming
    public class LoadEMME4BinaryMatrixInMatrixZoneSystem : IDataSource<SparseTwinIndex<float>>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> Data;

        [SubModelInformation(Required = true, Description = "The location of the matrix file to load.")]
        public FileLocation MatrixFile;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            BinaryHelpers.ExecuteReader(this, (reader) =>
            {
                var matrix = new EmmeMatrix(reader);
                if (matrix.Dimensions != 2)
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' the matrix loaded in from '" + MatrixFile + "' was not an OD binary matrix!");
                }
                Data = SparseTwinIndex<float>.CreateSquareTwinIndex(matrix.Indexes[0], matrix.Indexes[1], matrix.FloatData);
                Loaded = true;
            }, MatrixFile);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Data = null;
            Loaded = false;
        }
    }

}
