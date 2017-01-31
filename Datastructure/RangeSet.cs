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
using System.Text;
using static System.Char;
using static System.String;

namespace Datastructure
{
    public class RangeSet : IList<Range>
    {
        protected readonly Range[] SetRanges;

        public RangeSet(List<Range> tempRange)
        {
            SetRanges = tempRange.ToArray();
        }

        /// <summary>
        /// Creates a new RangeSet with inclusive values from the given integer set
        /// </summary>
        /// <param name="numbers">The numbers to use to generate the ranges</param>
        public RangeSet(IList<int> numbers)
        {
            if (numbers == null) throw new ArgumentNullException(nameof(numbers));
            var array = new int[numbers.Count];
            numbers.CopyTo(array, 0);
            Array.Sort(array);
            var tempRange = new List<Range>();
            var start = 0;
            for (var i = 1; i < array.Length; i++)
            {
                if (array[i] > array[i - 1] + 1)
                {
                    tempRange.Add(new Range(array[start], array[i - 1]));
                    start = i;
                }
            }
            // and in the end
            tempRange.Add(new Range(array[start], array[array.Length - 1]));
            SetRanges = tempRange.ToArray();
        }

        public int Count => SetRanges.Length;

        public bool IsReadOnly => false;

        public virtual Range this[int index]
        {
            get
            {
                return SetRanges[index];
            }

            set
            {
                SetRanges[index] = value;
            }
        }

        public static bool TryParse(string rangeString, out RangeSet output)
        {
            string error = null;
            return TryParse(ref error, rangeString, out output);
        }

        public static bool TryParse(ref string error, string rangeString, out RangeSet output)
        {
            var tempRange = new List<Range>();
            var length = rangeString.Length;
            var str = rangeString.ToCharArray();
            var index = 0;
            var start = 0;
            var end = 0;
            output = null;
            //Phase == 0 -> index
            //Phase == 1 -> start
            //Phase == 2 -> end
            var phase = 0;
            var lastPlus = false;
            var tallyingInZero = false;
            if (IsNullOrWhiteSpace(rangeString))
            {
                output = new RangeSet(tempRange);
                return true;
            }
            for (var i = 0; i < length; i++)
            {
                var c = str[i];
                if (IsWhiteSpace(c) || IsLetter(c)) continue;
                lastPlus = false;
                switch (phase)
                {
                    case 0:
                        if (IsNumber(c))
                        {
                            index = ((index << 3) + (index << 1)) + (c - '0');
                            tallyingInZero = true;
                        }
                        else switch (c)
                        {
                            case ',':
                                tempRange.Add(new Range(index, index));
                                index = 0;
                                start = 0;
                                end = 0;
                                break;
                            case '-':
                                if (!tallyingInZero)
                                {
                                    error = "No number was inserted before a range!";
                                    return false;
                                }
                                start = index;
                                end = 0;
                                phase = 2;
                                break;
                            case '+':
                                if (!tallyingInZero)
                                {
                                    error = "No number was inserted before a range!";
                                    return false;
                                }
                                end = int.MaxValue;
                                tempRange.Add(new Range(start, end));
                                index = 0;
                                start = 0;
                                phase = 0;
                                tallyingInZero = false;
                                lastPlus = true;
                                break;
                            default:
                                error = "Unrecognized symbol " + c;
                                return false;
                        }
                        break;

                    case 1:
                        if (IsNumber(c))
                        {
                            start = ((start << 3) + (start << 1)) + (c - '0');
                        }
                        else switch (c)
                        {
                            case '+':
                                end = int.MaxValue;
                                tempRange.Add(new Range(start, end));
                                index = 0;
                                start = 0;
                                phase = 0;
                                tallyingInZero = false;
                                lastPlus = true;
                                break;
                            case '-':
                                end = 0;
                                phase = 2;
                                break;
                        }
                        break;

                    case 2:
                        if (IsNumber(c))
                        {
                            end = ((end << 3) + (end << 1)) + (c - '0');
                        }
                        else if (c == ',')
                        {
                            tempRange.Add(new Range(start, end));
                            index = 0;
                            phase = 0;
                            start = 0;
                            end = 0;
                            tallyingInZero = false;
                        }
                        break;
                }
            }
            if (phase == 2)
            {
                tempRange.Add(new Range(start, end));
            }
            else if (phase == 0 && tallyingInZero)
            {
                tempRange.Add(new Range(start, end));
            }
            else if (!lastPlus)
            {
                error = "Ended while reading a " + (phase == 0 ? "range's index!" : "range's start value!");
                return false;
            }
            output = new RangeSet(tempRange);
            return true;
        }

        public void Add(Range item)
        {
            throw new InvalidOperationException("Unable to add items");
        }

        public void Clear()
        {
            throw new InvalidOperationException("Unable to remove items");
        }

        public bool Contains(Range item)
        {
            return IndexOf(item) != -1;
        }

        public bool Contains(int number)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                if ((number >= SetRanges[i].Start) && (number <= SetRanges[i].Stop))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(Range[] array, int arrayIndex)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                array[arrayIndex + i] = SetRanges[i];
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as RangeSet;
            if (other?.Count != Count) return false;
            for (var i = 0; i < SetRanges.Length; i++)
            {
                if (!(SetRanges[i] == other[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public IEnumerator<Range> GetEnumerator()
        {
            return ((ICollection<Range>)SetRanges).GetEnumerator();
        }

        public override int GetHashCode()
        {
            var hash = 0;
            for (var i = 0; i < SetRanges.Length; i++)
            {
                hash += SetRanges.GetHashCode();
            }
            return hash;
        }

        public int IndexOf(Range item)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                if (SetRanges[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gives the index in the range set where this integer is first contained.
        /// </summary>
        /// <param name="integerToFind">The integer to find</param>
        /// <returns>-1 if not found, otherwise the index of the Range in the rangeset that first contains this integer</returns>
        public int IndexOf(int integerToFind)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                if (SetRanges[i].ContainsInclusive(integerToFind))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, Range item)
        {
            this[index] = item;
        }

        public bool Overlaps(Range other)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                if (SetRanges[i].Contains(other.Start) || SetRanges[i].Contains(other.Stop))
                {
                    return true;
                }
            }
            return false;
        }

        public bool Overlaps(RangeSet other)
        {
            for (var i = 0; i < SetRanges.Length; i++)
            {
                for (var j = 0; j < other.SetRanges.Length; j++)
                {
                    if (SetRanges[i].Contains(other.SetRanges[j].Start) || SetRanges[i].Contains(other.SetRanges[j].Stop))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Remove(Range item)
        {
            throw new InvalidOperationException("Unable to remove items");
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException("Unable to remove items");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return SetRanges.GetEnumerator();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            var first = true;
            if (SetRanges.Length == 0)
            {
                // do nothing we already have a blank builder
            }
            else
            {
                foreach (var res in SetRanges)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }
                    if (res.Start != res.Stop)
                    {
                        builder.Append(res.Start);
                        builder.Append('-');
                        builder.Append(res.Stop);
                    }
                    else
                    {
                        builder.Append(res.Start);
                    }
                    first = false;
                }
            }
            return builder.ToString();
        }
    }
}