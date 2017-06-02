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
using System.IO;
using System.Text;

namespace Datastructure
{
    public class ZoneCreator
    {
        /// <summary>
        /// Converts a csv file into odc.
        /// </summary>
        /// <param name="csv">The CSV file to parse</param>
        /// <param name="highestZone">The highest numbered zone</param>
        /// <param name="types">The number of types of data per recored</param>
        /// <param name="zfc">The ZFC we are to produce</param>
        /// <param name="header">Does this csv file contain a header?</param>
        public static void CsvToZfc(string csv, int highestZone, int types, string zfc, bool header)
        {
            CsvToZfc(csv, highestZone, types, zfc, header, 0);
        }

        /// <summary>
        /// Converts a csv file into odc.
        /// </summary>
        /// <param name="csv">The CSV file to parse</param>
        /// <param name="highestZone">The highest numbered zone</param>
        /// <param name="types">The number of types of data per recored</param>
        /// <param name="zfc">The ZFC we are to produce</param>
        /// <param name="header">Does this csv file contain a header?</param>
        /// <param name="offset">How much other data comes before our new entries?</param>
        public static void CsvToZfc(string csv, int highestZone, int types, string zfc, bool header, int offset)
        {
            StreamReader reader = new StreamReader(new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.SequentialScan));
            BinaryWriter writer = new BinaryWriter(new
                FileStream(zfc, FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.None, 0x1000, FileOptions.RandomAccess),
                Encoding.Default);
            string line;
            writer.Write(highestZone);
            writer.Write(0);
            writer.Write(types);
            byte[] data = new byte[types * 4];
            if (header) reader.ReadLine();
            while ((line = reader.ReadLine()) != null)
            {
                int position;
                int origin = FastParse.ParseInt(line, 0, (position = line.IndexOf(',')));
                position++;

                writer.BaseStream.Seek((sizeof(int) * 3) +
                    ((origin * types) + offset) * sizeof(float), SeekOrigin.Begin);

                // It is faster to store the length before itterating over it
                int length = line.Length;
                int ammount = 0;
                for (int i = position; i < length; i++)
                {
                    if (line[i] == ',')
                    {
                        LoadData(data, FastParse.ParseFloat(line, position, i), ref ammount);
                        position = i + 1;
                    }
                }
                LoadData(data, FastParse.ParseFloat(line, position, line.Length), ref ammount);
                writer.Write(data, 0, ammount);
            }
            reader.Close();
            writer.Close();
        }

        private static void LoadData(byte[] data, float p, ref int ammount)
        {
            var temp = BitConverter.GetBytes(p);
            data[ammount] = temp[0];
            data[ammount + 1] = temp[1];
            data[ammount + 2] = temp[2];
            data[ammount + 3] = temp[3];
            ammount += 4;
        }
    }
}