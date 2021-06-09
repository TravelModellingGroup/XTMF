/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Frameworks.Data.Loading;
using TMG.Input;

namespace XTMF.Testing.TMG.Data.Loading
{
    [TestClass]
    public class TestLoadODDataFromCSV
    {
        [TestMethod]
        public void TestSquareMatrix()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var data = new float[10, 10];
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        data[i, j] = i;
                    }
                }
                using (var writer = new StreamWriter(tempFile))
                {
                    writer.Write("origin\\destination");
                    for (int i = 0; i < 10; i++)
                    {
                        writer.Write(',');
                        writer.Write(i);
                    }
                    writer.WriteLine();
                    for (int i = 0; i < data.GetLength(0); i++)
                    {
                        writer.Write(i);
                        for (int j = 0; j < data.GetLength(1); j++)
                        {
                            writer.Write(',');
                            writer.Write(data[i, j]);
                        }
                        writer.WriteLine();
                    }
                }
                string error = null;
                Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, tempFile, out FileFromOutputDirectory file), error);
                var module = new LoadODDataFromCSV()
                {
                    ContainsHeader = true,
                    CSVFormat = LoadODDataFromCSV.FileType.SquareMatrix,
                    LoadFrom = new FilePathFromOutputDirectory()
                    {
                        Name = "LoadFrom",
                        FileName = file
                    }
                };
                foreach (var point in module.Read())
                {
                    if (data[point.O, point.D] != point.Data)
                    {
                        Assert.Fail($"Expected {data[point.O, point.D]} but instead found {point.Data}!");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void TestThirdNormalizedMatrixAutoDetectMatrix()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var data = new float[10, 10];
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        data[i, j] = i;
                    }
                }
                using (var writer = new StreamWriter(tempFile))
                {
                    writer.WriteLine("origin,destination,value");
                    for (int i = 0; i < 10; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            writer.Write(i);
                            writer.Write(',');
                            writer.Write(j);
                            writer.Write(',');
                            writer.WriteLine(data[i, j]);
                        }
                    }
                }
                string error = null;
                Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, tempFile, out FileFromOutputDirectory file), error);
                var module = new LoadODDataFromCSV()
                {
                    ContainsHeader = true,
                    CSVFormat = LoadODDataFromCSV.FileType.ThirdNormalized,
                    ThirdNormalizedType = LoadODDataFromCSV.ReadType.AutoDetect,
                    LoadFrom = new FilePathFromOutputDirectory()
                    {
                        Name = "LoadFrom",
                        FileName = file
                    }
                };
                foreach (var point in module.Read())
                {
                    if (data[point.O, point.D] != point.Data)
                    {
                        Assert.Fail($"Expected {data[point.O, point.D]} but instead found {point.Data}!");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void TestThirdNormalizedMatrixForceMatrix()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var data = new float[10, 10];
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        data[i, j] = i;
                    }
                }
                using (var writer = new StreamWriter(tempFile))
                {
                    writer.WriteLine("origin,destination,value");
                    for (int i = 0; i < 10; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            writer.Write(i);
                            writer.Write(',');
                            writer.Write(j);
                            writer.Write(',');
                            writer.WriteLine(data[i, j]);
                        }
                    }
                }
                string error = null;
                Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, tempFile, out FileFromOutputDirectory file), error);
                var module = new LoadODDataFromCSV()
                {
                    ContainsHeader = true,
                    CSVFormat = LoadODDataFromCSV.FileType.ThirdNormalized,
                    ThirdNormalizedType = LoadODDataFromCSV.ReadType.Matrix,
                    LoadFrom = new FilePathFromOutputDirectory()
                    {
                        Name = "LoadFrom",
                        FileName = file
                    }
                };
                foreach (var point in module.Read())
                {
                    if (data[point.O, point.D] != point.Data)
                    {
                        Assert.Fail($"Expected {data[point.O, point.D]} but instead found {point.Data}!");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void TestThirdNormalizedMatrixAutoDetectVector()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var data = new float[10, 10];
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        data[i, j] = i;
                    }
                }
                using (var writer = new StreamWriter(tempFile))
                {
                    writer.WriteLine("origin,value");
                    for (int i = 0; i < 10; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            writer.Write(i);
                            writer.Write(',');
                            writer.WriteLine(data[i, 0]);
                        }
                    }
                }
                string error = null;
                Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, tempFile, out FileFromOutputDirectory file), error);
                var module = new LoadODDataFromCSV()
                {
                    ContainsHeader = true,
                    CSVFormat = LoadODDataFromCSV.FileType.ThirdNormalized,
                    ThirdNormalizedType = LoadODDataFromCSV.ReadType.AutoDetect,
                    LoadFrom = new FilePathFromOutputDirectory()
                    {
                        Name = "LoadFrom",
                        FileName = file
                    }
                };
                foreach (var point in module.Read())
                {
                    if (point.D != 0)
                    {
                        Assert.Fail($"Expected the destination to be 0 instead we have {point.D}!");
                    }
                    if (data[point.O, point.D] != point.Data)
                    {
                        Assert.Fail($"Expected {data[point.O, point.D]} but instead found {point.Data}!");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void TestThirdNormalizedMatrixForceVector()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var data = new float[10, 10];
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1); j++)
                    {
                        data[i, j] = i;
                    }
                }
                using (var writer = new StreamWriter(tempFile))
                {
                    writer.WriteLine("origin,value");
                    for (int i = 0; i < 10; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            writer.Write(i);
                            writer.Write(',');
                            writer.WriteLine(data[i, 0]);
                        }
                    }
                }
                string error = null;
                Assert.IsTrue(FileFromOutputDirectory.TryParse(ref error, tempFile, out FileFromOutputDirectory file), error);
                var module = new LoadODDataFromCSV()
                {
                    ContainsHeader = true,
                    CSVFormat = LoadODDataFromCSV.FileType.ThirdNormalized,
                    ThirdNormalizedType = LoadODDataFromCSV.ReadType.Vector,
                    LoadFrom = new FilePathFromOutputDirectory()
                    {
                        Name = "LoadFrom",
                        FileName = file
                    }
                };
                foreach (var point in module.Read())
                {
                    if (point.D != 0)
                    {
                        Assert.Fail($"Expected the destination to be 0 instead we have {point.D}!");
                    }
                    if (data[point.O, point.D] != point.Data)
                    {
                        Assert.Fail($"Expected {data[point.O, point.D]} but instead found {point.Data}!");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
