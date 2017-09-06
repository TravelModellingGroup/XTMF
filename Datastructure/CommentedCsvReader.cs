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
        private long LinesRead;
        private readonly CsvReader Reader;

        /// <summary>
        /// </summary>
        /// <param name="fileName">The full path to the file.</param>
        public CommentedCsvReader(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }
            Reader = new CsvReader(fileName);
            LinesRead = 0;
            SetupReader();
        }

        public CommentedCsvReader(Stream stream)
        {
            Reader = new CsvReader( stream );
            LinesRead = 0;
            SetupReader();
        }

        public Stream BaseStream => Reader.BaseStream;

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
            Reader.Dispose();
        }

        public void Dispose()
        {
            Reader.Dispose();
        }

        public void Get(out string item, int index)
        {
            Reader.Get( out item, index );
        }

        public void Get(out char item, int index)
        {
            Reader.Get( out item, index );
        }

        public void Get(out float item, int index)
        {
            Reader.Get( out item, index );
        }

        public void Get(out int item, int index)
        {
            Reader.Get( out item, index );
        }

        /// <summary>
        /// Loads the next line, checking if EOF
        /// </summary>
        /// <returns>True if the line exists, false if the file has ended.</returns>
        public bool NextLine()
        {
            while ( !Reader.EndOfFile )
            {
                NumberOfCurrentCells = Reader.LoadLine();

                if ( Reader.LinePosition == 0 ) continue; //Ignore blank lines
                if ( Reader.LinePosition >= 2 )
                {
                    if ( Reader.LineBuffer[0] == '/' & Reader.LineBuffer[1] == '/' ) continue; //Skip commented lines
                }

                LinesRead++;
                if (Headers == null ) return true;
                if (NumberOfCurrentCells != Headers.Length )
                {
                    throw new IOException( "Error reading file '" + Reader.FileName + "' at line " + LinesRead + ": number of cells in the row (" + NumberOfCurrentCells +
                        ") is not equal to the number of headers defined in the file (" + Headers.Length + ")." );
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
            while (NextLine() && NumberOfCurrentCells <= 0)
            {
                // iterate until we have found the line containing headers
            }

            Headers = new string[NumberOfCurrentCells];
            for (var i = 0; i < NumberOfCurrentCells; i++)
            {
                Get(out string h, i);
                Headers[i] = h;
            }
        }
    }
}