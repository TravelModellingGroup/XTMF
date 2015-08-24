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

namespace Datastructure
{
    public static class FastParse
    {
        public static float ParseFixedFloat(string line, int offset, int length)
        {
            int start = offset + length;
            while(start > 0 && ((line[start - 1] >= '0' & line[start - 1] <= '9') | line[start - 1] == '.'))
            {
                start--;
            }
            return ParseFloat(line, start, offset + length);
        }

        public static int ParseFixedInt(string line, int offset, int length)
        {
            int start = offset + length;
            while(start > 0 && (line[start - 1] >= '0' & line[start - 1] <= '9'))
            {
                start--;
            }
            return ParseInt(line, start, offset + length);
        }

        /// <summary>
        /// Use this to parse an float out of a string
        /// </summary>
        /// <param name="str">The string to parse</param>
        /// <param name="indexFrom">Where to start(including)</param>
        /// <param name="indexTo">Where to stop (excluding)</param>
        /// <returns></returns>
        public static float ParseFloat(string str, int indexFrom, int indexTo)
        {
            int ival = 0;
            int dval = 0;
            int multiplyer = 0;
            int i;
            char c;
            unsafe
            {
                fixed (char* p = str)
                {
                    int noPoint = 1;
                    for(i = indexFrom; i < indexTo; i++)
                    {
                        if((c = p[i]) == '.')
                        {
                            noPoint = 0;
                            break;
                        }
                        // Same as multiplying by 10 and add our new value
                        ival = (ival << 1) + (ival << 3) + (c - '0');
                    }
                    multiplyer = indexTo - (++i) + noPoint;
                    for(; i < indexTo; i++)
                    {
                        dval = (dval << 1) + (dval << 3) + (p[i] - '0');
                    }
                }
            }
            // return ival + (float)(dval * Math.Pow(10.0, -multiplyer));
            if(DivLookup.Length > multiplyer)
            {
                return ival + (dval * DivLookup[multiplyer]);
            }
            else
            {
                return ival + (float)(dval * Math.Pow(10.0, -multiplyer));
            }
        }

        static float[] DivLookup = new float[] {1.0f, 0.1f, 0.01f, 0.001f, 0.0001f, 0.00001f, 0.000001f, 0.0000001f, 0.00000001f, 0.000000001f, 0.0000000001f,
        0.00000000001f, 0.000000000001f, 0.0000000000001f, 0.00000000000001f};

        /// <summary>
        /// Use this to parse an integer out of a string
        /// </summary>
        /// <param name="str">The string's beginning</param>
        /// <param name="indexFrom">Where to start reading from</param>
        /// <param name="indexTo">Where to stop reading</param>
        /// <returns>The integer value</returns>
        public static int ParseInt(string str, int indexFrom, int indexTo)
        {
            int value = 0;
            for(int i = indexFrom; i < indexTo; i++)
            {
                if(str[i] == ' ') continue;
                // Same as multiplying by 10
                value = (value << 1) + (value << 3);
                value += str[i] - '0';
            }
            return value;
        }
    }
}