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
using System.Threading;

namespace Datastructure
{
    /// <summary>
    /// A generic Key->Data Hashtable
    /// </summary>
    /// <typeparam name="TK">The Identifyer</typeparam>
    /// <typeparam name="TD">What to store</typeparam>
    public class LocklessHashtable<TK, TD> where TK : IComparable<TK>
    {
        /// <summary>
        /// How many items we currently have
        /// </summary>
        protected int _Count;

        /// <summary>
        /// The table of starting nodes
        /// </summary>
        protected readonly Node[] Table;

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
        public int Count => _Count;

        /// <summary>
        /// The data stored in the hashtable
        /// </summary>
        public IEnumerable<TD> Data
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
        public IEnumerable<TK> Keys
        {
            get
            {
                var keysList = new List<TK>( Table.Length );
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
        public TD this[TK key]
        {
            get
            {
                var place = Math.Abs( key.GetHashCode() % Table.Length );
                var current = Table[place];
                while ( current != null )
                {
                    if (current.Key.CompareTo(key) == 0)
                    {
                        return current.Storage;
                    }
                    current = current.Next;
                }
                return default( TD );
            }
        }

        /// <summary>
        /// Add this Key Data pair to the Hashtable
        /// </summary>
        /// <param name="key">What the key for this data is</param>
        /// <param name="data">What data to store with this key</param>
        public virtual void Add(TK key, TD data)
        {
            var place = Math.Abs( key.GetHashCode() % Table.Length );
            var n = new Node();
            n.Key = key;
            n.Storage = data;
            n.Next = Table[place];
            Table[place] = n;
            Interlocked.Increment( ref _Count );
        }

        /// <summary>
        /// Checks to see if the key is in the hashtable
        /// </summary>
        /// <param name="key">What key to look for</param>
        /// <returns>True if it was found</returns>
        public bool Contains(TK key)
        {
            var place = Math.Abs( key.GetHashCode() % Table.Length );
            var current = Table[place];
            while ( current != null )
            {
                if ( current.Key.CompareTo( key ) == 0 )
                {
                    return true;
                }
                current = current.Next;
            }
            return false;
        }

        /// <summary>
        /// Checks to see if the data is in the Hashtable
        /// </summary>
        /// <param name="data">What data to look for</param>
        /// <returns>True if it was found</returns>
        public bool Contains(TD data)
        {
            var place = Math.Abs( data.GetHashCode() % Table.Length );
            var current = Table[place];
            while ( current != null )
            {
                if ( current.Storage.Equals( data ) )
                {
                    return true;
                }
                current = current.Next;
            }
            return false;
        }

        /// <summary>
        /// Remove an element from the hashtable
        /// </summary>
        /// <param name="key">What key to remove</param>
        /// <returns>True, if something was removed</returns>
        public virtual bool Remove(TK key)
        {
            throw new NotImplementedException( "Don't remove things quite yet" );
        }

        /// <summary>
        /// Adds a new element if it doesn't exist already
        /// </summary>
        /// <param name="key">What key to test for uniqueness and to store</param>
        /// <param name="data">What to store</param>
        public virtual void UniqueAdd(TK key, TD data)
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
                Interlocked.Increment( ref _Count );
            }
        }

        /// <summary>
        /// This is our internal linking
        /// </summary>
        protected class Node
        {
            public TK Key;
            public Node Next;
            public TD Storage;
        }
    }
}