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
        int i = 0;
        if (Avx512F.IsSupported)
        {
            Vector512<float> c_one = Vector512<float>.One;
            Vector512<float> half = Vector512.Create(0.5f);
            Vector512<float> exp_hi = Vector512.Create(88.3762626647949f);
            Vector512<float> exp_lo = Vector512.Create(-88.3762626647949f);
            Vector512<float> LOG2EF = Vector512.Create(1.44269504088896341f);
            Vector512<float> exp_C1 = Vector512.Create(0.693359375f);
            Vector512<float> exp_C2 = Vector512.Create(-2.12194440e-4f);
            Vector512<float> exp_p0 = Vector512.Create(1.9875691500E-4f);
            Vector512<float> exp_p1 = Vector512.Create(1.3981999507E-3f);
            Vector512<float> exp_p2 = Vector512.Create(8.3334519073E-3f);
            Vector512<float> exp_p3 = Vector512.Create(4.1665795894E-2f);
            Vector512<float> exp_p4 = Vector512.Create(1.6666665459E-1f);
            Vector512<float> exp_p5 = Vector512.Create(5.0000001201E-1f);
            Vector512<int> c0x7f = Vector512.Create(0x7F);
            for (; i < destination.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                Vector512<float> x = Vector512.LoadUnsafe(ref src[i]);
                x = Vector512.Min(x, exp_hi);
                x = Vector512.Max(x, exp_lo);
                Vector512<float> fx = Avx512F.FusedMultiplyAdd(x, LOG2EF, half);
                var tmp = Vector512.Floor(fx);
                Vector512<float> mask = Vector512.GreaterThan(tmp, fx);
                mask = Vector512.BitwiseAnd(tmp, mask);
                fx = tmp - mask;
                tmp = fx * exp_C1;
                var z = (fx * exp_C2);
                x = x - tmp - z;
                z = x * x;
                var y = exp_p0;
                y = Avx512F.FusedMultiplyAdd(y, x, exp_p1);
                y = Avx512F.FusedMultiplyAdd(y, x, exp_p2);
                y = Avx512F.FusedMultiplyAdd(y, x, exp_p3);
                y = Avx512F.FusedMultiplyAdd(y, x, exp_p4);
                y = Avx512F.FusedMultiplyAdd(y, x, exp_p5);
                y = Avx512F.FusedMultiplyAdd(y, z, x);
                y += c_one;
                var imm0 = Vector512.ConvertToInt32(fx);
                imm0 = Vector512.ShiftLeft(imm0 + c0x7f, 23);
                y = imm0.AsSingle() * y;
                Vector512.StoreUnsafe(y, ref destination[i]);
            }
        }
        else if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> c_one = Vector512<float>.One;
            Vector512<float> half = Vector512.Create(0.5f);
            Vector512<float> exp_hi = Vector512.Create(88.3762626647949f);
            Vector512<float> exp_lo = Vector512.Create(-88.3762626647949f);
            Vector512<float> LOG2EF = Vector512.Create(1.44269504088896341f);
            Vector512<float> exp_C1 = Vector512.Create(0.693359375f);
            Vector512<float> exp_C2 = Vector512.Create(-2.12194440e-4f);
            Vector512<float> exp_p0 = Vector512.Create(1.9875691500E-4f);
            Vector512<float> exp_p1 = Vector512.Create(1.3981999507E-3f);
            Vector512<float> exp_p2 = Vector512.Create(8.3334519073E-3f);
            Vector512<float> exp_p3 = Vector512.Create(4.1665795894E-2f);
            Vector512<float> exp_p4 = Vector512.Create(1.6666665459E-1f);
            Vector512<float> exp_p5 = Vector512.Create(5.0000001201E-1f);
            Vector512<int> c0x7f = Vector512.Create(0x7F);
            for (; i < destination.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                Vector512<float> x = Vector512.LoadUnsafe(ref src[i]);
                x = Vector512.Min(x, exp_hi);
                x = Vector512.Max(x, exp_lo);
                Vector512<float> fx = Avx512F.FusedMultiplyAdd(x, LOG2EF, half);
                var tmp = Vector512.Floor(fx);
                Vector512<float> mask = Vector512.GreaterThan(tmp, fx);
                mask = Vector512.BitwiseAnd(tmp, mask);
                fx = tmp - mask;
                tmp = fx * exp_C1;
                var z = (fx * exp_C2);
                x = x - tmp - z;
                z = x * x;
                var y = exp_p0;
                y = y * x + exp_p1;
                y = y * x + exp_p2;
                y = y * x + exp_p3;
                y = y * x + exp_p4;
                y = y * x + exp_p5;
                y = y * z + x;
                y += c_one;
                var imm0 = Vector512.ConvertToInt32(fx);
                imm0 = Vector512.ShiftLeft(imm0 + c0x7f, 23);
                y = imm0.AsSingle() * y;
                Vector512.StoreUnsafe(y, ref destination[i]);
            }
        }
        else if (Fma.IsSupported)
        {
            Vector256<float> c_one = Vector256<float>.One;
            Vector256<float> half = Vector256.Create(0.5f);
            Vector256<float> exp_hi = Vector256.Create(88.3762626647949f);
            Vector256<float> exp_lo = Vector256.Create(-88.3762626647949f);
            Vector256<float> LOG2EF = Vector256.Create(1.44269504088896341f);
            Vector256<float> exp_C1 = Vector256.Create(0.693359375f);
            Vector256<float> exp_C2 = Vector256.Create(-2.12194440e-4f);
            Vector256<float> exp_p0 = Vector256.Create(1.9875691500E-4f);
            Vector256<float> exp_p1 = Vector256.Create(1.3981999507E-3f);
            Vector256<float> exp_p2 = Vector256.Create(8.3334519073E-3f);
            Vector256<float> exp_p3 = Vector256.Create(4.1665795894E-2f);
            Vector256<float> exp_p4 = Vector256.Create(1.6666665459E-1f);
            Vector256<float> exp_p5 = Vector256.Create(5.0000001201E-1f);
            Vector256<int> c0x7f = Vector256.Create(0x7F);
            for (; i < destination.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> x = Vector256.LoadUnsafe(ref src[i]);
                x = Vector256.Min(x, exp_hi);
                x = Vector256.Max(x, exp_lo);
                Vector256<float> fx = Fma.MultiplyAdd(x, LOG2EF, half);
                var tmp = Vector256.Floor(fx);
                Vector256<float> mask = Vector256.GreaterThan(tmp, fx);
                mask = Vector256.BitwiseAnd(tmp, mask);
                fx = tmp - mask;
                tmp = fx * exp_C1;
                var z = (fx * exp_C2);
                x = x - tmp - z;
                z = x * x;
                var y = exp_p0;
                y = Fma.MultiplyAdd(y, x, exp_p1);
                y = Fma.MultiplyAdd(y, x, exp_p2);
                y = Fma.MultiplyAdd(y, x, exp_p3);
                y = Fma.MultiplyAdd(y, x, exp_p4);
                y = Fma.MultiplyAdd(y, x, exp_p5);
                y = Fma.MultiplyAdd(y, z, x);
                y += c_one;
                var imm0 = Vector256.ConvertToInt32(fx);
                imm0 = Vector256.ShiftLeft(imm0 + c0x7f, 23);
                y = imm0.AsSingle() * y;
                Vector256.StoreUnsafe(y, ref destination[i]);
            }
        }
        else
        {
            Vector256<float> c_one = Vector256<float>.One;
            Vector256<float> half = Vector256.Create(0.5f);
            Vector256<float> exp_hi = Vector256.Create(88.3762626647949f);
            Vector256<float> exp_lo = Vector256.Create(-88.3762626647949f);
            Vector256<float> LOG2EF = Vector256.Create(1.44269504088896341f);
            Vector256<float> exp_C1 = Vector256.Create(0.693359375f);
            Vector256<float> exp_C2 = Vector256.Create(-2.12194440e-4f);
            Vector256<float> exp_p0 = Vector256.Create(1.9875691500E-4f);
            Vector256<float> exp_p1 = Vector256.Create(1.3981999507E-3f);
            Vector256<float> exp_p2 = Vector256.Create(8.3334519073E-3f);
            Vector256<float> exp_p3 = Vector256.Create(4.1665795894E-2f);
            Vector256<float> exp_p4 = Vector256.Create(1.6666665459E-1f);
            Vector256<float> exp_p5 = Vector256.Create(5.0000001201E-1f);
            Vector256<int> c0x7f = Vector256.Create(0x7F);
            for (; i < destination.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> x = Vector256.LoadUnsafe(ref src[i]);
                x = Vector256.Min(x, exp_hi);
                x = Vector256.Max(x, exp_lo);
                Vector256<float> fx = Fma.MultiplyAdd(x, LOG2EF, half);
                var tmp = Vector256.Floor(fx);
                Vector256<float> mask = Vector256.GreaterThan(tmp, fx);
                mask = Vector256.BitwiseAnd(tmp, mask);
                fx = tmp - mask;
                tmp = fx * exp_C1;
                var z = (fx * exp_C2);
                x = x - tmp - z;
                z = x * x;
                var y = exp_p0;
                y = y * x + exp_p1;
                y = y * x + exp_p2;
                y = y * x + exp_p3;
                y = y * x + exp_p4;
                y = y * x + exp_p5;
                y = y * z + x;
                y += c_one;
                var imm0 = Vector256.ConvertToInt32(fx);
                imm0 = Vector256.ShiftLeft(imm0 + c0x7f, 23);
                y = imm0.AsSingle() * y;
                Vector256.StoreUnsafe(y, ref destination[i]);
            }
        }
        // Cleanup the remainder with the built-in algorithm
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
        if (Avx512F.IsSupported)
        {
            Vector512<float> c_one = Vector512<float>.One;
            Vector512<float> half = Vector512.Create(0.5f);
            Vector512<float> exp_hi = Vector512.Create(88.3762626647949f);
            Vector512<float> exp_lo = Vector512.Create(-88.3762626647949f);
            Vector512<float> LOG2EF = Vector512.Create(1.44269504088896341f);
            Vector512<float> exp_C1 = Vector512.Create(0.693359375f);
            Vector512<float> exp_C2 = Vector512.Create(-2.12194440e-4f);
            Vector512<float> exp_p0 = Vector512.Create(1.9875691500E-4f);
            Vector512<float> exp_p1 = Vector512.Create(1.3981999507E-3f);
            Vector512<float> exp_p2 = Vector512.Create(8.3334519073E-3f);
            Vector512<float> exp_p3 = Vector512.Create(4.1665795894E-2f);
            Vector512<float> exp_p4 = Vector512.Create(1.6666665459E-1f);
            Vector512<float> exp_p5 = Vector512.Create(5.0000001201E-1f);
            Vector512<int> c0x7f = Vector512.Create(0x7F);
            x = Vector512.Min(x, exp_hi);
            x = Vector512.Max(x, exp_lo);
            Vector512<float> fx = Avx512F.FusedMultiplyAdd(x, LOG2EF, half);
            var tmp = Vector512.Floor(fx);
            Vector512<float> mask = Vector512.GreaterThan(tmp, fx);
            mask = Vector512.BitwiseAnd(tmp, mask);
            fx = tmp - mask;
            tmp = fx * exp_C1;
            var z = (fx * exp_C2);
            x = x - tmp - z;
            z = x * x;
            var y = exp_p0;
            y = Avx512F.FusedMultiplyAdd(y, x, exp_p1);
            y = Avx512F.FusedMultiplyAdd(y, x, exp_p2);
            y = Avx512F.FusedMultiplyAdd(y, x, exp_p3);
            y = Avx512F.FusedMultiplyAdd(y, x, exp_p4);
            y = Avx512F.FusedMultiplyAdd(y, x, exp_p5);
            y = Avx512F.FusedMultiplyAdd(y, z, x);
            y += c_one;
            var imm0 = Vector512.ConvertToInt32(fx);
            imm0 = Vector512.ShiftLeft(imm0 + c0x7f, 23);
            y = imm0.AsSingle() * y;
            return y;
        }
        else
        {
            Vector512<float> c_one = Vector512<float>.One;
            Vector512<float> half = Vector512.Create(0.5f);
            Vector512<float> exp_hi = Vector512.Create(88.3762626647949f);
            Vector512<float> exp_lo = Vector512.Create(-88.3762626647949f);
            Vector512<float> LOG2EF = Vector512.Create(1.44269504088896341f);
            Vector512<float> exp_C1 = Vector512.Create(0.693359375f);
            Vector512<float> exp_C2 = Vector512.Create(-2.12194440e-4f);
            Vector512<float> exp_p0 = Vector512.Create(1.9875691500E-4f);
            Vector512<float> exp_p1 = Vector512.Create(1.3981999507E-3f);
            Vector512<float> exp_p2 = Vector512.Create(8.3334519073E-3f);
            Vector512<float> exp_p3 = Vector512.Create(4.1665795894E-2f);
            Vector512<float> exp_p4 = Vector512.Create(1.6666665459E-1f);
            Vector512<float> exp_p5 = Vector512.Create(5.0000001201E-1f);
            Vector512<int> c0x7f = Vector512.Create(0x7F);
            x = Vector512.Min(x, exp_hi);
            x = Vector512.Max(x, exp_lo);
            Vector512<float> fx = Avx512F.FusedMultiplyAdd(x, LOG2EF, half);
            var tmp = Vector512.Floor(fx);
            Vector512<float> mask = Vector512.GreaterThan(tmp, fx);
            mask = Vector512.BitwiseAnd(tmp, mask);
            fx = tmp - mask;
            tmp = fx * exp_C1;
            var z = (fx * exp_C2);
            x = x - tmp - z;
            z = x * x;
            var y = exp_p0;
            y = y * x + exp_p1;
            y = y * x + exp_p2;
            y = y * x + exp_p3;
            y = y * x + exp_p4;
            y = y * x + exp_p5;
            y = y * z + x;
            y += c_one;
            var imm0 = Vector512.ConvertToInt32(fx);
            imm0 = Vector512.ShiftLeft(imm0 + c0x7f, 23);
            y = imm0.AsSingle() * y;
            return y;
        }
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
        if (Fma.IsSupported)
        {
            Vector256<float> c_one = Vector256<float>.One;
            Vector256<float> half = Vector256.Create(0.5f);
            Vector256<float> exp_hi = Vector256.Create(88.3762626647949f);
            Vector256<float> exp_lo = Vector256.Create(-88.3762626647949f);
            Vector256<float> LOG2EF = Vector256.Create(1.44269504088896341f);
            Vector256<float> exp_C1 = Vector256.Create(0.693359375f);
            Vector256<float> exp_C2 = Vector256.Create(-2.12194440e-4f);
            Vector256<float> exp_p0 = Vector256.Create(1.9875691500E-4f);
            Vector256<float> exp_p1 = Vector256.Create(1.3981999507E-3f);
            Vector256<float> exp_p2 = Vector256.Create(8.3334519073E-3f);
            Vector256<float> exp_p3 = Vector256.Create(4.1665795894E-2f);
            Vector256<float> exp_p4 = Vector256.Create(1.6666665459E-1f);
            Vector256<float> exp_p5 = Vector256.Create(5.0000001201E-1f);
            Vector256<int> c0x7f = Vector256.Create(0x7F);
            x = Vector256.Min(x, exp_hi);
            x = Vector256.Max(x, exp_lo);
            Vector256<float> fx = Fma.MultiplyAdd(x, LOG2EF, half);
            var tmp = Vector256.Floor(fx);
            Vector256<float> mask = Vector256.GreaterThan(tmp, fx);
            mask = Vector256.BitwiseAnd(tmp, mask);
            fx = tmp - mask;
            tmp = fx * exp_C1;
            var z = (fx * exp_C2);
            x = x - tmp - z;
            z = x * x;
            var y = exp_p0;
            y = Fma.MultiplyAdd(y, x, exp_p1);
            y = Fma.MultiplyAdd(y, x, exp_p2);
            y = Fma.MultiplyAdd(y, x, exp_p3);
            y = Fma.MultiplyAdd(y, x, exp_p4);
            y = Fma.MultiplyAdd(y, x, exp_p5);
            y = Fma.MultiplyAdd(y, z, x);
            y += c_one;
            var imm0 = Vector256.ConvertToInt32(fx);
            imm0 = Vector256.ShiftLeft(imm0 + c0x7f, 23);
            y = imm0.AsSingle() * y;
            return y;
        }
        else
        {
            Vector256<float> c_one = Vector256<float>.One;
            Vector256<float> half = Vector256.Create(0.5f);
            Vector256<float> exp_hi = Vector256.Create(88.3762626647949f);
            Vector256<float> exp_lo = Vector256.Create(-88.3762626647949f);
            Vector256<float> LOG2EF = Vector256.Create(1.44269504088896341f);
            Vector256<float> exp_C1 = Vector256.Create(0.693359375f);
            Vector256<float> exp_C2 = Vector256.Create(-2.12194440e-4f);
            Vector256<float> exp_p0 = Vector256.Create(1.9875691500E-4f);
            Vector256<float> exp_p1 = Vector256.Create(1.3981999507E-3f);
            Vector256<float> exp_p2 = Vector256.Create(8.3334519073E-3f);
            Vector256<float> exp_p3 = Vector256.Create(4.1665795894E-2f);
            Vector256<float> exp_p4 = Vector256.Create(1.6666665459E-1f);
            Vector256<float> exp_p5 = Vector256.Create(5.0000001201E-1f);
            Vector256<int> c0x7f = Vector256.Create(0x7F);
            x = Vector256.Min(x, exp_hi);
            x = Vector256.Max(x, exp_lo);
            Vector256<float> fx = x * LOG2EF + half;
            var tmp = Vector256.Floor(fx);
            Vector256<float> mask = Vector256.GreaterThan(tmp, fx);
            mask = Vector256.BitwiseAnd(tmp, mask);
            fx = tmp - mask;
            tmp = fx * exp_C1;
            var z = (fx * exp_C2);
            x = x - tmp - z;
            z = x * x;
            var y = exp_p0;
            y = y * x + exp_p1;
            y = y * x + exp_p2;
            y = y * x + exp_p3;
            y = y * x + exp_p4;
            y = y * x + exp_p5;
            y = y * z + x;
            y += c_one;
            var imm0 = Vector256.ConvertToInt32(fx);
            imm0 = Vector256.ShiftLeft(imm0 + c0x7f, 23);
            y = imm0.AsSingle() * y;
            return y;
        }
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
        Vector<float> c_one = Vector<float>.One;
        Vector<float> half = new Vector<float>(0.5f);
        Vector<float> exp_hi = new(88.3762626647949f);
        Vector<float> exp_lo = new(-88.3762626647949f);
        Vector<float> LOG2EF = new(1.44269504088896341f);
        Vector<float> exp_C1 = new(0.693359375f);
        Vector<float> exp_C2 = new(-2.12194440e-4f);
        Vector<float> exp_p0 = new(1.9875691500E-4f);
        Vector<float> exp_p1 = new(1.3981999507E-3f);
        Vector<float> exp_p2 = new(8.3334519073E-3f);
        Vector<float> exp_p3 = new(4.1665795894E-2f);
        Vector<float> exp_p4 = new(1.6666665459E-1f);
        Vector<float> exp_p5 = new(5.0000001201E-1f);
        Vector<int> c0x7f = new(0x7F);
        x = Vector.Min(x, exp_hi);
        x = Vector.Max(x, exp_lo);
        Vector<float> fx = x * LOG2EF + half;
        var tmp = Vector.Floor(fx);
        Vector<float> mask = Vector.As<int, float>(Vector.GreaterThan(tmp, fx));
        mask = Vector.BitwiseAnd(tmp, mask);
        fx = tmp - mask;
        tmp = fx * exp_C1;
        var z = (fx * exp_C2);
        x = x - tmp - z;
        z = x * x;
        var y = exp_p0;
        y = y * x + exp_p1;
        y = y * x + exp_p2;
        y = y * x + exp_p3;
        y = y * x + exp_p4;
        y = y * x + exp_p5;
        y = y * z + x;
        y += c_one;
        var imm0 = Vector.ConvertToInt32(fx);
        imm0 = Vector.ShiftLeft(imm0 + c0x7f, 23);
        y = Vector.As<int, float>(imm0) * y;
        return y;

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
            unsafe
            {
                for (; i < length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var temp = Vector512.LoadUnsafe(ref x[i]);
                    temp = Exp(temp);
                    Vector512.StoreUnsafe(temp, ref x[i]);
                }
            }
        }
        // Fall back to 256 bit instructions
        else if (Vector256.IsHardwareAccelerated)
        {
            unsafe
            {
                for (; i < length - Vector256<float>.Count; i += Vector256<float>.Count)
                {
                    var temp = Vector256.LoadUnsafe(ref x[i]);
                    temp = Exp(temp);
                    Vector256.StoreUnsafe(temp, ref x[i]);
                }
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
