/*
    Copyright 2016-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Frameworks.Data.DataTypes;
using XTMF;
using Datastructure;
using TMG.Input;
using Agg = TMG.Frameworks.Data.Loading.LabeledDataFromCSV<float>.Aggregation;

namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation( Description =
@"This module is designed to load in LabeledData for later processing."
        )]
    // ReSharper disable once InconsistentNaming
    public class LabeledDataFromCSV<T> : IDataSource<LabeledData<T>>
    {
        [SubModelInformation(Required = true, Description = "")]
        public FileLocation LoadFrom;

        public enum Aggregation
        {
            None,
            Sum,
            Multiply,
            Count
        }

        [RunParameter("Aggregation", "None", typeof(Agg), "The aggregation to apply to the data while loading.")]
        public Agg AggregationToApply;


        public bool Loaded { get; set; }


        public string Name { get; set; }


        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        private LabeledData<T> _Data;

        public LabeledData<T> GiveData()
        {
            return _Data;
        }

        private void Add<TData>(LabeledData<TData> set, string label, TData data)
        {
            switch(AggregationToApply)
            {
                case Agg.None:
                    if (set.ContainsKey(label))
                    {
                        throw new XTMFRuntimeException($"In '{Name}' while loading in labeled data a label was loaded multiple times '{label}'!");
                    }
                    set.Add(label, data);
                    break;
                case Agg.Sum:
                    if (typeof(TData) == typeof(float))
                    {
                        // the optimizer should be able to solve this
                        var fData = (float)(object)data;
                        float alreadyContained;
                        var fSet = set as LabeledData<float>;
                        // ReSharper disable once PossibleNullReferenceException
                        fSet.TryGetValue(label, out alreadyContained);
                        fSet[label] = fData + alreadyContained;
                    }
                    break;
                case Agg.Multiply:
                    if (typeof(TData) == typeof(float))
                    {
                        // the optimizer should be able to solve this
                        var fData = (float)(object)data;
                        float alreadyContained;
                        var fSet = set as LabeledData<float>;
                        // ReSharper disable once PossibleNullReferenceException
                        if (!fSet.TryGetValue(label, out alreadyContained))
                        {
                            alreadyContained = 1.0f;
                        }
                        fSet[label] = fData * alreadyContained;
                    }
                    break;
                case Agg.Count:
                    if (typeof(TData) == typeof(float))
                    {
                        float alreadyContained;
                        var fSet = set as LabeledData<float>;
                        // ReSharper disable once PossibleNullReferenceException
                        fSet.TryGetValue(label, out alreadyContained);
                        fSet[label] = 1 + alreadyContained;
                    }
                    break;
            }
        }

        public void LoadData()
        {
            var ret = new LabeledData<T>();
            using (var reader = new CsvReader(LoadFrom, true))
            {
                //burn the header
                reader.LoadLine();
                int columns;
                string error = null;
                int lineNumber = 0;
                // load the data
                while(reader.LoadLine(out columns))
                {
                    lineNumber++;
                    if(columns >= 2)
                    {
                        string label; 
                        reader.Get(out label, 0);
                        if (typeof(T) == typeof(float))
                        {
                            float parsedData;
                            LabeledData<float> fRet = ret as LabeledData<float>;
                            reader.Get(out parsedData, 1);
                            Add(fRet, label, parsedData);
                        }
                        else
                        {
                            string data;
                            reader.Get(out data, 1);
                            var parsedData = ArbitraryParameterParser.ArbitraryParameterParse(typeof(T), data, ref error);
                            if (parsedData == null || error != null)
                            {
                                throw new XTMFRuntimeException($"In '{Name}' we were unable to parse the data in line number {lineNumber}!\r\n{error}");
                            }
                            Add(ret, label, (T)parsedData);
                        }
                    }
                }
            }
            _Data = ret;
            Loaded = true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(AggregationToApply != Agg.None && typeof(T) != typeof(float))
            {
                error = $"In '{Name}' only System.Single data can be aggregated.  Please set the aggregation type to null.";
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            _Data = null;
        }
    }

}
