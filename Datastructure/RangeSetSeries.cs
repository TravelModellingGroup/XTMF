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
using System.Text;

namespace Datastructure
{
    public sealed class RangeSetSeries : IList<RangeSet>
    {
        private readonly RangeSet[] RangeSets;

        public RangeSetSeries(List<RangeSet> tempRange)
        {
            RangeSets = tempRange.ToArray();
        }

        public int Count => RangeSets.Length;

        public bool IsReadOnly => false;

        public RangeSet this[int index]
        {
            get
            {
                return RangeSets[index];
            }

            set
            {
                RangeSets[index] = value;
            }
        }

        public static bool TryParse(string rangeString, out RangeSetSeries output)
        {
            string error = null;
            return TryParse( ref error, rangeString, out output );
        }

        public static bool TryParse(ref string error, string rangeString, out RangeSetSeries output)
        {
            var rangeSets = new List<RangeSet>();
            output = null;
            var strLength = rangeString.Length;
            int startPos;
            for ( startPos = 0; startPos < strLength; startPos++ )
            {
                if ( rangeString[startPos] == '{' )
                {
                    var success = false;
                    for ( var endPos = startPos + 1; endPos < strLength; endPos++ )
                    {
                        if ( rangeString[endPos] == '}' )
                        {
                            RangeSet temp;
                            if ( !RangeSet.TryParse( ref error, rangeString.Substring( startPos + 1, endPos - startPos - 1 ), out temp ) )
                            {
                                return false;
                            }
                            rangeSets.Add( temp );
                            startPos = endPos; // the increment will make sure we don't re explore this
                            success = true;
                            break;
                        }
                    }
                    if ( !success )
                    {
                        error = "There was an unmatched '{' at position " + startPos;
                        return false;
                    }
                }
            }
            // in case it is a set of 1 element
            if ( rangeSets.Count == 0 )
            {
                RangeSet temp;
                if ( RangeSet.TryParse( ref error, rangeString, out temp ) )
                {
                    return false;
                }
                rangeSets.Add( temp );
            }
            output = new RangeSetSeries( rangeSets );
            return true;
        }

        /// <summary>
        /// Not Supported
        /// </summary>
        /// <param name="item"></param>
        public void Add(RangeSet item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not Supported
        /// </summary>
        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(RangeSet item)
        {
            return IndexOf( item ) != -1;
        }

        public void CopyTo(RangeSet[] array, int arrayIndex)
        {
            var localRangeSets = RangeSets;
            if ( localRangeSets.Length + arrayIndex >= array.Length )
            {
                throw new ArgumentException( "The given array is not long enough to support copying starting at index " + arrayIndex );
            }
            if ( arrayIndex < 0 )
            {
                throw new ArgumentOutOfRangeException( "arrayIndex", "This argument must be greater than or equal to zero!" );
            }
            for ( var i = 0; i < localRangeSets.Length; i++ )
            {
                array[i + arrayIndex] = localRangeSets[i];
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as RangeSetSeries;
            if (Count != other?.Count ) return false;
            for ( var i = 0; i < RangeSets.Length; i++ )
            {
                if ( !RangeSets[i].Equals( other.RangeSets[i] ) )
                {
                    return false;
                }
            }
            return true;
        }

        public IEnumerator<RangeSet> GetEnumerator()
        {
            return ( (ICollection<RangeSet>)RangeSets).GetEnumerator();
        }

        public override int GetHashCode()
        {
            var hash = 0;
            for ( var i = 0; i < RangeSets.Length; i++ )
            {
                hash += RangeSets[i].GetHashCode();
            }
            return hash;
        }

        public int IndexOf(RangeSet item)
        {
            if ( item == null )
            {
                throw new ArgumentNullException( "item", "The item to search for must not be null!" );
            }
            for ( var i = 0; i < RangeSets.Length; i++ )
            {
                if ( item.Equals(RangeSets[i] ) )
                {
                    return i;
                }
            }
            return -1;
        }

        public int IndexOf(int numberToFind)
        {
            for ( var i = 0; i < RangeSets.Length; i++ )
            {
                if (RangeSets[i].Contains( numberToFind ) )
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Not Supported
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, RangeSet item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(RangeSet item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not Supported
        /// </summary>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return RangeSets.GetEnumerator();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var first = true;
            for ( var i = 0; i < RangeSets.Length; i++ )
            {
                if ( !first )
                {
                    builder.Append( ',' );
                }
                first = false;
                builder.Append( '{' );
                builder.Append( RangeSets[i] );
                builder.Append( '}' );
            }
            return builder.ToString();
        }
    }
}