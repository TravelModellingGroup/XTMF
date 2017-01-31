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
    /// This Cache is not thread safe, please use a Cache on each thread separately
    /// </summary>
    public sealed class OdCache : IDisposable
    {
        private int AmmountOfData;
        private Cache<Pair<int, int>, float[]> Cache;
        private byte[] DataLine;
        private readonly string FileName;
        private Index[] Indexes;
        private BinaryReader Reader;

        /// <summary>
        /// Create a new Cache Interface to the given file
        /// </summary>
        /// <param name="odcFile">The file to use as a cache</param>
        public OdCache(string odcFile)
            : this(odcFile, false)
        {
        }

        // ReSharper disable once UnusedParameter.Local
        public OdCache(string odcFile, bool threadSafe)
        {
            FileName = odcFile;
            Reload();
        }

        /// <summary>
        /// This releases the access to the file once this cache is released from memory
        /// </summary>
        ~OdCache()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the highest zone stored in the ODC
        /// </summary>
        public int HighestZone => Indexes.Last().End;

        public Dictionary<string, string> MetaData { get; private set; }

        public int Times { get; private set; }

        public int Types { get; private set; }

        public int Version { get; private set; }

        /// <summary>
        /// Get the data from O to D
        /// </summary>
        /// <param name="origin">Origin</param>
        /// <param name="destination">Destination</param>
        /// <returns>The data associated with this OD</returns>
        public float this[int origin, int destination] => this[origin, destination, 0];

        /// <summary>
        /// Get the data from the Zone to the Destination from the given time
        /// </summary>
        /// <param name="origin">Origin</param>
        /// <param name="destination">Destination</param>
        /// <param name="time">What time period to read</param>
        /// <returns>The value for that time</returns>
        public float this[int origin, int destination, int time] => this[origin, destination, time, 0];

        /// <summary>
        /// Get the data from the Zone to the Destination from the given time
        /// </summary>
        /// <param name="origin">Origin</param>
        /// <param name="destination">Destination</param>
        /// <param name="time">What time period to read</param>
        /// <param name="type">The type of data to read</param>
        /// <returns></returns>
        public float this[int origin, int destination, int time, int type]
        {
            get
            {
                var lookup = new Pair<int, int>(origin, destination);
                return (Cache[lookup] ?? LoadAndStore(lookup))[Times * type + time];
            }
        }

        public static bool FullyLoaded(string filename)
        {
            if (!File.Exists(filename))
            {
                return false;
            }
            var reader = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.RandomAccess), Encoding.Default);
            int complete = reader.ReadInt32();
            reader.Close();
            return complete != 0;
        }

        public bool ContainsIndex(int origin, int destination)
        {
            return GetPosition(origin, destination) != 0;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void ReGenerate(string dataDirectory, string outputDirectory)
        {
            string fileName = Path.GetFileNameWithoutExtension(FileName);

            string path = Path.GetDirectoryName(FileName);
            if (path == null)
            {
                throw new IOException($"Unable to find the Directory name of {FileName}!");
            }
            string xmlFile = Path.Combine(path, fileName + ".xml");

            XmlSerializer deserializer = new XmlSerializer(typeof(CacheGenerationInfo));

            TextReader textReader = new StreamReader(xmlFile);

            CacheGenerationInfo info = (CacheGenerationInfo)deserializer.Deserialize(textReader);
            textReader.Close();

            OdcCreator odcCreator = new OdcCreator(HighestZone, Types, Times, Indexes.Length);

            foreach (var dimensionInfo in info.CacheInfo)
            {
                string fname = Path.Combine(dataDirectory, dimensionInfo.FileName);

                if (dimensionInfo.Is311)
                {
                    odcCreator.LoadEmme2(fname, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
                }
                else
                {
                    if (dimensionInfo.SaveInTimes)
                    {
                        odcCreator.LoadCsvTimes(fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
                    }
                    else
                    {
                        odcCreator.LoadCsvTypes(fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
                    }
                }
            }
            var localFileName = Path.GetFileName(FileName);
            if (localFileName == null)
            {
                throw new IOException($"Unable to get the file name of {FileName}!");
            }
            odcCreator.Save(Path.Combine(outputDirectory, localFileName), true);
        }

        public void Release()
        {
            Reader?.Close();
            Reader = null;
        }

        public SparseTwinIndex<float[]> StoreAll()
        {
            if (Indexes == null)
            {
                return null;
            }
            int types = Types * Times;
            SparseIndexing index = new SparseIndexing();
            int firstLength = Indexes.Length;
            int iTotal = 0;
            index.Indexes = new SparseSet[Indexes.Length];
            float[][][] data = null;
            byte[] tempReader = new byte[(int)(Reader.BaseStream.Length - Reader.BaseStream.Position)];
            BlockingCollection<LoadRequest> requests = new BlockingCollection<LoadRequest>(50);
            // build all of the data / mallocs in a different thread
            Task.Factory.StartNew(() =>
           {
               for (int i = 0; i < firstLength; i++)
               {
                   index.Indexes[i].Start = Indexes[i].Start;
                   index.Indexes[i].Stop = Indexes[i].End;
                   iTotal += Indexes[i].End - Indexes[i].Start + 1;
               }
               int iSoFar = 0;
               data = new float[iTotal][][];
               for (int i = 0; i < firstLength; i++)
               {
                   var sub = Indexes[i].SubIndexes;
                   var subLength = sub.Length;
                   index.Indexes[i].SubIndex = new SparseIndexing();
                   index.Indexes[i].SubIndex.Indexes = new SparseSet[subLength];
                   int jTotal = 0;
                   var ithIndex = index.Indexes[i].SubIndex.Indexes;
                   for (int j = 0; j < subLength; j++)
                   {
                       ithIndex[j].Start = sub[j].Start;
                       ithIndex[j].Stop = sub[j].End;
                       jTotal += sub[j].End - sub[j].Start + 1;
                   }
                   int iSectionTotal = Indexes[i].End - Indexes[i].Start + 1;
                   for (int k = 0; k < iSectionTotal; k++)
                   {
                       data[iSoFar + k] = new float[jTotal][];
                   }
                   requests.Add(new LoadRequest() { OriginSectionTotal = iSectionTotal, DestinationSectionTotal = jTotal, DataIndex = iSoFar });
                   iSoFar += iSectionTotal;
               }
               requests.CompleteAdding();
           });
            // then we can read everything into memory
            int readInSoFar = 0;
            Reader.BaseStream.Position = Indexes[0].SubIndexes[0].Location;
            // store everything into memory
            while (readInSoFar < tempReader.Length)
            {
                readInSoFar += Reader.Read(tempReader, readInSoFar, tempReader.Length - readInSoFar);
            }
            int currentIndex = 0;
            // process the data from the stream
            foreach (var request in requests.GetConsumingEnumerable())
            {
                for (int k = 0; k < request.OriginSectionTotal; k++)
                {
                    for (int l = 0; l < request.DestinationSectionTotal; l++)
                    {
                        var row = data[request.DataIndex + k][l] = new float[types];
                        Buffer.BlockCopy(tempReader, currentIndex * sizeof(float), row, 0, types * sizeof(float));
                        currentIndex += types;
                    }
                }
            }

            return new SparseTwinIndex<float[]>(index, data);
        }

        internal void DumpToCreator(OdcCreator odcCreator)
        {
            foreach (var originBlock in Indexes)
            {
                for (int o = originBlock.Start; o < originBlock.End; o++)
                {
                    foreach (var destinationBlock in originBlock.SubIndexes)
                    {
                        for (int d = destinationBlock.Start; d < destinationBlock.End; d++)
                        {
                            odcCreator.Set(o, d, this);
                        }
                    }
                }
            }
        }

        private void Dispose(bool managed)
        {
            if (managed)
            {
                GC.SuppressFinalize(this);
            }
            Reader?.Dispose();
            Reader = null;
        }

        private long GetPosition(int o, int d)
        {
            for (int i = 0; i < Indexes.Length; i++)
            {
                if ((o >= Indexes[i].Start) & (o <= Indexes[i].End))
                {
                    var io = Indexes[i];
                    var totalJ = 0;
                    for (var j = 0; j < io.SubIndexes.Length; j++)
                    {
                        var sub = io.SubIndexes[j];
                        totalJ += sub.End - sub.Start + 1;
                    }
                    for (var j = 0; j < io.SubIndexes.Length; j++)
                    {
                        var sub = io.SubIndexes[j];
                        if ((sub.Start <= d) & (sub.End >= d))
                        {
                            return io.SubIndexes[j].Location + ((i - io.Start) * (totalJ) + d) * AmmountOfData;
                        }
                    }
                }
            }
            return 0;
        }

        private float[] Load(int first, int second)
        {
            if (Reader == null)
            {
                Reload();
            }
            float[] data = new float[Times * Types];
            long pos = GetPosition(first, second);
            if (pos > 0)
            {
                lock (this)
                {
                    Reader.BaseStream.Position = pos;
                    Reader.Read(DataLine, 0, AmmountOfData);
                    for (int i = 0, j = 0; i < data.Length; i++, j += 4)
                    {
                        data[i] = BitConverter.ToSingle(DataLine, j);
                    }
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0;
                }
            }
            return data;
        }

        /// <summary>
        /// Loads the data from the file into the cache
        /// </summary>
        /// <param name="lookup"></param>
        /// <returns></returns>
        private float[] LoadAndStore(Pair<int, int> lookup)
        {
            float[] data = Load(lookup.First, lookup.Second);
            Pair<int, int> storeMe = new Pair<int, int>(lookup.First, lookup.Second);
            Cache.Add(storeMe, data);
            return data;
        }

        private void LoadIndexes()
        {
            long primaryLocation = Reader.BaseStream.Position;
            for (int i = 0; i < Indexes.Length; i++)
            {
                Reader.BaseStream.Position = primaryLocation;
                Indexes[i] = new Index();
                Indexes[i].Start = Reader.ReadInt32();
                Indexes[i].End = Reader.ReadInt32();
                long offset = Reader.ReadInt64();
                primaryLocation = Reader.BaseStream.Position;
                Reader.BaseStream.Position = offset;
                uint length = Reader.ReadUInt32();
                Indexes[i].SubIndexes = new SubIndex[length];
                for (int j = 0; j < Indexes[i].SubIndexes.Length; j++)
                {
                    Indexes[i].SubIndexes[j] = new SubIndex();
                    Indexes[i].SubIndexes[j].Start = Reader.ReadInt32();
                    Indexes[i].SubIndexes[j].End = Reader.ReadInt32();
                    Indexes[i].SubIndexes[j].Location = Reader.ReadInt64();
                }
            }
        }

        private void LoadVersion2Data(BinaryReader binaryReader)
        {
            // burn the int for now
            try
            {
                var length = binaryReader.ReadInt32();
                if (length > 0)
                {
                    var start = binaryReader.BaseStream.Position;
                    // Version 2.0 string meta data
                    var numberOfV2Entries = binaryReader.ReadInt32();
                    MetaData = new Dictionary<string, string>(numberOfV2Entries);
                    for (int i = 0; i < numberOfV2Entries; i++)
                    {
                        MetaData[binaryReader.ReadString()] = binaryReader.ReadString();
                    }
                    // At the end set our position to the end of all of the meta data
                    binaryReader.BaseStream.Position = start + length;
                }
            }
            catch (IOException)
            {
                throw new IOException("Unable to read the MetaData entries in \"" + FileName + "\"");
            }
        }

        private void Reload()
        {
            if (!File.Exists(FileName)) throw new IOException("FILE: '" + FileName + "' DOES NOT EXIST!");

            Reader = new BinaryReader(new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.RandomAccess), Encoding.Default);
            if ((Version = Reader.ReadInt32()) == 0)
            {
                throw new IOException("FILE: '" + FileName + "' not fully generated");
            }
            Times = Reader.ReadInt32();
            Types = Reader.ReadInt32();
            if (Version > 1)
            {
                // Load version 2 data
                LoadVersion2Data(Reader);
            }
            else
            {
                MetaData = new Dictionary<string, string>(0);
            }
            int num = Reader.ReadInt32();
            Indexes = new Index[num];
            AmmountOfData = Times * Types * sizeof(float);
            DataLine = new byte[AmmountOfData];
            LoadIndexes();
            Cache = new Cache<Pair<int, int>, float[]>(200);
        }

        private struct Index
        {
            public int End;
            public int Start;
            public SubIndex[] SubIndexes;
        }

        private struct LoadRequest
        {
            internal int DataIndex;
            internal int OriginSectionTotal;
            internal int DestinationSectionTotal;
        }

        private struct SubIndex
        {
            public int End;
            public long Location;
            public int Start;
        }
    }
}