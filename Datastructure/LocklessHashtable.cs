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
using System.Threading;

namespace Datastructure
{
    /// <summary>
    /// A generic Key->Data Hashtable
    /// </summary>
    /// <typeparam name="K">The Identifyer</typeparam>
    /// <typeparam name="D">What to store</typeparam>
    public class LocklessHashtable<K, D> where K : IComparable<K>
    {
        /// <summary>
        /// How many items we currently have
        /// </summary>
        protected int count = 0;

        /// <summary>
        /// The table of starting nodes
        /// </summary>
        protected Node[] Table = null;

        /// <summary>
        /// Create a new Hashtable
        /// </summary>
        public LocklessHashtable()
            : this( 100 )
        {
        }

        /// <summary>
        /// Create a hashtable with the given amount of entries
        /// </summary>
        /// <param name="capacity"></param>
        public LocklessHashtable(int capacity)
        {
            Table = new Node[capacity];
        }

        /// <summary>
        /// Learn how many items we have
        /// </summary>
        public int Count => count;

        /// <summary>
        /// The data stored in the hashtable
        /// </summary>
        public IEnumerable<D> Data
        {
            get
            {
                for ( var i = 0; i < Table.Length; i++ )
                {
                    var current = Table[i];
                    while ( current != null )
                    {
                        yield return ( current.Storage );
                        current = current.Next;
                    }
                }
            }
        }

        /// <summary>
        /// The keys stored in the hash table
        /// </summary>
        public IEnumerable<K> Keys
        {
            get
            {
                var keysList = new List<K>( Table.Length );
                for ( var i = 0; i < Table.Length; i++ )
                {
                    keysList.Clear();

                    var current = Table[i];
                    while ( current != null )
                    {
                        keysList.Add( current.Key );
                        current = current.Next;
                    }
                    foreach ( var k in keysList )
                    {
                        yield return k;
                    }
                }
            }
        }

        /// <summary>
        /// Get the Data for the given Key
        /// </summary>
        /// <param name="key">The identifier for this data</param>
        /// <returns></returns>
        public D this[K key]
        {
            get
            {
                var place = Math.Abs( key.GetHashCode() % Table.Length );
                Node current = null;
                current = Table[place];
                while ( current != null )
                {
                    if ( current.Key.CompareTo( key ) == 0 ) return current.Storage;
                    current = current.Next;
                }
                return current != null ? current.Storage : default( D );
            }
        }

        /// <summary>
        /// Add this Key Data pair to the Hashtable
        /// </summary>
        /// <param name="key">What the key for this data is</param>
        /// <param name="data">What data to store with this key</param>
        public virtual void Add(K key, D data)
        {
            var place = Math.Abs( key.GetHashCode() % Table.Length );
            var n = new Node();
            n.Key = key;
            n.Storage = data;
            n.Next = Table[place];
            Table[place] = n;
            Interlocked.Increment( ref count );
        }

        /// <summary>
        /// Checks to see if the key is in the hashtable
        /// </summary>
        /// <param name="key">What key to look for</param>
        /// <returns>True if it was found</returns>
        public bool Contains(K key)
        {
            var place = Math.Abs( key.GetHashCode() % Table.Length );
            Node current = null;
            var found = false;

            current = Table[place];
            while ( current != null )
            {
                if ( current.Key.CompareTo( key ) == 0 )
                {
                    found = true;
                    return found;
                }
                current = current.Next;
            }
            return found;
        }

        /// <summary>
        /// Checks to see if the data is in the Hashtable
        /// </summary>
        /// <param name="data">What data to look for</param>
        /// <returns>True if it was found</returns>
        public bool Contains(D data)
        {
            var place = Math.Abs( data.GetHashCode() % Table.Length );
            Node current = null;
            var found = false;
            current = Table[place];
            while ( current != null )
            {
                if ( current.Storage.Equals( data ) )
                {
                    found = true;
                    return found;
                }
                current = current.Next;
            }
            return found;
        }

        /// <summary>
        /// Remove an element from the hashtable
        /// </summary>
        /// <param name="key">What key to remove</param>
        /// <returns>True, if something was removed</returns>
        public virtual bool Remove(K key)
        {
            throw new NotImplementedException( "Don't remove things quite yet" );
        }

        /// <summary>
        /// Adds a new element if it doesn't exist already
        /// </summary>
        /// <param name="key">What key to test for uniqueness and to store</param>
        /// <param name="data">What to store</param>
        public virtual void UniqueAdd(K key, D data)
        {
            var place = Math.Abs( key.GetHashCode() % Table.Length );
            var n = new Node();
            n.Key = key;
            n.Storage = data;

            n.Next = Table[place];
            var current = Table[place];
            var add = true;
            while ( current != null )
            {
                if ( current.Key.CompareTo( key ) == 0 )
                {
                    add = false;
                    break;
                }
                current = current.Next;
            }
            if ( add )
            {
                Table[place] = n;
                Interlocked.Increment( ref count );
            }
        }

        /// <summary>
        /// This is our internal linking
        /// </summary>
        protected class Node
        {
            public K Key;
            public Node Next;
            public D Storage;
        }
    }
}