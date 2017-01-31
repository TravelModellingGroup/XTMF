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
using System.Threading;

namespace Datastructure
{
    /// <summary>
    /// A cache is a data structure that provides order of 1 access
    /// to data contained within it.  By adding new items you can
    /// end up removing older ones.
    ///
    /// Thread Safe
    /// </summary>
    /// <typeparam name="TK">The type for the key</typeparam>
    /// <typeparam name="TD">The type for the data</typeparam>
    public class LocklessCache<TK, TD> : LocklessHashtable<TK, TD> where TK : IComparable<TK>
    {
        /// <summary>
        /// Create a new Cache
        /// </summary>
        public LocklessCache()
            : this( 100 )
        {
        }

        /// <summary>
        /// Creates a new cache with a certain amount of entries
        /// </summary>
        /// <param name="capacity"></param>
        public LocklessCache(int capacity)
            : base( capacity )
        {
        }

        /// <summary>
        /// Adds a new entry into the cache
        /// </summary>
        /// <param name="key">The key for this entry</param>
        /// <param name="data">The data contained for this key</param>
        public override void Add(TK key, TD data)
        {
            var place = Math.Abs( ( key.GetHashCode() ) % Table.Length );
            if (Table[place] == null )
            {
                var n = new Node();
                n.Key = key;
                n.Storage = data;
                n.Next = null;
                Table[place] = n;
                Interlocked.Increment( ref _Count);
            }
            else
            {
                Table[place].Key = key;
                Table[place].Storage = data;
                Table[place].Next = null;
            }
        }
    }
}