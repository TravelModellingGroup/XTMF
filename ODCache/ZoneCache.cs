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
using System.IO;
using System.Text;

namespace Datastructure;

/// <summary>
/// Provides access to a ZFC file containing zoneal information
/// </summary>
public class ZoneCache<T> : IDisposable
{
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
    /// <param name="zoneFile"></param>
    /// <param name="makeType">Convert floats to your type</param>
    /// <param name="cacheSize"></param>
    public ZoneCache(string zoneFile, Func<int, float[], T> makeType, int cacheSize = 0)
    {
        if (!File.Exists(zoneFile)) throw new IOException("FILE: '" + zoneFile + "' DOES NOT EXIST!");
        Reader = new BinaryReader(new FileStream(zoneFile, FileMode.Open, FileAccess.Read, FileShare.Read, 0x5000, FileOptions.RandomAccess), Encoding.Default);
        Zones = Reader.ReadInt32();
        if (cacheSize > 0)
        {
            Cache = new LocklessCache<int, T>(cacheSize);
        }
        else
        {
            Cache = new LocklessCache<int, T>();
        }
        Version = Reader.ReadInt32();
        Types = Reader.ReadInt32();
        Make = makeType;
        DataLine = new byte[Types * 4];
        FileSize = Reader.BaseStream.Length;
        LoadSparseIndexes();
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
    /// <param name="zone">Zone</param>
    /// <returns>The data assosiated with this OD</returns>
    public T this[int zone]
    {
        get
        {
            T element;
            element = Cache[zone];

            element ??= LoadAndStore(zone);
            return element;
        }
    }

    public void Release()
    {
        Reader?.Close();
        Reader = null;
    }

    public SparseArray<T> StoreAll()
    {
        SparseIndexing indexing;
        int numberOfSegments = Segments.Length;
        indexing.Indexes = new SparseSet[numberOfSegments];
        int total = 0;
        Reader.BaseStream.Position = Segments[0].DiskLocation;
        for (int i = 0; i < numberOfSegments; i++)
        {
            indexing.Indexes[i].Start = Segments[i].Start;
            indexing.Indexes[i].Stop = Segments[i].Stop;
            total += indexing.Indexes[i].Stop - indexing.Indexes[i].Start + 1;
        }
        T[] data = new T[total];
        int types = Types;
        float[] typeData = new float[types];
        int k = 0;
        for (int i = 0; i < numberOfSegments; i++)
        {
            for (int j = indexing.Indexes[i].Start; j <= indexing.Indexes[i].Stop; j++)
            {
                for (int t = 0; t < types; t++)
                {
                    typeData[t] = Reader.ReadSingle();
                }
                data[k++] = Make(j, typeData);
            }
        }
        return new SparseArray<T>(indexing, data);
    }

    private bool GetTransformedIndex(ref int o)
    {
        int min = 0;
        int max = Segments.Length - 1;
        while (min <= max)
        {
            int mid = ((min + max) / 2);
            var midIndex = Segments[mid];

            if (o < midIndex.Start)
            {
                max = mid - 1;
            }
            else if (o > midIndex.Stop)
            {
                min = mid + 1;
            }
            else
            {
                // then we are in a vlid range
                o = (o - midIndex.Start + midIndex.BaseLocation);
                return true;
            }
        }
        return false;
    }

    private T Load(int zoneId)
    {
        float[] data = new float[Types];
        long pos;
        if (!GetTransformedIndex(ref zoneId)
            || (pos = (sizeof(int) * 3) + zoneId * Types * sizeof(float)) >= FileSize)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }
        }
        else
        {
            Reader.BaseStream.Position = pos;
            Reader.Read(DataLine, 0, DataLine.Length);
            for (int i = 0, j = 0; i < data.Length; i++, j += 4)
            {
                data[i] = BitConverter.ToSingle(DataLine, j);
            }
        }
        return Make(zoneId, data);
    }

    /// <summary>
    /// Loads the data from the file into the cache
    /// </summary>
    /// <returns></returns>
    private T LoadAndStore(int zoneId)
    {
        var value = Load(zoneId);
        Cache.Add(zoneId, value);
        return value;
    }

    private void LoadSparseIndexes()
    {
        if (Version >= 2)
        {
            int numberOfSegments;
            Segments = new SparseSegment[numberOfSegments = Reader.ReadInt32()];
            int total = 0;
            for (int i = 0; i < numberOfSegments; i++)
            {
                Segments[i].Start = Reader.ReadInt32();
                Segments[i].Stop = Reader.ReadInt32();
                Segments[i].DiskLocation = Reader.ReadInt64();
                Segments[i].BaseLocation = total;
                total += Segments[i].Stop - Segments[i].Start + 1;
            }
        }
        else
        {
            Segments = new SparseSegment[1];
            Segments[0].Start = 0;
            Segments[0].Stop = Zones;
            Segments[0].BaseLocation = 0;
            Segments[0].DiskLocation = Reader.BaseStream.Position;
        }
    }

    private struct SparseSegment
    {
        public int BaseLocation;
        public long DiskLocation;
        public int Start;
        public int Stop;
    }

    /// <summary>
    /// This releases the access to the file once this cache is released from memory
    /// </summary>
    ~ZoneCache()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            Reader?.Dispose();
            Reader = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}// end class
// end namespace