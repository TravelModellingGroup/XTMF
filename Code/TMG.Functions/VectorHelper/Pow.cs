/*
    Copyright 2015-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace TMG.Functions;

public static partial class VectorHelper
{
    public static void Pow(float[] flat, float[] lhs, float rhs)
    {
        nint i = 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(flat.Length, lhs.Length);
        ref var pFlat = ref flat[0];
        ref var pLhs = ref lhs[0];
        if (Vector512.IsHardwareAccelerated)
        {
            var vy = Vector512.Create(rhs);
            for (; i <= flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vx = Vector512.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref Unsafe.Add(ref pFlat, i));
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var vy = Vector256.Create(rhs);
            for (; i <= flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vx = Vector256.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref Unsafe.Add(ref pFlat, i));
            }
        }
        for (; i < flat.Length; i++)
        {
            flat[i] = MathF.Pow(lhs[i], rhs);
        }
    }

    public static void Pow(float[] flat, float lhs, float[] rhs)
    {
        nint i = 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(flat.Length, rhs.Length);
        ref var pFlat = ref flat[0];
        ref var pRhs = ref rhs[0];
        if (Vector512.IsHardwareAccelerated)
        {
            var vx = Vector512.Create(lhs);
            for (; i < flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vy = Vector512.LoadUnsafe(ref Unsafe.Add(ref pRhs, i));
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref Unsafe.Add(ref pFlat, i));
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var vx = Vector256.Create(lhs);
            for (; i < flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vy = Vector256.LoadUnsafe(ref Unsafe.Add(ref pRhs, i));
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref Unsafe.Add(ref pFlat, i));
            }
        }
        for (; i < flat.Length; i++)
        {
            flat[i] = MathF.Pow(lhs, rhs[i]);
        }
    }

    public static void Pow(float[] flat, float[] lhs, float[] rhs)
    {
        nint i = 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(flat.Length, lhs.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(flat.Length, rhs.Length);
        ArgumentOutOfRangeException.ThrowIfNotEqual(lhs.Length, rhs.Length);
        ref var pFlat = ref flat[0];
        ref var pLhs = ref lhs[0];
        ref var pRhs = ref rhs[0];
        if (Vector512.IsHardwareAccelerated)
        {

            for (; i <= flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vx = Vector512.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var vy = Vector512.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref flat[i]);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vx = Vector256.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var vy = Vector256.LoadUnsafe(ref Unsafe.Add(ref pLhs, i));
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref Unsafe.Add(ref pFlat, i));
            }
        }
        for (; i < flat.Length; i++)
        {
            flat[i] = MathF.Pow(lhs[i], rhs[i]);
        }
    }

    public static void Pow(float[][] flat, float[][] lhs, float[][] rhs)
    {
        Parallel.For(0, flat.Length, i =>
        {
            Pow(flat[i], lhs[i], rhs[i]);
        });
    }

    public static void Pow(float[][] flat, float[][] lhs, float rhs)
    {
        Parallel.For(0, flat.Length, i =>
        {
            Pow(flat[i], lhs[i], rhs);
        });
    }

    public static void Pow(float[][] flat, float lhs, float[][] rhs)
    {
        Parallel.For(0, flat.Length, i =>
        {
            Pow(flat[i], lhs, rhs[i]);
        });
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector512<float> Pow(Vector512<float> x, float y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector512.LessThanAny(x, Vector512<float>.Zero)
            && float.IsInteger(y))
        {
            int intY = (int)y;
            var result = Exp(y * Log(Vector512.Abs(x)));
            // if it is odd then we need to negate the result
            if (int.IsOddInteger(intY))
            {
                var negativeMask = Vector512.LessThan(x, Vector512<float>.Zero);
                result = Blend(result, Vector512.Negate(result), negativeMask);
            }
            return result;
        }
        return Exp(y * Log(x));
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector256<float> Pow(Vector256<float> x, float y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector256.LessThanAny(x, Vector256<float>.Zero)
            && float.IsInteger(y))
        {
            int intY = (int)y;
            var result = Exp(y * Log(Vector256.Abs(x)));
            // if it is odd then we need to negate the result
            if (int.IsOddInteger(intY))
            {
                var negativeMask = Vector256.LessThan(x, Vector256<float>.Zero);
                var abw = Vector256.Exp(result);
                result = Blend(result, Vector256.Negate(result), negativeMask);
            }
            return result;
        }
        return Exp(y * Log(x));
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector<float> Pow(Vector<float> x, float y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector.LessThanAny(x, Vector<float>.Zero)
            && float.IsInteger(y))
        {
            int intY = (int)y;
            var result = Exp(y * Log(Vector.Abs(x)));
            // if it is odd then we need to negate the result
            if (int.IsOddInteger(intY))
            {
                var negativeMask = Vector.LessThan(x, Vector<float>.Zero);
                result = Blend(result, Vector.Negate(result), negativeMask);
            }
            return result;
        }
        return Exp(y * Log(x));
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector512<float> Pow(Vector512<float> x, Vector512<float> y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector512.LessThanAny(x, Vector512<float>.Zero))
        {
            return SlowPow(x, y);
        }
        return Exp(y * Log(x));
    }

    private static Vector512<float> SlowPow(Vector512<float> x, Vector512<float> y)
    {
        var integerMask = Vector512.Equals(Vector512.Floor(y), y);
        // Only take the abs of integer exponents, everything else should be NaN
        var result = Exp(y * Log(Blend(x, Vector512.Abs(x), integerMask)));
        // if it is odd then we need to negate the result
        var iy = Vector512.ConvertToInt32Native(y);
        var isOdd = Vector512.Equals(Vector512.BitwiseAnd(iy, Vector512<int>.One), Vector512<int>.One)
            .As<int, float>();
        // Negative mask to only apply to odd exponents where the base is negative
        var negativeMask = Vector512.BitwiseAnd(
                Vector512.IsNegative(x),
                isOdd);
        result = Blend(result, Vector512.Negate(result), negativeMask);
        return result;
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector256<float> Pow(Vector256<float> x, Vector256<float> y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector256.LessThanAny(x, Vector256<float>.Zero))
        {
            return SlowPow(x, y);
        }
        return Exp(y * Log(x));
    }

    private static Vector256<float> SlowPow(Vector256<float> x, Vector256<float> y)
    {
        var integerMask = Vector256.Equals(Vector256.Floor(y), y);
        // Only take the abs of integer exponents, everything else should be NaN
        var result = Exp(y * Log(Blend(x, Vector256.Abs(x), integerMask)));
        var iy = Vector256.ConvertToInt32Native(y);
        var isOdd = Vector256.Equals(Vector256.BitwiseAnd(iy, Vector256<int>.One), Vector256<int>.One)
            .As<int, float>();
        // Negative mask to only apply to odd exponents where the base is negative
        var negativeMask = Vector256.BitwiseAnd(
                Vector256.IsNegative(x),
                isOdd);
        result = Blend(result, Vector256.Negate(result), negativeMask);
        return result;
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector<float> Pow(Vector<float> x, Vector<float> y)
    {
        // We need to handle cases where y is negative but is an integer
        // If it is not an integer, let it fail like normal
        if (Vector.LessThanAny(x, Vector<float>.Zero))
        {
            return SlowPow(x, y);
        }
        return Exp(y * Log(x));
    }

    private static Vector<float> SlowPow(Vector<float> x, Vector<float> y)
    {
        var integerMask = Vector.Equals(Vector.Floor(y), y);
        // Only take the abs of integer exponents, everything else should be NaN
        var result = Exp(y * Log(Blend(x, Vector.Abs(x), integerMask)));
        // if it is odd then we need to negate the result
        var iy = Vector.ConvertToInt32Native(y);
        var isOdd = Vector.Equals(Vector.BitwiseAnd(iy, Vector<int>.One), Vector<int>.One)
            .As<int, float>();
        // Negative mask to only apply to odd exponents where the base is negative
        var negativeMask = Vector.BitwiseAnd(
                Vector.IsNegative(x),
                isOdd);
        result = Blend(result, Vector.Negate(result), negativeMask);
        return result;
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector512<float> Pow(float x, Vector512<float> y)
    {
        var vx = Vector512.Create(x);
        return Exp(y * Log(vx));
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector256<float> Pow(float x, Vector256<float> y)
    {
        var vx = Vector256.Create(x);
        return Exp(y * Log(vx));
    }

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector<float> Pow(float x, Vector<float> y)
    {
        var vx = new Vector<float>(x);
        return Exp(y * Log(vx));
    }

}
