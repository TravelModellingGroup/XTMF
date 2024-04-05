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

namespace Datastructure
{
    /// <summary>
    /// Modified StreamReader which ignores any lines starting with "//"
    /// </summary>
    public class CommentedStreamReader : StreamReader
    {
        public CommentedStreamReader(string filename)
            : base( filename )
        {
        }

        public override string? ReadLine()
        {
            string? line;
            while ( !EndOfStream )
            {
                line = base.ReadLine();
                if ( line != null && !line.StartsWith( "//" ) )
                {
                    return line;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Flexible sparse structure for loading multi-dimensional tables.
    ///
    /// Supports I/O to/from a CSV with a single-line header. The column headers are used to attach
    /// metadata to this class.
    ///
    /// It uses a custom CommentedStreamReader class which ignores and line starting with '//'
    ///
    /// </summary>
    public class SparseMultiIndexTable
    {
        public readonly string[] IndexNames;

        //Metadata on index names
        public string Description = "";

        private readonly Dictionary<string, float> Data; //The actual data.
        private readonly float DefaultValue;
        private readonly HashSet<int>[] MappedIndices; //Stores the contained values of each index. Its length is equal to the number of dimensions
        //The default value for this table.

        public SparseMultiIndexTable(int numberOfIndices, float defaultValue)
        {
            MappedIndices = new HashSet<int>[numberOfIndices];
            for ( var i = 0; i < numberOfIndices; i++ ) MappedIndices[i] = [];

            DefaultValue = defaultValue;
            IndexNames = new string[numberOfIndices];
            Data = [];
        }

        public SparseMultiIndexTable(string fileName, float defaultValue = 0.0f)
        {
            string[]? indexedNames = null;
            HashSet<int>[]? mappedIndices = null;
            long lineNumber = 2;
            Data = [];
            try
            {
                using (var reader = new CommentedStreamReader(fileName))
                {
                    var line = reader.ReadLine(); //get the header

                    if (line != null)
                    {
                        var cells = line.Split(',');
                        if (cells.Length < 2)
                        {
                            throw new IOException("A multi-index table requires at least two columns!");
                        }

                        //Load header data, initialize this class.
                        mappedIndices = new HashSet<int>[cells.Length - 1];
                        for (var i = 0; i < Dimensions; i++) mappedIndices[i] = [];

                        indexedNames = new string[Dimensions];
                        for (var i = 0; i < Dimensions; i++) indexedNames[i] = cells[i];
                        DefaultValue = defaultValue;

                        var indices = new int[Dimensions]; //This is getting constantly recycled, so there's no sense in using a malloc each time.

                        //Read the actual data
                        while (!reader.EndOfStream)
                        {
                            line = reader.ReadLine();
                            if (line == null)
                            {
                                return;
                            }
                            cells = line.Split(',');
                            lineNumber++;

                            if (cells.Length != (Dimensions + 1))
                            {
                                throw new IOException("Error reading line " + lineNumber + ": The number of cells on this line needs to be " + (Dimensions + 1) +
                                                       ", instead was " + cells.Length + " to match the correct size of this table.");
                            }

                            var value = Convert.ToSingle(cells[Dimensions]); //Get the last index
                                                                             // ReSharper disable once CompareOfFloatsByEqualityOperator
                            if (value == DefaultValue)
                                continue; //Skip cells with this table's default value.

                            for (var i = 0; i < Dimensions; i++) indices[i] = Convert.ToInt32(cells[i]); //Parse each int

                            for (var i = 0; i < Dimensions; i++) mappedIndices[i].Add(indices[i]); // Store the mapped index to the HashSet.

                            var key = ConvertAddress(indices);
                            Data[key] = value;
                        }
                    }
                }
            }
            finally
            {
                MappedIndices = mappedIndices ?? Array.Empty<HashSet<int>>();
                IndexNames = indexedNames ?? Array.Empty<string>();
            }
        }

        public int Dimensions => MappedIndices.Length;

        public int NumberOfEntries => Data.Keys.Count;

        public bool CheckHeaders(IEnumerable<string> headers)
        {
            var localHeaders = headers as IList<string> ?? headers.ToList();
            foreach ( var s in localHeaders )
            {
                if ( !IndexNames.Contains( s ) )
                    return false;
            }

            foreach ( var s in IndexNames)
            {
                if ( !localHeaders.Contains( s ) )
                    return false;
            }

            return true;
        }

        public float Get(int[] address)
        {
            var key = ConvertAddress( address );
            if (Data.ContainsKey( key ) )
            {
                return Data[key];
            }

            return DefaultValue;
        }

        public float Get(string address)
        {
            if (Data.ContainsKey( address ) )
            {
                return Data[address];
            }

            return DefaultValue;
        }

        public void Set(int[] address, float value)
        {
            Data[ConvertAddress( address )] = value;
        }

        //-----------------------------------------------------------------------------------------------
        public void Set(string address, float value)
        {
            Data[address] = value;
        }

        private static string ConvertAddress(int[] address)
        {
            var result = "" + address[0];
            for ( var i = 1; i < address.Length; i++ )
            {
                result += " " + address[i];
            }
            return result;
        }
    }
}