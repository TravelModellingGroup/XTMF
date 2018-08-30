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

namespace Datastructure
{
    public struct Range
    {
        public readonly int Start;
        public readonly int Stop;

        public Range(int start, int stop)
        {
            Start = start;
            Stop = stop;
        }

        public static bool operator !=(Range first, Range other)
        {
            return (first.Start != other.Start) | (first.Stop != other.Stop);
        }

        public static bool operator ==(Range first, Range other)
        {
            return (first.Start == other.Start) & (first.Stop == other.Stop);
        }

        public override bool Equals(object obj)
        {
            if (obj is Range other)
            {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() * Stop.GetHashCode();
        }

        public static SparseArray<Range> Parse(string rangeString)
        {
            var tempRange = new List<Pair<int, Range>>();
            LoadRanges(tempRange, rangeString);
            return SaveToArray(tempRange);
        }

        /// <summary>
        /// Checks if a given value is inside the range defined by [Start, Stop)
        /// </summary>
        /// <param name="i"> The int value to check.</param>
        /// <returns>True IFF i is greater than or equal to Start and i is less than Stop.</returns>
        public bool Contains(int i)
        {
            return (i >= Start) & (i < Stop);
        }

        /// <summary>
        /// Checks if a given value is inside the range defined by (Start, Stop)
        /// </summary>
        /// <param name="i">The int value to check</param>
        /// <returns>True IFF i is less than Start and i is less than Stop.</returns>
        public bool ContainsExcusive(int i)
        {
            return (i > Start) & (i < Stop);
        }

        /// <summary>
        /// Checks if a given value is inside the range defined by (Start, Stop)
        /// </summary>
        /// <param name="valueToFind">The value to check for</param>
        /// <returns>True if the value is contained within the range</returns>
        public bool ContainsExcusive(float valueToFind)
        {
            return (valueToFind > Start) & (valueToFind < Stop);
        }

        /// <summary>
        /// Checks if a given value is inside the range defined by [Start, Stop]
        /// </summary>
        /// <param name="i">The int value to check</param>
        /// <returns>True IFF i is greater than or equal to Start and i is less than or equal to Stop.</returns>
        public bool ContainsInclusive(int i)
        {
            return (i >= Start) & (i <= Stop);
        }

        public IEnumerable<int> EnumerateInclusive()
        {
            for (int i = Start; i <= Stop; i++)
            {
                yield return i;
            }
        }

        public IEnumerable<int> EnumerateExclusive()
        {
            for (int i = Start; i < Stop; i++)
            {
                yield return i;
            }
        }

        /// <summary>
        /// Checks if a given value is inside the range defined by [Start, Stop]
        /// </summary>
        /// <param name="valueToFind">The value to check for</param>
        /// <returns>True if the value is contained within the range</returns>
        public bool ContainsInclusive(float valueToFind)
        {
            return (valueToFind >= Start) & (valueToFind <= Stop);
        }

        /// <summary>
        /// Checks if another Range overlaps this one.
        /// </summary>
        /// <param name="other">The other range to check against.</param>
        /// <returns></returns>
        public bool Overlaps(Range other)
        {
            return ContainsInclusive(other.Start) || ContainsInclusive(other.Stop);
        }

        public override string ToString()
        {
            return String.Format("{0}-{1}", Start, Stop);
        }

        private static void LoadRanges(List<Pair<int, Range>> tempRange, string rangeString)
        {
            var length = rangeString.Length;
            var str = rangeString.ToCharArray();
            var index = 0;
            var start = 0;
            var end = 0;
            //Phase == 0 -> index
            //Phase == 1 -> start
            //Phase == 2 -> end
            var phase = 0;
            var lastPlus = false;
            for (var i = 0; i < length; i++)
            {
                var c = str[i];
                if (Char.IsWhiteSpace(c) || Char.IsLetter(c)) continue;
                lastPlus = false;
                switch (phase)
                {
                    case 0:
                        if (Char.IsNumber(c))
                        {
                            index = (index << 3) + (index << 1) + (c - '0');
                        }
                        else if (c == ':')
                        {
                            start = 0;
                            phase = 1;
                        }
                        break;

                    case 1:
                        if (Char.IsNumber(c))
                        {
                            start = (start << 3) + (start << 1) + (c - '0');
                        }
                        else if (c == '+')
                        {
                            end = int.MaxValue;
                            tempRange.Add(new Pair<int, Range>(index, new Range(start, end)));
                            index = 0;
                            start = 0;
                            phase = 0;
                            lastPlus = true;
                        }
                        else if (c == '-')
                        {
                            end = 0;
                            phase = 2;
                        }
                        break;

                    case 2:
                        if (Char.IsNumber(c))
                        {
                            end = (end << 3) + (end << 1) + (c - '0');
                        }
                        else if (c == ',')
                        {
                            tempRange.Add(new Pair<int, Range>(index, new Range(start, end)));
                            index = 0;
                            phase = 0;
                        }
                        break;
                }
            }
            if (phase == 2)
            {
                tempRange.Add(new Pair<int, Range>(index, new Range(start, end)));
            }
            else if (!lastPlus)
            {
                throw new ArgumentException("The range string was incomplete we ended while reading " + (phase == 0 ? "range's index!" : "range's start value!"));
            }
        }

        private static SparseArray<Range> SaveToArray(List<Pair<int, Range>> tempRange)
        {
            var found = tempRange.Count;
            var place = new int[found];
            var ranges = new Range[found];
            for (var i = 0; i < found; i++)
            {
                var pair = tempRange[i];
                place[i] = pair.First;
                ranges[i] = pair.Second;
            }
            for (var i = 0; i < found; i++)
            {
                for (var j = i + 1; j < found; j++)
                {
                    if (place[i] == place[j])
                    {
                        throw new ArgumentException("There were multiple ranges with the index " + place[i] + "!");
                    }
                }
            }
            return SparseArray<Range>.CreateSparseArray(place, ranges);
        }
    }
}