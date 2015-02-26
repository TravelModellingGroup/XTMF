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
    public sealed class SparseTwinIndex<T>
    {
        internal SparseIndexing Indexes;

        private T[][] Data;

        public SparseTwinIndex(SparseIndexing indexes, T[][] data = null)
        {
            this.Indexes = indexes;
            if ( data != null )
            {
                this.Data = data;
            }
            if ( indexes.Indexes != null )
            {
                GenerateStructure();
            }
        }

        public int Count
        {
            get;
            private set;
        }

        public T this[int o, int d]
        {
            get
            {
                if ( this.GetTransformedIndexes( ref o, ref d ) )
                {
                    return this.Data[o][d];
                }
                else
                {
                    // return null / whatever the closest thing to null is
                    return default( T );
                }
            }

            set
            {
                int originalO = o;
                int originalD = d;
                if ( this.GetTransformedIndexes( ref o, ref d ) )
                {
                    this.Data[o][d] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException( String.Format( "The location {0}:{1} is invalid for this Sparse Twin Index Datastructure!", originalO, originalD ) );
                }
            }
        }

        public static SparseTwinIndex<T> CreateSimilarArray<J, K>(SparseArray<J> first, SparseArray<K> second)
        {
            SparseIndexing indexes = new SparseIndexing();
            indexes.Indexes = first.Indexing.Indexes.Clone() as SparseSet[];
            var length = indexes.Indexes.Length;
            for ( int i = 0; i < length; i++ )
            {
                indexes.Indexes[i].SubIndex = new SparseIndexing() { Indexes = second.Indexing.Indexes.Clone() as SparseSet[] };
            }
            return new SparseTwinIndex<T>( indexes );
        }

        public static SparseTwinIndex<T> CreateTwinIndex(int[] first, int[] second, T[] data)
        {
            var length = data.Length;
            if ( length == 0 )
            {
                return new SparseTwinIndex<T>( new SparseIndexing() { Indexes = null }, null );
            }
            SortStruct[] indexes = new SortStruct[length];
            for ( int i = 0; i < length; i++ )
            {
                indexes[i].SparseSpaceFirst = first[i];
                indexes[i].SparseSpaceSecond = second[i];
                indexes[i].DataSpace = i;
            }
            Array.Sort( indexes, new CompareSortStruct() );
            var processedIndexes = GenerateIndexes( indexes );
            T[][] Data;

            return new SparseTwinIndex<T>( ConvertToIndexes( processedIndexes, out Data, data, indexes ), Data ) { Count = length };
        }

        public bool ContainsIndex(int o, int d)
        {
            return this.GetTransformedIndexes( ref o, ref d );
        }

        public SparseTwinIndex<K> CreateSimilarArray<K>()
        {
            SparseTwinIndex<K> ret = new SparseTwinIndex<K>( this.Indexes );
            return ret;
        }

        public T[][] GetFlatData()
        {
            return this.Data;
        }

        public int GetFlatIndex(int sparseSpaceIndex)
        {
            if ( this.GetTransformedIndex( ref sparseSpaceIndex ) )
            {
                // the now transformed sparse space index for O
                return sparseSpaceIndex;
            }
            return -1;
        }

        public int GetFlatIndex(int sparseSpaceIndexO, int sparseSpaceIndexD)
        {
            if ( this.GetTransformedIndexes( ref sparseSpaceIndexO, ref sparseSpaceIndexD ) )
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
        /// <returns>The coresponding sparse address, -1 if it doesn't exist</returns>
        public int GetSparseIndex(int flatIndex)
        {
            int soFar = 0;
            for ( int i = 0; i < this.Indexes.Indexes.Length; i++ )
            {
                var index = this.Indexes.Indexes[i];
                var length = index.Stop - index.Start + 1;
                if ( soFar + length > flatIndex )
                {
                    return index.Start + ( flatIndex - soFar );
                }
                soFar += length;
            }
            return -1;
        }

        /// <summary>
        /// Get the sprase index of a second index flat data location
        /// </summary>
        /// <param name="flatIndexI">The first dimension's flat address</param>
        /// <param name="flatIndexJ">The second dimension's flat address</param>
        /// <returns>The second dimension's sparse address, -1 if it doesn't exist</returns>
        public int GetSparseIndex(int flatIndexI, int flatIndexJ)
        {
            int soFar = 0;
            for ( int i = 0; i < this.Indexes.Indexes.Length; i++ )
            {
                var index = this.Indexes.Indexes[i];
                var length = index.Stop - index.Start + 1;
                if ( soFar + length > flatIndexI )
                {
                    soFar = 0;
                    var iIndex = this.Indexes.Indexes[i];
                    for ( int j = 0; j < iIndex.SubIndex.Indexes.Length; j++ )
                    {
                        index = iIndex.SubIndex.Indexes[j];
                        length = index.Stop - index.Start + 1;
                        if ( soFar + length > flatIndexJ )
                        {
                            return index.Start + ( flatIndexJ - soFar );
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
            int pos = 0;
            int[] ret = new int[this.Data.Length];
            var length = this.Indexes.Indexes.Length;
            for ( int i = 0; i < length; i++ )
            {
                int stop = this.Indexes.Indexes[i].Stop;
                for ( int j = this.Indexes.Indexes[i].Start; j <= stop; j++ )
                {
                    ret[pos++] = j;
                }
            }
            return ret;
        }

        public IEnumerable<int> ValidIndexes()
        {
            if ( Indexes.Indexes != null )
            {
                int length = this.Indexes.Indexes.Length;
                for ( int i = 0; i < length; i++ )
                {
                    int stop = this.Indexes.Indexes[i].Stop;
                    for ( int j = this.Indexes.Indexes[i].Start; j <= stop; j++ )
                    {
                        yield return j;
                    }
                }
            }
        }

        public IEnumerable<int> ValidIndexes(int first)
        {
            SparseSet oSet;
            var indexes = Indexes.Indexes;
            if ( indexes != null )
            {
                if ( this.TansformO( indexes, ref first, out oSet ) )
                {
                    int length = oSet.SubIndex.Indexes.Length;
                    for ( int i = 0; i < length; i++ )
                    {
                        int stop = oSet.SubIndex.Indexes[i].Stop;
                        for ( int j = oSet.SubIndex.Indexes[i].Start; j <= stop; j++ )
                        {
                            yield return j;
                        }
                    }
                }
            }
        }

        private static SparseIndexing ConvertToIndexes(List<SparseSet> processedIndexes, out T[][] Data, T[] data, SortStruct[] index)
        {
            SparseIndexing start = new SparseIndexing();
            var iLength = processedIndexes.Count;
            Data = new T[iLength][];
            start.Indexes = new SparseSet[iLength];
            int dataProcessed = 0;
            for ( int i = 0; i < iLength; i++ )
            {
                start.Indexes[i] = processedIndexes[i];
                var jLength = 0;
                var jSections = processedIndexes[i].SubIndex.Indexes.Length;
                for ( int jSection = 0; jSection < jSections; jSection++ )
                {
                    var indexStop = processedIndexes[i].SubIndex.Indexes[jSection].Stop;
                    var indexStart = processedIndexes[i].SubIndex.Indexes[jSection].Start;
                    start.Indexes[i].SubIndex.Indexes[jSection].Start = indexStart;
                    start.Indexes[i].SubIndex.Indexes[jSection].Stop = indexStop;
                    jLength += indexStop - indexStart + 1;
                }
                Data[i] = new T[jLength];
                for ( int j = 0; j < jLength; j++ )
                {
                    Data[i][j] = data[index[dataProcessed++].DataSpace];
                }
            }
            return start;
        }

        private static List<SparseSet> GenerateIndexes(SortStruct[] indexes)
        {
            List<SparseSet> meta = new List<SparseSet>();
            var length = indexes.Length;
            // Phase 1: Add in all of the sets we are going to see
            SparseSet currentSet = new SparseSet();
            currentSet.Start = currentSet.Stop = indexes[0].SparseSpaceSecond;
            List<SparseSet> subSets = new List<SparseSet>();
            for ( int i = 1; i < indexes.Length; i++ )
            {
                if ( indexes[i].SparseSpaceFirst == indexes[i - 1].SparseSpaceFirst )
                {
                    if ( indexes[i].SparseSpaceSecond > indexes[i - 1].SparseSpaceSecond + 1 )
                    {
                        subSets.Add( currentSet );
                        currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceSecond;
                    }
                    else
                    {
                        currentSet.Stop = indexes[i].SparseSpaceSecond;
                    }
                }
                else
                {
                    subSets.Add( currentSet );
                    meta.Add( new SparseSet()
                    {
                        Start = indexes[i - 1].SparseSpaceFirst,
                        Stop = indexes[i - 1].SparseSpaceFirst,
                        SubIndex = new SparseIndexing() { Indexes = subSets.ToArray() }
                    } );
                    subSets.Clear();
                    currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceSecond;
                }
            }
            subSets.Add( currentSet );
            meta.Add( new SparseSet()
            {
                Start = indexes[length - 1].SparseSpaceFirst,
                Stop = indexes[length - 1].SparseSpaceFirst,
                SubIndex = new SparseIndexing() { Indexes = subSets.ToArray() }
            } );
            return meta;
        }

        private void GenerateStructure()
        {
            int totalFirst = 0;
            bool malloc = ( this.Data == null );
            for ( int i = 0; i < Indexes.Indexes.Length; i++ )
            {
                Indexes.Indexes[i].BaseLocation = totalFirst;
                totalFirst += Indexes.Indexes[i].Stop - Indexes.Indexes[i].Start + 1;
            }
            if ( malloc )
            {
                this.Data = new T[totalFirst][];
            }
            int currentDataPlace = 0;
            // Now make the matrix's second rows
            for ( int i = 0; i < Indexes.Indexes.Length; i++ )
            {
                var length = Indexes.Indexes[i].SubIndex.Indexes.Length;
                int totalSecond = 0;
                // calculate the total
                for ( int j = 0; j < length; j++ )
                {
                    Indexes.Indexes[i].SubIndex.Indexes[j].BaseLocation = totalSecond;
                    totalSecond += Indexes.Indexes[i].SubIndex.Indexes[j].Stop - Indexes.Indexes[i].SubIndex.Indexes[j].Start + 1;
                }
                if ( malloc )
                {
                    // malloc the data
                    for ( int k = Indexes.Indexes[i].Start; k <= Indexes.Indexes[i].Stop; k++ )
                    {
                        this.Data[currentDataPlace++] = new T[totalSecond];
                    }
                }
            }
        }

        private bool GetTransformedIndex(ref int o)
        {
            var indexes = this.Indexes.Indexes;
            int min = 0;
            int max = indexes.Length - 1;
            while ( min <= max )
            {
                int mid = ( ( min + max ) >> 1 );
                var midIndex = indexes[mid];

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

        private const int LookUpLinearMax = 64;

        private bool GetTransformedIndexes(ref int o, ref int d)
        {
            SparseSet oSet;
            var indexes = this.Indexes.Indexes;
            if ( this.Indexes.Indexes == null ) return false;
            if ( TansformO( indexes, ref o, out oSet ) )
            {
                var subIndexes = oSet.SubIndex.Indexes;
                if ( subIndexes.Length >= LookUpLinearMax )
                {
                    int min = 0;
                    int max = subIndexes.Length - 1;
                    while ( min <= max )
                    {
                        int mid = ( ( min + max ) >> 1 );

                        if ( d < subIndexes[mid].Start )
                        {
                            max = mid - 1;
                        }
                        else if ( d > subIndexes[mid].Stop )
                        {
                            min = mid + 1;
                        }
                        else
                        {
                            // then we are in a valid range
                            d = ( d - subIndexes[mid].Start + subIndexes[mid].BaseLocation );
                            return true;
                        }
                    }
                }
                else
                {
                    for ( int i = 0; i < subIndexes.Length; i++ )
                    {
                        if ( ( subIndexes[i].Start <= d & subIndexes[i].Stop >= d ) )
                        {
                            // then we are in a valid range
                            d = ( d - subIndexes[i].Start + subIndexes[i].BaseLocation );
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
            if ( subIndexes.Length < LookUpLinearMax )
            {
                //otherwise just do a linear search\
                for ( int i = 0; i < subIndexes.Length; i++ )
                {
                    if ( ( subIndexes[i].Start <= o & subIndexes[i].Stop >= o ) )
                    {
                        o = ( o - subIndexes[i].Start + subIndexes[i].BaseLocation );
                        oSet = subIndexes[i];
                        return true;
                    }
                }
            }
            else
            {
                int min = 0;
                int max = subIndexes.Length - 1;
                while ( min <= max )
                {
                    int mid = ( ( min + max ) >> 1 );
                    if ( o < subIndexes[mid].Start )
                    {
                        max = mid - 1;
                    }
                    else if ( o > subIndexes[mid].Stop )
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        // then we are in a valid range
                        o = ( o - subIndexes[mid].Start + subIndexes[mid].BaseLocation );
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
                if ( x.SparseSpaceFirst < y.SparseSpaceFirst ) return -1;
                if ( x.SparseSpaceFirst == y.SparseSpaceFirst )
                {
                    if ( x.SparseSpaceSecond < y.SparseSpaceSecond )
                    {
                        return -1;
                    }
                    else if ( x.SparseSpaceSecond == y.SparseSpaceSecond )
                    {
                        return 0;
                    }
                }
                return 1;
            }
        }
    }
}