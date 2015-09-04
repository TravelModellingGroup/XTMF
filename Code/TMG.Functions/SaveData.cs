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
                    Parallel.For(0, zones.Length, (int i) =>
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
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine(header);
                for (int i = 0; i < zoneLines.Length; i++)
                {
                    writer.WriteLine(zoneLines[i]);
                }
            }
        }

        public static void SaveVector(SparseArray<float> data, string saveTo)
        {
            var flatData = data.GetFlatData();
            var indexes = data.ValidIndexArray().Select(index => index.ToString()).ToArray();
            using (StreamWriter writer = new StreamWriter(saveTo))
            {
                writer.WriteLine("Zone,Value");
                for (int i = 0; i < flatData.Length; i++)
                {
                    writer.Write(indexes[i]);
                    writer.Write(',');
                    writer.WriteLine(flatData[i]);
                }
            }
        }

        public static void SaveMatrixThirdNormalized(SparseTwinIndex<float> matrix, FileLocation saveLocation)
        {
            using (StreamWriter writer = new StreamWriter(saveLocation))
            {
                writer.WriteLine("Origin,Destination,Data");
                foreach (var o in matrix.ValidIndexes())
                {
                    foreach (var d in matrix.ValidIndexes(o))
                    {
                        writer.Write(o);
                        writer.Write(',');
                        writer.Write(d);
                        writer.Write(',');
                        writer.WriteLine(matrix[o, d]);
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
            StringBuilder[] zoneLines = new StringBuilder[zones.Length];
            var dir = Path.GetDirectoryName(fileName);
            if (!String.IsNullOrWhiteSpace(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                BlockingCollection<SaveTask> toWrite = new BlockingCollection<SaveTask>();
                var saveTask = Task.Run(() =>
                {
                    int nextRow = 0;
                    SortedList<int, SaveTask> backlog = new SortedList<int, SaveTask>();
                    foreach (var newTask in toWrite.GetConsumingEnumerable())
                    {
                        var task = newTask;
                        do
                        {
                            string currentString = task.Text;
                            int currentRow = task.RowNumber;
                            if (nextRow == currentRow)
                            {
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
                            if(backlog.TryGetValue(nextRow, out task))
                            {
                                backlog.Remove(nextRow);
                                continue;
                            }
                        } while (false);
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
                Parallel.For(0, zones.Length,()=> new StringBuilder(),
                    (int i, ParallelLoopState _, StringBuilder strBuilder) =>
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
                }, (StringBuilder _) => { });
                toWrite.CompleteAdding();
                saveTask.Wait();
            }
        }

        public static void SaveMatrix(IZone[] zones, float[] data, string fileName)
        {
            StringBuilder header = null;
            StringBuilder[] zoneLines = new StringBuilder[zones.Length];
            if (data.Length != zones.Length * zones.Length)
            {
                throw new ArgumentException("The data must be a square matrix in size to the zones!", "data");
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
                    Parallel.For(0, zones.Length, (int i) =>
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
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine(header);
                for (int i = 0; i < zoneLines.Length; i++)
                {
                    writer.WriteLine(zoneLines[i]);
                }
            }
        }
    }
}