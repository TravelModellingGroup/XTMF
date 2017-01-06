/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TMG.Functions
{
    public static partial class VectorHelper
    {
        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[] dest, float value, float[] data)
        {
            // check if we are supposed to just clear everything and use a faster function for that
            if (value == 0.0f)
            {
                Array.Clear(dest, 0, dest.Length);
            }
            else
            {
                if (Vector.IsHardwareAccelerated)
                {
                    int i;
                    if (dest.Length != data.Length)
                    {
                        throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
                    }
                    Vector<float> zero = Vector<float>.Zero;
                    Vector<float> vValue = new Vector<float>(value);
                    for (i = 0; i < data.Length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        var vData = new Vector<float>(data, i);
                        Vector.ConditionalSelect(Vector.Equals(vData, zero), zero, vValue).CopyTo(dest, i);
                    }
                    for (; i < data.Length; i++)
                    {
                        dest[i] = data[i] == 0 ? 0.0f : value;
                    }
                }
                else
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        dest[i] = data[i] == 0 ? 0.0f : value;
                    }
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[] destination, int destIndex, float[] lhs, int lhsIndex, float[] rhs, int rhsIndex, int length)
        {
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> zero = Vector<float>.Zero;
                if ((destIndex | lhsIndex | rhsIndex) == 0)
                {
                    int i = 0;
                    for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        var f = new Vector<float>(lhs, i);
                        var s = new Vector<float>(rhs, i);
                        Vector.ConditionalSelect(Vector.Equals(f, zero), zero, s).CopyTo(destination, i);
                    }
                    // copy the remainder
                    for (; i < length; i++)
                    {
                        destination[i] = lhs[i] == 0.0f ? 0.0f : rhs[i];
                    }
                }
                else
                {
                    for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        Vector.ConditionalSelect(Vector.Equals(new Vector<float>(lhs, i + lhsIndex), zero), zero, new Vector<float>(rhs, i + rhsIndex))
                            .CopyTo(destination, i + destIndex);
                    }
                    // copy the remainder
                    for (int i = length - (length % Vector<float>.Count); i < length; i++)
                    {
                        destination[i + destIndex] = lhs[i + lhsIndex] == 0.0f ? 0.0f : rhs[i + rhsIndex];
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    destination[i + destIndex] = lhs[i + lhsIndex] == 0.0f ? 0.0f : rhs[i + rhsIndex];
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[][] dest, float[][] data, float literalValue)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                FlagAnd(dest[i], data[i], literalValue);
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[] dest, float[] data, float literalValue)
        {
            FlagAnd(dest, literalValue, data);
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[][] dest, float[][] lhs, float[][] rhs)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                FlagAnd(dest[i], 0, lhs[i], 0, rhs[i], 0, dest.Length);
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagAnd(float[][] v1, float literalValue, float[][] v2)
        {
            for (int i = 0; i < v1.Length; i++)
            {
                FlagAnd(v1[i], literalValue, v2[i]);
            }
        }
    }
}
