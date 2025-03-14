/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Reflection;

namespace TMG.Functions;

public static partial class VectorHelper
{
    /// <summary>
    /// Converts all of the values in src to their expatiated versions.
    /// Based on "MathIsFun" http://gruntthepeon.free.fr/ssemath/
    /// and https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="src"></param>
    public static unsafe void Exp(float[] destination, float[] src)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, src.Length);
        nint i = 0;
        ref var pSrc = ref src[0];
        ref var pDest = ref destination[0];
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= destination.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var temp = Vector512.LoadUnsafe(ref Unsafe.Add(ref pSrc, i));
                temp = Exp(temp);
                Vector512.StoreUnsafe(temp, ref Unsafe.Add(ref pDest, i));
            }
        }
        // Fall back to 256 bit instructions
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= destination.Length - (nint)Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var temp = Vector256.LoadUnsafe(ref Unsafe.Add(ref pSrc, i));
                temp = Exp(temp);
                Vector256.StoreUnsafe(temp, ref Unsafe.Add(ref pDest, i));
            }
        }
        // Fallback to basic for everything not accelerated
        for (; i < destination.Length; i++)
        {
            destination[i] = MathF.Exp(src[i]);
        }
    }

    /// <summary>
    /// Converts the given vectors elements to exp(x).
    /// Based on "MathIsFun" http://gruntthepeon.free.fr/ssemath/
    /// and https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x">The values to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector512<float> Exp(Vector512<float> x)
    {
        return Vector512.Exp(x);
    }

    /// <summary>
    /// Converts the given vectors elements to exp(x).
    /// Based on "MathIsFun" http://gruntthepeon.free.fr/ssemath/
    /// and https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x">The values to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector256<float> Exp(Vector256<float> x)
    {
        return Vector256.Exp(x);
    }


    /// <summary>
    /// Converts the given vectors elements to exp(x).
    /// Based on "MathIsFun" http://gruntthepeon.free.fr/ssemath/
    /// and https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x">The values to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector<float> Exp(Vector<float> x)
    {
        return Vector.Exp(x);
    }

    /// <summary>
    /// Applies exp(x) for each element in the array
    /// </summary>
    /// <param name="destination">Where to save the results.</param>
    /// <param name="destIndex">An offset into the array to start saving.</param>
    /// <param name="x">The vector to use as the exponent.</param>
    /// <param name="xIndex">The offset into the exponent vector to start from.</param>
    /// <param name="length">The number of elements to convert.</param>
    /// <remarks>The series is unrolled 30 times which approximates the .Net implementation from System.Math.Exp</remarks>
    public static void Exp(float[] destination, int destIndex, float[] x, int xIndex, int length)
    {
        int i = 0;
        // If this is going to copy everything
        if (destination.Length == length && x.Length == length && xIndex == 0 && destIndex == 0)
        {
            Exp(destination, x);
            return;
        }
        // Check to see if we have 512 bit instructions
        else if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var temp = Vector512.LoadUnsafe(ref x[i]);
                temp = Exp(temp);
                Vector512.StoreUnsafe(temp, ref x[i]);
            }
        }
        // Fall back to 256 bit instructions
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var temp = Vector256.LoadUnsafe(ref x[i]);
                temp = Exp(temp);
                Vector256.StoreUnsafe(temp, ref x[i]);
            }
        }
        // Fallback to basic for everything not accelerated
        for (; i < length; i++)
        {
            destination[i + destIndex] = MathF.Exp(x[i + xIndex]);
        }
    }

    /// <summary>
    /// Applies exp(x) for each element in the array
    /// </summary>
    /// <param name="destination">Where to save the results.</param>
    /// <param name="x">The vector to use as the exponent.</param>
    public static void Exp(float[][] destination, float[][] x)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            Exp(destination[i], x[i]);
        }
    }

}
