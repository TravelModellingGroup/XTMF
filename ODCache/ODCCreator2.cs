/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using System.Text;

namespace Datastructure
{
    public class OdcCreator2<T>
    {
        public int Times;

        public int Types;

        private SparseTwinIndex<float[]> Data;

        public OdcCreator2(SparseArray<T> sparseRepresentation, int types, int times)
        {
            Data = sparseRepresentation.CreateSquareTwinArray<float[]>();
            Types = types;
            Times = times;
        }

        public int HighestZone
        {
            get
            {
                return Data.ValidIndexes().Last();
            }
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// Multiple entries are stored as different types.
        /// </summary>
        /// <param name="csv">The CSV file to read</param>
        /// <param name="header">Does the CSV have a header?</param>
        /// <param name="offsetTimes">The offset into the times</param>
        /// <param name="offsetType">Should we offset the CSV's information in the types?</param>
        public void LoadCsvTimes(string csv, bool header, int offsetTimes, int offsetType)
        {
            // Gain access to the files
            using StreamReader reader = new(new
                FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read,
                0x1000, FileOptions.SequentialScan));
            string line;
            var ammountOfData = Types * Times;
            int injectIndex = Times * offsetType + offsetTimes;
            if (header) reader.ReadLine();
            // Read the line from the CSV
            while ((line = reader.ReadLine()) != null)
            {
                // Calculate where to store the data
                int pos, next;
                int o = FastParse.ParseInt(line, 0, (pos = line.IndexOf(',')));
                int d = FastParse.ParseInt(line, pos + 1, (next = line.IndexOf(',', pos + 1)));
                pos = next + 1;
                int length = line.Length;
                int entry = 0;
                var position = Data[o, d];
                if (position == null)
                {
                    Data[o, d] = position = new float[ammountOfData];
                }
                for (int i = pos; i < length; i++)
                {
                    if (line[i] == ',')
                    {
                        position[injectIndex + entry] = FastParse.ParseFixedFloat(line, pos, i - pos);
                        entry++;
                        pos = i + 1;
                    }
                }
                if (pos < length)
                {
                    position[injectIndex + entry] = FastParse.ParseFixedFloat(line, pos, length - pos);
                }
            }
            // Close our access to the file streams
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// </summary>
        /// <param name="csv">The CSV file to read</param>
        /// <param name="header">Does the CSV have a header?</param>
        public void LoadCsvTypes(string csv, bool header)
        {
            LoadCsvTypes(csv, header, 0, 0);
        }

        /// <param name="csv">The file path to the csv file to load</param>
        /// <param name="header">Does the CSV have a header?</param>
        /// <param name="offset">Should we offset the CSV's information in the types?</param>
        public void LoadCsvTypes(string csv, bool header, int offset)
        {
            LoadCsvTypes(csv, header, 0, offset);
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// Multiple entries are stored as different types.
        /// </summary>
        /// <param name="csv">The CSV file to read</param>
        /// <param name="header">Does the CSV have a header?</param>
        /// <param name="offsetTimes">The offset into the times</param>
        /// <param name="offsetType">Should we offset the CSV's information in the types?</param>
        public void LoadCsvTypes(string csv, bool header, int offsetTimes, int offsetType)
        {
            // Gain access to the files
            using StreamReader reader = new(new
                FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read,
                0x1000, FileOptions.SequentialScan));
            string line;
            var ammountOfData = Types * Times;
            int injectIndex = Times * offsetType + offsetTimes;
            if (header) reader.ReadLine();
            // Read the line from the CSV

            while ((line = reader.ReadLine()) != null)
            {
                // Calculate where to store the data
                string[] data = line.Split(',');
                int o = int.Parse(data[0]);
                int d = int.Parse(data[1]);
                var position = Data[o, d];
                if (position == null)
                {
                    Data[o, d] = position = new float[ammountOfData];
                }
                int entry = 0;
                for (int i = 2; i < data.Length; i++)
                {
                    position[injectIndex + entry] = float.Parse(data[i]);
                    entry++;
                }
            }
            // Close our access to the file streams
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// </summary>
        /// <summary>
        ///  Loads the data from an emme2 311 file into a ODC
        /// </summary>
        /// <param name="emme2File">The emm2 file to read from</param>
        /// <param name="offset">The type offset to use</param>
        public void LoadEmme2(string emme2File, int offset)
        {
            LoadEmme2(emme2File, 0, offset);
        }

        /// <summary>
        ///  Loads the data from an emme2 311 file into a ODC
        /// </summary>
        /// <param name="emme2File">The emm2 file to read from</param>
        /// <param name="offsetTimes">The time offset to use</param>
        /// <param name="offsetType">The type offset to use</param>
        public void LoadEmme2(string emme2File, int offsetTimes, int offsetType)
        {
            string line;
            int pos;
            var ammountOfData = Types * Times;
            // do this because highest zone isn't high enough for array indexes
            using StreamReader reader = new(new
                FileStream(emme2File, FileMode.Open, FileAccess.Read, FileShare.Read,
                0x1000, FileOptions.SequentialScan));
            int injectIndex = Times * offsetType + offsetTimes;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length > 0 && line[0] == 'a') break;
            }
            while ((line = reader.ReadLine()) != null)
            {
                int length = line.Length;
                // don't read blank lines
                if (length < 7) continue;
                int o = FastParse.ParseFixedInt(line, 0, 7);
                pos = 7;
                while (pos + 8 <= length)
                {
                    int d = FastParse.ParseFixedInt(line, pos, 7);
                    var data = Data[o, d];
                    if (data == null)
                    {
                        Data[o, d] = data = new float[ammountOfData];
                    }
                    if (line[pos + 7] == ':')
                    {
                        data[injectIndex] = FastParse.ParseFixedFloat(line, pos + 8, 5);
                    }
                    else
                    {
                        data[injectIndex] = FastParse.ParseFixedFloat(line, pos + 8, (length > ((pos + 8) + 9) ? 9 : (length - (pos + 8))));
                    }
                    pos += 13;
                }
            }
        }

        /// <summary>
        /// Save the ODC to file
        /// </summary>
        /// <param name="fileName">The file to save this as.</param>
        /// <param name="xmlInfo"></param>
        public void Save(string fileName, bool xmlInfo)
        {
            using BinaryWriter writer = new(new
            FileStream(fileName, FileMode.Create, FileAccess.Write,
            FileShare.None, 0x10000, FileOptions.RandomAccess),
            Encoding.Default);
            //Write the primary header
            writer.Write(0);
            writer.Write(Times);
            writer.Write(Types);
            var version2DataSize = WriteVersion2Data(writer);
            Index[] gaps = CreateIndexes(Data);
            writer.Write(gaps.Length); // we will figure what this number is later
            Save(gaps, writer, version2DataSize);
            //complete
            writer.Seek(0, SeekOrigin.Begin);
            // write that this is a version 2 file
            writer.Write(2);
        }

        protected virtual Dictionary<string, string> GetMetaData()
        {
            // we don't actually do anything here
            return null;
        }

        private Index[] CreateIndexes(SparseTwinIndex<float[]> data)
        {
            var validIndexes = data.ValidIndexArray();
            // since we are creating a square structure this will work
            var subs = GetSubIndexes(validIndexes);
            for (var i = 0; i < subs.Length; i++)
            {
                subs[i].SubIndex = subs;
            }
            return subs;
        }

        private Index[] GetSubIndexes(int[] validIndexes)
        {
            List<Index> ret = [];
            Index current = new();
            int position = current.Start = validIndexes[0];
            for (int i = 1; i < validIndexes.Length; i++)
            {
                if (position < validIndexes[i] - 1)
                {
                    current.End = validIndexes[i - 1];
                    ret.Add(current);
                    current.Start = validIndexes[i];
                }
                position = validIndexes[i];
            }
            current.End = validIndexes[validIndexes.Length - 1];
            ret.Add(current);
            return ret.ToArray();
        }

        private void Save(Index[] blocks, BinaryWriter writer, int versionDataSize)
        {
            // ForAll oIndex [versionDataSize includes the header size]
            long indexLocation = 4 * sizeof(float) + versionDataSize;
            // the header + each o block [4] start [4] end [8] location +
            long subIndexLocation = (blocks.Length * Index.SizeOf) + indexLocation;
            //#of d blocks (sizeof(unit)) [ since it is square we can just look at the top level]
            long dataLocation = subIndexLocation + (blocks.Length * sizeof(uint)) + ((blocks.Length * blocks.Length) * Index.SizeOf);
            long initDataLocation = dataLocation;
            var ammountOfData = Times * Types;
            foreach (var oBlock in blocks)
            {
                // Store(oIndex,startByte)
                writer.BaseStream.Position = indexLocation;
                writer.Write((uint)oBlock.Start);
                writer.Write((uint)oBlock.End);
                writer.Write(subIndexLocation);

                indexLocation = writer.BaseStream.Position;
                writer.BaseStream.Position = subIndexLocation;
                writer.Write((uint)oBlock.SubIndex.Length);
                foreach (var dBlock in oBlock.SubIndex)
                {
                    writer.Write((uint)dBlock.Start);
                    writer.Write((uint)dBlock.End);
                    writer.Write(dataLocation);
                    dataLocation += (dBlock.End - dBlock.Start + 1) *
                        (oBlock.End - oBlock.Start + 1) * ammountOfData * sizeof(float);
                }
                subIndexLocation = writer.BaseStream.Position;
            }

            // Now Store data
            var flatData = Data.GetFlatData();
            writer.BaseStream.Position = initDataLocation;
            for (int i = 0; i < flatData.Length; i++)
            {
                for (int j = 0; j < flatData[i].Length; j++)
                {
                    if (flatData[i][j] == null)
                    {
                        for (int k = 0; k < ammountOfData; k++)
                        {
                            writer.Write(0.0f);
                        }
                    }
                    else
                    {
                        for (int k = 0; k < ammountOfData; k++)
                        {
                            writer.Write(flatData[i][j][k]);
                        }
                    }
                }
            }
        }

        private void WriteMetaData(BinaryWriter writer, Dictionary<string, string> metaData)
        {
            // write the data to disk
            writer.Write(metaData.Count);
            foreach (var entry in metaData)
            {
                writer.Write(entry.Key);
                writer.Write(entry.Value);
            }
        }

        private int WriteVersion2Data(BinaryWriter writer)
        {
            // write the total length int
            writer.Write(0);
            // include all of the version 2 information
            var start = writer.BaseStream.Position;
            // write the description to the stream
            var metaData = GetMetaData();
            if (metaData != null)
            {
                WriteMetaData(writer, metaData);
            }
            var length = (int)(writer.BaseStream.Position - start);
            writer.Seek(-length - sizeof(int), SeekOrigin.Current);
            writer.Write(length);
            // no + sizeof( int ) because we just wrote to the stream sizeof( int )
            writer.Seek(length, SeekOrigin.Current);
            // +4 because we also have the length stored in an int32
            return length + 4;
        }

        private struct Index
        {
            // [4] start, [4] end, [16] sub index location
            public static int SizeOf = 16;

            public int End;
            public int Start;
            public Index[] SubIndex;

            public override string ToString()
            {
                return String.Concat(Start.ToString(), "->", End.ToString());
            }
        }
    }
}