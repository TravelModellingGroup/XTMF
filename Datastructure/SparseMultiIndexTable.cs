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

        public override string ReadLine()
        {
            string line;
            while ( !base.EndOfStream )
            {
                line = base.ReadLine();
                if ( !line.StartsWith( "//" ) )
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
        public string Decription = "";

        private Dictionary<string, float> Data; //The actual data.
        private float DefaultValue;
        private HashSet<int>[] MappedIndices; //Stores the contained values of each index. Its length is equal to the number of dimensions
        //The default value for this table.

        public SparseMultiIndexTable(int NumberOfIndices, float DefaultValue)
        {
            MappedIndices = new HashSet<int>[NumberOfIndices];
            for ( var i = 0; i < NumberOfIndices; i++ ) MappedIndices[i] = new HashSet<int>();

            this.DefaultValue = DefaultValue;
            IndexNames = new string[NumberOfIndices];
            Data = new Dictionary<string, float>();
        }

        public SparseMultiIndexTable(string FileName, float DefaultValue = 0.0f)
        {
            long lineNumber = 2;
            Data = new Dictionary<string, float>();

            using ( var reader = new CommentedStreamReader( FileName ) )
            {
                var line = reader.ReadLine(); //get the header

                var cells = line.Split( ',' );
                if ( cells.Length < 2 )
                {
                    throw new IOException( "A multi-index table requires at least two columns!" );
                }

                //Load header data, initialize this class.
                MappedIndices = new HashSet<int>[cells.Length - 1];
                for ( var i = 0; i < Dimensions; i++ ) MappedIndices[i] = new HashSet<int>();

                IndexNames = new string[Dimensions];
                for ( var i = 0; i < Dimensions; i++ ) IndexNames[i] = cells[i];
                this.DefaultValue = DefaultValue;

                var indices = new int[Dimensions]; //This is getting constantly recycled, so there's no sense in using a malloc each time.
                string key;

                //Read the actual data
                while ( !reader.EndOfStream )
                {
                    cells = reader.ReadLine().Split( ',' );
                    lineNumber++;

                    if ( cells.Length != (Dimensions + 1 ) )
                    {
                        throw new IOException( "Error reading line " + lineNumber + ": The number of cells on this line needs to be " + (Dimensions + 1 ) +
                            ", instead was " + cells.Length + " to match the correct size of this table." );
                    }

                    var value = Convert.ToSingle( cells[Dimensions] ); //Get the last index
                    if ( value == this.DefaultValue )
                        continue; //Skip cells with this table's default value.

                    for ( var i = 0; i < Dimensions; i++ ) indices[i] = Convert.ToInt32( cells[i] ); //Parse each int

                    for ( var i = 0; i < Dimensions; i++ ) MappedIndices[i].Add( indices[i] ); // Store the mapped index to the HashSet.

                    key = ConvertAddress( indices );
                    Data[key] = value;
                }
            }
        }

        public int Dimensions => MappedIndices.Length;

        public int NumberOfEntries => Data.Keys.Count;

        public bool CheckHeaders(IEnumerable<string> Headers)
        {
            foreach ( var s in Headers )
            {
                if ( !IndexNames.Contains( s ) )
                    return false;
            }

            foreach ( var s in IndexNames)
            {
                if ( !Headers.Contains( s ) )
                    return false;
            }

            return true;
        }

        public float get(int[] address)
        {
            var key = ConvertAddress( address );
            if (Data.ContainsKey( key ) )
            {
                return Data[key];
            }

            return DefaultValue;
        }

        public float get(string address)
        {
            if (Data.ContainsKey( address ) )
            {
                return Data[address];
            }

            return DefaultValue;
        }

        public void set(int[] address, float value)
        {
            Data[ConvertAddress( address )] = value;
        }

        //-----------------------------------------------------------------------------------------------
        public void set(string address, float value)
        {
            Data[address] = value;
        }

        private string ConvertAddress(int[] Address)
        {
            var result = "" + Address[0];

            for ( var i = 1; i < Address.Length; i++ )
            {
                result += " " + Address[i];
            }

            return result;
        }

        private int Pow(int val, int exponent)
        {
            var result = 1;
            if ( exponent < 0 ) throw new DivideByZeroException( "Cannot return an int for exponents less than 0" );
            for ( var i = 0; i < exponent; i++ )
            {
                result *= val;
            }
            return result;
        }
    }

    //------------------------------------------------------------------------------------------------------
}