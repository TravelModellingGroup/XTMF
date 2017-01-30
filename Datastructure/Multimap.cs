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
    public class Multimap<K, V>
        : AvlTree<Pair<K, V>> where K : IComparable<K>
    {
        /// <summary>
        /// This is how you are supposed to access inserting
        /// and getting data fom the multimap
        /// </summary>
        /// <param name="k"></param>
        /// <returns></returns>
        public V this[K k]
        {
            get
            {
                return Get( k );
            }

            set
            {
                Set( k, value );
            }
        }

        /// <summary>
        /// Checks to see if the data exists in the AST
        /// </summary>
        /// <param name="item">What we are looking for</param>
        /// <returns>True if it is found, false otherwise</returns>
        public V Get(K item)
        {
            if ( item == null ) return default( V );
            IncreaseReaders();
            var current = Root;
            while ( current != null )
            {
                var dif = item.CompareTo( current.Data.First );
                if ( dif < 0 )
                {
                    current = current.Left;
                }
                else if ( dif > 0 )
                {
                    current = current.Right;
                }
                else
                {
                    break;
                }
            }
            DecreaseReaders();
            return ( current != null ? current.Data.Second : default( V ) );
        }

        /// <summary>
        /// Checks to see if the data exists in the AST
        /// </summary>
        /// <param name="item">What we are looking for</param>
        /// <returns>True if it is found, false otherwise</returns>
        public bool Get(K key, ref V value)
        {
            if ( key == null ) return false;
            IncreaseReaders();
            var current = Root;
            while ( current != null )
            {
                var dif = key.CompareTo( current.Data.First );
                if ( dif < 0 )
                {
                    current = current.Left;
                }
                else if ( dif > 0 )
                {
                    current = current.Right;
                }
                else
                {
                    break;
                }
            }
            DecreaseReaders();
            if ( current != null )
            {
                value = current.Data.Second;
            }
            return false;
        }

        /// <summary>
        /// Checks to see if the data exists in the AST
        /// </summary>
        /// <param name="item">What we are looking for</param>
        /// <returns>True if it is found, false otherwise</returns>
        public void Set(K key, V value)
        {
            if ( key == null ) return;
            var path = new Stack<AvlTree<Pair<K, V>>.Node>();
            lock (WriterLock)
            {
                Node current = Root, prev = null;
                while ( current != null )
                {
                    prev = current;
                    path.Push( current );
                    var dif = key.CompareTo( current.Data.First );
                    if ( dif < 0 )
                    {
                        current = current.Left;
                    }
                    else if ( dif > 0 )
                    {
                        current = current.Right;
                    }
                    else
                    {
                        break;
                    }
                }

                if ( current == null )
                {
                    var n = new AvlTree<Pair<K, V>>.Node();
                    n.Height = 0;
                    n.Left = n.Right = null;
                    n.Data = new Pair<K, V>();
                    n.Data.First = key;
                    n.Data.Second = value;
                    WaitForReaders();
                    IncreaseCount();
                    if ( prev != null )
                    {
                        if ( key.CompareTo( prev.Data.First ) < 0 )
                        {
                            prev.Left = n;
                        }
                        else
                        {
                            prev.Right = n;
                        }
                        BalanceTree( path );
                    }
                    else
                    {
                        Root = n;
                    }
                }
                else
                {
                    // if we found the item
                    current.Data.Second = value;
                }
                // We need to make sure all of the nodes memory is now shared between processors
                System.Threading.Thread.MemoryBarrier();
            } // end writer's lock
        }
    }
}