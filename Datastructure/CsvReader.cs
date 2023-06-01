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
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;


namespace Datastructure
{
    public sealed class CsvReader : IDisposable
    {
        public long LineNumber { get; private set; } = 0;

        internal char[] LineBuffer = new char[512];

        internal int LinePosition;

        /// <summary>
        /// The reader we use to get the IO data
        /// </summary>
        private BinaryReader Reader;

        /// <summary>
        /// This field will be set if we needed to
        /// create an inner stream for decompression.
        /// Reset using this stream instead of reader if
        /// it is not null.
        /// </summary>
        private Stream? _innerStream;

        /// <summary>
        /// The segments set when we read in a line
        /// </summary>
        private CsvPartition[] Data = new CsvPartition[50];
        private const int _bufferSize = 0x40000;
        private char[]? DataBuffer = new char[_bufferSize];
        private char[]? DataBuffer2;

        private int DataBufferLength;

        private int DataBufferPosition;

        private readonly bool LoadedFromStream;

        private readonly bool SpacesAsSeperator;

        /// <summary>
        /// Create a link to a CSV file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="spacesAsSeperator">If true, spaces outside of spaces will denote breaks for columns</param>
        public CsvReader(string fileName, bool spacesAsSeperator = false)
        {
            FileName = fileName;
            LoadReaderFromFile();
            BaseStream = Reader!.BaseStream;
            LoadedFromStream = false;
            DataBufferLength = -1;
            SpacesAsSeperator = spacesAsSeperator;
        }

        private void LoadReaderFromFile()
        {
            BaseStream?.Dispose();
            _innerStream?.Dispose();
            // Check to see if the CSV is compressed
            if (Path.GetExtension(FileName)?.Equals(".gz", StringComparison.OrdinalIgnoreCase) == true)
            {
                _innerStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                BaseStream = new GZipStream(_innerStream, CompressionMode.Decompress, false);  
            }
            else
            {
                BaseStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            Reader = new BinaryReader(BaseStream);
        }

        public CsvReader(FileInfo fileInfo, bool spacesAsSeperator = false)
            : this(fileInfo.FullName, spacesAsSeperator)
        {
        }

        public CsvReader(Stream stream, bool spacesAsSeperator = false)
        {
            FileName = "Stream";
            Reader = new BinaryReader(stream);
            BaseStream = Reader.BaseStream;
            LoadedFromStream = true;
            DataBufferLength = -1;
            SpacesAsSeperator = spacesAsSeperator;
        }
        public Stream BaseStream { get; private set; }

        public bool EndOfFile => DataBufferLength == 0;

        /// <summary>
        /// The file name
        /// </summary>
        public string FileName { get; private set; }

        public void Close()
        {
            Dispose(false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out float item, int pos)
        {
            int first;
            var data = Data[pos];
            item = CsvParse.ParseFixedFloat(LineBuffer, (first = data.Start), data.End - first);
        }

        /// <summary>
        /// Get an int from a col position
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out int item, int pos)
        {
            int first;
            var data = Data[pos];
            item = CsvParse.ParseFixedInt(LineBuffer, (first = data.Start), data.End - first);
        }

        /// <summary>
        /// Get a character out
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out char item, int pos)
        {
            item = LineBuffer[Data[pos].Start];
        }

        /// <summary>
        /// Get a string out
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out string item, int pos)
        {
            var data = Data[pos];
            item = new string(LineBuffer, data.Start, data.End - data.Start);
        }

        /// <summary>
        /// Get a bool out
        /// </summary>
        /// <param name="item">Where to put the data</param>
        /// <param name="pos">Which column to read from, 0 indexed</param>
        public void Get(out bool item, int pos)
        {
            var data = Data[pos];
            bool.TryParse(new string(LineBuffer, data.Start, data.End - data.Start), out item);
        }

        /// <summary>
        /// Reads in a line and gives the number of columns
        /// </summary>
        /// <returns>The number of columns returned</returns>
        public int LoadLine()
        {
            LoadLine(out int res);
            return res;
        }

        /// <summary>
        /// Reads in a line and gives the number of columns
        /// </summary>
        /// <param name="columns">The number of columns read in for this line.</param>
        /// <returns>True if data was read. (Not end of file)</returns>
        public bool LoadLine(out int columns)
        {
            LineNumber++;
            var numberOfColumns = 0;
            LinePosition = 0;
            if (Reader == null) throw new IOException("No file has been loaded!");
            if (FastEndOfFile())
            {
                columns = 0;
                return false;
            }
            var prevEnd = -1;
            var addOne = false;
            var i = 0;
            var prevC = '\0';
            var quote = false;
            var previousWasQuote = false;
            if (SpacesAsSeperator)
            {
                while (true)
                {
                    char c;
                    // make sure there is data
                    if (DataBufferPosition >= DataBufferLength)
                    {
                        LoadInData();
                        // if we are at the end of file just end it
                        if (DataBufferLength <= 0)
                        {
                            if (addOne)
                            {
                                Data[numberOfColumns].Start = prevEnd + 1;
                                Data[numberOfColumns++].End = i;
                            }
                            columns = numberOfColumns;
                            return true;
                        }
                    }
                    c = DataBuffer![DataBufferPosition++];
                    if ((c == '\n') | (c == '\0'))
                    {
                        if (Data.Length <= numberOfColumns)
                        {
                            ExpandDataSections();
                        }
                        Data[numberOfColumns].Start = prevEnd + 1;
                        Data[numberOfColumns++].End = i;
                        addOne = false;
                        break;
                    }
                    if ((c == '"'))
                    {
                        if (previousWasQuote)
                        {
                            // if the previous was a quote, reactive quote mode and
                            // add it to the line buffer
                            previousWasQuote = false;
                            quote = true;
                        }
                        else
                        {
                            previousWasQuote = true;
                            if (prevEnd == i - 1)
                            {
                                quote = true;
                                continue;
                            }
                            if (quote)
                            {
                                quote = false;
                                continue;
                            }
                        }
                        // if it is just in the middle continue on
                    }
                    else
                    {
                        previousWasQuote = false;
                    }
                    if (LinePosition >= LineBuffer.Length)
                    {
                        Array.Resize(ref LineBuffer, LineBuffer.Length * 2);
                    }
                    if (c != '\r')
                    {
                        LineBuffer[LinePosition++] = c;
                        // if a comma or an end quote followed by a comma
                        if ((!quote && (c == ',' || c == '\t' || (prevC != ' ' && c == ' '))))
                        {
                            addOne = false;
                            if (Data.Length <= numberOfColumns)
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
                }
            }
            else
            {
                while (true)
                {
                    char c;
                    // make sure there is data
                    if (DataBufferPosition >= DataBufferLength)
                    {
                        LoadInData();
                        // if we are at the end of file just end it
                        if (DataBufferLength <= 0)
                        {
                            if (addOne)
                            {
                                Data[numberOfColumns].Start = prevEnd + 1;
                                Data[numberOfColumns++].End = i;
                            }
                            columns = numberOfColumns;
                            return true;
                        }
                    }
                    c = DataBuffer![DataBufferPosition++];
                    if ((c == '\n') | (c == '\0'))
                    {
                        if (Data.Length <= numberOfColumns)
                        {
                            ExpandDataSections();
                        }
                        Data[numberOfColumns].Start = prevEnd + 1;
                        Data[numberOfColumns++].End = i;
                        addOne = false;
                        break;
                    }
                    if ((c == '"'))
                    {
                        if (previousWasQuote)
                        {
                            // if the previous was a quote, reactive quote mode and
                            // add it to the line buffer
                            previousWasQuote = false;
                            quote = true;
                        }
                        else
                        {
                            previousWasQuote = true;
                            if (prevEnd == i - 1)
                            {
                                quote = true;
                                continue;
                            }
                            if (quote)
                            {
                                quote = false;
                                continue;
                            }
                        }
                        // if it is just in the middle continue on
                    }
                    else
                    {
                        previousWasQuote = false;
                    }
                    if (LinePosition >= LineBuffer.Length)
                    {
                        Array.Resize(ref LineBuffer, LineBuffer.Length * 2);
                    }
                    if (c != '\r')
                    {
                        LineBuffer[LinePosition++] = c;

                        // if a comma or an end quote followed by a comma
                        if ((!quote && (c == ',' || c == '\t'))
                            || c == '\r')
                        {
                            addOne = false;
                            if (Data.Length <= numberOfColumns)
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
                        i++;
                    }
                }
            }
            // check to see if there was actually no data
            if (LinePosition == 0 || (numberOfColumns == 1 && Data[0].End == 0))
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
            if (!BaseStream.CanSeek)
            {
                LoadReaderFromFile();
            }
            else
            {
                BaseStream.Position = 0;
            }
            DataBuffer2 = null;
            DataBufferLength = -1;
            LineNumber = 0;
        }

        private void ExpandDataSections()
        {
            Array.Resize(ref Data, Data.Length * 2);
        }

        private bool FastEndOfFile()
        {
            return DataBufferLength == 0;
        }

        private volatile bool NextDataReady;
        private volatile int NextDataBufferLength;

        private void LoadInData()
        {
            if (DataBuffer2 == null)
            {
                DataBuffer2 = new char[_bufferSize];
                NextDataBufferLength = Reader.Read(DataBuffer2, 0, DataBuffer!.Length);
                NextDataReady = true;
            }
            // spin-wait on this being ready until the data is ready
            while (!NextDataReady)
            {
            }
            DataBufferPosition = 0;
            var temp = DataBuffer;
            DataBuffer = DataBuffer2;
            DataBuffer2 = temp;
            DataBufferLength = NextDataBufferLength;
            NextDataReady = false;
            // load the next set of data in parallel
            Task.Run(() =>
            {
                NextDataBufferLength = Reader.Read(DataBuffer2!, 0, DataBuffer.Length);
                Thread.MemoryBarrier();
                NextDataReady = true;
            });
        }

        private struct CsvPartition
        {
            public int End;
            public int Start;
        }

        #region IDisposable Members

        private void Dispose(bool gcCalled)
        {
            if (!gcCalled)
            {
                GC.SuppressFinalize(this);
            }
            if (!LoadedFromStream)
            {
                Reader?.Close();
            }
        }

        ~CsvReader()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion IDisposable Members
    }
}