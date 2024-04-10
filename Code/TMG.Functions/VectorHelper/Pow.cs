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
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace TMG.Functions;

public static partial class VectorHelper
{
    public static void Pow(float[] flat, float[] lhs, float rhs)
    {
        int i = 0;
        if(Vector512.IsHardwareAccelerated)
        {
            var vy = Vector512.Create(rhs);
            for (; i < flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vx = Vector512.LoadUnsafe(ref lhs[i]);
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref flat[i]);
            }
        }
        else if(Vector256.IsHardwareAccelerated)
        {
            var vy = Vector256.Create(rhs);
            for (; i < flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vx = Vector256.LoadUnsafe(ref lhs[i]);
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref flat[i]);
            }
        }
        for (; i < flat.Length; i++)
        {
            flat[i] = MathF.Pow(lhs[i], rhs);
        }
    }

    public static void Pow(float[] flat, float lhs, float[] rhs)
    {
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var vx = Vector512.Create(lhs);
            for (; i < flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vy = Vector512.LoadUnsafe(ref rhs[i]);
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref flat[i]);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var vx = Vector256.Create(lhs);
            for (; i < flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vy = Vector256.LoadUnsafe(ref rhs[i]);
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref flat[i]);
            }
        }
        for (; i < flat.Length; i++)
        {
            flat[i] = MathF.Pow(lhs, rhs[i]);
        }
    }

    public static void Pow(float[] flat, float[] lhs, float[] rhs)
    {
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            
            for (; i < flat.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vx = Vector512.LoadUnsafe(ref lhs[i]);
                var vy = Vector512.LoadUnsafe(ref rhs[i]);
                var res = Pow(vx, vy);
                Vector512.StoreUnsafe(res, ref flat[i]);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i < flat.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var vx = Vector256.LoadUnsafe(ref lhs[i]);
                var vy = Vector256.LoadUnsafe(ref rhs[i]);
                var res = Pow(vx, vy);
                Vector256.StoreUnsafe(res, ref flat[i]);
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
        return Exp(y * Log(x));
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
    public static Vector512<float> Pow(Vector512<float> x, Vector512<float> y)
    {
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
        return Exp(y * Log(x));
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
    public static Vector256<float> Pow(Vector256<float> x, Vector256<float> y)
    {
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
        return Exp(y * Log(x));
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

    /// <summary>
    /// Computes x^y for each element in the vector.
    /// </summary>
    /// <param name="x">The base of the exponent.</param>
    /// <param name="y">The exponential term</param>
    /// <returns>A vector with x^y</returns>
    public static Vector<float> Pow(Vector<float> x, Vector<float> y)
    {
        return Exp(y * Log(x));
    }

}
