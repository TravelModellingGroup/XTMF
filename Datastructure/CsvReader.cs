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

namespace Datastructure
{
    public sealed class CsvReader : IDisposable
    {
        internal char[] LineBuffer = new char[512];

        internal int LinePosition = 0;

        /// <summary>
        /// The reader we use to get the IO data
        /// </summary>
        internal BinaryReader Reader;

        /// <summary>
        /// The segments set when we read in a line
        /// </summary>
        private CSVPartition[] Data = new CSVPartition[50];

        private char[] DataBuffer = new char[0x400];

        private int DataBufferLength;

        private int DataBufferPosition;

        private bool LoadedFromStream;

        private long StreamLength;

        /// <summary>
        /// Create a link to a CSV file
        /// </summary>
        /// <param name="fileName"></param>
        public CsvReader(string fileName)
        {
            this.FileName = fileName;
            this.Reader = new BinaryReader( File.OpenRead( fileName ) );
            this.BaseStream = this.Reader.BaseStream;
            this.LoadedFromStream = false;
            this.StreamLength = this.BaseStream.Length;
            this.DataBufferLength = -1;
        }

        public CsvReader(Stream stream)
        {
            this.FileName = "Stream";
            this.Reader = new BinaryReader( stream );
            this.BaseStream = this.Reader.BaseStream;
            this.LoadedFromStream = true;
            this.DataBufferLength = -1;
        }

        public Stream BaseStream
        {
            get;
            private set;
        }

        public bool EndOfFile
        {
            get
            {
                return this.DataBufferLength == 0;
            }
        }

        /// <summary>
        /// The file name
        /// </summary>
        public string FileName { get; private set; }

        public void Close()
        {
            this.Dispose( false );
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out float item, int pos)
        {
            int first;
            var data = this.Data[pos];
            item = CsvParse.ParseFixedFloat( this.LineBuffer, ( first = data.Start ), data.End - first );
        }

        /// <summary>
        /// Get an int from a col position
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out int item, int pos)
        {
            int first;
            var data = this.Data[pos];
            item = CsvParse.ParseFixedInt( this.LineBuffer, ( first = data.Start ), data.End - first );
        }

        /// <summary>
        /// Get a character out
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out char item, int pos)
        {
            item = this.LineBuffer[this.Data[pos].Start];
        }

        /// <summary>
        /// Get a string out
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out string item, int pos)
        {
            var data = this.Data[pos];
            item = new string( this.LineBuffer, data.Start, data.End - data.Start );
        }

        /// <summary>
        /// Reads in a line and gives the number of columns
        /// </summary>
        /// <returns>The number of columns returned</returns>
        public int LoadLine()
        {
            int res;
            LoadLine( out res );
            return res;
        }

        /// <summary>
        /// Reads in a line and gives the number of columns
        /// </summary>
        /// <param name="columns">The number of columns read in for this line.</param>
        /// <returns>True if data was read. (Not end of file)</returns>
        public bool LoadLine(out int columns)
        {
            int numberOfColumns = 0;
            this.LinePosition = 0;
            if ( Reader == null ) throw new IOException( "No file has been loaded!" );
            if ( this.FastEndOfFile() )
            {
                columns = 0;
                return false;
            }
            int prevEnd = -1;
            bool addOne = false;
            int i = 0;
            char prevC = '\0';
            bool quote = false;
            while ( true )
            {
                char c;
                // make sure there is data
                if ( this.DataBufferPosition >= this.DataBufferLength )
                {
                    this.LoadInData();
                    // if we are at the end of file just end it
                    if ( this.DataBufferLength <= 0 )
                    {
                        if ( addOne )
                        {
                            Data[numberOfColumns].Start = prevEnd + 1;
                            Data[numberOfColumns++].End = prevEnd = i - 1;
                        }
                        columns = numberOfColumns;
                        return true;
                    }
                }
                c = this.DataBuffer[this.DataBufferPosition++];
                if ( ( c == '\n' ) | ( c == '\0' ) )
                {
                    if ( prevC != '\r' )
                    {
                        if ( Data.Length <= numberOfColumns )
                        {
                            ExpandDataSections();
                        }
                        quote = false;
                        Data[numberOfColumns].Start = prevEnd + 1;
                        Data[numberOfColumns++].End = prevEnd = i;
                    }
                    else
                    {
                        prevEnd = i;
                    }
                    break;
                }
                else if ( ( c == '"' ) )
                {
                    if ( prevEnd == i - 1 )
                    {
                        quote = true;
                        continue;
                    }
                    else if ( quote )
                    {
                        quote = false;
                        continue;
                    }
                    // if it is just in the middle continue on
                }
                if ( LinePosition >= this.LineBuffer.Length )
                {
                    var temp = new char[this.LineBuffer.Length * 2];
                    Array.Copy( this.LineBuffer, temp, LineBuffer.Length );
                    this.LineBuffer = temp;
                }
                this.LineBuffer[this.LinePosition++] = c;
                // if a comma or an end quote followed by a comma
                if ( ( !quote & ( c == ',' | c == '\t' ) )
                    | c == '\r' )
                {
                    addOne = false;
                    if ( Data.Length <= numberOfColumns )
                    {
                        ExpandDataSections();
                    }
                    Data[numberOfColumns].Start = prevEnd + 1;
                    Data[numberOfColumns++].End = prevEnd = i;
                }
                else
                {
                    addOne = true;
                }
                prevC = c;
                i++;
            }
            // check to see if there was actually no data
            if ( this.LinePosition == 0 || ( numberOfColumns == 1 && Data[0].End == 0 ) )
            {
                columns = 0;
            }
            else
            {
                columns = addOne ? numberOfColumns + 1 : numberOfColumns;
            }
            return true;
        }

        /// <summary>
        /// Reset to go to the start
        /// </summary>
        public void Reset()
        {
            if ( this.Reader.BaseStream == null )
            {
                if ( !this.LoadedFromStream )
                {
                    this.Reader.Dispose();
                }
                this.Reader = new BinaryReader( File.OpenRead( FileName ) );
                this.BaseStream = this.Reader.BaseStream;
                this.LoadInData();
            }
            else
            {
                this.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );
            }
            this.DataBufferLength = -1;
        }

        private void ExpandDataSections()
        {
            var temp = new CSVPartition[this.Data.Length * 2];
            var local = this.Data;
            for ( int i = 0; i < local.Length; i++ )
            {
                temp[i] = local[i];
            }
            this.Data = temp;
        }

        private bool FastEndOfFile()
        {
            return this.DataBufferLength == 0;
        }

        private void LoadInData()
        {
            this.DataBufferPosition = 0;
            this.DataBufferLength = this.Reader.Read( this.DataBuffer, 0, this.DataBuffer.Length );
        }

        private struct CSVPartition
        {
            public int End;
            public int Start;
        }

        #region IDisposable Members

        public void Dispose(bool gcCalled)
        {
            if ( !gcCalled )
            {
                GC.SuppressFinalize( this );
            }
            if ( !this.LoadedFromStream )
            {
                this.Reader.Close();
            }
        }

        public void Dispose()
        {
            this.Dispose( true );
        }

        #endregion IDisposable Members
    }
}