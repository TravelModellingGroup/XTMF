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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Frameworks.Data.DataTypes;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Processing
{
    [ModuleInformation( Description =
@"This module is designed to aggregate the labeled data shaping it to be similar to another data source.  Afterwards the data can then be operated on by ODMath or used as a SparseArray."        
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

        [SubModelInformation(Required = true, Description = "(DestinationName,OriginalName,Fraction)")]
        public FileLocation DataMap;

        public LabeledData<float> GiveData()
        {
            return _Data;
        }

        public void LoadData()
        {
            LabeledData<float> shapeData = LoadData(FitToShape);
            LabeledData<float> toAggregate = LoadData(DataToAggregate);
            LabeledData<float> ret = new LabeledData<float>();
            // add all of the keys with a zero value
            foreach(var e in shapeData)
            {
                ret.Add(e.Key, 0.0f);
            }
            //Load in the map
            using (var reader = new CsvReader(DataMap, true))
            {
                //burn header
                reader.LoadLine();
                int columns;
                while(reader.LoadLine(out columns))
                {
                    if (columns >= 3)
                    {
                        string destName, originName;
                        float toApply;
                        reader.Get(out destName, 0);
                        reader.Get(out originName, 1);
                        reader.Get(out toApply, 2);
                        float destValue, originValue;
                        if(!ret.TryGetValue(destName, out destValue))
                        {
                            continue;
                        }
                        if(!toAggregate.TryGetValue(originName, out originValue))
                        {
                            continue;
                        }
                        ret[destName] = originValue * toApply + destValue;
                    }
                }
            }
            _Data = ret;
            Loaded = true;
        }

        private LabeledData<float> LoadData(IDataSource<LabeledData<float>> fitToShape)
        {
            bool loadedData;
            if(loadedData = !fitToShape.Loaded)
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
