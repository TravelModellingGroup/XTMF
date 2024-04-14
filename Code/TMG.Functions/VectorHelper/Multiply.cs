/*
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

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace TMG.Functions;

public static partial class VectorHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                int i = 0;
                // copy everything we can do inside of a vector
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var local = (f * s);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var s = Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    var local = (f * s);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                int i = 0;
                // copy everything we can do inside of a vector
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    (f * s).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i + firstIndex);
                    var s = new Vector<float>(second, i + secondIndex);
                    (f * s).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[destIndex + i] = first[firstIndex + i] * second[secondIndex + i];
            }
        }
    }

    public static void Multiply(float[] dest, float[] source, float scalar)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var constant = Vector512.Create(scalar);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= source.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var dynamic = Vector512.LoadUnsafe(ref source[i]);
                var local = (constant * dynamic);
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
            // copy the remainder
            for (; i < source.Length; i++)
            {
                dest[i] = source[i] * scalar;
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> constant = new(scalar);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= source.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var dynamic = new Vector<float>(source, i);
                (constant * dynamic).CopyTo(dest, i);
            }
            // copy the remainder
            for (; i < source.Length; i++)
            {
                dest[i] = source[i] * scalar;
            }
        }
        else
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = source[i] * scalar;
            }
        }
    }

    public static void Multiply(float[][] destination, float lhs, float[][] rhs)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var n = Vector512.Create(lhs);
                var dest = destination[row];
                var length = dest.Length;
                var denom = rhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var d = Vector512.LoadUnsafe(ref denom[i]);
                    var local = (n * d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = lhs * denom[i];
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                Vector<float> n = new(lhs);
                var dest = destination[row];
                var length = dest.Length;
                var denom = rhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var d = new Vector<float>(denom, i);
                    (n * d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = lhs * denom[i];
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = lhs * rhs[i][j];
                }
            });
        }
    }

    public static void Multiply(float[][] destination, float[][] lhs, float rhs)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var d = Vector512.Create(rhs);
                var dest = destination[row];
                var length = dest.Length;
                var num = lhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var n = Vector512.LoadUnsafe(ref num[i]);
                    var local = (n * d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] * rhs;
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                Vector<float> d = new(rhs);
                var dest = destination[row];
                var length = dest.Length;
                var num = lhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var n = new Vector<float>(num, i);
                    (n * d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] * rhs;
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = lhs[i][j] * rhs;
                }
            });
        }
    }

    public static void Multiply(float[][] destination, float[][] lhs, float[][] rhs)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var dest = destination[row];
                var length = dest.Length;
                var num = lhs[row];
                var denom = rhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var n = Vector512.LoadUnsafe(ref num[i]);
                    var d = Vector512.LoadUnsafe(ref denom[i]);
                    var local = (n * d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] * denom[i];
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var dest = destination[row];
                var length = dest.Length;
                var num = lhs[row];
                var denom = rhs[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var n = new Vector<float>(num, i);
                    var d = new Vector<float>(denom, i);
                    (n * d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] * denom[i];
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = lhs[i][j] * rhs[i][j];
                }
            });
        }
    }

    /// <summary>
    /// Multiply an array by a scalar and store it in another array.
    /// </summary>
    /// <param name="destination">Where to store the results</param>
    /// <param name="destIndex">The offset to start at</param>
    /// <param name="first">The array to multiply</param>
    /// <param name="firstIndex">The first index to multiply</param>
    /// <param name="scalar">The value to multiply against</param>
    /// <param name="length">The number of elements to multiply</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(float[] destination, int destIndex, float[] first, int firstIndex, float scalar, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var scalarV = Vector512.Create(scalar);
            if ((destIndex | firstIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = (Vector512.LoadUnsafe(ref first[i]) * scalarV);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * scalar;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = (Vector512.LoadUnsafe(ref first[i + firstIndex]) * scalarV);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * scalar;
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> scalarV = new(scalar);
            if ((destIndex | firstIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) * scalarV)
                        .CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * scalar;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) * scalarV)
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * scalar;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[destIndex + i] = first[firstIndex + i] * scalar;
            }
        }
    }

    /// <summary>
    /// Multiply first, second, and the scalar and save into the destination vector
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="destIndex"></param>
    /// <param name="first"></param>
    /// <param name="firstIndex"></param>
    /// <param name="second"></param>
    /// <param name="secondIndex"></param>
    /// <param name="scalar"></param>
    /// <param name="length"></param>
    internal static void Multiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, float scalar, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var vScalar = Vector512.Create(scalar);
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var local = (f * s * vScalar);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i] * scalar;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var s = Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    var local = (f * s * vScalar);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * scalar;
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var vScalar = new Vector<float>(scalar);
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    (f * s * vScalar).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i] * scalar;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i + firstIndex);
                    var s = new Vector<float>(second, i + secondIndex);
                    (f * s * vScalar).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * scalar;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[destIndex + i] = first[firstIndex + i] * second[secondIndex + i] * scalar;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="destIndex"></param>
    /// <param name="first"></param>
    /// <param name="firstIndex"></param>
    /// <param name="second"></param>
    /// <param name="secondIndex"></param>
    /// <param name="third"></param>
    /// <param name="thirdIndex"></param>
    /// <param name="fourth"></param>
    /// <param name="fourthIndex"></param>
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex,
        float[] third, int thirdIndex, float[] fourth, int fourthIndex, int length)
    {
        int i = 0;
        if(Vector512.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex | thirdIndex | fourthIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var t = Vector512.LoadUnsafe(ref third[i]);
                    var f4 = Vector512.LoadUnsafe(ref fourth[i]);
                    Vector512.StoreUnsafe((f * s) * (t * f4), ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i] * third[i] * fourth[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var s = Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    var t = Vector512.LoadUnsafe(ref third[i + thirdIndex]);
                    var f4 = Vector512.LoadUnsafe(ref fourth[i + fourthIndex]);
                    Vector512.StoreUnsafe((f * s) * (t * f4), ref destination[i + destIndex]);
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex | thirdIndex | fourthIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    var t = new Vector<float>(third, i);
                    var f4 = new Vector<float>(fourth, i);
                    ((f * s) * (t * f4)).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] * second[i] * third[i] * fourth[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i + firstIndex);
                    var s = new Vector<float>(second, i + secondIndex);
                    var t = new Vector<float>(third, i + thirdIndex);
                    var f4 = new Vector<float>(fourth, i + fourthIndex);
                    ((f * s) * (t * f4)).CopyTo(destination, i + destIndex);
                }
            }
        }
        // copy the remainder
        for (; i < length; i++)
        {
            destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * fourth[i + fourthIndex];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static float MultiplyAndSumNoStore(Span<float> first, Span<float> second)
    {
        // Make sure our data is of the right size
        if (first.Length != second.Length) ThrowVectorsMustBeSameSize();
        var remainder = first.Length % Vector<float>.Count;
        var accV = Vector<float>.Zero;
        var acc = 0.0f;
        var firstV = first[..^remainder].ReinterpretSpan<float, Vector<float>>();
        var secondV = second[..^remainder].ReinterpretSpan<float, Vector<float>>();
        for (int i = 0; i < firstV.Length; i++)
        {
            accV += firstV[i] * secondV[i];
        }
        for (int i = first.Length - remainder; i < first.Length; i++)
        {
            acc += first[i] * second[i];
        }
        return Vector.Sum(accV) + acc;
    }
}
