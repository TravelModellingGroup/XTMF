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

namespace Datastructure
{
    public class IndexedRangeSet : RangeSet
    {
        protected int[] Indexes;

        public IndexedRangeSet(List<Range> ranges, List<int> indexes)
            : base( ranges )
        {
        }

        public IndexedRangeSet(List<Range> ranges)
            : base( ranges )
        {
        }

        public override Range this[int index]
        {
            get
            {
                return base[GetIndexOf( index )];
            }

            set
            {
                base[GetIndexOf( index )] = value;
            }
        }

        public static bool TryParse(string rangeString, out IndexedRangeSet output)
        {
            string error = null;
            return TryParse( ref error, rangeString, out output );
        }

        public static bool TryParse(ref string error, string rangeString, out IndexedRangeSet output)
        {
            var tempRange = new List<Range>();
            var tempIndexes = new List<int>();
            var length = rangeString.Length;
            var str = rangeString.ToCharArray();
            var index = 0;
            var start = 0;
            var end = 0;
            output = null;
            //Phase == 0 -> index
            //Phase == 1 -> start
            //Phase == 2 -> end
            var phase = 0;
            var lastPlus = false;
            var tallyingInZero = false;
            if ( String.IsNullOrWhiteSpace( rangeString ) )
            {
                output = new IndexedRangeSet( tempRange, tempIndexes );
                return true;
            }
            for ( var i = 0; i < length; i++ )
            {
                var c = str[i];
                if ( Char.IsWhiteSpace( c ) || Char.IsLetter( c ) ) continue;
                lastPlus = false;
                switch ( phase )
                {
                    case 0:
                        if ( Char.IsNumber( c ) )
                        {
                            index = ( ( index << 3 ) + ( index << 1 ) ) + ( c - '0' );
                            tallyingInZero = true;
                        }
                        else if ( c == ':' )
                        {
                            start = 0;
                            phase = 1;
                        }
                        else if ( c == ',' )
                        {
                            tempRange.Add( new Range() { Start = index, Stop = index } );
                            tempIndexes.Add( index );
                            index = 0;
                            start = 0;
                            end = 0;
                        }
                        else if ( c == '-' )
                        {
                            if ( !tallyingInZero )
                            {
                                error = "No number was inserted before a range!";
                                return false;
                            }
                            start = index;
                            end = 0;
                            phase = 2;
                        }
                        else if ( c == '+' )
                        {
                            if ( !tallyingInZero )
                            {
                                error = "No number was inserted before a range!";
                                return false;
                            }
                            end = int.MaxValue;
                            tempRange.Add( new Range { Start = start, Stop = end } );
                            tempIndexes.Add( start );
                            index = 0;
                            start = 0;
                            phase = 0;
                            tallyingInZero = false;
                            lastPlus = true;
                        }
                        else
                        {
                            error = "Unrecognized symbol " + c;
                            return false;
                        }
                        break;

                    case 1:
                        if ( Char.IsNumber( c ) )
                        {
                            start = ( ( start << 3 ) + ( start << 1 ) ) + ( c - '0' );
                        }
                        else if ( c == '+' )
                        {
                            end = int.MaxValue;
                            tempRange.Add( new Range() { Start = start, Stop = end } );
                            tempIndexes.Add( index );
                            index = 0;
                            start = 0;
                            phase = 0;
                            tallyingInZero = false;
                            lastPlus = true;
                        }
                        else if ( c == '-' )
                        {
                            end = 0;
                            phase = 2;
                        }
                        break;

                    case 2:
                        if ( Char.IsNumber( c ) )
                        {
                            end = ( ( end << 3 ) + ( end << 1 ) ) + ( c - '0' );
                        }
                        else if ( c == ',' )
                        {
                            tempRange.Add( new Range() { Start = start, Stop = end } );
                            tempIndexes.Add( index );
                            index = 0;
                            phase = 0;
                            start = 0;
                            end = 0;
                            tallyingInZero = false;
                        }
                        break;
                }
            }
            if ( phase == 2 )
            {
                tempRange.Add( new Range() { Start = start, Stop = end } );
                tempIndexes.Add( index );
            }
            else if ( phase == 0 && tallyingInZero )
            {
                tempRange.Add( new Range() { Start = index, Stop = index } );
                tempIndexes.Add( index );
            }
            else if ( !lastPlus )
            {
                error = "Ended while reading a " + ( phase == 0 ? "range's index!" : "range's start value!" );
                return false;
            }
            output = new IndexedRangeSet( tempRange, tempIndexes );
            return true;
        }

        private int GetIndexOf(int index)
        {
            for ( var i = 0; i < Indexes.Length; i++ )
            {
                if (Indexes[i] == index )
                {
                    return i;
                }
            }
            return -1;
        }
    }
}