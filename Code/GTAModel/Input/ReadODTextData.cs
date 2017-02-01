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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    [ModuleInformation(Description =
        @"<p>This module provides the ability to read OD data from .txt formats or .csv formats.  
It works by reading in the first three “columns”, where they could be separated by spaces, or tabs, or commas.  
It also tries to read in the standard</p>
<p>-----------------</p>
<p>My Header, some information about the data</p>
<p>--------------------------</p>
<p>headers and ignore them.  If one of these headers is not present or the header parameter is set 
it will skip the first line so that standard csv data will also work.</p>"
        )]
    public class ReadODTextData : IReadODData<float>
    {
        [RunParameter("Contains Dimension Information", false, "Does the input file contain a row after the header containing information about the dimensionality of the data?")]
        public bool ContainsDimensionInformation;

        [RunParameter("File Name", "ODData.txt", "The name of the file to read in (.txt or .csv) relative to the input directory.")]
        public string FileName;

        [RunParameter("Header", true, "Does this file contain a header?")]
        public bool Header;

        [RunParameter("From Input Directory", true, "Should we load from the input directory (true), or the output directory (false)?")]
        public bool FromInputDirectory;

        [RootModule]
        public IModelSystemTemplate Root;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get;
            set;
        }

        public IEnumerable<ODData<float>> Read()
        {
            BinaryReader reader = null;
            var fileName = FromInputDirectory ? GetInputFileName(FileName) : FileName;
            try
            {
                reader = new BinaryReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException("Unable to read the file " + fileName + " please make sure that this file exists!");
            }
            // you can not have a try while using yield
            using (reader)
            {
                char c = reader.ReadChar();
                // check to see if this file has a header
                if (c == '/' | c == '-' | Header)
                {
                    // Check to see if it is a CSV header or the --- header type
                    if (c == '-')
                    {
                        // burn the rest of the line, and all lines until after the next set
                        do
                        {
                            BurnLine(reader);
                        } while (reader.ReadChar() != '-');
                        // burn the line before the rest of the text
                        BurnLine(reader);
                        if (ContainsDimensionInformation)
                        {
                            // if it has dimensionality information we should also burn an extra line
                            BurnLine(reader);
                        }
                    }
                    else if (c == '/')
                    {
                        do
                        {
                            c = reader.ReadChar();
                            if (c != '/')
                            {
                                reader.BaseStream.Position--;
                                break;
                            }
                            BurnLine(reader);
                            c = reader.ReadChar();
                        } while (reader.BaseStream.Position < reader.BaseStream.Length &&
                            c == '/');
                    }
                    // the csv header only needs to burn the one line
                    if (Header)
                    {
                        BurnLine(reader);
                    }
                    // if we are at the end of file break
                    if (EndOfFile(reader)) yield break;
                    c = reader.ReadChar();
                }
                reader.BaseStream.Position--;
                // start to process the data
                ODData<float> data;
                while (!EndOfFile(reader))
                {
                    if (ReadDataLine(reader, out data))
                    {
                        yield return data;
                    }
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        protected static bool WhiteSpace(char p)
        {
            switch (p)
            {
                case '\t':
                case ' ':
                case ',':
                    return true;

                default:
                    return false;
            }
        }

        protected void BurnLine(BinaryReader reader)
        {
            // read until we are at the end of file or when we hit a new line
            while (!EndOfFile(reader) && reader.ReadChar() != '\n') ;
        }

        protected void BurnWhiteSpace(BinaryReader reader, ref char c)
        {
            while (!EndOfFile(reader) && WhiteSpace(c = reader.ReadChar())) ;
        }

        protected bool EndOfFile(BinaryReader reader)
        {
            var bs = reader.BaseStream;
            var length = bs.Length;
            var position = bs.Position;
            Progress = (float)position / length;
            return length == position;
        }

        protected string GetInputFileName(string localPath)
        {
            var fullPath = localPath;
            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.Combine(Root.InputBaseDirectory, fullPath);
            }
            return fullPath;
        }

        protected virtual bool ReadDataLine(BinaryReader reader, out ODData<float> data)
        {
            char c = '\0';
            // Read in the origin
            data.O = 0;
            data.D = 0;
            data.Data = 0f;
            if (!ReadInteger(reader, ref c, out data.O)) return false;
            if (!ReadInteger(reader, ref c, out data.D)) return false;
            if (!ReadFloat(reader, ref c, out data.Data)) return false;
            // burn the remainder of the line
            if (c != '\n')
            {
                BurnLine(reader);
            }
            return true;
        }

        protected bool ReadFloat(BinaryReader reader, ref char c, out float p)
        {
            // Read in the Data
            BurnWhiteSpace(reader, ref c);
            int pastDecimal = -1;
            int exponent = 0;
            p = 0;
            bool exponential = false;
            bool negative = false;
            bool negativeExponential = false;
            do
            {
                if (exponential)
                {
                    if (c == '-')
                    {
                        negativeExponential = true;
                    }
                    else if (c == '+')
                    {
                        // do nothing
                    }
                    else if ((c < '0' | c > '9'))
                    {
                        throw new XTMFRuntimeException("In " + Name + ", We found a " + c + " while trying to read in the zone data in the file '" + (FromInputDirectory ? GetInputFileName(FileName) : FileName) + "'!");
                    }
                    else
                    {
                        exponent = exponent * 10 + (c - '0');
                    }
                }
                else
                {
                    if ((c == '.'))
                    {
                        pastDecimal = 0;
                    }
                    else
                    {
                        if (c == 'e' | c == 'E')
                        {
                            exponential = true;
                        }
                        else if (c == '-')
                        {
                            negative = true;
                        }
                        else if ((c < '0' | c > '9'))
                        {
                            throw new XTMFRuntimeException("In " + Name + ", We found a " + c + " while trying to read in the zone data in the file '" + (FromInputDirectory ? GetInputFileName(FileName) : FileName) + "'!");
                        }
                        else
                        {
                            p = p * 10 + (c - '0');
                            if (pastDecimal >= 0)
                            {
                                pastDecimal++;
                            }
                        }
                    }
                }
            } while (!EndOfFile(reader) && (c = reader.ReadChar()) != '\t' & c != '\n' & c != '\r' & c != ' ' & c != ',');
            if (negativeExponential)
            {
                exponent = -exponent;
            }
            if (pastDecimal > 0)
            {
                p = p * (float)Math.Pow(0.1, pastDecimal - exponent);
            }
            if (negative)
            {
                p = -p;
            }
            return true;
        }

        protected bool ReadInteger(BinaryReader reader, ref char c, out int p)
        {
            p = 0;
            BurnWhiteSpace(reader, ref c);
            do
            {
                if (c == '\n' | c == '\r')
                {
                    p = -1;
                    return false;
                }
                if (c < '0' | c > '9')
                {
                    throw new XTMFRuntimeException("In " + Name + ", We found a " + c + " while trying to read in the origin zone in the file '" + (FromInputDirectory ? GetInputFileName(FileName) : FileName) + "'!");
                }
                p = p * 10 + (c - '0');
            } while (!EndOfFile(reader) && (c = reader.ReadChar()) != '\t' & c != ' ' & c != ',');
            return true;
        }
    }
}