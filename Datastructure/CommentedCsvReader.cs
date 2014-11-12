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
    /// <summary>
    /// A warpper around the CsvReader which reads a CSV file of a specified format. The first line in the file is assumed to
    /// be a header with column labels and all subsequent lines are data. Metadata can be added anywhere in the file, by beginning
    /// the line with '//'. These lines are skipped by the CommentedCsvReader.
    ///
    /// </summary>
    public sealed class CommentedCsvReader : IDisposable
    {
        private long linesRead;
        private CsvReader Reader;

        /// <summary>
        /// </summary>
        /// <param name="FileName">The full path to the file.</param>
        public CommentedCsvReader(string FileName)
        {
            this.Reader = new CsvReader( FileName );
            this.linesRead = 0;
            SetupReader();
        }

        public CommentedCsvReader(Stream Stream)
        {
            this.Reader = new CsvReader( Stream );
            this.linesRead = 0;
            SetupReader();
        }

        public Stream BaseStream
        {
            get
            {
                return this.Reader.BaseStream;
            }
        }

        /// <summary>
        /// The array of column labels.
        /// </summary>
        public string[] Headers
        {
            get;
            private set;
        }

        /// <summary>
        /// The number of cells (length) in the current row.
        /// </summary>
        public int NumberOfCurrentCells
        {
            get;
            private set;
        }

        public void Close()
        {
            this.Reader.Dispose();
        }

        public void Dispose()
        {
            this.Reader.Dispose();
        }

        public void Get(out string Item, int Index)
        {
            this.Reader.Get( out Item, Index );
        }

        public void Get(out char Item, int Index)
        {
            this.Reader.Get( out Item, Index );
        }

        public void Get(out float Item, int Index)
        {
            this.Reader.Get( out Item, Index );
        }

        public void Get(out int Item, int Index)
        {
            this.Reader.Get( out Item, Index );
        }

        /// <summary>
        /// Loads the next line, checking if EOF
        /// </summary>
        /// <returns>True if the line exists, false if the file has ended.</returns>
        public bool NextLine()
        {
            while ( !Reader.EndOfFile )
            {
                this.NumberOfCurrentCells = Reader.LoadLine();

                if ( Reader.LinePosition == 0 ) continue; //Ignore blank lines
                if ( Reader.LinePosition >= 2 )
                {
                    if ( Reader.LineBuffer[0] == '/' & Reader.LineBuffer[1] == '/' ) continue; //Skip commented lines
                }

                this.linesRead++;
                if ( this.Headers == null ) return true;
                if ( this.NumberOfCurrentCells != this.Headers.Length )
                {
                    throw new IOException( "Error reading file '" + Reader.FileName + "' at line " + linesRead + ": number of cells in the row (" + this.NumberOfCurrentCells +
                        ") is not equal to the number of headers defined in the file (" + this.Headers.Length + ")." );
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Initialize the reader and parse the headers
        /// </summary>
        private void SetupReader()
        {
            // iterate here until we are either at the end of the file or a place with a header
            while ( this.NextLine() && this.NumberOfCurrentCells <= 0 ) ;

            this.Headers = new string[this.NumberOfCurrentCells];
            string h = "";
            for ( int i = 0; i < this.NumberOfCurrentCells; i++ )
            {
                this.Get( out h, i );
                Headers[i] = h;
            }
        }
    }
}