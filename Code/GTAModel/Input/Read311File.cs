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
using System.Collections.Generic;
using System.IO;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class Read311File : IReadODData<float>
    {
        [SubModelInformation(Required = true, Description = "The location of the .311 file to read in.")]
        public FileLocation EmmeFile;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<ODData<float>> Read()
        {
            string line;
            int pos;
            var path = EmmeFile.GetFilePath();
            if (!File.Exists(path))
            {
                throw new XTMFRuntimeException(this, $"File not found at '{path}'!");
            }
            ODData<float> data = new ODData<float>();
            // do this because highest zone isn't high enough for array indexes
            using (StreamReader reader = new StreamReader(new
                FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                0x1000, FileOptions.SequentialScan)))
            {
                BurnHeader(reader);
                while ((line = reader.ReadLine()) != null)
                {
                    pos = 0;
                    int length = line.Length;
                    // don't read blank lines
                    if (ReadNextInt(line, length, ref pos, out data.O))
                    {
                        while (ReadNextInt(line, length, ref pos, out data.D))
                        {
                            if (ReadNextFloat(line, length, ref pos, out data.Data))
                            {
                                yield return data;
                            }
                        }
                    }
                }
            }
        }

        private static bool ReadNextFloat(string line, int lineLength, ref int position, out float result)
        {
            int start = -1;
            bool any = false;
            for (; position < lineLength; position++)
            {
                var c = line[position];
                if (Char.IsDigit(c) | (c == '.'))
                {
                    if (start < 0)
                    {
                        start = position;
                        any = true;
                    }
                }
                else if (any)
                {
                    break;
                }
            }
            if (any)
            {
                result = FastParse.ParseFixedFloat(line, start, position - start);
                return true;
            }
            result = -1;
            return false;
        }

        private static bool ReadNextInt(string line, int lineLength, ref int position, out int result)
        {
            int start = -1;
            bool any = false;
            for (; position < lineLength; position++)
            {
                var c = line[position];
                if (Char.IsDigit(c))
                {
                    if (start < 0)
                    {
                        start = position;
                        any = true;
                    }
                }
                else if (any)
                {
                    break;
                }
            }
            if (any)
            {
                result = FastParse.ParseFixedInt(line, start, position - start);
                return true;
            }
            result = -1;
            return false;
        }

        private static void BurnHeader(StreamReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length > 0 && line[0] == 't')
                {
                    //Requires a correct t-record
                    if (!line.StartsWith("t matrices")) throw new IOException("Invalid matrix file");
                    break;
                }
            }
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length > 0 && line[0] == 'a') break;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}