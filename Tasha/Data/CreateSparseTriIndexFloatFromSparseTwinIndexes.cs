/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace Tasha.Data
{
    [ModuleInformation(Description = "This module provides the ability to construct SparseTriIndex<float> by building up SparseTwinIndex<float> data sources.")]
    public class CreateSparseTriIndexFloatFromSparseTwinIndexes : IDataSource<SparseTriIndex<float>>
    {
        public bool Loaded { get;set; }
        

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTriIndex<float> Data;

        [RunParameter("Top Level Indices", "0", typeof(RangeSet), "A set of ranges to assign to the TwinIndex data sources to build the tri-index data source.")]
        public RangeSet TriIndexSet;

        [SubModelInformation(Required = true, Description = "The data sources to bind")]
        public IDataSource<SparseTwinIndex<float>>[] TwinSources;

        public SparseTriIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            float[][][] data = new float[TwinSources.Length][][];
            SparseIndexing indicies = RootCreateIndices();
            for(int i = 0; i < TwinSources.Length; i++)
            {
                TwinSources[i].LoadData();
                var innerData = TwinSources[i].GiveData();
                CreateIndices(indicies, i, innerData);
                data[i] = innerData.GetFlatData();
                TwinSources[i].UnloadData();
            }
            Data = new SparseTriIndex<float>(indicies, data);
            Loaded = true;
        }

        private SparseIndexing RootCreateIndices()
        {
            SparseIndexing rootLevel = new SparseIndexing();
            rootLevel.Indexes = TriIndexSet.Select(range => new SparseSet() { Start = range.Start, Stop = range.Stop }).ToArray();
            return rootLevel;
        }

        private void CreateIndices(SparseIndexing rootLevel, int i, SparseTwinIndex<float> innerData)
        {
            rootLevel.Indexes[GetIndexForFlat(rootLevel, i)].SubIndex = innerData.Indexes;
        }

        private int GetIndexForFlat(SparseIndexing rootLevel, int flatIndex)
        {
            var indexes = rootLevel.Indexes;
            int pos = 0;
            for(int i = 0; i < indexes.Length; i++)
            {
                var delta = indexes[i].Stop - indexes[i].Start + 1;
                if(flatIndex <= pos + delta)
                {
                    return i;
                }
                pos += delta;
            }
            return -1;
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
