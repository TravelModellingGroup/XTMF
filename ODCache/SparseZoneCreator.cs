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
using System.Text;

namespace Datastructure
{
    public class SparseZoneCreator
    {
        private const int Version = 2;
        private float[][] Data;
        private int Types;

        public SparseZoneCreator(int highestZone, int types)
        {
            Types = types;
            Data = new float[highestZone + 1][];
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// </summary>
        /// <param name="csv">The CSV file to parse</param>
        /// <param name="header">Does this csv file contain a header?</param>
        /// <param name="offset">How much other data comes before our new entries?</param>
        public void LoadCsv(string csv, bool header, int offset = 0)
        {
            using CsvReader reader = new(csv);
            var dataLength = Data.Length;
            if (header) reader.LoadLine();
            while (!reader.EndOfFile)
            {
                int length;
                if ((length = reader.LoadLine()) < 2) continue;
                reader.Get(out int origin, 0);
                if ((origin < 0)) continue;
                if (origin >= dataLength)
                {
                    var temp = new float[origin + 1][];
                    Array.Copy(Data, temp, dataLength);
                    Data = temp;
                    dataLength = origin + 1;
                }
                if (Data[origin] == null)
                {
                    Data[origin] = new float[Types];
                }
                // add in the offset off the bat
                int loaded = offset;
                for (int i = 1; i < length; i++)
                {
                    reader.Get(out Data[origin][loaded++], i);
                }
            }
        }

        public void Save(string fileName)
        {
            using BinaryWriter writer = new(new
                FileStream(fileName, FileMode.Create, FileAccess.Write,
                FileShare.None, 0x8000, FileOptions.SequentialScan),
                Encoding.Default);
            var dataLength = Data.Length;
            int highestZone = 0;

            for (int i = 0; i < dataLength; i++)
            {
                if (Data[i] != null) highestZone = i;
            }
            writer.Write(highestZone);
            writer.Write(Version);
            writer.Write(Types);
            WriteSparseIndexes(writer);

            for (int i = 0; i < dataLength; i++)
            {
                if (Data[i] != null)
                {
                    for (int j = 0; j < Types; j++)
                    {
                        writer.Write(Data[i][j]);
                    }
                }
            }
        }

        private void GenerateIndexes(List<SparseSet> indexes)
        {
            int length = Data.Length;
            for (int i = 0; i < length; i++)
            {
                if (Data[i] == null) continue;
                for (int j = i + 1; ; j++)
                {
                    if (j == length || Data[j] == null)
                    {
                        indexes.Add(new SparseSet() { Start = i, Stop = j - 1 });
                        // we don't add one here since the outer for loop will increment it
                        i = j;
                        break;
                    }
                }
            }
        }

        private void WriteSparseIndexes(BinaryWriter writer)
        {
            List<SparseSet> indexes = [];
            GenerateIndexes(indexes);
            var numberOfIndexes = indexes.Count;
            // start == 4 stop == 4, disk location = 4
            long baseLocation = 12 + 16 * numberOfIndexes + 4; // skip the header and the indexes for the start of data (also skip number of headers)
            writer.Write(numberOfIndexes);
            for (int i = 0; i < numberOfIndexes; i++)
            {
                writer.Write(indexes[i].Start);
                writer.Write(indexes[i].Stop);
                writer.Write(baseLocation);
                baseLocation += (indexes[i].Stop - indexes[i].Start + 1) * Types * 4;
            }
        }
    }
}