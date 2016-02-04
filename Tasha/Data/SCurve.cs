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
namespace Tasha.Data
{
    public class SCurve : IDataSource<SparseArray<float>>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseArray<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        public IResource OriginalData;

        [RunParameter("Offset X", 0.0f, "Applied to X to offset the SCurve")]
        public float OffsetX;

        [RunParameter("Stretch", 1.0f, "The stretch applied to the X for the SCurve.")]
        public float Stretch;

        public void LoadData()
        {
            var original = OriginalData.AcquireResource<SparseArray<float>>();
            var oData = original.GetFlatData();
            var ours = original.CreateSimilarArray<float>();
            var ourData = ours.GetFlatData();
            for(int i = 0; i < oData.Length; i++)
            {
                ourData[i] = 1.0f / (1.0f + (float)Math.Exp(Stretch * -(oData[i] + OffsetX)));
            }
            Data = ours;
            Loaded = true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Data = null;
        }
    }

}
