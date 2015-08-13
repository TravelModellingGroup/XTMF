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
    public sealed class SparseTriIndex<T>
    {
        private T[][][] Data;
        private SparseIndexing Indexes;

        public SparseTriIndex(SparseIndexing indexes, T[][][] data = null)
        {
            Indexes = indexes;
            if(data != null)
            {
                Data = data;
            }
            GenerateStructure();
        }

        /// <summary>
        /// Create the null space
        /// </summary>
        private SparseTriIndex()
        {
            Data = new T[0][][];
        }

        public T this[int o, int d, int t]
        {
            get
            {
                if(Data.Length > 0 && GetTransformedIndexes(ref o, ref d, ref t))
                {
                    return Data[o][d][t];
                }
                else
                {
                    // return null / whatever the closest thing to null is
                    return default(T);
                }
            }

            set
            {
                int originalO = o;
                int originalD = d;
                int originalT = t;
                if(Data.Length > 0 && GetTransformedIndexes(ref o, ref d, ref t))
                {
                    Data[o][d][t] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException(string.Format("The location {0}:{1}:{2} is invalid for this Sparse Tri Index Datastructure!", originalO, originalD, originalT));
                }
            }
        }

        public static SparseTriIndex<T> CreateSimilarArray<J, K, L>(SparseArray<J> first, SparseArray<K> second, SparseArray<L> third)
        {
            SparseIndexing indexes = new SparseIndexing();
            indexes.Indexes = first.Indexing.Indexes.Clone() as SparseSet[];
            var length = indexes.Indexes.Length;
            for(int i = 0; i < length; i++)
            {
                SparseSet[] subIndexes;
                indexes.Indexes[i].SubIndex = new SparseIndexing() { Indexes = (subIndexes = second.Indexing.Indexes.Clone() as SparseSet[]) };
                var subindexLength = indexes.Indexes[i].SubIndex.Indexes.Length;
                for(int j = 0; j < subindexLength; j++)
                {
                    subIndexes[j].SubIndex.Indexes = third.Indexing.Indexes.Clone() as SparseSet[];
                }
            }
            return new SparseTriIndex<T>(indexes);
        }

        public static SparseTriIndex<T> CreateSparseTriIndex(int[] first, int[] second, int[] third, T[] data)
        {
            T[][][] Data;
            var length = data.Length;
            if(length == 0)
            {
                // create a null tri indexed space
                return new SparseTriIndex<T>();
            }
            SortStruct[] indexes = new SortStruct[length];
            // Copy everything into a struct for sanity
            for(int i = 0; i < length; i++)
            {
                indexes[i].SparseSpaceFirst = first[i];
                indexes[i].SparseSpaceSecond = second[i];
                indexes[i].SparseSpaceThird = third[i];
                indexes[i].DataSpace = i;
            }
            Array.Sort(indexes, new CompareSortStruct());
            var processedIndexes = GenerateIndexes(indexes);

            return new SparseTriIndex<T>(ConvertToIndexes(processedIndexes, out Data, data, indexes), Data);
        }

        public bool ContainsIndex(int o, int d, int t)
        {
            return Data.Length > 0 && GetTransformedIndexes(ref o, ref d, ref t);
        }

        public SparseTriIndex<K> CreateSimilarArray<K>()
        {
            if(Data.Length == 0)
            {
                return new SparseTriIndex<K>();
            }
            return new SparseTriIndex<K>(Indexes);
        }

        public T[][][] GetFlatData()
        {
            return Data;
        }

        public int GetFlatIndex(int sparseSpaceIndex)
        {
            SparseSet unused;
            if(TansformO(Indexes.Indexes, ref sparseSpaceIndex, out unused))
            {
                // the now transformed sparse space index for O
                return sparseSpaceIndex;
            }
            return -1;
        }

        public int GetFlatIndex(int sparseSpaceIndexO, int sparseSpaceIndexD)
        {
            if(GetTransformedIndexes(ref sparseSpaceIndexO, ref sparseSpaceIndexD))
            {
                // the now transformed sparse space index for D
                return sparseSpaceIndexD;
            }
            return -1;
        }

        public bool GetFlatIndex(ref int sparseSpaceIndexO, ref int sparseSpaceIndexD)
        {
            if(GetTransformedIndexes(ref sparseSpaceIndexO, ref sparseSpaceIndexD))
            {
                // the now transformed sparse space index for D
                return true;
            }
            return false;
        }

        public int GetFlatIndex(int sparseSpaceIndexI, int sparseSpaceIndexJ, int sparseSpaceIndexK)
        {
            if(GetTransformedIndexes(ref sparseSpaceIndexI, ref sparseSpaceIndexJ, ref sparseSpaceIndexK))
            {
                // the now transformed sparse space index for D
                return sparseSpaceIndexK;
            }
            return -1;
        }

        public bool GetFlatIndex(ref int sparseSpaceIndexI, ref int sparseSpaceIndexJ, ref int sparseSpaceIndexK)
        {
            if(GetTransformedIndexes(ref sparseSpaceIndexI, ref sparseSpaceIndexJ, ref sparseSpaceIndexK))
            {
                // the now transformed sparse space index for D
                return true;
            }
            return false;
        }

        public IEnumerable<int> ValidIndexes()
        {
            if(Data.Length == 0) yield break;
            int length = Indexes.Indexes.Length;
            for(int i = 0; i < length; i++)
            {
                int stop = Indexes.Indexes[i].Stop;
                for(int j = Indexes.Indexes[i].Start; j <= stop; j++)
                {
                    yield return j;
                }
            }
        }

        public IEnumerable<int> ValidIndexes(int first)
        {
            SparseSet oSet;
            if(Data.Length == 0) yield break;
            if(TansformO(Indexes.Indexes, ref first, out oSet))
            {
                int length = oSet.SubIndex.Indexes.Length;
                for(int i = 0; i < length; i++)
                {
                    int stop = oSet.SubIndex.Indexes[i].Stop;
                    for(int j = oSet.SubIndex.Indexes[i].Start; j <= stop; j++)
                    {
                        yield return j;
                    }
                }
            }
        }

        public IEnumerable<int> ValidIndexes(int first, int second)
        {
            SparseSet oSet;
            SparseSet dSet;
            if(Data.Length == 0) yield break;
            if(TansformO(Indexes.Indexes, ref first, out oSet))
            {
                if(TansformD(oSet, ref second, out dSet))
                {
                    int length = dSet.SubIndex.Indexes.Length;
                    for(int i = 0; i < length; i++)
                    {
                        int stop = dSet.SubIndex.Indexes[i].Stop;
                        for(int j = dSet.SubIndex.Indexes[i].Start; j <= stop; j++)
                        {
                            yield return j;
                        }
                    }
                }
            }
        }

        private static SparseIndexing ConvertToIndexes(List<Pair<int, List<SparseSet>>> processedIndexes, out T[][][] Data, T[] data, SortStruct[] index)
        {
            SparseIndexing start = new SparseIndexing();
            var iLength = processedIndexes.Count;
            Data = new T[iLength][][];
            start.Indexes = new SparseSet[iLength];
            int dataProcessed = 0;
            for(int i = 0; i < iLength; i++)
            {
                var jLength = processedIndexes[i].Second.Count;
                Data[i] = new T[jLength][];
                start.Indexes[i].Start = start.Indexes[i].Stop = processedIndexes[i].First;
                start.Indexes[i].SubIndex.Indexes = new SparseSet[jLength];
                for(int j = 0; j < jLength; j++)
                {
                    var totalK = 0;
                    var currentJ = processedIndexes[i].Second[j];
                    start.Indexes[i].SubIndex.Indexes[j] = currentJ;
                    var kSections = currentJ.SubIndex.Indexes.Length;
                    for(int kSection = 0; kSection < kSections; kSection++)
                    {
                        totalK += (start.Indexes[i].SubIndex.Indexes[j].SubIndex.Indexes[kSection].Stop = processedIndexes[i].Second[j].SubIndex.Indexes[kSection].Stop)
                            - (start.Indexes[i].SubIndex.Indexes[j].SubIndex.Indexes[kSection].Start = processedIndexes[i].Second[j].SubIndex.Indexes[kSection].Start) + 1;
                    }
                    Data[i][j] = new T[totalK];
                    for(int k = 0; k < totalK; k++)
                    {
                        Data[i][j][k] = data[index[dataProcessed++].DataSpace];
                    }
                }
            }
            if(dataProcessed != data.Length)
            {
                throw new InvalidOperationException("The data processed should always be of the data's length!");
            }
            return start;
        }

        private static List<Pair<int, List<SparseSet>>> GenerateIndexes(SortStruct[] indexes)
        {
            List<Pair<int, List<SparseSet>>> meta = new List<Pair<int, List<SparseSet>>>();
            var length = indexes.Length;
            List<SparseSet> subSetsI = new List<SparseSet>();
            List<SparseSet> subSetsJ = new List<SparseSet>();
            SparseSet currentSet = new SparseSet();
            currentSet.Start = currentSet.Stop = indexes[0].SparseSpaceThird;
            for(int i = 1; i < length; i++)
            {
                if(indexes[i].SparseSpaceFirst == indexes[i - 1].SparseSpaceFirst)
                {
                    if(indexes[i].SparseSpaceSecond == indexes[i - 1].SparseSpaceSecond)
                    {
                        if(indexes[i].SparseSpaceThird > indexes[i - 1].SparseSpaceThird + 1)
                        {
                            subSetsJ.Add(currentSet);
                            currentSet.Stop = currentSet.Start = indexes[i].SparseSpaceThird;
                        }
                        else
                        {
                            if(currentSet.Stop == indexes[i].SparseSpaceThird)
                            {
                                throw new InvalidOperationException(string.Format("Sparse space was duplicated!\r\n{0}->{1}->{2} had more than one definition!", indexes[i].SparseSpaceFirst,
                                    indexes[i].SparseSpaceSecond, indexes[i].SparseSpaceThird));
                            }
                            currentSet.Stop = indexes[i].SparseSpaceThird;
                        }
                    }
                    else
                    {
                        subSetsJ.Add(currentSet);
                        subSetsI.Add(new SparseSet()
                        {
                            Start = indexes[i - 1].SparseSpaceSecond,
                            Stop = indexes[i - 1].SparseSpaceSecond,
                            SubIndex = new SparseIndexing() { Indexes = subSetsJ.ToArray() }
                        });
                        subSetsJ.Clear();
                        currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceThird;
                    }
                }
                else
                {
                    subSetsJ.Add(currentSet);
                    subSetsI.Add(new SparseSet()
                    {
                        Start = indexes[i - 1].SparseSpaceSecond,
                        Stop = indexes[i - 1].SparseSpaceSecond,
                        SubIndex = new SparseIndexing() { Indexes = subSetsJ.ToArray() }
                    });
                    subSetsJ.Clear();
                    meta.Add(new Pair<int, List<SparseSet>>(indexes[i - 1].SparseSpaceFirst, subSetsI));
                    subSetsI = new List<SparseSet>();
                    currentSet.Start = currentSet.Stop = indexes[i].SparseSpaceThird;
                }
            }
            subSetsJ.Add(currentSet);
            subSetsI.Add(new SparseSet()
            {
                Start = indexes[length - 1].SparseSpaceSecond,
                Stop = indexes[length - 1].SparseSpaceSecond,
                SubIndex = new SparseIndexing() { Indexes = subSetsJ.ToArray() }
            });
            subSetsJ.Clear();
            meta.Add(new Pair<int, List<SparseSet>>(indexes[length - 1].SparseSpaceFirst, subSetsI));
            return meta;
        }

        private void GenerateStructure()
        {
            bool malloc = (Data == null);
            ProcessFirst(malloc);
            ProcessSecond(malloc);
            ProcessThird(malloc);
        }

        private bool GetTransformedIndexes(ref int o, ref int d)
        {
            SparseSet oSet;
            if(Indexes.Indexes == null) return false;
            if(TansformO(Indexes.Indexes, ref o, out oSet))
            {
                SparseSet dSet;
                if(TansformD(oSet, ref d, out dSet))
                {
                    return true;
                }
            }
            return false;
        }

        private const int LookUpLinearMax = 64;

        private bool GetTransformedIndexes(ref int o, ref int d, ref int t)
        {
            SparseSet oSet;
            if(TansformO(Indexes.Indexes, ref o, out oSet))
            {
                SparseSet dSet;
                if(TansformD(oSet, ref d, out dSet))
                {
                    return TransformT(dSet, ref t);
                }
            }
            return false;
        }

        private bool ProcessFirst(bool malloc)
        {
            int totalFirst = 0;

            for(int i = 0; i < Indexes.Indexes.Length; i++)
            {
                Indexes.Indexes[i].BaseLocation = totalFirst;
                totalFirst += Indexes.Indexes[i].Stop - Indexes.Indexes[i].Start + 1;
            }
            if(malloc)
            {
                Data = new T[totalFirst][][];
            }
            return malloc;
        }

        private void ProcessSecond(bool malloc)
        {
            int currentDataPlace = 0;
            // Now make the matrix's second rows
            var iLength = Indexes.Indexes.Length;
            for(int iIndex = 0; iIndex < iLength; iIndex++)
            {
                var numberOfJIndexes = Indexes.Indexes[iIndex].SubIndex.Indexes.Length;
                int totalSecond = 0;
                // calculate the total
                for(int jIndex = 0; jIndex < numberOfJIndexes; jIndex++)
                {
                    Indexes.Indexes[iIndex].SubIndex.Indexes[jIndex].BaseLocation = totalSecond;
                    totalSecond += Indexes.Indexes[iIndex].SubIndex.Indexes[jIndex].Stop - Indexes.Indexes[iIndex].SubIndex.Indexes[jIndex].Start + 1;
                }
                if(malloc)
                {
                    // malloc the data
                    for(int k = Indexes.Indexes[iIndex].Start; k <= Indexes.Indexes[iIndex].Stop; k++)
                    {
                        Data[currentDataPlace++] = new T[totalSecond][];
                    }
                }
            }
        }

        private void ProcessThird(bool malloc)
        {
            var iLength = Indexes.Indexes.Length;
            for(int iIndex = 0; iIndex < iLength; iIndex++)
            {
                var iTotal = Indexes.Indexes[iIndex].Stop - Indexes.Indexes[iIndex].Start + 1;
                var jLength = Indexes.Indexes[iIndex].SubIndex.Indexes.Length;
                int currentDataPlace = 0;
                int jOffset = 0;
                for(int j = 0; j < jLength; j++)
                {
                    var kLength = Indexes.Indexes[iIndex].SubIndex.Indexes[j].SubIndex.Indexes.Length;
                    int totalThird = 0;
                    for(int k = 0; k < kLength; k++)
                    {
                        Indexes.Indexes[iIndex].SubIndex.Indexes[j].SubIndex.Indexes[k].BaseLocation = totalThird;
                        totalThird += Indexes.Indexes[iIndex].SubIndex.Indexes[j].SubIndex.Indexes[k].Stop - Indexes.Indexes[iIndex].SubIndex.Indexes[j].SubIndex.Indexes[k].Start + 1;
                    }
                    if(malloc)
                    {
                        // malloc the data
                        for(int i = 0; i < iTotal; i++)
                        {
                            currentDataPlace = 0;
                            for(int k = Indexes.Indexes[iIndex].SubIndex.Indexes[j].Start; k <= Indexes.Indexes[iIndex].SubIndex.Indexes[j].Stop; k++)
                            {
                                Data[i + Indexes.Indexes[iIndex].BaseLocation][jOffset + currentDataPlace++] = new T[totalThird];
                            }
                        }
                        jOffset += currentDataPlace;
                    }
                }
            }
        }

        private bool TansformD(SparseSet oSet, ref int d, out SparseSet dSet)
        {
            var oIndex = oSet.SubIndex;
            dSet = default(SparseSet);
            var subIndexes = oIndex.Indexes;
            if(subIndexes.Length >= LookUpLinearMax)
            {
                int min = 0;
                int max = oIndex.Indexes.Length - 1;
                while(min <= max)
                {
                    int mid = ((min + max) >> 1);

                    if(d < subIndexes[mid].Start)
                    {
                        max = mid - 1;
                    }
                    else if(d > subIndexes[mid].Stop)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        // then we are in a valid range
                        d = (d - subIndexes[mid].Start + subIndexes[mid].BaseLocation);
                        dSet = subIndexes[mid];
                        return true;
                    }
                }
            }
            else
            {
                for(int i = 0; i < subIndexes.Length; i++)
                {
                    if((subIndexes[i].Start <= d & subIndexes[i].Stop >= d))
                    {
                        // then we are in a valid range
                        d = (d - subIndexes[i].Start + subIndexes[i].BaseLocation);
                        dSet = subIndexes[i];
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TansformO(SparseSet[] subIndexes, ref int o, out SparseSet oSet)
        {
            // if it is large use binary search
            if(subIndexes.Length < LookUpLinearMax)
            {
                //otherwise just do a linear search\
                for(int i = 0; i < subIndexes.Length; i++)
                {
                    if((subIndexes[i].Start <= o & subIndexes[i].Stop >= o))
                    {
                        o = (o - subIndexes[i].Start + subIndexes[i].BaseLocation);
                        oSet = subIndexes[i];
                        return true;
                    }
                }
            }
            else
            {
                int min = 0;
                int max = subIndexes.Length - 1;
                while(min <= max)
                {
                    int mid = ((min + max) >> 1);
                    if(o < subIndexes[mid].Start)
                    {
                        max = mid - 1;
                    }
                    else if(o > subIndexes[mid].Stop)
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

        private bool TransformT(SparseSet dSet, ref int t)
        {
            var dIndex = dSet.SubIndex;
            var subIndexes = dIndex.Indexes;
            if(subIndexes.Length >= LookUpLinearMax)
            {
                int min = 0;
                int max = dIndex.Indexes.Length - 1;
                while(min <= max)
                {
                    int mid = ((min + max) >> 1);
                    if(t < subIndexes[mid].Start)
                    {
                        max = mid - 1;
                    }
                    else if(t > subIndexes[mid].Stop)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        // then we are in a valid range
                        t = (t - subIndexes[mid].Start + subIndexes[mid].BaseLocation);
                        return true;
                    }
                }
            }
            else
            {
                for(int i = 0; i < subIndexes.Length; i++)
                {
                    if((subIndexes[i].Start <= t & subIndexes[i].Stop >= t))
                    {
                        // then we are in a valid range
                        t = (t - subIndexes[i].Start + subIndexes[i].BaseLocation);
                        return true;
                    }
                }
            }
            return false;
        }

        private struct SortStruct
        {
            public int DataSpace;
            public int SparseSpaceFirst;
            public int SparseSpaceSecond;
            public int SparseSpaceThird;

            public override string ToString()
            {
                return "[" + DataSpace + "]" + SparseSpaceFirst + ":" + SparseSpaceSecond + ":" + SparseSpaceThird;
            }
        }

        private class CompareSortStruct : IComparer<SortStruct>
        {
            public int Compare(SortStruct x, SortStruct y)
            {
                if(x.SparseSpaceFirst < y.SparseSpaceFirst) return -1;
                if(x.SparseSpaceFirst == y.SparseSpaceFirst)
                {
                    if(x.SparseSpaceSecond < y.SparseSpaceSecond)
                    {
                        return -1;
                    }
                    else if(x.SparseSpaceSecond == y.SparseSpaceSecond)
                    {
                        if(x.SparseSpaceThird < y.SparseSpaceThird)
                        {
                            return -1;
                        }
                        else if(x.SparseSpaceThird == y.SparseSpaceThird)
                        {
                            return 0;
                        }
                    }
                }
                return 1;
            }
        }
    }
}