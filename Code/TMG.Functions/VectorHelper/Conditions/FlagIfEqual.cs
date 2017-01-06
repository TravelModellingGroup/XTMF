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
        /// <param name="dest"></param>
        /// <param name="value"></param>
        /// <param name="data"></param>
        public static void FlagIfEqual(float[] dest, float value, float[] data)
        {
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                if (dest.Length != data.Length)
                {
                    throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
                }
                Vector<float> zero = Vector<float>.Zero;
                Vector<float> one = Vector<float>.One;
                Vector<float> vValue = new Vector<float>(value);
                for (i = 0; i < data.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var vData = new Vector<float>(data, i);
                    Vector.ConditionalSelect(Vector.Equals(vData, vValue), one, zero).CopyTo(dest, i);
                }
                for (; i < data.Length; i++)
                {
                    dest[i] = data[i] == value ? 1 : 0;
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                {
                    dest[i] = data[i] == value ? 1 : 0;
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagIfEqual(float[] destination, int destIndex, float[] lhs, int lhsIndex, float[] rhs, int rhsIndex, int length)
        {
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> zero = Vector<float>.Zero;
                Vector<float> one = Vector<float>.One;
                if ((destIndex | lhsIndex | rhsIndex) == 0)
                {
                    int i = 0;
                    for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        var f = new Vector<float>(lhs, i);
                        var s = new Vector<float>(rhs, i);
                        Vector.ConditionalSelect(Vector.Equals(f, s), one, zero).CopyTo(destination, i);
                    }
                    // copy the remainder
                    for (; i < length; i++)
                    {
                        destination[i] = lhs[i] == rhs[i] ? 1 : 0;
                    }
                }
                else
                {
                    for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                    {
                        Vector.ConditionalSelect(Vector.Equals(new Vector<float>(lhs, i + lhsIndex), new Vector<float>(rhs, i + rhsIndex)), one, zero)
                            .CopyTo(destination, i + destIndex);
                    }
                    // copy the remainder
                    for (int i = length - (length % Vector<float>.Count); i < length; i++)
                    {
                        destination[i + destIndex] = lhs[i + lhsIndex] == rhs[i + rhsIndex] ? 1 : 0;
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    destination[i + destIndex] = lhs[i + lhsIndex] == rhs[i + rhsIndex] ? 1 : 0;
                }
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagIfEqual(float[][] dest, float[][] data, float literalValue)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                FlagIfEqual(dest[i], data[i], literalValue);
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagIfEqual(float[] dest, float[] data, float literalValue)
        {
            FlagIfEqual(dest, literalValue, data);
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagIfEqual(float[][] dest, float[][] lhs, float[][] rhs)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                FlagIfEqual(dest[i], 0, lhs[i], 0, rhs[i], 0, dest.Length);
            }
        }

        /// <summary>
        /// Set the value to one if the condition is met.
        /// </summary>
        public static void FlagIfEqual(float[][] dest, float literalValue, float[][] data)
        {
            for (int i = 0; i < dest.Length; i++)
            {
                FlagIfEqual(dest[i], literalValue, data[i]);
            }
        }
    }
}
