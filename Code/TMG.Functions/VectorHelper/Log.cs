
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
        var cephes_SQRTHF = Vector512.Create(0.707106781186547524f);
        var cephes_log_p0 = Vector512.Create(7.0376836292E-2f);
        var cephes_log_p1 = Vector512.Create(-1.1514610310E-1f);
        var cephes_log_p2 = Vector512.Create(1.1676998740E-1f);
        var cephes_log_p3 = Vector512.Create(-1.2420140846E-1f);
        var cephes_log_p4 = Vector512.Create(+1.4249322787E-1f);
        var cephes_log_p5 = Vector512.Create(-1.6668057665E-1f);
        var cephes_log_p6 = Vector512.Create(+2.0000714765E-1f);
        var cephes_log_p7 = Vector512.Create(-2.4999993993E-1f);
        var cephes_log_p8 = Vector512.Create(+3.3333331174E-1f);
        var cephes_log_q1 = Vector512.Create(-2.12194440e-4f);
        var cephes_log_q2 = Vector512.Create(0.693359375f);
        var min_normalized = Vector512.Create(0x00800000).As<int, float>();
        var invMantMask = Vector512.Create(~0x7f800000).As<int, float>();
        var half = Vector512.Create(0.5f);
        var c0x7f = Vector512.Create(0x7F);
        var one = Vector512<float>.One;

        // Generate the error masks before we start processing
        var invalidMask = Vector512.BitwiseOr(Vector512.LessThan(x, Vector512<float>.Zero), CreateNaNMask(x));
        var zeroMask = Vector512.Equals(x, Vector512<float>.Zero);
        var posInf = Vector512.Create(float.PositiveInfinity);
        var negInfV = Vector512.Create(float.NegativeInfinity);
        var posInfMask = Vector512.GreaterThanOrEqual(x, posInf);

        // Ignore denomalized values
        x = Vector512.Max(x, min_normalized);
        var imm0 = Vector512.ShiftRightLogical(x.AsInt32(), 23);
        x = Vector512.BitwiseAnd(x, invMantMask);
        x = Vector512.BitwiseOr(x, half);
        imm0 = imm0 - c0x7f;
        var e = Vector512.ConvertToSingle(imm0) + one;
        var mask = Vector512.LessThan(x, cephes_SQRTHF);
        var temp = Vector512.BitwiseAnd(x, mask);
        x = x - one;

        e = e - Vector512.BitwiseAnd(one, mask);
        x = x + temp;

        var z = x * x;

        var y = cephes_log_p0;
        if (Avx512F.IsSupported)
        {
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p1);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p2);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p3);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p4);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p5);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p6);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p7);
            y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p8);
        }
        else
        {
            y = y * x + cephes_log_p1;
            y = y * x + cephes_log_p2;
            y = y * x + cephes_log_p3;
            y = y * x + cephes_log_p4;
            y = y * x + cephes_log_p5;
            y = y * x + cephes_log_p6;
            y = y * x + cephes_log_p7;
            y = y * x + cephes_log_p8;
        }

        y = y * x;
        y = y * z;

        if (Avx512F.IsSupported)
        {
            y = Avx512F.FusedMultiplyAdd(e, cephes_log_q1, y);
            // y = y - (z * half);
            y = Avx512F.FusedMultiplyAddNegated(z, half, y);
            x = Avx512F.FusedMultiplyAdd(e, cephes_log_q2, (x + y));
        }
        else
        {
            y = y + e * cephes_log_q1;
            y = y - (z * half);
            x = (x + y) + (e * cephes_log_q2);
        }

        // Apply error masks
        x = Vector512.BitwiseOr(x, invalidMask); // negative arg will be NAN
        x = Blend(x, negInfV, zeroMask);
        x = Blend(x, posInf, posInfMask);
        return x;
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
        var cephes_SQRTHF = Vector256.Create(0.707106781186547524f);
        var cephes_log_p0 = Vector256.Create(7.0376836292E-2f);
        var cephes_log_p1 = Vector256.Create(-1.1514610310E-1f);
        var cephes_log_p2 = Vector256.Create(1.1676998740E-1f);
        var cephes_log_p3 = Vector256.Create(-1.2420140846E-1f);
        var cephes_log_p4 = Vector256.Create(+1.4249322787E-1f);
        var cephes_log_p5 = Vector256.Create(-1.6668057665E-1f);
        var cephes_log_p6 = Vector256.Create(+2.0000714765E-1f);
        var cephes_log_p7 = Vector256.Create(-2.4999993993E-1f);
        var cephes_log_p8 = Vector256.Create(+3.3333331174E-1f);
        var cephes_log_q1 = Vector256.Create(-2.12194440e-4f);
        var cephes_log_q2 = Vector256.Create(0.693359375f);
        var min_normalized = Vector256.Create(0x00800000).As<int, float>();
        var invMantMask = Vector256.Create(~0x7f800000).As<int, float>();
        var half = Vector256.Create(0.5f);
        var c0x7f = Vector256.Create(0x7F);
        var one = Vector256<float>.One;
        // Generate the error masks before we start processing
        var invalidMask = Vector256.BitwiseOr(Vector256.LessThan(x, Vector256<float>.Zero), CreateNaNMask(x));
        var zeroMask = Vector256.Equals(x, Vector256<float>.Zero);
        var posInf = Vector256.Create(float.PositiveInfinity);
        var negInfV = Vector256.Create(float.NegativeInfinity);
        var posInfMask = Vector256.GreaterThanOrEqual(x, posInf);

        // Ignore denomalized values
        x = Vector256.Max(x, min_normalized);
        var imm0 = Vector256.ShiftRightLogical(x.AsInt32(), 23);
        x = Vector256.BitwiseAnd(x, invMantMask);
        x = Vector256.BitwiseOr(x, half);
        imm0 = imm0 - c0x7f;
        var e = Vector256.ConvertToSingle(imm0) + one;
        var mask = Vector256.LessThan(x, cephes_SQRTHF);
        var temp = Vector256.BitwiseAnd(x, mask);
        x = x - one;

        e = e - Vector256.BitwiseAnd(one, mask);
        x = x + temp;

        var z = x * x;

        var y = cephes_log_p0;
        if (Fma.IsSupported)
        {
            y = Fma.MultiplyAdd(y, x, cephes_log_p1);
            y = Fma.MultiplyAdd(y, x, cephes_log_p2);
            y = Fma.MultiplyAdd(y, x, cephes_log_p3);
            y = Fma.MultiplyAdd(y, x, cephes_log_p4);
            y = Fma.MultiplyAdd(y, x, cephes_log_p5);
            y = Fma.MultiplyAdd(y, x, cephes_log_p6);
            y = Fma.MultiplyAdd(y, x, cephes_log_p7);
            y = Fma.MultiplyAdd(y, x, cephes_log_p8);
        }
        else
        {
            y = y * x + cephes_log_p1;
            y = y * x + cephes_log_p2;
            y = y * x + cephes_log_p3;
            y = y * x + cephes_log_p4;
            y = y * x + cephes_log_p5;
            y = y * x + cephes_log_p6;
            y = y * x + cephes_log_p7;
            y = y * x + cephes_log_p8;
        }

        y = y * x * z;

        if (Fma.IsSupported)
        {
            y = Fma.MultiplyAdd(e, cephes_log_q1, y);
            y = Fma.MultiplyAddNegated(z, half, y);
            x = Fma.MultiplyAdd(e, cephes_log_q2, (x + y));
        }
        else
        {
            y = y + e * cephes_log_q1;
            y = y - (z * half);
            x = (x + y) + (e * cephes_log_q2);
        }

        // Apply error masks
        x = Vector256.BitwiseOr(x, invalidMask); // negative arg will be NAN
        x = Blend(x, negInfV, zeroMask);
        x = Blend(x, posInf, posInfMask);
        return x;
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
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var cephes_SQRTHF = Vector512.Create(0.707106781186547524f);
            var cephes_log_p0 = Vector512.Create(7.0376836292E-2f);
            var cephes_log_p1 = Vector512.Create(-1.1514610310E-1f);
            var cephes_log_p2 = Vector512.Create(1.1676998740E-1f);
            var cephes_log_p3 = Vector512.Create(-1.2420140846E-1f);
            var cephes_log_p4 = Vector512.Create(+1.4249322787E-1f);
            var cephes_log_p5 = Vector512.Create(-1.6668057665E-1f);
            var cephes_log_p6 = Vector512.Create(+2.0000714765E-1f);
            var cephes_log_p7 = Vector512.Create(-2.4999993993E-1f);
            var cephes_log_p8 = Vector512.Create(+3.3333331174E-1f);
            var cephes_log_q1 = Vector512.Create(-2.12194440e-4f);
            var cephes_log_q2 = Vector512.Create(0.693359375f);
            var min_normalized = Vector512.Create(0x00800000).As<int, float>();
            var invMantMask = Vector512.Create(~0x7f800000).As<int, float>();
            var half = Vector512.Create(0.5f);
            var c0x7f = Vector512.Create(0x7F);
            var one = Vector512<float>.One;
            for (; i < source.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var x = Vector512.LoadUnsafe(ref source[i]);
                // Generate the error masks before we start processing
                var invalidMask = Vector512.BitwiseOr(Vector512.LessThan(x, Vector512<float>.Zero), CreateNaNMask(x));
                var zeroMask = Vector512.Equals(x, Vector512<float>.Zero);
                var posInf = Vector512.Create(float.PositiveInfinity);
                var negInfV = Vector512.Create(float.NegativeInfinity);
                var posInfMask = Vector512.GreaterThanOrEqual(x, posInf);

                // Ignore denomalized values
                x = Vector512.Max(x, min_normalized);
                var imm0 = Vector512.ShiftRightLogical(x.AsInt32(), 23);
                x = Vector512.BitwiseAnd(x, invMantMask);
                x = Vector512.BitwiseOr(x, half);
                imm0 = imm0 - c0x7f;
                var e = Vector512.ConvertToSingle(imm0) + one;
                var mask = Vector512.LessThan(x, cephes_SQRTHF);
                var temp = Vector512.BitwiseAnd(x, mask);
                x = x - one;

                e = e - Vector512.BitwiseAnd(one, mask);
                x = x + temp;

                var z = x * x;

                var y = cephes_log_p0;
                if (Avx512F.IsSupported)
                {
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p1);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p2);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p3);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p4);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p5);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p6);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p7);
                    y = Avx512F.FusedMultiplyAdd(y, x, cephes_log_p8);
                }
                else
                {
                    y = y * x + cephes_log_p1;
                    y = y * x + cephes_log_p2;
                    y = y * x + cephes_log_p3;
                    y = y * x + cephes_log_p4;
                    y = y * x + cephes_log_p5;
                    y = y * x + cephes_log_p6;
                    y = y * x + cephes_log_p7;
                    y = y * x + cephes_log_p8;
                }

                y = y * x * z;

                if (Avx512F.IsSupported)
                {
                    y = Avx512F.FusedMultiplyAdd(e, cephes_log_q1, y);
                    y = Avx512F.FusedMultiplyAddNegated(z, half, y);
                    x = Avx512F.FusedMultiplyAdd(e, cephes_log_q2, (x + y));
                }
                else
                {
                    y = y + e * cephes_log_q1;
                    y = y - (z * half);
                    x = (x + y) + (e * cephes_log_q2);
                }

                // Apply error masks
                x = Vector512.BitwiseOr(x, invalidMask); // negative arg will be NAN
                x = Blend(x, negInfV, zeroMask);
                x = Blend(x, posInf, posInfMask);
                Vector512.StoreUnsafe(x, ref destination[i]);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var cephes_SQRTHF = Vector256.Create(0.707106781186547524f);
            var cephes_log_p0 = Vector256.Create(7.0376836292E-2f);
            var cephes_log_p1 = Vector256.Create(-1.1514610310E-1f);
            var cephes_log_p2 = Vector256.Create(1.1676998740E-1f);
            var cephes_log_p3 = Vector256.Create(-1.2420140846E-1f);
            var cephes_log_p4 = Vector256.Create(+1.4249322787E-1f);
            var cephes_log_p5 = Vector256.Create(-1.6668057665E-1f);
            var cephes_log_p6 = Vector256.Create(+2.0000714765E-1f);
            var cephes_log_p7 = Vector256.Create(-2.4999993993E-1f);
            var cephes_log_p8 = Vector256.Create(+3.3333331174E-1f);
            var cephes_log_q1 = Vector256.Create(-2.12194440e-4f);
            var cephes_log_q2 = Vector256.Create(0.693359375f);
            var min_normalized = Vector256.Create(0x00800000).As<int, float>();
            var invMantMask = Vector256.Create(~0x7f800000).As<int, float>();
            var half = Vector256.Create(0.5f);
            var c0x7f = Vector256.Create(0x7F);
            var one = Vector256<float>.One;
            for (; i < source.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var x = Vector256.LoadUnsafe(ref source[i]);
                // Generate the error masks before we start processing
                var invalidMask = Vector256.BitwiseOr(Vector256.LessThan(x, Vector256<float>.Zero), CreateNaNMask(x));
                var zeroMask = Vector256.Equals(x, Vector256<float>.Zero);
                var posInf = Vector256.Create(float.PositiveInfinity);
                var negInfV = Vector256.Create(float.NegativeInfinity);
                var posInfMask = Vector256.GreaterThanOrEqual(x, posInf);

                // Ignore denomalized values
                x = Vector256.Max(x, min_normalized);
                var imm0 = Vector256.ShiftRightLogical(x.AsInt32(), 23);
                x = Vector256.BitwiseAnd(x, invMantMask);
                x = Vector256.BitwiseOr(x, half);
                imm0 = imm0 - c0x7f;
                var e = Vector256.ConvertToSingle(imm0) + one;
                var mask = Vector256.LessThan(x, cephes_SQRTHF);
                var temp = Vector256.BitwiseAnd(x, mask);
                x = x - one;

                e = e - Vector256.BitwiseAnd(one, mask);
                x = x + temp;

                var z = x * x;

                var y = cephes_log_p0;
                if (Fma.IsSupported)
                {
                    y = Fma.MultiplyAdd(y, x, cephes_log_p1);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p2);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p3);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p4);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p5);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p6);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p7);
                    y = Fma.MultiplyAdd(y, x, cephes_log_p8);
                }
                else
                {
                    y = y * x + cephes_log_p1;
                    y = y * x + cephes_log_p2;
                    y = y * x + cephes_log_p3;
                    y = y * x + cephes_log_p4;
                    y = y * x + cephes_log_p5;
                    y = y * x + cephes_log_p6;
                    y = y * x + cephes_log_p7;
                    y = y * x + cephes_log_p8;
                }

                y = y * x * z;

                if (Fma.IsSupported)
                {
                    y = Fma.MultiplyAdd(e, cephes_log_q1, y);
                    y = Fma.MultiplyAddNegated(z, half, y);
                    x = Fma.MultiplyAdd(e, cephes_log_q2, (x + y));
                }
                else
                {
                    y = y + e * cephes_log_q1;
                    y = y - (z * half);
                    x = (x + y) + (e * cephes_log_q2);
                }

                // Apply error masks
                x = Vector256.BitwiseOr(x, invalidMask); // negative arg will be NAN
                x = Blend(x, negInfV, zeroMask);
                x = Blend(x, posInf, posInfMask);
                Vector256.StoreUnsafe(x, ref destination[i]);
            }
        }
        for (; i < source.Length; i++)
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
