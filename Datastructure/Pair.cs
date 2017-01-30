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

namespace Datastructure
{
    /// <summary>
    /// This class repersents a pair of objects
    /// </summary>
    /// <typeparam name="F">The first type of object</typeparam>
    /// <typeparam name="S">The second type of object</typeparam>
    public class Pair<F, S> : IComparable<Pair<F, S>>
    {
        public Pair(F first, S second)
        {
            First = first;
            Second = second;
        }

        public Pair()
        {
        }

        /// <summary>
        /// The first object in the pair
        /// </summary>
        public F First { get; set; }

        /// <summary>
        /// The second object in the pair
        /// </summary>
        public S Second { get; set; }

        /// <summary>
        /// Attempts to compair, F then S and in the last case it trys to compare hashcodes
        /// </summary>
        /// <param name="other">The pair we want to compare with</param>
        /// <returns>Less than zero if it is less, 0 if equal, otherwise greater than zero</returns>
        public int CompareTo(Pair<F, S> other)
        {
            var compFirst = First as IComparable<F>;
            var compSecond = Second as IComparable<S>;
            // if they are both comparible then we will just see if they are both equal, if not -1
            if ( compFirst != null & compSecond != null )
            {
                int value;
                return ( ( value = compFirst.CompareTo( other.First ) ) == 0 ) ? ( ( ( value = compSecond.CompareTo( other.Second ) ) == 0 ) ? 0 : value ) : value;
            }
            // try to compare the first value
            if ( compFirst != null )
            {
                var res = compFirst.CompareTo( other.First );
                // if we were equal,if we can compair the second, do that
                res = res == 0 ? ( compSecond != null ? compSecond.CompareTo( Second ) : 0 ) : 0;
                return res;
            }
            // if not, try the second
            if ( compSecond != null )
            {
                return compSecond.CompareTo( other.Second );
            }
            //see if they are equal
            if ( First.Equals( other.First ) && Second.Equals( other.Second ) )
            {
                return 0;
            }
            // if they are not equal, subract the total of the hashcodes, this can cause false equals
            return First.GetHashCode() + Second.GetHashCode() - other.First.GetHashCode() - other.Second.GetHashCode();
        }//end CompareTo

        public override bool Equals(object obj)
        {
            if ( obj != null && obj is Pair<F, S> )
            {
                var other = obj as Pair<F, S>;
                return (First.Equals( other.First ) ) && (Second.Equals( other.Second ) );
            }
            return base.Equals( obj );
        }

        /// <summary>
        /// We need this to have a hashcode for every pair
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            // Hopefully multiplying two "random" numbers is unique enough
            // To go into datastructures, works best when large
            return (First.GetHashCode() * 2 ) * Second.GetHashCode();
        }
    }//end class
}//end namespace