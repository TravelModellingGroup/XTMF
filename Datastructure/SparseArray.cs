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
using System.IO;
using System.Text;

namespace Datastructure
{
    public sealed class SparseArray<T>
    {
        internal SparseIndexing Indexing;
        private const int Version = 2;
        private T[] Data;

        public SparseArray(SparseIndexing indexing, T[] rawData = null)
        {
            Indexing = indexing;
            if(rawData != null)
            {
                Data = rawData;
            }
            GenerateStructure();
        }

        public int Count => Data.Length;

        public int Top { get; private set; }

        public T this[int o]
        {
            get
            {
                if(GetTransformedIndex(ref o))
                {
                    return Data[o];
                }
                else
                {
                    // return null / whatever the closest thing to null is
                    return default(T);
                }
            }

            set
            {
                var originalO = o;
                if(GetTransformedIndex(ref o))
                {
                    Data[o] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException(String.Format("The location {0} is invalid for this SparseArray Datastructure!", originalO));
                }
            }
        }

        public static SparseArray<T> CreateSparseArray(int[] sparseSpace, IList<T> data)
        {
            var length = sparseSpace.Length;
            var indexes = new SortStruct[length];
            for(var i = 0; i < length; i++)
            {
                indexes[i].SparseSpace = sparseSpace[i];
                indexes[i].DataSpace = i;
            }
            return CreateSparseArray(data, length, indexes);
        }

        public static SparseArray<T> CreateSparseArray(Func<T, int> PlaceFunction, IList<T> data)
        {
            var length = data.Count;
            var indexes = new SortStruct[length];
            for(var i = 0; i < length; i++)
            {
                indexes[i].SparseSpace = PlaceFunction(data[i]);
                indexes[i].DataSpace = i;
            }
            return CreateSparseArray(data, length, indexes);
        }

        public bool ContainsIndex(int o)
        {
            return GetTransformedIndex(ref o);
        }

        public SparseArray<K> CreateSimilarArray<K>()
        {
            var ret = new SparseArray<K>(Indexing);
            return ret;
        }

        public SparseTwinIndex<K> CreateSquareTwinArray<K>()
        {
            SparseIndexing twinIndex;
            var length = Indexing.Indexes.Length;
            twinIndex.Indexes = new SparseSet[length];
            for(var i = 0; i < length; i++)
            {
                twinIndex.Indexes[i].Start = Indexing.Indexes[i].Start;
                twinIndex.Indexes[i].Stop = Indexing.Indexes[i].Stop;
                twinIndex.Indexes[i].SubIndex = new SparseIndexing() { Indexes = new SparseSet[length] };
                for(var j = 0; j < length; j++)
                {
                    twinIndex.Indexes[i].SubIndex.Indexes[j] = Indexing.Indexes[j];
                }
            }
            return new SparseTwinIndex<K>(twinIndex);
        }

        public T[] GetFlatData()
        {
            return Data;
        }

        public int GetFlatIndex(int sparseSpaceIndex)
        {
            if(GetTransformedIndex(ref sparseSpaceIndex))
            {
                return sparseSpaceIndex;
            }
            return -1;
        }

        public int GetSparseIndex(int flatIndex)
        {
            var soFar = 0;
            for(var i = 0; i < Indexing.Indexes.Length; i++)
            {
                var index = Indexing.Indexes[i];
                var length = index.Stop - index.Start + 1;
                if(soFar + length > flatIndex)
                {
                    return index.Start + (flatIndex - soFar);
                }
                soFar += length;
            }
            return -1;
        }

        public void Save(string fileName, Func<T, float[]> Decompose, int Types)
        {
            using (var writer = new BinaryWriter(new
                FileStream(fileName, FileMode.Create, FileAccess.Write,
                FileShare.None, 0x8000, FileOptions.SequentialScan),
                Encoding.Default))
            {
                var dataLength = Data.Length;
                var highestZone = 0;

                for(var i = 0; i < dataLength; i++)
                {
                    if(Data[i] != null) highestZone = i;
                }
                writer.Write(highestZone);
                writer.Write(Version);
                writer.Write(Types);
                WriteSparseIndexes(writer, Types);

                for(var i = 0; i < dataLength; i++)
                {
                    if(Data[i] != null)
                    {
                        var data = Decompose(Data[i]);
                        for(var j = 0; j < data.Length; j++)
                        {
                            writer.Write(data[j]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get a copy of all of the valid indexes
        /// </summary>
        /// <returns>An array of all of the valid indexes</returns>
        public int[] ValidIndexArray()
        {
            var ret = new int[Data.Length];
            var pos = 0;
            var length = Indexing.Indexes.Length;
            for(var i = 0; i < length; i++)
            {
                var stop = Indexing.Indexes[i].Stop;
                for(var j = Indexing.Indexes[i].Start; j <= stop; j++)
                {
                    ret[pos++] = j;
                }
            }
            return ret;
        }

        /// <summary>
        /// Get an enumeration of all of the valid indexes in the sparse array
        /// </summary>
        /// <returns>An enumeration of the indexes</returns>
        public IEnumerable<int> ValidIndexies()
        {
            var length = Indexing.Indexes.Length;
            for(var i = 0; i < length; i++)
            {
                var stop = Indexing.Indexes[i].Stop;
                for(var j = Indexing.Indexes[i].Start; j <= stop; j++)
                {
                    yield return j;
                }
            }
        }

        private static SparseArray<T> CreateSparseArray(IList<T> data, int length, SortStruct[] indexes)
        {
            Array.Sort(indexes, new CompareSortStruct());
            var Data = new T[length];
            if (data != null)
            {
                for (var i = 0; i < length; i++)
                {
                    Data[i] = data[indexes[i].DataSpace];
                }
            }
            return new SparseArray<T>(GenerateIndexes(indexes), Data);
        }

        private static SparseIndexing GenerateIndexes(SortStruct[] data)
        {
            var Indexes = new SparseIndexing();
            var elements = new List<SparseSet>();
            var length = data.Length;
            var current = default(SparseSet);
            if(length == 0) return Indexes;
            current.Start = data[0].SparseSpace;
            var expected = data[0].SparseSpace + 1;
            for(var i = 1; i < length; i++)
            {
                if(data[i].SparseSpace != expected)
                {
                    current.Stop = data[i - 1].SparseSpace;
                    current.BaseLocation = 0;
                    elements.Add(current);
                    current.Start = data[i].SparseSpace;
                }
                expected = data[i].SparseSpace + 1;
            }
            current.Stop = data[length - 1].SparseSpace;
            elements.Add(current);
            Indexes.Indexes = elements.ToArray();
            return Indexes;
        }

        private void GenerateStructure()
        {
            var total = 0;
            for(var i = 0; i < Indexing.Indexes.Length; i++)
            {
                Indexing.Indexes[i].BaseLocation = total;
                total += Indexing.Indexes[i].Stop - Indexing.Indexes[i].Start + 1;
                if(Indexing.Indexes[i].Stop > Top)
                {
                    Top = Indexing.Indexes[i].Stop;
                }
            }
            if(Data == null)
            {
                Data = new T[total];
            }
        }


        private const int LookUpLinearMax = 64;

        private bool GetTransformedIndex(ref int o)
        {
            var indexes = Indexing.Indexes;
            if(indexes.Length >= LookUpLinearMax)
            {
                var min = 0;
                var max = indexes.Length - 1;
                while(min <= max)
                {
                    var mid = ((min + max) >> 1);
                    var midIndex = indexes[mid];

                    if(o < midIndex.Start)
                    {
                        max = mid - 1;
                    }
                    else if(o > midIndex.Stop)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        // then we are in a valid range
                        o = (o - midIndex.Start + midIndex.BaseLocation);
                        return true;
                    }
                }
            }
            else
            {
                //otherwise just do a linear search
                for(var i = 0; i < indexes.Length; i++)
                {
                    if(indexes[i].Stop >= o)
                    {
                        if(indexes[i].Start <= o)
                        {
                            o = (o - indexes[i].Start + indexes[i].BaseLocation);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return false;
        }

        private void WriteSparseIndexes(BinaryWriter writer, int types)
        {
            var numberOfIndexes = Indexing.Indexes.Length;
            long baseLocation = 12 + 8 * numberOfIndexes; // skip the header and the indexes for the start of data
            writer.Write(numberOfIndexes);
            for(var i = 0; i < numberOfIndexes; i++)
            {
                writer.Write(Indexing.Indexes[i].Start);
                writer.Write(Indexing.Indexes[i].Stop);
                writer.Write(baseLocation);
                baseLocation += (Indexing.Indexes[i].Stop - Indexing.Indexes[i].Start + 1) * types * 4;
            }
        }

        private struct SortStruct
        {
            public int DataSpace;
            public int SparseSpace;

            public override string ToString()
            {
                return SparseSpace + "->" + DataSpace;
            }
        }

        private class CompareSortStruct : IComparer<SortStruct>
        {
            public int Compare(SortStruct x, SortStruct y)
            {
                if(x.SparseSpace < y.SparseSpace) return -1;
                if(x.SparseSpace == y.SparseSpace)
                {
                    return 0;
                }
                return 1;
            }
        }
    }
}