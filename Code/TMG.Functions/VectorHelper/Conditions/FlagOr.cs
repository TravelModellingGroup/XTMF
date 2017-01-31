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
using System.Numerics;
using System.Threading.Tasks;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.Functions
{
    public static partial class VectorHelper
    {
        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[] dest, float value, float[] data)
        {
            // check if we are supposed to just copy everything and use a faster function for that
            if (value == 0.0f)
            {
                Array.Copy(dest, 0, data, 0, dest.Length);
            }
            else
            {
                // the vector implementation performed faster than the serial version by multiples
                int i = 0;
                var one = Vector<float>.One;
                for (; i <= dest.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    one.CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < dest.Length; i++)
                {
                    dest[i] = 1.0f;
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[] destination, int destIndex, float[] lhs, int lhsIndex, float[] rhs, int rhsIndex, int length)
        {
            if (Vector.IsHardwareAccelerated)
            {
                var one = Vector<float>.One;
                if ((destIndex | lhsIndex | rhsIndex) == 0)
                {
                    int i = 0;
                    for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        var f = new Vector<float>(lhs, i);
                        var s = new Vector<float>(rhs, i);
                        Vector.ConditionalSelect(Vector.Equals(f, one), one, s).CopyTo(destination, i);
                    }
                    // copy the remainder
                    for (; i < length; i++)
                    {
                        destination[i] = lhs[i] == 1.0f ? 1.0f : rhs[i];
                    }
                }
                else
                {
                    for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        Vector.ConditionalSelect(Vector.Equals(new Vector<float>(lhs, i + lhsIndex), one), one, new Vector<float>(rhs, i + rhsIndex))
                            .CopyTo(destination, i + destIndex);
                    }
                    // copy the remainder
                    for (int i = length - (length % Vector<float>.Count); i < length; i++)
                    {
                        destination[i + destIndex] = lhs[i + lhsIndex] == 1.0f ? 1.0f : rhs[i + rhsIndex];
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    destination[i + destIndex] = lhs[i + lhsIndex] == 1.0f ? 1.0f : rhs[i + rhsIndex];
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[][] dest, float[][] data, float literalValue)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FlagOr(dest[i], data[i], literalValue);
            });
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[] dest, float[] data, float literalValue)
        {
            FlagOr(dest, literalValue, data);
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[][] dest, float[][] lhs, float[][] rhs)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FlagOr(dest[i], 0, lhs[i], 0, rhs[i], 0, dest.Length);
            });
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagOr(float[][] v1, float literalValue, float[][] v2)
        {
            Parallel.For(0, v1.Length, i =>
            {
                FlagOr(v1[i], literalValue, v2[i]);
            });
        }
    }
}
