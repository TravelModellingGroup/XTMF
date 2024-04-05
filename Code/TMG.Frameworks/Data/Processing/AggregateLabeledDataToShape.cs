﻿/*
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
using System.Linq;
using TMG.Frameworks.Data.DataTypes;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Processing
{
    [ModuleInformation( Description =
@"This module is designed to aggregate the labeled data shaping it to be similar to another data source.  Afterwards the data can then be operated on by ODMath or used as a SparseArray. 
If a mapping file is not provided it will do a left join onto the DataToAggregate's shape."        
        )]
    public class AggregateLabeledDataToShape : IDataSource<LabeledData<float>>
    {
        public bool Loaded { get; set; }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private LabeledData<float> _Data;

        [SubModelInformation(Required = true, Description = "The shape of the data to build towards.")]
        public IDataSource<LabeledData<float>> FitToShape;

        [SubModelInformation(Required = true, Description = "The data to gather the results from.")]
        public IDataSource<LabeledData<float>> DataToAggregate;

        [SubModelInformation(Required = false, Description = "(DestinationName,OriginalName,Fraction)")]
        public FileLocation DataMap;

        public LabeledData<float> GiveData()
        {
            return _Data;
        }

        public void LoadData()
        {
            LabeledData<float> shapeData = LoadData(FitToShape);
            LabeledData<float> toAggregate = LoadData(DataToAggregate);
            LabeledData<float> ret = [];
            // add all of the keys with a zero value
            foreach(var e in shapeData)
            {
                ret.Add(e.Key, 0.0f);
            }
            if (DataMap != null)
            {
                //Load in the map
                using (var reader = new CsvReader(DataMap, true))
                {
                    //burn header
                    reader.LoadLine();
                    while (reader.LoadLine(out int columns))
                    {
                        if (columns >= 3)
                        {
                            reader.Get(out string destName, 0);
                            reader.Get(out string originName, 1);
                            reader.Get(out float toApply, 2);
                            if (!ret.TryGetValue(destName, out float destValue))
                            {
                                continue;
                            }
                            if (!toAggregate.TryGetValue(originName, out float originValue))
                            {
                                continue;
                            }
                            ret[destName] = originValue * toApply + destValue;
                        }
                    }
                }
            }
            else
            {
                var keys = ret.Keys.ToList();
                foreach(var key in keys)
                {
                    if (toAggregate.TryGetValue(key, out float data))
                    {
                        ret[key] = data;
                    }
                }
            }
            _Data = ret;
            Loaded = true;
        }

        private LabeledData<float> LoadData(IDataSource<LabeledData<float>> fitToShape)
        {
            var loadedData = !fitToShape.Loaded;
            if (loadedData)
            {
                fitToShape.LoadData();
            }
            var ret = fitToShape.GiveData();
            if(loadedData)
            {
                fitToShape.UnloadData();
            }
            return ret;
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
