/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TMG.Functions
{
    public static class SaveData
    {
        public static void SaveMatrix(SparseTwinIndex<float> matrix, string fileName)
        {
            var zones = matrix.ValidIndexArray();
            var data = matrix.GetFlatData();
            StringBuilder header = null;
            StringBuilder[] zoneLines = new StringBuilder[zones.Length];
            Parallel.Invoke(
                () =>
                {
                    var dir = Path.GetDirectoryName(fileName);
                    if (!String.IsNullOrWhiteSpace(dir))
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                    }
                },
                () =>
                {
                    header = new StringBuilder();
                    header.Append("Zones O\\D");
                    for (int i = 0; i < zones.Length; i++)
                    {
                        header.Append(',');
                        header.Append(zones[i]);
                    }
                },
                () =>
                {
                    Parallel.For(0, zones.Length, i =>
                    {
                        zoneLines[i] = new StringBuilder();
                        zoneLines[i].Append(zones[i]);
                        var row = data[i];
                        if (row == null)
                        {
                            for (int j = 0; j < zones.Length; j++)
                            {
                                zoneLines[i].Append(',');
                                zoneLines[i].Append('0');
                            }
                        }
                        else
                        {
                            for (int j = 0; j < zones.Length; j++)
                            {
                                zoneLines[i].Append(',');
                                zoneLines[i].Append(row[j]);
                            }
                        }
                    });
                });
            using StreamWriter writer = new StreamWriter(fileName);
            writer.WriteLine(header);
            for (int i = 0; i < zoneLines.Length; i++)
            {
                writer.WriteLine(zoneLines[i]);
            }
        }

        public static void SaveVector(SparseArray<float> data, string saveTo)
        {
            SaveVector(data, saveTo, false);
        }

        public static void SaveVector(SparseArray<float> data, string saveTo, bool skipZeros)
        {
            var flatData = data.GetFlatData();
            var indexes = data.ValidIndexArray().Select(index => index.ToString()).ToArray();
            using StreamWriter writer = new StreamWriter(saveTo, false, Encoding.UTF8);
            void WriteRecord(string zone, float value)
            {
                writer.Write(zone);
                writer.Write(',');
                writer.WriteLine(value);
            }
            writer.WriteLine("Zone,Value");
            if (skipZeros)
            {
                for (int i = 0; i < flatData.Length; i++)
                {
                    if (flatData[i] != 0)
                    {
                        WriteRecord(indexes[i], flatData[i]);
                    }
                }
            }
            else
            {
                for (int i = 0; i < flatData.Length; i++)
                {
                    WriteRecord(indexes[i], flatData[i]);
                }
            }
        }

        public static void SaveMatrixThirdNormalized(IZone[] zones, float[][] data, string saveLocation)
        {
            SaveMatrixThirdNormalized(zones, data, saveLocation, false);
        }

        public static void SaveMatrixThirdNormalized(IZone[] zones, float[][] data, string saveLocation, bool skipZeros)
        {
            var zoneNumbers = zones.Select(z => z.ZoneNumber.ToString()).ToArray();
            using StreamWriter writer = new StreamWriter(saveLocation, false, Encoding.UTF8);
            void WriteRecord(string origin, string destination, float value)
            {
                writer.Write(origin);
                writer.Write(',');
                writer.Write(destination);
                writer.Write(',');
                writer.WriteLine(value);
            }
            writer.WriteLine("Origin,Destination,Data");
            for (int i = 0; i < data.Length; i++)
            {
                var row = data[i];
                if (skipZeros)
                {
                    for (int j = 0; j < row.Length; j++)
                    {
                        if (row[j] != 0)
                        {
                            WriteRecord(zoneNumbers[i], zoneNumbers[j], row[j]);
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < row.Length; j++)
                    {
                        WriteRecord(zoneNumbers[i], zoneNumbers[j], row[j]);
                    }
                }
            }
        }

        public static void SaveMatrixThirdNormalized(SparseTwinIndex<float> matrix, FileLocation saveLocation)
        {
            SaveMatrixThirdNormalized(matrix, saveLocation, false);
        }

        public static void SaveMatrixThirdNormalized(SparseTwinIndex<float> matrix, FileLocation saveLocation, bool skipZeros)
        {
            using StreamWriter writer = new StreamWriter(saveLocation, false, Encoding.UTF8);
            writer.WriteLine("Origin,Destination,Data");
            void WriteRecord(int origin, int destination, float value)
            {
                writer.Write(origin);
                writer.Write(',');
                writer.Write(destination);
                writer.Write(',');
                writer.WriteLine(value);
            }
            foreach (var o in matrix.ValidIndexes())
            {
                if (skipZeros)
                {
                    foreach (var d in matrix.ValidIndexes(o))
                    {
                        var entry = matrix[o, d];
                        if (entry != 0.0)
                        {
                            WriteRecord(o, d, entry);
                        }
                    }
                }
                else
                {
                    foreach (var d in matrix.ValidIndexes(o))
                    {
                        WriteRecord(o, d, matrix[o, d]);
                    }
                }
            }
        }

        private struct SaveTask
        {
            internal int RowNumber;
            internal string Text;
        }

        public static void SaveMatrix(IZone[] zones, float[][] data, string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            if (!String.IsNullOrWhiteSpace(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            using StreamWriter writer = new StreamWriter(fileName, false, Encoding.UTF8);
            BlockingCollection<SaveTask> toWrite = [];
            var saveTask = Task.Run(() =>
            {
                int nextRow = 0;
                SortedList<int, SaveTask> backlog = [];
                foreach (var newTask in toWrite.GetConsumingEnumerable())
                {
                    var task = newTask;
                    do
                    {
                        string currentString = task.Text;
                        int currentRow = task.RowNumber;
                        if (nextRow == currentRow)
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            writer.WriteLine(currentString);
                            nextRow++;
                        }
                        else
                        {
                            backlog.Add(currentRow, new SaveTask() { RowNumber = currentRow, Text = currentString });
                            break;
                        }
                        if (backlog.Count == 0)
                        {
                            break;
                        }
                        if (backlog.TryGetValue(nextRow, out task))
                        {
                            backlog.Remove(nextRow);
                            continue;
                        }
                        break;
                    } while (true);
                }
            });
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Zones O\\D");
            for (int i = 0; i < zones.Length; i++)
            {
                stringBuilder.Append(',');
                stringBuilder.Append(zones[i].ZoneNumber);
            }
            toWrite.Add(new SaveTask() { RowNumber = 0, Text = stringBuilder.ToString() });
            Parallel.For(0, zones.Length, () => new StringBuilder(),
                (i, _, strBuilder) =>
            {
                strBuilder.Clear();
                strBuilder.Append(zones[i].ZoneNumber);
                var row = data[i];
                if (row == null)
                {
                    for (int j = 0; j < zones.Length; j++)
                    {
                        strBuilder.Append(",0");
                    }
                }
                else
                {
                    for (int j = 0; j < row.Length; j++)
                    {
                        strBuilder.Append(',');
                        strBuilder.Append(row[j]);
                    }
                }
                toWrite.Add(new SaveTask() { RowNumber = i + 1, Text = strBuilder.ToString() });
                return strBuilder;
            }, _ => { });
            toWrite.CompleteAdding();
            saveTask.Wait();
        }

        public static void SaveMatrix(IZone[] zones, float[] data, string fileName)
        {
            StringBuilder header = null;
            StringBuilder[] zoneLines = new StringBuilder[zones.Length];
            if (data.Length != zones.Length * zones.Length)
            {
                throw new ArgumentException("The data must be a square matrix in size to the zones!", nameof(data));
            }
            Parallel.Invoke(
                () =>
                {
                    var dir = Path.GetDirectoryName(fileName);
                    if (!String.IsNullOrWhiteSpace(dir))
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                    }
                },
                () =>
                {
                    header = new StringBuilder();
                    header.Append("Zones O\\D");
                    for (int i = 0; i < zones.Length; i++)
                    {
                        header.Append(',');
                        header.Append(zones[i].ZoneNumber);
                    }
                },
                () =>
                {
                    Parallel.For(0, zones.Length, i =>
                    {
                        zoneLines[i] = new StringBuilder();
                        zoneLines[i].Append(zones[i].ZoneNumber);
                        var iOffset = i * zones.Length;
                        for (int j = 0; j < zones.Length; j++)
                        {
                            zoneLines[i].Append(',');
                            zoneLines[i].Append(data[iOffset + j]);
                        }
                    });
                });
            using StreamWriter writer = new StreamWriter(fileName);
            writer.WriteLine(header);
            for (int i = 0; i < zoneLines.Length; i++)
            {
                writer.WriteLine(zoneLines[i]);
            }
        }
    }
}