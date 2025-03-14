
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
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
using System.Numerics;
using System;

namespace TMG.Functions;

public static partial class VectorHelper
{
    /// <summary>
    /// Provides a 512-bit accelerated implementation of Log based on MathIsFun
    /// Based on MathIsFun https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector512<float> Log(Vector512<float> x)
    {
        return Vector512.Log(x);
    }

    /// <summary>
    /// Provides a 256-bit accelerated implementation of Log based on MathIsFun
    /// Based on MathIsFun https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    static Vector256<float> Log(Vector256<float> x)
    {
        return Vector256.Log(x);
    }


    /// <summary>
    /// Provides a 256-bit accelerated implementation of Log based on MathIsFun
    /// Based on MathIsFun https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    static Vector<float> Log(Vector<float> x)
    {
        return Vector.Log(x);
    }

    /// <summary>
    /// Provides a 512-bit with a 256-bit fallback accelerated implementation of Log based on MathIsFun
    /// Based on MathIsFun https://github.com/reyoung/avx_mathfun/blob/master/avx_mathfun.h
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    static void Log(float[] destination, float[] source)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, source.Length);
        nint i = 0;
        ref var pSrc = ref source[0];
        ref var pDest = ref destination[0];
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= destination.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var temp = Vector512.LoadUnsafe(ref Unsafe.Add(ref pSrc, i));
                temp = Log(temp);
                Vector512.StoreUnsafe(temp, ref Unsafe.Add(ref pDest, i));
            }
        }
        // Fall back to 256 bit instructions
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= destination.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var temp = Vector256.LoadUnsafe(ref Unsafe.Add(ref pSrc, i));
                temp = Log(temp);
                Vector256.StoreUnsafe(temp, ref Unsafe.Add(ref pDest, i));
            }
        }
        // Fallback to basic for everything not accelerated
        for (; i < destination.Length; i++)
        {
            destination[i] = MathF.Log(source[i]);
        }
    }

    /// <summary>
    /// Compute the log from all of source into destination.
    /// The implementation is based on MathIsFun.
    /// </summary>
    /// <param name="destination">The place to store the results.</param>
    /// <param name="source">The values to read from.</param>
    public static void Log(float[][] destination, float[][] source)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            Log(destination[i], source[i]);
        }
    }
}
