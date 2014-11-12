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
using System.IO;
using System.Text;

namespace Datastructure
{
    /// <summary>
    /// Provides access to a ZFC file containing zoneal information
    /// </summary>
    public class ZoneCache<T> : IDisposable
    {
        private static int cacheSize = 0;
        private LocklessCache<int, T> Cache;
        private byte[] DataLine;
        private long FileSize;
        private Func<int, float[], T> Make;
        private BinaryReader Reader;
        private SparseSegment[] Segments;
        private int Version;

        /// <summary>
        /// Create a new Cache Interface to the given file
        /// </summary>
        /// <param name="ODCFile">The file to use as a cache</param>
        /// <param name="MakeType">Convert floats to your type</param>
        public ZoneCache(string ZoneFile, Func<int, float[], T> MakeType, int cacheSize = 0)
        {
            if ( !File.Exists( ZoneFile ) ) throw new IOException( "FILE: '" + ZoneFile + "' DOES NOT EXIST!" );
            this.Reader = new BinaryReader( new FileStream( ZoneFile, FileMode.Open, FileAccess.Read, FileShare.Read, 0x5000, FileOptions.RandomAccess ), Encoding.Default );
            this.Zones = this.Reader.ReadInt32();
            if ( cacheSize > 0 )
            {
                this.Cache = new LocklessCache<int, T>( cacheSize );
            }
            else
            {
                this.Cache = new LocklessCache<int, T>();
            }
            this.Version = this.Reader.ReadInt32();
            this.Types = this.Reader.ReadInt32();
            this.Make = MakeType;
            this.DataLine = new byte[this.Types * 4];
            this.FileSize = this.Reader.BaseStream.Length;
            this.LoadSparseIndexes( this.Reader );
        }

        /// <summary>
        /// This releases the access to the file once this cache is released from memory
        /// </summary>
        ~ZoneCache()
        {
            this.Dispose( false );
        }

        public static int CacheSize
        {
            get
            {
                return cacheSize;
            }

            set
            {
                cacheSize = value;
            }
        }

        /// <summary>
        /// The number of types of information stored for each zone
        /// </summary>
        public int Types { get; private set; }

        /// <summary>
        /// The number of zones that are in this cache
        /// </summary>
        public int Zones { get; private set; }

        /// <summary>
        /// Get the data from O to D
        /// </summary>
        /// <param name="Zone">Zone</param>
        /// <returns>The data assosiated with this OD</returns>
        public T this[int Zone]
        {
            get
            {
                T element;
                element = this.Cache[Zone];

                if ( element == null ) element = this.LoadAndStore( Zone );
                return element;
            }
        }

        public void Release()
        {
            try
            {
                this.Reader.Close();
            }
            catch { }
        }

        public SparseArray<T> StoreAll()
        {
            SparseIndexing indexing;
            int numberOfSegments = this.Segments.Length;
            indexing.Indexes = new SparseSet[numberOfSegments];
            int total = 0;
            this.Reader.BaseStream.Position = this.Segments[0].DiskLocation;
            for ( int i = 0; i < numberOfSegments; i++ )
            {
                indexing.Indexes[i].Start = this.Segments[i].Start;
                indexing.Indexes[i].Stop = this.Segments[i].Stop;
                total += indexing.Indexes[i].Stop - indexing.Indexes[i].Start + 1;
            }
            T[] data = new T[total];
            int types = this.Types;
            float[] typeData = new float[types];
            int k = 0;
            for ( int i = 0; i < numberOfSegments; i++ )
            {
                for ( int j = indexing.Indexes[i].Start; j <= indexing.Indexes[i].Stop; j++ )
                {
                    for ( int t = 0; t < types; t++ )
                    {
                        typeData[t] = this.Reader.ReadSingle();
                    }
                    data[k++] = this.Make( j, typeData );
                }
            }
            return new SparseArray<T>( indexing, data );
        }

        private bool GetTransformedIndex(ref int o)
        {
            int min = 0;
            int max = this.Segments.Length - 1;
            while ( min <= max )
            {
                int mid = ( ( min + max ) / 2 );
                var midIndex = this.Segments[mid];

                if ( o < midIndex.Start )
                {
                    max = mid - 1;
                }
                else if ( o > midIndex.Stop )
                {
                    min = mid + 1;
                }
                else
                {
                    // then we are in a vlid range
                    o = ( o - midIndex.Start + midIndex.BaseLocation );
                    return true;
                }
            }
            return false;
        }

        private T Load(int ZoneID)
        {
            float[] data = new float[this.Types];
            T value;
            long pos = 0;
            if ( !GetTransformedIndex( ref ZoneID )
                || ( pos = ( sizeof( int ) * 3 ) + ZoneID * this.Types * sizeof( float ) ) >= this.FileSize )
            {
                for ( int i = 0; i < data.Length; i++ )
                {
                    data[i] = 0;
                }
            }
            else
            {
                this.Reader.BaseStream.Position = pos;
                this.Reader.Read( this.DataLine, 0, this.DataLine.Length );
                for ( int i = 0, j = 0; i < data.Length; i++, j += 4 )
                {
                    data[i] = BitConverter.ToSingle( this.DataLine, j );
                }
            }
            value = this.Make( ZoneID, data );
            return value;
        }

        /// <summary>
        /// Loads the data from the file into the cache
        /// </summary>
        /// <param name="Lookup"></param>
        /// <returns></returns>
        private T LoadAndStore(int ZoneID)
        {
            T value = Load( ZoneID );
            this.Cache.Add( ZoneID, value );
            return value;
        }

        private void LoadSparseIndexes(BinaryReader binaryReader)
        {
            if ( Version >= 2 )
            {
                int numberOfSegments;
                this.Segments = new SparseSegment[numberOfSegments = Reader.ReadInt32()];
                int total = 0;
                for ( int i = 0; i < numberOfSegments; i++ )
                {
                    this.Segments[i].Start = Reader.ReadInt32();
                    this.Segments[i].Stop = Reader.ReadInt32();
                    this.Segments[i].DiskLocation = Reader.ReadInt64();
                    this.Segments[i].BaseLocation = total;
                    total += this.Segments[i].Stop - this.Segments[i].Start + 1;
                }
            }
            else
            {
                this.Segments = new SparseSegment[1];
                this.Segments[0].Start = 0;
                this.Segments[0].Stop = this.Zones;
                this.Segments[0].BaseLocation = 0;
                this.Segments[0].DiskLocation = this.Reader.BaseStream.Position;
            }
        }

        private struct SparseSegment
        {
            public int BaseLocation;
            public long DiskLocation;
            public int Start;
            public int Stop;
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Reader != null )
            {
                this.Reader.Close();
                this.Reader = null;
            }
        }
    }// end class
}// end namespace