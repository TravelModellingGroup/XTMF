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
using System.IO;
using System.Linq;
using TMG.Frameworks.Data.DataTypes;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Saving
{
    // ReSharper disable once InconsistentNaming
    public class SaveLabeledDataToCSV<T> : ISelfContainedModule
    {
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Required = true, Description = "The data to save.")]
        public IDataSource<LabeledData<T>> DataToSave;

        [SubModelInformation(Required = true, Description = "The location to save to.")]
        public FileLocation SaveTo;

        public void Start()
        {
            var weNeedToLoad = !DataToSave.Loaded;
            if(weNeedToLoad)
            {
                DataToSave.LoadData();
            }
            var data = DataToSave.GiveData();
            if(weNeedToLoad)
            {
                DataToSave.UnloadData();
            }
            // now that we have the data save to to disk
            using (var writer = new StreamWriter(SaveTo))
            {
                writer.WriteLine("Label,Value");
                // provide an optimized path for float
                if(typeof(T) == typeof(float))
                {
                    var fData = (LabeledData<float>)(object)data;
                    foreach(var element in fData.OrderBy(e => e.Key))
                    {
                        writer.Write(element.Key);
                        writer.Write(',');
                        writer.WriteLine(element.Value);
                    }
                }
                else
                {
                    foreach (var element in data.OrderBy(e => e.Key))
                    {
                        writer.Write(element.Key);
                        writer.Write(',');
                        writer.WriteLine(element.Value.ToString());
                    }
                }
            }
        }
    }

}
