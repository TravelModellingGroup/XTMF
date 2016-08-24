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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Frameworks.Data.DataTypes;
using XTMF;
using Datastructure;
using TMG.Input;

namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation( Description =
@"This module is designed to load in LabeledData for later processing."
        )]
    public class LabeledDataFromCSV<T> : IDataSource<LabeledData<T>>
    {
        [SubModelInformation(Required = true, Description = "")]
        public FileLocation LoadFrom;

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
                            fRet.Add(label, parsedData);
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
                            ret.Add(label, (T)parsedData);
                        }
                    }
                }
            }
            _Data = ret;
            Loaded = true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            _Data = null;
        }
    }

}
