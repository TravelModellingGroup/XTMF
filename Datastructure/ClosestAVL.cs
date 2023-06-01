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

namespace Datastructure
{
    /// <summary>
    /// Allows for you to be able to get the closest element to the one you want to search for.
    /// The Comparison method must be fine grained.
    /// Int32 only will return 1, 0 and -1 and thus will not give the correct results
    /// </summary>
    /// <typeparam name="TK">The Key to use for this datastructure</typeparam>
    public class ClosestAvl<TK> : AvlTree<TK> where TK : IComparable<TK>
    {
        /// <summary>
        /// Finds the closest item to the given item.
        /// </summary>
        /// <param name="item">The item we wish to find something close to</param>
        /// <returns>The data for that item, or the default value if there is no data in the tree.</returns>
        public TK? FindClosest(TK item)
        {
            Node? current;
            IncreaseReaders();
            current = Root;
            while ( current != null )
            {
                var diff = current.Data.CompareTo( item );
                if ( diff > 0 )
                {
                    var closestLeft = GetRightmost( current.Left );
                    if ( closestLeft == null )
                    {
                        DecreaseReaders();
                        return current.Data;
                    }
                    var closeDiff = closestLeft.Data.CompareTo( item );
                    if ( closeDiff <= 0 )
                    {
                        DecreaseReaders();
                        var absDiff = Math.Abs( diff );
                        return ( Math.Min( absDiff, Math.Abs( closeDiff ) ) == absDiff ) ? current.Data : closestLeft.Data;
                    }
                    else
                    {
                        current = current.Left;
                    }
                }
                else if ( diff < 0 )
                {
                    var closestRight = GetLeftmost( current.Right );
                    if ( closestRight == null )
                    {
                        DecreaseReaders();
                        return current.Data;
                    }
                    var closeDiff = closestRight.Data.CompareTo( item );
                    if ( closeDiff >= 0 )
                    {
                        DecreaseReaders();
                        var absDiff = Math.Abs( diff );
                        return ( Math.Min( absDiff, Math.Abs( closeDiff ) ) == absDiff ) ? current.Data : closestRight.Data;
                    }
                    else
                    {
                        current = current.Right;
                    }
                }
                else
                {
                    // if we found it, we are done
                    DecreaseReaders();
                    return current.Data;
                }
            }
            DecreaseReaders();
            return default( TK );
        }//end FindClosest

        /// <summary>
        /// Grabs the node that is the farthest left of the given node
        /// </summary>
        /// <param name="node">The node that we start on</param>
        /// <returns>Null if the node doesn't exist, otherwise the leftmost node</returns>
        private Node? GetLeftmost(Node? node)
        {
            var prev = node;
            while ( node != null )
            {
                prev = node;
                node = node.Left;
            }
            return prev;
        }

        /// <summary>
        /// Grabs the node that is the farthest right of the given node
        /// </summary>
        /// <param name="node">The node that we start on</param>
        /// <returns>Null if the node doesn't exist, otherwise the rightmost node</returns>
        private Node? GetRightmost(Node? node)
        {
            var prev = node;
            while ( node != null )
            {
                prev = node;
                node = node.Right;
            }
            return prev;
        }
    }//end class
}//end namespace