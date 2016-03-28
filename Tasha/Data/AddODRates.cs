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
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;
namespace Tasha.Data
{
    [ModuleInformation(Description =
        @"This module is designed to add two rates together for each OD.")]
    public class AddODRates : IDataSource<SparseTwinIndex<float>>
    {
        private SparseTwinIndex<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = false, Description = "The first Matrix")]
        public IResource FirstRateToApply;

        [SubModelInformation(Required = false, Description = "The first Matrix")]
        public IDataSource<SparseTwinIndex<float>> FirstRateToApplyRaw;

        [SubModelInformation(Required = false, Description = "The second Matrix.")]
        public IResource SecondRateToApply;

        [SubModelInformation(Required = false, Description = "The second Matrix.")]
        public IDataSource<SparseTwinIndex<float>> SecondRateToApplyRaw;


        [SubModelInformation(Required = false, Description = "Additional rates to add.")]
        public IResource[] AdditionalRates;

        [SubModelInformation(Required = false, Description = "Additional rates to add.")]
        public IDataSource<SparseTwinIndex<float>>[] AdditionalRatesRaw;

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
            var first = ModuleHelper.GetDataFromDatasourceOrResource(FirstRateToApplyRaw, FirstRateToApply, FirstRateToApplyRaw != null);
            var firstRate = first.GetFlatData();
            var secondRate = ModuleHelper.GetDataFromDatasourceOrResource(SecondRateToApplyRaw, SecondRateToApply, SecondRateToApplyRaw != null).GetFlatData();
            SparseTwinIndex<float> data;
            data = first.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            var dereferenced = AdditionalRates.Select(a => a.AcquireResource<SparseTwinIndex<float>>().GetFlatData()).Union
                (
                    AdditionalRatesRaw.Select(source =>
                   {
                       source.LoadData();
                       var ret = source.GiveData();
                       source.UnloadData();
                       return ret.GetFlatData();
                   })
                ).ToArray();
            for (int i = 0; i < flatData.Length; i++)
            {
                VectorHelper.Add(flatData[i], 0, firstRate[i], 0, secondRate[i], 0, flatData[i].Length);
                for (int j = 0; j < dereferenced.Length; j++)
                {
                    VectorHelper.Add(flatData[i], 0, flatData[i], 0, dereferenced[j][i], 0, flatData[i].Length);
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
            if (!this.EnsureExactlyOneAndOfSameType(FirstRateToApplyRaw, FirstRateToApply, ref error) ||
                !this.EnsureExactlyOneAndOfSameType(SecondRateToApplyRaw, SecondRateToApply, ref error))
            {
                return false;
            }
            for (int i = 0; i < AdditionalRates.Length; i++)
            {
                if (!AdditionalRates[i].CheckResourceType<SparseTwinIndex<float>>())
                {
                    error = "In '" + Name + "' the additional rate resource named '" + AdditionalRates[i].Name + "' at position '" + i.ToString() + "' is not of type SparseTwinIndex<float>!";
                    return false;
                }
            }
            return true;
        }
    }
}
