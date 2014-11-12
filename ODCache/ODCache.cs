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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Datastructure
{
    /// <summary>
    /// Provides a Cache and Lookup for OD information Stored in an .odc file
    /// This Cache is not thread safe, please use a Cache on each thread seperatly
    /// </summary>
    public class ODCache : IDisposable
    {
        private int AmmountOfData;
        private Cache<Pair<int, int>, float[]> Cache;
        private byte[] DataLine;
        private string FileName;
        private Index[] Indexes;
        private BinaryReader Reader;

        /// <summary>
        /// Create a new Cache Interface to the given file
        /// </summary>
        /// <param name="ODCFile">The file to use as a cache</param>
        public ODCache(string ODCFile)
            : this( ODCFile, false )
        {
        }

        public ODCache(string ODCFile, bool threadSafe)
        {
            this.FileName = ODCFile;
            Reload();
        }

        /// <summary>
        /// This releases the access to the file once this cache is released from memory
        /// </summary>
        ~ODCache()
        {
            this.Dispose( false );
        }

        /// <summary>
        /// Gets the highest zone stored in the ODC
        /// </summary>
        public int HighestZone { get { return this.Indexes.Last().End; } }

        public Dictionary<string, string> MetaData { get; private set; }

        public int Times { get; internal set; }

        public int Types { get; internal set; }

        public int Version { get; private set; }

        /// <summary>
        /// Get the data from O to D
        /// </summary>
        /// <param name="Zone">Zone</param>
        /// <param name="Destination">Destination</param>
        /// <returns>The data assosiated with this OD</returns>
        public float this[int Origin, int Destination]
        {
            get
            {
                return this[Origin, Destination, 0];
            }
        }

        /// <summary>
        /// Get the data from the Zone to the Destination from the given time
        /// </summary>
        /// <param name="Zone">Zone</param>
        /// <param name="Destination">Destination</param>
        /// <param name="Time">What time period to read</param>
        /// <returns>The value for that time</returns>
        public float this[int Origin, int Destination, int Time]
        {
            get
            {
                return this[Origin, Destination, Time, 0];
            }
        }

        /// <summary>
        /// Get the data from the Zone to the Destination from the given time
        /// </summary>
        /// <param name="Zone">Zone</param>
        /// <param name="Destination">Destination</param>
        /// <param name="Time">What time period to read</param>
        /// <param name="Type">The type of data to read</param>
        /// <returns></returns>
        public float this[int Origin, int Destination, int Time, int Type]
        {
            get
            {
                float[] f;
                var lookup = new Pair<int, int>();
                lookup.First = Origin;
                lookup.Second = Destination;
                f = this.Cache[lookup];
                if ( f == null ) f = this.LoadAndStore( lookup );
                return f[this.Times * Type + Time];
            }
        }

        public static bool FullyLoaded(string Filename)
        {
            if ( !File.Exists( Filename ) )
            {
                return false;
            }

            BinaryReader reader = new BinaryReader( new FileStream( Filename, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.RandomAccess ), Encoding.Default );

            int complete = reader.ReadInt32();

            reader.Close();

            return ( complete != 0 );
        }

        public bool ContainsIndex(int origin, int destination)
        {
            return this.GetPosition( origin, destination ) != 0;
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        public void ReGenerate(string dataDirectory, string outputDirectory)
        {
            string fileName = Path.GetFileNameWithoutExtension( this.FileName );

            string path = Path.GetDirectoryName( this.FileName );
            string xmlFile = Path.Combine( path, fileName + ".xml" );

            XmlSerializer deserializer = new XmlSerializer( typeof( CacheGenerationInfo ) );

            TextReader textReader = new StreamReader( xmlFile );

            CacheGenerationInfo info = (CacheGenerationInfo)deserializer.Deserialize( textReader );
            textReader.Close();

            ODCCreator odcCreator = new ODCCreator( this.HighestZone, this.Types, this.Times, this.Indexes.Length );

            foreach ( var dimensionInfo in info.CacheInfo )
            {
                string fname = Path.Combine( dataDirectory, dimensionInfo.FileName );

                if ( dimensionInfo.Is311 )
                {
                    odcCreator.LoadEMME2( fname, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex );
                }
                else
                {
                    if ( dimensionInfo.SaveInTimes )
                    {
                        odcCreator.LoadCSVTimes( fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex );
                    }
                    else
                    {
                        odcCreator.LoadCSVTypes( fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex );
                    }
                }
            }
            odcCreator.Save( Path.Combine( outputDirectory, Path.GetFileName( this.FileName ) ), true );
        }

        public void Release()
        {
            try
            {
                this.Reader.Close();
                this.Reader = null;
            }
            catch { }
        }

        public SparseTwinIndex<float[]> StoreAll()
        {
            if ( this.Indexes == null )
            {
                return null;
            }
            int types = this.Types * this.Times;
            SparseIndexing index = new SparseIndexing();
            int firstLength = this.Indexes.Length;
            int iTotal = 0;
            index.Indexes = new SparseSet[this.Indexes.Length];
            float[][][] data = null;
            byte[] tempReader = new byte[(int)( this.Reader.BaseStream.Length - this.Reader.BaseStream.Position )];
            BlockingCollection<LoadRequest> requests = new BlockingCollection<LoadRequest>( 50 );
            // build all of the data / mallocs in a different thread
            Task.Factory.StartNew( () =>
            {
                for ( int i = 0; i < firstLength; i++ )
                {
                    index.Indexes[i].Start = this.Indexes[i].Start;
                    index.Indexes[i].Stop = this.Indexes[i].End;
                    iTotal += this.Indexes[i].End - this.Indexes[i].Start + 1;
                }
                int iSoFar = 0;
                data = new float[iTotal][][];
                for ( int i = 0; i < firstLength; i++ )
                {
                    var sub = this.Indexes[i].SubIndex;
                    var subLength = sub.Length;
                    index.Indexes[i].SubIndex = new SparseIndexing();
                    index.Indexes[i].SubIndex.Indexes = new SparseSet[subLength];
                    int jTotal = 0;
                    var ithIndex = index.Indexes[i].SubIndex.Indexes;
                    for ( int j = 0; j < subLength; j++ )
                    {
                        ithIndex[j].Start = sub[j].Start;
                        ithIndex[j].Stop = sub[j].End;
                        jTotal += sub[j].End - sub[j].Start + 1;
                    }
                    int iSectionTotal = this.Indexes[i].End - this.Indexes[i].Start + 1;
                    for ( int k = 0; k < iSectionTotal; k++ )
                    {
                        data[iSoFar + k] = new float[jTotal][];
                    }
                    requests.Add( new LoadRequest() { ISectionTotal = iSectionTotal, JSectionTotal = jTotal, DataIndex = iSoFar } );
                    iSoFar += iSectionTotal;
                }
                requests.CompleteAdding();
            } );
            // then we can read everything into memory
            int readInSoFar = 0;
            this.Reader.BaseStream.Position = this.Indexes[0].SubIndex[0].Location;
            // store everything into memory
            while ( readInSoFar < tempReader.Length )
            {
                readInSoFar += this.Reader.Read( tempReader, readInSoFar, tempReader.Length - readInSoFar );
            }
            int currentIndex = 0;
            // process the data from the stream
            foreach ( var request in requests.GetConsumingEnumerable() )
            {
                for ( int k = 0; k < request.ISectionTotal; k++ )
                {
                    for ( int l = 0; l < request.JSectionTotal; l++ )
                    {
                        var row = data[request.DataIndex + k][l] = new float[types];
                        Buffer.BlockCopy( tempReader, currentIndex * sizeof( float ), row, 0, types * sizeof( float ) );
                        currentIndex += types;
                    }
                }
            }

            return new SparseTwinIndex<float[]>( index, data );
        }

        internal void DumpToCreator(ODCCreator oDCCreator)
        {
            foreach ( var originBlock in this.Indexes )
            {
                for ( int o = originBlock.Start; o < originBlock.End; o++ )
                {
                    foreach ( var destinationBlock in originBlock.SubIndex )
                    {
                        for ( int d = destinationBlock.Start; d < destinationBlock.End; d++ )
                        {
                            oDCCreator.Set( o, d, this );
                        }
                    }
                }
            }
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Reader != null )
            {
                this.Reader.Dispose();
                this.Reader = null;
            }
        }

        private long GetPosition(int o, int d)
        {
            for ( int i = 0; i < this.Indexes.Length; i++ )
            {
                if ( ( o >= this.Indexes[i].Start ) & ( o <= this.Indexes[i].End ) )
                {
                    Index io = this.Indexes[i];
                    int totalJ = 0;
                    for ( int j = 0; j < io.SubIndex.Length; j++ )
                    {
                        var sub = io.SubIndex[j];
                        totalJ += sub.End - sub.Start + 1;
                    }
                    for ( int j = 0; j < io.SubIndex.Length; j++ )
                    {
                        var sub = io.SubIndex[j];
                        if ( ( sub.Start <= d ) & ( sub.End >= d ) )
                        {
                            return io.SubIndex[j].Location + ( ( i - io.Start ) * ( totalJ ) + d ) * this.AmmountOfData;
                        }
                    }
                }
            }
            return 0;
        }

        private float[] Load(int first, int second)
        {
            if ( this.Reader == null )
            {
                this.Reload();
            }
            float[] data = new float[this.Times * this.Types];
            long pos = GetPosition( first, second );
            if ( pos > 0 )
            {
                lock ( this )
                {
                    this.Reader.BaseStream.Position = pos;
                    this.Reader.Read( this.DataLine, 0, this.AmmountOfData );
                    for ( int i = 0, j = 0; i < data.Length; i++, j += 4 )
                    {
                        data[i] = BitConverter.ToSingle( this.DataLine, j );
                    }
                }
            }
            else
            {
                for ( int i = 0; i < data.Length; i++ )
                {
                    data[i] = 0;
                }
            }
            return data;
        }

        /// <summary>
        /// Loads the data from the file into the cache
        /// </summary>
        /// <param name="Lookup"></param>
        /// <returns></returns>
        private float[] LoadAndStore(Pair<int, int> Lookup)
        {
            float[] data = Load( Lookup.First, Lookup.Second );
            Pair<int, int> StoreMe = new Pair<int, int>();
            StoreMe.First = Lookup.First;
            StoreMe.Second = Lookup.Second;
            this.Cache.Add( StoreMe, data );
            return data;
        }

        private void LoadData()
        {
        }

        private void LoadIndexes()
        {
            long primaryLocation = this.Reader.BaseStream.Position;
            for ( int i = 0; i < this.Indexes.Length; i++ )
            {
                this.Reader.BaseStream.Position = primaryLocation;
                this.Indexes[i] = new Index();
                this.Indexes[i].Start = this.Reader.ReadInt32();
                this.Indexes[i].End = this.Reader.ReadInt32();
                long offset = this.Reader.ReadInt64();
                primaryLocation = this.Reader.BaseStream.Position;
                this.Reader.BaseStream.Position = offset;
                uint length = this.Reader.ReadUInt32();
                this.Indexes[i].SubIndex = new SubIndex[length];
                for ( int j = 0; j < this.Indexes[i].SubIndex.Length; j++ )
                {
                    this.Indexes[i].SubIndex[j] = new SubIndex();
                    this.Indexes[i].SubIndex[j].Start = this.Reader.ReadInt32();
                    this.Indexes[i].SubIndex[j].End = this.Reader.ReadInt32();
                    this.Indexes[i].SubIndex[j].Location = this.Reader.ReadInt64();
                }
            }
        }

        private void LoadVersion2Data(BinaryReader binaryReader)
        {
            // burn the int for now
            try
            {
                var length = binaryReader.ReadInt32();
                if ( length > 0 )
                {
                    var start = binaryReader.BaseStream.Position;
                    // Version 2.0 string meta data
                    var numberOfV2Entries = binaryReader.ReadInt32();
                    this.MetaData = new Dictionary<string, string>( numberOfV2Entries );
                    for ( int i = 0; i < numberOfV2Entries; i++ )
                    {
                        this.MetaData[binaryReader.ReadString()] = binaryReader.ReadString();
                    }
                    // At the end set our position to the end of all of the meta data
                    binaryReader.BaseStream.Position = start + length;
                }
            }
            catch ( IOException )
            {
                throw new IOException( "Unable to read the MetaData entries in \"" + this.FileName + "\"" );
            }
        }

        private void Reload()
        {
            if ( !File.Exists( this.FileName ) ) throw new IOException( "FILE: '" + this.FileName + "' DOES NOT EXIST!" );

            this.Reader = new BinaryReader( new FileStream( this.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.RandomAccess ), Encoding.Default );
            if ( ( this.Version = this.Reader.ReadInt32() ) == 0 )
            {
                throw new IOException( "FILE: '" + this.FileName + "' not fully generated" );
            }
            this.Times = this.Reader.ReadInt32();
            this.Types = this.Reader.ReadInt32();
            if ( this.Version > 1 )
            {
                // Load version 2 data
                LoadVersion2Data( this.Reader );
            }
            else
            {
                this.MetaData = new Dictionary<string, string>( 0 );
            }
            int num = this.Reader.ReadInt32();
            this.Indexes = new Index[num];
            this.AmmountOfData = Times * Types * sizeof( float );
            this.DataLine = new byte[this.AmmountOfData];
            LoadIndexes();
            LoadData();
            this.Cache = new Cache<Pair<int, int>, float[]>( 200 );
        }

        private struct Index
        {
            public static int SizeOf = 16;
            public int End;
            public int Start;
            public SubIndex[] SubIndex;
        }

        private struct LoadRequest
        {
            internal int DataIndex;
            internal int ISectionTotal;
            internal int JSectionTotal;
        }

        private struct SubIndex
        {
            public static int SizeOf = 16;
            public int End;
            public long Location;
            public int Start;
        }
    }
}