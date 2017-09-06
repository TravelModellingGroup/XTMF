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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using TMG.Frameworks.Data.DataTypes;
using TMG.Frameworks.Data.Loading;
using TMG.Frameworks.Data.Processing;
using TMG.Input;
using System.IO;

namespace XTMF.Testing.TMG.Data
{
    [TestClass]
    public class TestLabeledData
    {

        private FileLocation CreateFileLocationFromOutputDirectory(string path)
        {
            string error = null;
            Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, path, out FileFromOutputDirectory fileOut));
            return new FilePathFromOutputDirectory() { FileName = fileOut };
        }

        private LabeledData<float> LoadLabeledData(string path)
        {
            LabeledDataFromCSV<float> loader = new LabeledDataFromCSV<float>();
            loader.LoadFrom = CreateFileLocationFromOutputDirectory(path);
            loader.LoadData();
            var data = loader.GiveData();
            loader.UnloadData();
            return data;
        }

        [TestMethod]
        public void TestLoadingLabeledData()
        {
            const string originalData = "Data.csv";
            using (var writer = new StreamWriter(originalData))
            {
                writer.WriteLine("Label,Data");
                for (int i = 0; i < 26; i++)
                {
                    writer.Write((char)('a' + i));
                    writer.Write(',');
                    writer.WriteLine(i);
                }
            }
            try
            {
                var data = LoadLabeledData(originalData);
                Assert.AreEqual(26, data.Count);
                for (int i = 0; i < 26; i++)
                {
                    Assert.IsTrue(data.ContainsKey(((char)('a' + i)).ToString()));
                    Assert.AreEqual(i, data[((char)('a' + i)).ToString()]);
                }
            }
            finally
            {
                File.Delete("Data.csv");
            }
        }

        [TestMethod]
        public void TestAggregatingLabeledData()
        {
            const string originalData = "Data.csv";
            const string mapToLocation = "MapTo.csv";
            const string mapLocation = "Map.csv";
            try
            {
                // create the data that is going to be mapped
                using (var writer = new StreamWriter(originalData))
                {
                    writer.WriteLine("Label,Data");
                    for (int i = 0; i < 26; i++)
                    {
                        writer.Write((char)('a' + i));
                        writer.Write(',');
                        writer.WriteLine(i);
                    }
                }
                // create the data that will define the new shape
                using (var writer = new StreamWriter(mapToLocation))
                {
                    writer.WriteLine("Label,Data");
                    for (int i = 0; i < 2; i++)
                    {
                        writer.Write((char)('1' + i));
                        writer.Write(',');
                        writer.WriteLine(i);
                    }
                }
                // create the mapping file
                using (var writer = new StreamWriter(mapLocation))
                {
                    writer.WriteLine("DestLabel,OriginLabel,Amount");
                    for (int i = 0; i < 26; i++)
                    {
                        writer.Write((char)('1' + (i / 13)));
                        writer.Write(',');
                        writer.Write((char)('a' + i));
                        writer.Write(',');
                        writer.WriteLine(1.0f);
                    }
                }
                // now that our data files have been created create the aggregation
                AggregateLabeledDataToShape agg = new AggregateLabeledDataToShape();
                agg.DataMap = CreateFileLocationFromOutputDirectory(mapLocation);
                agg.DataToAggregate = new TestDataSource<LabeledData<float>>(LoadLabeledData(originalData));
                agg.FitToShape = new TestDataSource<LabeledData<float>>(LoadLabeledData(mapToLocation));
                agg.LoadData();
                var combinedData = agg.GiveData();
                agg.UnloadData();
                // now test the properties
                Assert.AreEqual(2, combinedData.Count);
                Assert.IsTrue(combinedData.ContainsKey("1"));
                Assert.AreEqual(78, combinedData["1"]);
                Assert.IsTrue(combinedData.ContainsKey("2"));
                Assert.AreEqual(247, combinedData["2"]);
            }
            finally
            {
                File.Delete(originalData);
                File.Delete(mapToLocation);
                File.Delete(mapLocation);
            }
        }

        [TestMethod]
        public void TestAggregatingLabeledDataWithoutMapFile()
        {
            const string originalData = "Data.csv";
            const string mapToLocation = "MapTo.csv";
            try
            {
                // create the data that is going to be mapped
                using (var writer = new StreamWriter(originalData))
                {
                    writer.WriteLine("Label,Data");
                    for (int i = 0; i < 26; i++)
                    {
                        writer.Write((char)('a' + i));
                        writer.Write(',');
                        writer.WriteLine(i);
                    }
                }
                // create the data that will define the new shape
                using (var writer = new StreamWriter(mapToLocation))
                {
                    writer.WriteLine("Label,Data");
                    for (int i = 0; i < 26; i+=2)
                    {
                        writer.Write((char)('a' + i));
                        writer.Write(',');
                        writer.WriteLine(i);
                    }
                }
                // now that our data files have been created create the aggregation
                AggregateLabeledDataToShape agg = new AggregateLabeledDataToShape();
                agg.DataToAggregate = new TestDataSource<LabeledData<float>>(LoadLabeledData(originalData));
                agg.FitToShape = new TestDataSource<LabeledData<float>>(LoadLabeledData(mapToLocation));
                agg.LoadData();
                var combinedData = agg.GiveData();
                agg.UnloadData();
                // now test the properties
                Assert.AreEqual(13, combinedData.Count);
                Assert.AreEqual(156.00, combinedData.Sum(val => val.Value), 0.00001);
            }
            finally
            {
                File.Delete(originalData);
                File.Delete(mapToLocation);
            }
        }

        [TestMethod]
        public void TestLabeledDataToSparseArray()
        {
            const string originalData = "Data.csv";
            using (var writer = new StreamWriter(originalData))
            {
                writer.WriteLine("Label,Data");
                for (int i = 0; i < 26; i++)
                {
                    writer.Write((char)('a' + i));
                    writer.Write(',');
                    writer.WriteLine(i);
                }
            }
            try
            {
                ConvertLabeledDataToSparseArray cv = new ConvertLabeledDataToSparseArray();
                cv.Labeled = new TestDataSource<LabeledData<float>>(LoadLabeledData(originalData));
                cv.LoadData();
                var array = cv.GiveData();
                cv.UnloadData();
                Assert.AreEqual(26, array.Count);
                Assert.AreEqual(0, array.GetSparseIndex(0));
                var flat = array.GetFlatData();
                for (int i = 0; i < flat.Length; i++)
                {
                    Assert.AreEqual(i, flat[i]);
                }
            }
            finally
            {
                File.Delete("Data.csv");
            }
        }
    }
}
