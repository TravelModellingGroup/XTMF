/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Saving
{
    [ModuleInformation(Description = "This module takes in a 2 OD matrices, one representing a reference to category, the second an amount to assign to that category and aggregates them into a CSV file.")]
    public class SaveHistogramData : ISelfContainedModule
    {
        public string Name { get; set; }
        public float Progress => 0f;
        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        [SubModelInformation(Required = true, Description = "The category value for each cell.")]
        public IDataSource<SparseTwinIndex<float>> Values;

        [SubModelInformation(Required = true, Description = "The amount of values at this cell.")]
        public IDataSource<SparseTwinIndex<float>> Amount;

        [RunParameter("Categories", "{0-5} {6+}", typeof(RangeSetSeries), "The categories to process the data into.")]
        public RangeSetSeries Categories;

        [SubModelInformation(Required = true, Description = "The output file location CSV(Category,Amount)")]
        public FileLocation OutputFile;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private static float[][] LoadDataSource(IDataSource<SparseTwinIndex<float>> source)
        {
            var preloaded = source.Loaded;
            if (!preloaded)
            {
                source.LoadData();
            }
            var ret = source.GiveData().GetFlatData();
            if (!preloaded)
            {
                source.UnloadData();
            }
            return ret;
        }

        public void Start()
        {
            var values = LoadDataSource(Values);
            var accumulation = LoadDataSource(Amount);
            var acc = new float[Categories.Count];
            for (int i = 0; i < values.Length; i++)
            {
                for (int j = 0; j < values[i].Length; j++)
                {
                    var index = Categories.IndexOf(values[i][j]);
                    if (index >= 0)
                    {
                        acc[index] += accumulation[i][j];
                    }
                }
            }
            using (var writer = new StreamWriter(OutputFile))
            {
                writer.WriteLine("Category,Amount");
                for (int i = 0; i < acc.Length; i++)
                {
                    writer.Write('"');
                    writer.Write(Categories[i].ToString());
                    writer.Write('"');
                    writer.Write(',');
                    writer.WriteLine(acc[i]);
                }
            }
        }
    }
}
