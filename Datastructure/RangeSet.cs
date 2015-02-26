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
using System.Runtime.CompilerServices;
using System.Text;

namespace Datastructure
{
    public class RangeSet : IList<Range>
    {
        protected Range[] SetRanges;

        public RangeSet(List<Range> tempRange)
        {
            this.SetRanges = tempRange.ToArray();
        }

        /// <summary>
        /// Creates a new RangeSet with inclusive values from the given integer set
        /// </summary>
        /// <param name="numbers">The numbers to use to generate the ranges</param>
        public RangeSet(IList<int> numbers)
        {
            var array = new int[numbers.Count];
            numbers.CopyTo( array, 0 );
            Array.Sort( array );
            List<Range> tempRange = new List<Range>();
            int start = 0;
            for ( int i = 1; i < array.Length; i++ )
            {
                if ( array[i] > array[i - 1] + 1 )
                {
                    tempRange.Add( new Range() { Start = array[start], Stop = array[i - 1] } );
                    start = i;
                }
            }
            // and in the end
            tempRange.Add( new Range() { Start = array[start], Stop = array[array.Length - 1] } );
            this.SetRanges = tempRange.ToArray();
        }

        public int Count
        {
            get { return this.SetRanges.Length; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public virtual Range this[int index]
        {
            get
            {
                return this.SetRanges[index];
            }

            set
            {
                this.SetRanges[index] = value;
            }
        }

        public static bool TryParse(string rangeString, out RangeSet output)
        {
            string error = null;
            return TryParse( ref error, rangeString, out output );
        }

        public static bool TryParse(ref string error, string rangeString, out RangeSet output)
        {
            var tempRange = new List<Range>();
            var length = rangeString.Length;
            var str = rangeString.ToCharArray();
            int index = 0;
            int start = 0;
            int end = 0;
            output = null;
            //Phase == 0 -> index
            //Phase == 1 -> start
            //Phase == 2 -> end
            int phase = 0;
            bool lastPlus = false;
            bool tallyingInZero = false;
            if ( String.IsNullOrWhiteSpace( rangeString ) )
            {
                output = new RangeSet( tempRange );
                return true;
            }
            for ( int i = 0; i < length; i++ )
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
                        else if ( c == ',' )
                        {
                            tempRange.Add( new Range() { Start = index, Stop = index } );
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
            }
            else if ( phase == 0 && tallyingInZero )
            {
                tempRange.Add( new Range() { Start = index, Stop = index } );
            }
            else if ( !lastPlus )
            {
                error = "Ended while reading a " + ( phase == 0 ? "range's index!" : "range's start value!" );
                return false;
            }
            output = new RangeSet( tempRange );
            return true;
        }

        public void Add(Range item)
        {
            throw new InvalidOperationException( "Unable to add items" );
        }

        public void Clear()
        {
            throw new InvalidOperationException( "Unable to remove items" );
        }

        public bool Contains(Range item)
        {
            return this.IndexOf( item ) != -1;
        }

        public bool Contains(int number)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                if ( ( number >= this.SetRanges[i].Start ) && ( number <= this.SetRanges[i].Stop ) )
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(Range[] array, int arrayIndex)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                array[arrayIndex + i] = this.SetRanges[i];
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as RangeSet;
            if ( other != null )
            {
                if ( other.Count != this.Count ) return false;
                for ( int i = 0; i < this.SetRanges.Length; i++ )
                {
                    if ( !( this.SetRanges[i] == other[i] ) )
                    {
                        return false;
                    }
                }
                return true;
            }
            return base.Equals( obj );
        }

        public IEnumerator<Range> GetEnumerator()
        {
            return ( (ICollection<Range>)this.SetRanges ).GetEnumerator();
        }

        public override int GetHashCode()
        {
            int hash = 0;
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                hash += this.SetRanges.GetHashCode();
            }
            return hash;
        }

        public int IndexOf(Range item)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                if ( this.SetRanges[i] == item )
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gives the index in the range set where this integer is first contained.
        /// </summary>
        /// <param name="integerToFind">The integer to find</param>
        /// <returns>-1 if not found, otherwise the index of the Range in the rangeset that first contains this integer</returns>
        public int IndexOf(int integerToFind)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                if ( this.SetRanges[i].ContainsInclusive( integerToFind ) )
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, Range item)
        {
            this[index] = item;
        }

        public bool Overlaps(Range other)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                if ( this.SetRanges[i].Contains( other.Start ) || this.SetRanges[i].Contains( other.Stop ) )
                {
                    return true;
                }
            }
            return false;
        }

        public bool Overlaps(RangeSet other)
        {
            for ( int i = 0; i < this.SetRanges.Length; i++ )
            {
                for ( int j = 0; j < other.SetRanges.Length; j++ )
                {
                    if ( this.SetRanges[i].Contains( other.SetRanges[j].Start ) || this.SetRanges[i].Contains( other.SetRanges[j].Stop ) )
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Remove(Range item)
        {
            throw new InvalidOperationException( "Unable to remove items" );
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException( "Unable to remove items" );
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.SetRanges.GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            bool first = true;
            if ( this.SetRanges.Length == 0 )
            {
                // do nothing we already have a blank builder
            }
            else
            {
                foreach ( var res in this.SetRanges )
                {
                    if ( !first )
                    {
                        builder.Append( ',' );
                    }
                    if ( res.Start != res.Stop )
                    {
                        builder.Append( res.Start );
                        builder.Append( '-' );
                        builder.Append( res.Stop );
                    }
                    else
                    {
                        builder.Append( res.Start );
                    }
                    first = false;
                }
            }
            return builder.ToString();
        }
    }
}