﻿/*
    Copyright 2015-2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace TMG.Functions;

public static partial class VectorHelper
{
    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[] destination, int destIndex, float[] lhs, int lhsIndex, float[] rhs, int rhsIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var zero = Vector512<float>.Zero;
            var one = Vector512<float>.One;
            if ((destIndex | lhsIndex | rhsIndex) == 0)
            {
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref lhs[i]);
                    var s = Vector512.LoadUnsafe(ref rhs[i]);
                    var local = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(f, s), one, zero);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = lhs[i] <= rhs[i] ? 1 : 0;
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref lhs[i + lhsIndex]);
                    var s = Vector512.LoadUnsafe(ref rhs[i + rhsIndex]);
                    var local = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(f, s), one, zero);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = lhs[i + lhsIndex] <= rhs[i + rhsIndex] ? 1 : 0;
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var zero = Vector<float>.Zero;
            var one = Vector<float>.One;
            if ((destIndex | lhsIndex | rhsIndex) == 0)
            {
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(lhs, i);
                    var s = new Vector<float>(rhs, i);
                    Vector.ConditionalSelect(Vector.LessThanOrEqual(f, s), one, zero).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = lhs[i] <= rhs[i] ? 1 : 0;
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    Vector.ConditionalSelect(Vector.LessThanOrEqual(new Vector<float>(lhs, i + lhsIndex), new Vector<float>(rhs, i + rhsIndex)), one, zero)
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = lhs[i + lhsIndex] <= rhs[i + rhsIndex] ? 1 : 0;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[i + destIndex] = lhs[i + lhsIndex] <= rhs[i + rhsIndex] ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[][] dest, float[][] data, float literalValue)
    {
        Parallel.For(0, dest.Length, i =>
        {
            FlagIfLessThanOrEqual(dest[i], data[i], literalValue);
        });
    }

    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[][] dest, float[][] lhs, float[][] rhs)
    {
        Parallel.For(0, dest.Length, i =>
        {
            FlagIfLessThanOrEqual(dest[i], 0, lhs[i], 0, rhs[i], 0, dest.Length);
        });
    }

    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[][] dest, float literalValue, float[][] data)
    {
        Parallel.For(0, dest.Length, i =>
        {
            FlagIfLessThanOrEqual(dest[i], literalValue, data[i]);
        });
    }
}
