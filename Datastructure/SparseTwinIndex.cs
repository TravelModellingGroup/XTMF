/*
    Copyright 2014-2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Datastructure
{
    public sealed class SparseTwinIndex<T>
    {
        public SparseIndexing Indexes { get; private set; }

        private T?[][] Data;

        public SparseTwinIndex(SparseIndexing indexes, T?[][]? data = null)
        {
            Indexes = indexes;
            if (data is not null)
            {
                Data = data;
            }
            if (indexes.Indexes is not null)
            {
                Data = GenerateStructure();
            }
            else if(Data is null)
            {
                throw new ArgumentException("You must define either the indexes if you do not provide the data.");
            }
            //Generate _Count
            var count = 0;
            for (var i = 0; i < Data.Length; i++)
            {
                count += Data[i].Length;
            }
            Count = count;
        }

        public int Count
        {
            get;
            private set;
        }

        public T? this[int o, int d]
        {
            get
            {
                if (GetTransformedIndexes(ref o, ref d))
                {
                    return Data[o][d];
                }
                else
                {
                    // return null / whatever the closest thing to null is
                    return default;
                }
            }

            set
            {
                var originalO = o;
                var originalD = d;
                if (GetTransformedIndexes(ref o, ref d))
                {
                    Data[o][d] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException(String.Format("The location {0}:{1} is invalid for this Sparse Twin Index Datastructure!", originalO, originalD));
                }
            }
        }

        /// <summary>
        /// Get the result if the sparse indexes exist.
        /// </summary>
        /// <param name="o">The row to store to</param>
        /// <param name="d">The column to store to.</param>
        /// <param name="value">The value that was read, default value otherwise</param>
        /// <returns>True if the index was found, false otherwise.</returns>
        public bool TryGet(int o, int d, [NotNullWhen(true)] out T? value)
        {
            if (GetTransformedIndexes(ref o, ref d))
            {
                value = Data[o][d]!;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Store the result if the sparse indexes exist.
        /// </summary>
        /// <param name="o">The row to store to</param>
        /// <param name="d">The column to store to.</param>
        /// <param name="value">The value to store</param>
        /// <returns></returns>
        public bool TryStore(int o, int d, T value)
        {
            if (GetTransformedIndexes(ref o, ref d))
            {
                Data[o][d] = value;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static SparseTwinIndex<T> CreateSimilarArray<TFirst, TSecond>(SparseArray<TFirst> first, SparseArray<TSecond> second)
        {
            var indexes = new SparseIndexing();
            indexes.Indexes = (SparseSet[])first.Indexing.Indexes.Clone();
            var length = indexes.Indexes.Length;
            for (var i = 0; i < length; i++)
            {
                indexes.Indexes[i].SubIndex = new SparseIndexing() { Indexes = (SparseSet[])second.Indexing.Indexes.Clone() };
            }
            return new SparseTwinIndex<T>(indexes);
        }

        public static SparseTwinIndex<T> CreateTwinIndex(int[] first, int[] second, T[] data)
        {
            var length = data.Length;
            if (length == 0)
            {
                return new SparseTwinIndex<T>(new SparseIndexing());
            }
            var indexes = new SortStruct[length];
            for (var i = 0; i < length; i++)
            {
                indexes[i].SparseSpaceFirst = first[i];
                indexes[i].SparseSpaceSecond = second[i];
                indexes[i].DataSpace = i;
            }
            Array.Sort(indexes, new CompareSortStruct());
            var processedIndexes = GenerateIndexes(indexes);
            return new SparseTwinIndex<T>(ConvertToIndexes(processedIndexes, out T[][] localData, data, indexes), localData) { Count = length };
        }

        /// <summary>
        /// Create a new square SparseTwinIndex using the given indexes, and optionally pre-filling it with data.
        /// </summary>
        /// <param name="firstIndex">The origin indexes</param>
        /// <param name="secondIndex">The destination indexes</param>
        /// <param name="data">(Optional)The data to initialize the structure with</param>
        /// <returns>A SparseTwinIndex in the shape provided and optionally filled with the given data.</returns>
        public static SparseTwinIndex<T> CreateSquareTwinIndex(int[] firstIndex, int[] secondIndex, T[]? data = null)
        {
            // check the parameters
            ArgumentNullException.ThrowIfNull(firstIndex, nameof(firstIndex));
            ArgumentNullException.ThrowIfNull(secondIndex, nameof(secondIndex));

            if (data != null)
            {
                if (firstIndex.Length * secondIndex.Length != data.Length)
                {
                    throw new ArgumentException("The lengths of firstIndex multiplied by secondIndex is not the same as the length of data!");
                }
            }
            // now that we know our parameters are of the right length build the square matrix
            // first figure out the stretches of consecutive values for each index
            var firstRangeSet = new RangeSet(firstIndex);
            var secondRangeSet = new RangeSet(secondIndex);
            return new SparseTwinIndex<T>(CreateSquareIndexes(firstRangeSet, secondRangeSet), ConvertArrayToMatrix(firstIndex, secondIndex, data)) { Count = firstIndex.Length * secondIndex.Length };
        }

        /// <summary>
        /// Create a square twin index
        /// </summary>
        /// <param name="indexes">The indexes to use for both origin and destination. Must be monotonic if providing data.</param>
        /// <param name="data">Optional, the data to use for the matrix.</param>
        /// <returns>A SquareTwinIndex sized using the given indexes</returns>
        public static SparseTwinIndex<T> CreateSquareTwinIndex(int[] indexes, T[][]? data = null)
        {
            ArgumentNullException.ThrowIfNull(indexes, nameof(indexes));

            // Check the data if it exists to make sure that the size is correct
            if(data is not null)
            {
                if(data.Length != indexes.Length)
                {
                    throw new ArgumentException($"The number of rows in the data is not the same as the number of indexes!");
                }
                for ( var i = 0; i < data.Length; i++)
                {
                    if (data[i].Length != indexes.Length)
                    {
                        throw new ArgumentException($"The number of columns in the data is not the same as the number of indexes!");
                    }
                }
                if (!IsMonotonic(indexes))
                {
                    throw new ArgumentException($"The indexes must be monotonic!", nameof(indexes));
                }
            }
            // Now that we know that the given data is fine, build the twin index
            var ranges = new RangeSet(indexes);
            return new SparseTwinIndex<T>(CreateSquareIndexes(ranges, ranges), data);
        }

        private static bool IsMonotonic(int[] indexes)
        {
            for (int i = 1; i < indexes.Length; i++)
            {
                if (indexes[i - 1] >= indexes[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static SparseIndexing CreateSquareIndexes(RangeSet firstRangeSet, RangeSet secondRangeSet)
        {
            var ret = new SparseIndexing() { Indexes = CreateSparseSetFromRangeSet(firstRangeSet) };
            var indexes = ret.Indexes;
            for (var i = 0; i < indexes.Length; i++)
            {
                indexes[i].SubIndex = new SparseIndexing() { Indexes = CreateSparseSetFromRangeSet(secondRangeSet) };
            }
            return ret;
        }

        private static SparseSet[] CreateSparseSetFromRangeSet(RangeSet set)
        {
            var ret = new SparseSet[set.Count];
            for (var i = 0; i < ret.Length; i++)
            {
                ret[i] = new SparseSet() { Start = set[i].Start, Stop = set[i].Stop };
            }
            return ret;
        }



        private static T[][]? ConvertArrayToMatrix(int[] firstIndex, int[] secondIndex, T[]? data)
        {
            // if there is nothing to do, we are already done
            if (data is null) return null;
            // if there is data to copy into the new structure build it and copy
            var ret = new T[firstIndex.Length][];
            var pos = 0;
            for (var i = 0; i < ret.Length; i++)
            {
                ret[i] = new T[secondIndex.Length];
                Array.Copy(data, pos, ret[i], 0, ret[i].Length);
                pos += ret[i].Length;
            }
            return ret;
        }

        public bool ContainsIndex(int o, int d)
        {
            return GetTransformedIndexes(ref o, ref d);
        }

        public SparseTwinIndex<TKey> CreateSimilarArray<TKey>()
        {
            var ret = new SparseTwinIndex<TKey>(Indexes);
            return ret;
        }

        public T?[][] GetFlatData()
        {
            return Data;
        }

        public int GetFlatIndex(int sparseSpaceIndex)
        {
            if (GetTransformedIndex(ref sparseSpaceIndex))
            {
                // the now transformed sparse space index for O
                return sparseSpaceIndex;
            }
            return -1;
        }

        public int GetFlatIndex(int sparseSpaceIndexO, int sparseSpaceIndexD)
        {
            if (GetTransformedIndexes(ref sparseSpaceIndexO, ref sparseSpaceIndexD))
            {
                // the now transformed sparse space index for D
                return sparseSpaceIndexD;
            }
            return -1;
        }

        /// <summary>
        /// Get the sparse index of a flat data location
        /// </summary>
        /// <param name="flatIndex">The flat address to lookup</param>
        /// <returns>The corresponding sparse address, -1 if it doesn't exist</returns>
        public int GetSparseIndex(int flatIndex)
        {
            var soFar = 0;
            for (var i = 0; i < Indexes.Indexes.Length; i++)
            {
                var index = Indexes.Indexes[i];
                var length = index.Stop - index.Start + 1;
                if (soFar + length > flatIndex)
                {
                    return index.Start + (flatIndex - soFar);
                }
                soFar += length;
            }
            return -1;
        }

        /// <summary>
        /// Get the sparse index of a second index flat data location
        /// </summary>
        /// <param name="flatIndexI">The first dimension's flat address</param>
        /// <param name="flatIndexJ">The second dimension's flat address</param>
        /// <returns>The second dimension's sparse address, -1 if it doesn't exist</returns>
        public int GetSparseIndex(int flatIndexI, int flatIndexJ)
        {
            var soFar = 0;
            for (var i = 0; i < Indexes.Indexes.Length; i++)
            {
                var index = Indexes.Indexes[i];
                var length = index.Stop - index.Start + 1;
                if (soFar + length > flatIndexI)
                {
                    soFar = 0;
                    var iIndex = Indexes.Indexes[i];
                    for (var j = 0; j < iIndex.SubIndex.Indexes.Length; j++)
                    {
                        index = iIndex.SubIndex.Indexes[j];
                        length = index.Stop - index.Start + 1;
                        if (soFar + length > flatIndexJ)
                        {
                            return index.Start + (flatIndexJ - soFar);
                        }
                        soFar += length;
                    }
                    return -1;
                }
                soFar += length;
            }
            return -1;
        }

        /// <summary>
        /// Get a copy of all of the valid indexes in the first dimension
        /// </summary>
        /// <returns>An array of all of the valid indexes</returns>
        public int[] ValidIndexArray()
        {
            var pos = 0;
            var ret = new int[Data.Length];
            var length = Indexes.Indexes.Length;
            for (var i = 0; i < length; i++)
            {
                var stop = Indexes.Indexes[i].Stop;
                for (var j = Indexes.Indexes[i].Start; j <= stop; j++)
                {
                    ret[pos++] = j;
                }
            }
            return ret;
        }

        public IEnumerable<int> ValidIndexes()
        {
            if (Indexes.Indexes != null)
            {
                var length = Indexes.Indexes.Length;
                for (var i = 0; i < length; i++)
                {
                    var stop = Indexes.Indexes[i].Stop;
                    for (var j = Indexes.Indexes[i].Start; j <= stop; j++)
                    {
                        yield return j;
                    }
                }
            }
        }

        public IEnumerable<int> ValidIndexes(int first)
        {
            var indexes = Indexes.Indexes;
            if (indexes != null)
            {
                if (TansformO(indexes, ref first, out SparseSet oSet))
                {
                    var length = oSet.SubIndex.Indexes.Length;
                    for (var i = 0; i < length; i++)
                    {
                        var stop = oSet.SubIndex.Indexes[i].Stop;
                        for (var j = oSet.SubIndex.Indexes[i].Start; j <= stop; j++)
                        {
                            yield return j;
                        }
                    }
                }
            }
        }

        private static SparseIndexing ConvertToIndexes(List<SparseSet> processedIndexes, out T[][] outputData, T[] data, SortStruct[] index)
        {
            var start = new SparseIndexing();
            var iLength = processedIndexes.Count;
            outputData = new T[iLength][];
            start.Indexes = new SparseSet[iLength];
            var dataProcessed = 0;
            for (var i = 0; i < iLength; i++)
            {
                start.Indexes[i] = processedIndexes[i];
                var jLength = 0;
                var jSections = processedIndexes[i].SubIndex.Indexes.Length;
                for (var jSection = 0; jSection < jSections; jSection++)
                {
                    var indexStop = processedIndexes[i].SubIndex.Indexes[jSection].Stop;
                    var indexStart = processedIndexes[i].SubIndex.Indexes[jSection].Start;
                    start.Indexes[i].SubIndex.Indexes[jSection].Start = indexStart;
                    start.Indexes[i].SubIndex.Indexes[jSection].Stop = indexStop;
                    jLength += indexStop - indexStart + 1;
                }
                outputData[i] = new T[jLength];
                for (var j = 0; j < jLength; j++)
                {
                    outputData[i][j] = data[index[dataProcessed++].DataSpace];
                }
            }
            return start;
        }

        private static List<SparseSet> GenerateIndexes(SortStruct[] indexes)
        {
            var meta = new List<SparseSet>();
            var length = indexes.Length;
            // Phase 1: Add in all of the sets we are going to see
            var currentSet = new SparseSet();
            currentSet.Start = currentSet.Stop = indexes[0].SparseSpaceSecond;
            var subSets = new List<SparseSet>();
            for (var i = 1; i < indexes.Length; i++)
            {
                if (indexes[i].SparseSpaceFirst == indexes[i - 1].SparseSpaceFirst)
                {
                    if (indexes[i].SparseSpaceSecond > indexes[i - 1].SparseSpaceSecond + 1)
                    {
                        subSets.Add(currentSet);
                        currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceSecond;
                    }
                    else
                    {
                        currentSet.Stop = indexes[i].SparseSpaceSecond;
                    }
                }
                else
                {
                    subSets.Add(currentSet);
                    meta.Add(new SparseSet()
                    {
                        Start = indexes[i - 1].SparseSpaceFirst,
                        Stop = indexes[i - 1].SparseSpaceFirst,
                        SubIndex = new SparseIndexing() { Indexes = subSets.ToArray() }
                    });
                    subSets.Clear();
                    currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceSecond;
                }
            }
            subSets.Add(currentSet);
            meta.Add(new SparseSet()
            {
                Start = indexes[length - 1].SparseSpaceFirst,
                Stop = indexes[length - 1].SparseSpaceFirst,
                SubIndex = new SparseIndexing() { Indexes = subSets.ToArray() }
            });
            return meta;
        }

        private T?[][] GenerateStructure()
        {
            var totalFirst = 0;
            for (var i = 0; i < Indexes.Indexes.Length; i++)
            {
                Indexes.Indexes[i].BaseLocation = totalFirst;
                totalFirst += Indexes.Indexes[i].Stop - Indexes.Indexes[i].Start + 1;
            }
            var ret = Data ?? new T[totalFirst][];
            var currentDataPlace = 0;
            // Now make the matrix's second rows
            for (var i = 0; i < Indexes.Indexes.Length; i++)
            {
                var length = Indexes.Indexes[i].SubIndex.Indexes.Length;
                var totalSecond = 0;
                // calculate the total
                for (var j = 0; j < length; j++)
                {
                    Indexes.Indexes[i].SubIndex.Indexes[j].BaseLocation = totalSecond;
                    totalSecond += Indexes.Indexes[i].SubIndex.Indexes[j].Stop - Indexes.Indexes[i].SubIndex.Indexes[j].Start + 1;
                }
                if (Data is null)
                {
                    for (var k = Indexes.Indexes[i].Start; k <= Indexes.Indexes[i].Stop; k++)
                    {
                        ret[currentDataPlace++] = new T[totalSecond];
                    }
                }
            }
            return ret;
        }

        private bool GetTransformedIndex(ref int o)
        {
            var indexes = Indexes.Indexes;
            var min = 0;
            var max = indexes.Length - 1;
            while (min <= max)
            {
                var mid = ((min + max) >> 1);
                var midIndex = indexes[mid];

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
                    // then we are in a valid range
                    o = (o - midIndex.Start + midIndex.BaseLocation);
                    return true;
                }
            }
            return false;
        }

        private const int LookUpLinearMax = 64;

        private bool GetTransformedIndexes(ref int o, ref int d)
        {
            var indexes = Indexes.Indexes;
            if (Indexes.Indexes == null) return false;
            if (TansformO(indexes, ref o, out SparseSet oSet))
            {
                var subIndexes = oSet.SubIndex.Indexes;
                if (subIndexes.Length >= LookUpLinearMax)
                {
                    var min = 0;
                    var max = subIndexes.Length - 1;
                    while (min <= max)
                    {
                        var mid = ((min + max) >> 1);

                        if (d < subIndexes[mid].Start)
                        {
                            max = mid - 1;
                        }
                        else if (d > subIndexes[mid].Stop)
                        {
                            min = mid + 1;
                        }
                        else
                        {
                            // then we are in a valid range
                            d = (d - subIndexes[mid].Start + subIndexes[mid].BaseLocation);
                            return true;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < subIndexes.Length; i++)
                    {
                        if ((subIndexes[i].Start <= d & subIndexes[i].Stop >= d))
                        {
                            // then we are in a valid range
                            d = (d - subIndexes[i].Start + subIndexes[i].BaseLocation);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool TansformO(SparseSet[] subIndexes, ref int o, out SparseSet oSet)
        {
            // if it is large use binary search
            if (subIndexes.Length < LookUpLinearMax)
            {
                //otherwise just do a linear search\
                for (var i = 0; i < subIndexes.Length; i++)
                {
                    if ((subIndexes[i].Start <= o & subIndexes[i].Stop >= o))
                    {
                        o = (o - subIndexes[i].Start + subIndexes[i].BaseLocation);
                        oSet = subIndexes[i];
                        return true;
                    }
                }
            }
            else
            {
                var min = 0;
                var max = subIndexes.Length - 1;
                while (min <= max)
                {
                    var mid = ((min + max) >> 1);
                    if (o < subIndexes[mid].Start)
                    {
                        max = mid - 1;
                    }
                    else if (o > subIndexes[mid].Stop)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        // then we are in a valid range
                        o = (o - subIndexes[mid].Start + subIndexes[mid].BaseLocation);
                        oSet = subIndexes[mid];
                        return true;
                    }
                }
            }
            oSet = default(SparseSet);
            return false;
        }

        private struct SortStruct
        {
            public int DataSpace;
            public int SparseSpaceFirst;
            public int SparseSpaceSecond;

            public override string ToString()
            {
                return "[" + DataSpace + "]" + SparseSpaceFirst + ":" + SparseSpaceSecond;
            }
        }

        private class CompareSortStruct : IComparer<SortStruct>
        {
            public int Compare(SortStruct x, SortStruct y)
            {
                if (x.SparseSpaceFirst < y.SparseSpaceFirst) return -1;
                if (x.SparseSpaceFirst == y.SparseSpaceFirst)
                {
                    if (x.SparseSpaceSecond < y.SparseSpaceSecond)
                    {
                        return -1;
                    }
                    else if (x.SparseSpaceSecond == y.SparseSpaceSecond)
                    {
                        return 0;
                    }
                }
                return 1;
            }
        }

        /// <summary>
        /// Creates a shallow clone of the matrix.
        /// </summary>
        /// <returns>A new matrix that contains a shallow clone of the data.</returns>
        public SparseTwinIndex<T> Clone()
        {
            var clone = new SparseTwinIndex<T>(Indexes);
            for (int i = 0; i < Data.Length; i++)
            {
                Array.Copy(Data[i], clone.Data[i], Data[i].Length);
            }
            return clone;
        }
    }
}