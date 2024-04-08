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
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace TMG.Functions;

/// <summary>
/// This class is designed to help facilitate the use of the SIMD instructions available in
/// modern .Net.
/// </summary>
public static partial class VectorHelper
{
    /// <summary>
    /// A vector containing the maximum value of a float
    /// </summary>
    private static Vector<float> MaxFloat;

    private static Vector512<float> MaxFloat512;

    static VectorHelper()
    {
        MaxFloat = new Vector<float>(float.MaxValue);
        MaxFloat512 = Vector512.Create(float.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Span<K> ReinterpretSpan<T, K>(this Span<T> span)
        where T : struct
        where K : struct
    {
        return MemoryMarshal.Cast<T, K>(span);
    }

    [DoesNotReturn]
    private static void ThrowVectorsMustBeSameSize()
    {
        throw new ArgumentException("Vectors must be the same size!");
    }

    /// <summary>
    /// Add up the elements in the vector
    /// </summary>
    /// <param name="v">The vector to sum</param>
    /// <returns>The sum of the elements in the vector</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sum(ref Vector<float> v)
    {
        return Vector.Sum(v);
    }


    /// <summary>
    /// Sum an array
    /// </summary>
    /// <param name="array">The array to Sum</param>
    /// <param name="startIndex">The index to start summing from</param>
    /// <param name="length">The number of elements to add</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(float[] array, int startIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            var acc2 = Vector512<float>.Zero;
            var acc3 = Vector512<float>.Zero;
            int endIndex = (startIndex + length);
            // copy everything we can do inside of a vector
            int i = startIndex;
            for (; i <= (endIndex - (Vector512<float>.Count * 3)); i += (Vector512<float>.Count * 3))
            {
                var f = Vector512.LoadUnsafe(ref array[i]);
                var s = Vector512.LoadUnsafe(ref array[i + Vector512<float>.Count]);
                var t = Vector512.LoadUnsafe(ref array[i + Vector512<float>.Count * 2]);
                acc += f;
                acc2 += s;
                acc3 += t;
            }
            // copy the remainder
            for (; i < endIndex; i++)
            {
                remainderSum += array[i];
            }
            acc = acc + acc2 + acc3;
            return remainderSum + Vector512.Sum(acc);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            var acc2 = Vector<float>.Zero;
            var acc3 = Vector<float>.Zero;
            int endIndex = startIndex + length;
            // copy everything we can do inside of a vector
            int i = startIndex;
            for (; i <= endIndex - (Vector<float>.Count * 3); i += (Vector<float>.Count * 3))
            {
                var f = new Vector<float>(array, i);
                var s = new Vector<float>(array, i + Vector<float>.Count);
                var t = new Vector<float>(array, i + Vector<float>.Count * 2);
                acc += f;
                acc2 += s;
                acc3 += t;
            }
            // copy the remainder
            for (; i < endIndex; i++)
            {
                remainderSum += array[i];
            }
            acc = acc + acc2 + acc3;
            return remainderSum + Sum(ref acc);
        }
        else
        {
            var sum = 0.0f;
            int end = startIndex + length;
            for (int i = startIndex; i < end; i++)
            {
                sum += array[i];
            }
            return sum;
        }
    }

    /// <summary>
    /// Take the average of the absolute values
    /// </summary>
    /// <param name="first">The first vector</param>
    /// <param name="firstIndex">Where to start in the first vector</param>
    /// <param name="second">The second vector</param>
    /// <param name="secondIndex">Where to start in the second vector</param>
    /// <param name="length">The number of elements to read</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AbsDiffAverage(float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            var acc2 = Vector512<float>.Zero;
            int i = firstIndex;
            if ((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - (Vector512<float>.Count * 2);
                for (; i <= highestForVector; i += Vector512<float>.Count * 2)
                {
                    var f1 = Vector512.LoadUnsafe(ref first[i]);
                    var s1 = Vector512.LoadUnsafe(ref second[i]);
                    var f2 = Vector512.LoadUnsafe(ref first[i + Vector512<float>.Count]);
                    var s2 = Vector512.LoadUnsafe(ref second[i + Vector512<float>.Count]);
                    acc += Vector512.Abs(f1 - s1);
                    acc2 += Vector512.Abs(f2 - s2);
                }
                acc += acc2;
            }
            else
            {
                int highestForVector = length - Vector512<float>.Count + firstIndex;
                int s = secondIndex;
                for (; i <= highestForVector; i += Vector512<float>.Count)
                {
                    acc += Vector512.Abs(Vector512.LoadUnsafe(ref first[i]) - Vector512.LoadUnsafe(ref second[s]));
                    s += Vector512<float>.Count;
                }
            }
            // copy the remainder
            for (; i < length; i++)
            {
                remainderSum += Math.Abs(first[i + firstIndex] - second[i + secondIndex]);
            }
            return (remainderSum + Vector512.Sum(acc)) / length;
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            var acc2 = Vector<float>.Zero;
            int i = firstIndex;
            if ((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - (Vector<float>.Count * 2);
                for (; i <= highestForVector; i += Vector<float>.Count * 2)
                {
                    var f1 = new Vector<float>(first, i);
                    var s1 = new Vector<float>(second, i);
                    var f2 = new Vector<float>(first, i + Vector<float>.Count);
                    var s2 = new Vector<float>(second, i + Vector<float>.Count);
                    acc += Vector.Abs(f1 - s1);
                    acc2 += Vector.Abs(f2 - s2);
                }
                acc += acc2;
            }
            else
            {
                int highestForVector = length - Vector<float>.Count + firstIndex;
                int s = secondIndex;
                for (; i <= highestForVector; i += Vector<float>.Count)
                {
                    acc += Vector.Abs(new Vector<float>(first, i) - new Vector<float>(second, s));
                    s += Vector<float>.Count;
                }
            }
            // copy the remainder
            for (; i < length; i++)
            {
                remainderSum += Math.Abs(first[i + firstIndex] - second[i + secondIndex]);
            }
            return (remainderSum + Sum(ref acc)) / length;
        }
        else
        {
            float diff = 0.0f;
            for (int i = 0; i < length; i++)
            {
                diff += Math.Abs(first[firstIndex + i] - second[secondIndex + i]);
            }
            return diff / length;
        }
    }

    /// <summary>
    /// Get the maximum difference from two arrays.
    /// </summary>
    /// <param name="first">The first vector</param>
    /// <param name="firstIndex">Where to start in the first vector</param>
    /// <param name="second">The second vector</param>
    /// <param name="secondIndex">Where to start in the second vector</param>
    /// <param name="length">The number of elements to read</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AbsDiffMax(float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderMax = 0.0f;
            var vectorMax = Vector512<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - Vector512<float>.Count;
                for (int i = 0; i <= highestForVector; i += Vector512<float>.Count)
                {
                    vectorMax = Vector512.Max(Vector512.Abs(Vector512.LoadUnsafe(ref first[i]) - Vector512.LoadUnsafe(ref second[i])), vectorMax);
                }
            }
            else
            {
                int highestForVector = length - Vector512<float>.Count + firstIndex;
                int s = secondIndex;
                for (int f = 0; f <= highestForVector; f += Vector512<float>.Count)
                {
                    vectorMax = Vector512.Max(Vector512.Abs(Vector512.LoadUnsafe(ref first[f]) - Vector512.LoadUnsafe(ref second[s])), vectorMax);
                    s += Vector<float>.Count;
                }
            }
            // copy the remainder
            for (int i = length - (length % Vector512<float>.Count); i < length; i++)
            {
                remainderMax = Math.Max(remainderMax, Math.Abs(first[i + firstIndex] - second[i + secondIndex]));
            }
            Span<float> temp = stackalloc float[Vector512<float>.Count];
            vectorMax.CopyTo(temp);
            for (int i = 0; i < temp.Length; i++)
            {
                remainderMax = Math.Max(temp[i], remainderMax);
            }
            return remainderMax;
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderMax = 0.0f;
            var vectorMax = Vector<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - Vector<float>.Count;
                for (int i = 0; i <= highestForVector; i += Vector<float>.Count)
                {
                    vectorMax = Vector.Max(Vector.Abs(new Vector<float>(first, i) - new Vector<float>(second, i)), vectorMax);
                }
            }
            else
            {
                int highestForVector = length - Vector<float>.Count + firstIndex;
                int s = secondIndex;
                for (int f = 0; f <= highestForVector; f += Vector<float>.Count)
                {
                    vectorMax = Vector.Max(Vector.Abs(new Vector<float>(first, f) - new Vector<float>(second, s)), vectorMax);
                    s += Vector<float>.Count;
                }
            }
            // copy the remainder
            for (int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                remainderMax = Math.Max(remainderMax, Math.Abs(first[i + firstIndex] - second[i + secondIndex]));
            }
            Span<float> temp = stackalloc float[Vector<float>.Count];
            vectorMax.CopyTo(temp);
            for (int i = 0; i < temp.Length; i++)
            {
                remainderMax = Math.Max(temp[i], remainderMax);
            }
            return remainderMax;
        }
        else
        {
            var max = 0.0f;
            for (int i = 0; i < length; i++)
            {
                max = Math.Max(max, Math.Abs(first[firstIndex + i] - second[secondIndex + i]));
            }
            return max;
        }
    }

    /// <summary>
    /// Sum the square differences of two arrays
    /// </summary>
    /// <param name="first">The array to Sum</param>
    /// <param name="firstIndex">The index to start summing from</param>
    /// <param name="second">The array to Sum</param>
    /// <param name="secondIndex">The index to start summing from</param>
    /// <param name="length">The number of elements to add</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SquareDiff(float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var diff = Vector512.LoadUnsafe(ref first[i]) - Vector512.LoadUnsafe(ref second[i]);
                    acc += diff * diff;
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    var diff = first[i] - second[i];
                    remainderSum += diff * diff;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var diff = Vector512.LoadUnsafe(ref first[i + firstIndex]) - Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    acc += diff * diff;
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    var diff = first[i + firstIndex] - second[i + secondIndex];
                    remainderSum += diff * diff;
                }
            }
            return remainderSum + Vector512.Sum(acc);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var diff = new Vector<float>(first, i) - new Vector<float>(second, i);
                    acc += diff * diff;
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    var diff = first[i] - second[i];
                    remainderSum += diff * diff;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var diff = new Vector<float>(first, i + firstIndex) - new Vector<float>(second, i + secondIndex);
                    acc += diff * diff;
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    var diff = first[i + firstIndex] - second[i + secondIndex];
                    remainderSum += diff * diff;
                }
            }
            return remainderSum + Sum(ref acc);
        }
        else
        {
            var diff2 = 0.0f;
            for (int i = 0; i < length; i++)
            {
                // no abs needed since we are going to square
                var diff = first[firstIndex + i] - second[secondIndex + i];
                diff2 += diff * diff;
            }
            return diff2;
        }
    }

    /// <summary>
    /// Assign the given value to the whole array
    /// </summary>
    /// <param name="dest">The array to set</param>
    /// <param name="value">The value to set it to</param>
    public static void Set(float[] dest, float value)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            int i = 0;
            var vValue = Vector512.Create(value);
            for (; i < dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                Vector512.StoreUnsafe(vValue, ref dest[i]);
            }
            for (; i < dest.Length; i++)
            {
                dest[i] = value;
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            int i = 0;
            var vValue = new Vector<float>(value);
            for (; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                vValue.CopyTo(dest, i);
            }
            for (; i < dest.Length; i++)
            {
                dest[i] = value;
            }
        }
        else
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = value;
            }
        }
    }

    public static void Abs(float[] dest, float[] source)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var dynamic = Vector512.LoadUnsafe(ref source[i]);
                var local = (Vector512.Abs(dynamic));
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
            // copy the remainder
            for (; i < dest.Length; i++)
            {
                dest[i] = Math.Abs(source[i]);
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var dynamic = new Vector<float>(source, i);
                (Vector.Abs(dynamic)).CopyTo(dest, i);
            }
            // copy the remainder
            for (; i < dest.Length; i++)
            {
                dest[i] = Math.Abs(source[i]);
            }
        }
        else
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = Math.Abs(source[i]);
            }
        }
    }

    public static void Abs(float[][] dest, float[][] source)
    {
        for (int row = 0; row < dest.Length; row++)
        {
            Abs(dest[row], source[row]);
        }
    }

    /// <summary>
    /// Multiply the two vectors and store the results in the destination.  Return a running sum.
    /// </summary>
    /// <param name="destination">Where to save the data</param>
    /// <param name="destIndex">What index to start at</param>
    /// <param name="first">The first array to multiply</param>
    /// <param name="firstIndex">The index to start at</param>
    /// <param name="second">The second array to multiply</param>
    /// <param name="secondIndex">The index to start at for the second array</param>
    /// <param name="length">The amount of data to multiply</param>
    /// <returns>The sum of all of the multiplies</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MultiplyAndSum(float[] destination, int destIndex, float[] first, int firstIndex,
        float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var local = (f * s);
                    acc += local;
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += destination[i] = first[i] * second[i];
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
                    acc += local;
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Vector512.Sum(acc);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    var local = (f * s);
                    acc += local;
                    local.CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += destination[i] = first[i] * second[i];
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
                    var local = (f * s);
                    acc += local;
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Sum(ref acc);
        }
        else
        {
            float remainderSum = 0.0f;
            for (int i = 0; i < length; i++)
            {
                remainderSum += destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
            }
            return remainderSum;
        }
    }

    /// <summary>
    /// Multiply the two vectors without storing the results but returning the total.
    /// </summary>
    /// <param name="first">The first array to multiply</param>
    /// <param name="firstIndex">The index to start at</param>
    /// <param name="second">The second array to multiply</param>
    /// <param name="secondIndex">The index to start at for the second array</param>
    /// <param name="length">The amount of data to multiply</param>
    /// <returns>The sum of all of the multiplies</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MultiplyAndSum(float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            var acc2 = Vector512<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - (Vector512<float>.Count * 2); i += (Vector512<float>.Count * 2))
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var f2 = Vector512.LoadUnsafe(ref first[i + Vector<float>.Count]);
                    var s2 = Vector512.LoadUnsafe(ref second[i + Vector<float>.Count]);

                    acc += (f * s);
                    acc2 += (f2 * s2);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i] * second[i];
                }
                acc += acc2;
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    acc += (Vector512.LoadUnsafe(ref first[i + firstIndex]) * Vector512.LoadUnsafe(ref second[i + secondIndex]));
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Vector512.Sum(acc);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            var acc2 = Vector<float>.Zero;
            if ((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - (Vector<float>.Count * 2); i += (Vector<float>.Count * 2))
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    var f2 = new Vector<float>(first, i + Vector<float>.Count);
                    var s2 = new Vector<float>(second, i + Vector<float>.Count);
                    acc += (f * s);
                    acc2 += (f2 * s2);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i] * second[i];
                }
                acc += acc2;
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    acc += (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex));
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Sum(ref acc);
        }
        else
        {
            var remainderSum = 0.0f;
            for (int i = 0; i < length; i++)
            {
                remainderSum += first[i + firstIndex] * second[i + secondIndex];
            }
            return remainderSum;
        }
    }

    /// <summary>
    /// Multiply the two vectors without storing the results but returning the total.
    /// </summary>
    /// <param name="first">The first array to multiply</param>
    /// <param name="firstIndex">The index to start at</param>
    /// <param name="second">The second array to multiply</param>
    /// <param name="secondIndex">The index to start at for the second array</param>
    /// <param name="thirdIndex"></param>
    /// <param name="length">The amount of data to multiply</param>
    /// <param name="third"></param>
    /// <returns>The sum of all of the multiplies</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Multiply3AndSum(float[] first, int firstIndex, float[] second, int secondIndex,
        float[] third, int thirdIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector512<float>.Zero;
            var acc2 = Vector512<float>.Zero;
            if ((firstIndex | secondIndex | thirdIndex) == 0)
            {
                int i = 0;
                // copy everything we can do inside of a vector
                for (; i <= length - (Vector512<float>.Count * 2); i += (Vector512<float>.Count * 2))
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var t = Vector512.LoadUnsafe(ref third[i]);
                    var f2 = Vector512.LoadUnsafe(ref first[i + Vector512<float>.Count]);
                    var s2 = Vector512.LoadUnsafe(ref second[i + Vector512<float>.Count]);
                    var t2 = Vector512.LoadUnsafe(ref third[i + Vector512<float>.Count]);
                    acc += (f * s * t);
                    acc2 += (f2 * s2 * t2);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i] * second[i] * third[i];
                }
                acc += acc2;
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var s = Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    var t = Vector512.LoadUnsafe(ref third[i + thirdIndex]);
                    var local = (f * s * t);
                    acc += local;
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex];
                }
            }
            return remainderSum + Vector512.Sum(acc);
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            var acc2 = Vector<float>.Zero;
            if ((firstIndex | secondIndex | thirdIndex) == 0)
            {
                int i = 0;
                // copy everything we can do inside of a vector
                for (; i <= length - (Vector<float>.Count * 2); i += (Vector<float>.Count * 2))
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    var t = new Vector<float>(third, i);
                    var f2 = new Vector<float>(first, i + Vector<float>.Count);
                    var s2 = new Vector<float>(second, i + Vector<float>.Count);
                    var t2 = new Vector<float>(third, i + Vector<float>.Count);
                    acc += (f * s * t);
                    acc2 += (f2 * s2 * t2);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    remainderSum += first[i] * second[i] * third[i];
                }
                acc += acc2;
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * new Vector<float>(third, i + thirdIndex));
                    acc += local;
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex];
                }
            }
            return remainderSum + Sum(ref acc);
        }
        else
        {
            var remainderSum = 0.0f;
            for (int i = 0; i < length; i++)
            {
                remainderSum += first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex];
            }
            return remainderSum;
        }
    }

    /// <summary>
    /// Multiply the two vectors and store the results in the destination.  Return a running sum.
    /// </summary>
    /// <param name="destination">Where to save the data</param>
    /// <param name="destIndex">What index to start at</param>
    /// <param name="first">The first array to multiply</param>
    /// <param name="firstIndex">The index to start at</param>
    /// <param name="second">The second array to multiply</param>
    /// <param name="secondIndex">The index to start at for the second array</param>
    /// <param name="columnIndex"></param>
    /// <param name="length">The amount of data to multiply</param>
    /// <param name="scalar"></param>
    /// <param name="columnSum"></param>
    /// <returns>The sum of all of the multiplies</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply2Scalar1AndColumnSum(float[] destination, int destIndex, float[] first, int firstIndex,
        float[] second, int secondIndex, float scalar, float[] columnSum, int columnIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var scalarV = Vector512.Create(scalar);
            if ((destIndex | firstIndex | secondIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = Vector512.LoadUnsafe(ref first[i]) * Vector512.LoadUnsafe(ref second[i]) * scalarV;
                    Vector512.StoreUnsafe((Vector512.LoadUnsafe(ref columnSum[i]) + local), ref columnSum[i]);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = Vector512.LoadUnsafe(ref first[i + firstIndex]) * Vector512.LoadUnsafe(ref second[i + secondIndex]) * scalarV;
                    Vector512.StoreUnsafe(Vector512.LoadUnsafe(ref columnSum[i + columnIndex]) + local, ref columnSum[i + columnIndex]);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * scalar);
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> scalarV = new(scalar);
            if ((destIndex | firstIndex | secondIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i) * new Vector<float>(second, i) * scalarV;
                    (new Vector<float>(columnSum, i) + local).CopyTo(columnSum, i);
                    local.CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * scalarV;
                    (new Vector<float>(columnSum, i + columnIndex) + local).CopyTo(columnSum, i + columnIndex);
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * scalar);
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * scalar);
            }
        }
    }

    /// <summary>
    /// Multiply the two vectors and store the results in the destination.  Return a running sum.
    /// </summary>
    /// <param name="destination">Where to save the data</param>
    /// <param name="destIndex">What index to start at</param>
    /// <param name="first">The first array to multiply</param>
    /// <param name="firstIndex">The index to start at</param>
    /// <param name="second">The second array to multiply</param>
    /// <param name="secondIndex">The index to start at for the second array</param>
    /// <param name="columnIndex"></param>
    /// <param name="length">The amount of data to multiply</param>
    /// <param name="third"></param>
    /// <param name="thirdIndex"></param>
    /// <param name="scalar"></param>
    /// <param name="columnSum"></param>
    /// <returns>The sum of all of the multiplies</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply3Scalar1AndColumnSum(float[] destination, int destIndex, float[] first, int firstIndex,
        float[] second, int secondIndex, float[] third, int thirdIndex, float scalar, float[] columnSum, int columnIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var scalarV = Vector512.Create(scalar);
            if ((destIndex | firstIndex | secondIndex | thirdIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = Vector512.LoadUnsafe(ref first[i]) * Vector512.LoadUnsafe(ref second[i]) * Vector512.LoadUnsafe(ref third[i]) * scalarV;
                    Vector512.StoreUnsafe(Vector512.LoadUnsafe(ref columnSum[i]) + local, ref columnSum[i]);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * third[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = Vector512.LoadUnsafe(ref first[i + firstIndex]) * Vector512.LoadUnsafe(ref second[i + secondIndex]) * Vector512.LoadUnsafe(ref third[i + thirdIndex]) * scalarV;
                    Vector512.StoreUnsafe(Vector512.LoadUnsafe(ref columnSum[i + columnIndex]) + local, ref columnSum[i + columnIndex]);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * scalar);
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> scalarV = new(scalar);
            if ((destIndex | firstIndex | secondIndex | thirdIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i) * new Vector<float>(second, i) * new Vector<float>(third, i) * scalarV;
                    (new Vector<float>(columnSum, i) + local).CopyTo(columnSum, i);
                    local.CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * third[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * new Vector<float>(third, i + thirdIndex) * scalarV;
                    (new Vector<float>(columnSum, i + columnIndex) + local).CopyTo(columnSum, i + columnIndex);
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * scalar);
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * scalar);
            }
        }
    }

    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[] dest, float lhs, float[] rhs)
    {
        if (dest.Length != rhs.Length)
        {
            throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
        }
        if(Vector512.IsHardwareAccelerated)
        {
            int i;
            var zero = Vector512<float>.Zero;
            var one = Vector512<float>.One;
            var vValue = Vector512.Create(lhs);
            for (i = 0; i < rhs.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vData = Vector512.LoadUnsafe(ref rhs[i]);
                Vector512.StoreUnsafe(Vector512.ConditionalSelect(Vector512.LessThanOrEqual(vData, vValue), one, zero),
                    ref dest[i]);
            }
            for (; i < rhs.Length; i++)
            {
                dest[i] = lhs <= rhs[i] ? 1 : 0;
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            int i;
            Vector<float> zero = Vector<float>.Zero;
            Vector<float> one = Vector<float>.One;
            Vector<float> vValue = new(lhs);
            for (i = 0; i < rhs.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var vData = new Vector<float>(rhs, i);
                Vector.ConditionalSelect(Vector.LessThanOrEqual(vData, vValue), one, zero).CopyTo(dest, i);
            }
            for (; i < rhs.Length; i++)
            {
                dest[i] = lhs <= rhs[i] ? 1 : 0;
            }
        }
        else
        {
            for (int i = 0; i < rhs.Length; i++)
            {
                dest[i] = lhs <= rhs[i] ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// Set the value to one if the condition is met.
    /// </summary>
    public static void FlagIfLessThanOrEqual(float[] dest, float[] lhs, float rhs)
    {
        if (dest.Length != lhs.Length)
        {
            throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
        }
        if (Vector512.IsHardwareAccelerated)
        {
            int i;
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> one = Vector512<float>.One;
            Vector512<float> vValue = Vector512.Create(rhs);
            for (i = 0; i < lhs.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var vData = Vector512.LoadUnsafe(ref lhs[i]);
                Vector512.StoreUnsafe(Vector512.ConditionalSelect(Vector512.LessThanOrEqual(vData, vValue), one, zero),
                    ref dest[i]);
            }
            for (; i < lhs.Length; i++)
            {
                dest[i] = lhs[i] <= rhs ? 1 : 0;
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            int i;  
            Vector<float> zero = Vector<float>.Zero;
            Vector<float> one = Vector<float>.One;
            Vector<float> vValue = new(rhs);
            for (i = 0; i < lhs.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var vData = new Vector<float>(lhs, i);
                Vector.ConditionalSelect(Vector.LessThanOrEqual(vData, vValue), one, zero).CopyTo(dest, i);
            }
            for (; i < lhs.Length; i++)
            {
                dest[i] = lhs[i] <= rhs ? 1 : 0;
            }
        }
        else
        {
            for (int i = 0; i < lhs.Length; i++)
            {
                dest[i] = lhs[i] <= rhs ? 1 : 0;
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
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Average(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            Vector512<float> half = Vector512.Create(0.5f);
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    Vector512.StoreUnsafe((f + s) * half,
                        ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = (first[i] + second[i]) * 0.5f;
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var s = Vector512.LoadUnsafe(ref second[i + secondIndex]);
                    Vector512.StoreUnsafe((f + s) * half,
                        ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = (first[i + firstIndex] + second[i + secondIndex]) * 0.5f;
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> half = new(0.5f);
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    ((f + s) * half).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = (first[i] + second[i]) * 0.5f;
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i + firstIndex);
                    var s = new Vector<float>(second, i + secondIndex);
                    ((f + s) * half).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = (first[i + firstIndex] + second[i + secondIndex]) * 0.5f;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[i + destIndex] = (first[i + firstIndex] + second[i + secondIndex]) * 0.5f;
            }
        }
    }

    /// <summary>
    /// Produce a new vector selecting the original value if it is finite.  If it is not,
    /// select the alternative value.
    /// </summary>
    /// <param name="baseValues">The values to test for their finite property</param>
    /// <param name="alternateValues">The values to replace if the base value is not finite</param>
    /// <returns>A new vector containing the proper mix of the base and alternate values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> SelectIfFinite(Vector<float> baseValues, Vector<float> alternateValues)
    {
        //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
        return Vector.ConditionalSelect(
            Vector.BitwiseAnd(Vector.LessThanOrEqual(Vector.Abs(baseValues), MaxFloat), Vector.GreaterThanOrEqual(baseValues, baseValues)),
            baseValues, alternateValues
            );
    }

    /// <summary>
    /// Produce a new vector selecting the original value if it is finite.  If it is not,
    /// select the alternative value.
    /// </summary>
    /// <param name="baseValues">The values to test for their finite property</param>
    /// <param name="alternateValues">The values to replace if the base value is not finite</param>
    /// <returns>A new vector containing the proper mix of the base and alternate values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> SelectIfFinite(Vector512<float> baseValues, Vector512<float> alternateValues)
    {
        //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
        return Vector512.ConditionalSelect(
            Vector512.BitwiseAnd(Vector512.LessThanOrEqual(Vector512.Abs(baseValues), MaxFloat512), Vector512.GreaterThanOrEqual(baseValues, baseValues)),
            baseValues, alternateValues
            );
    }

    /// <summary>
    /// Produce a new vector selecting the original value if it is finite.  If it is not,
    /// select the alternative value.
    /// </summary>
    /// <param name="baseValues">The values to test for their finite property</param>
    /// <param name="alternateValues">The values to replace if the base value is not finite</param>
    /// <param name="minimumV"></param>
    /// <returns>A new vector containing the proper mix of the base and alternate values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> SelectIfFiniteAndLessThan(Vector<float> baseValues, Vector<float> alternateValues, Vector<float> minimumV)
    {
        //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
        return Vector.ConditionalSelect(
            Vector.BitwiseAnd(Vector.BitwiseAnd(Vector.LessThanOrEqual(Vector.Abs(baseValues), MaxFloat),
            Vector.GreaterThanOrEqual(baseValues, baseValues)), Vector.GreaterThanOrEqual(baseValues, minimumV)),
            baseValues, alternateValues
            );
    }

    /// <summary>
    /// Produce a new vector selecting the original value if it is finite.  If it is not,
    /// select the alternative value.
    /// </summary>
    /// <param name="baseValues">The values to test for their finite property</param>
    /// <param name="alternateValues">The values to replace if the base value is not finite</param>
    /// <param name="minimumV"></param>
    /// <returns>A new vector containing the proper mix of the base and alternate values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> SelectIfFiniteAndLessThan(Vector512<float> baseValues, Vector512<float> alternateValues, Vector512<float> minimumV)
    {
        //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
        return Vector512.ConditionalSelect(
            Vector512.BitwiseAnd(Vector512.BitwiseAnd(Vector512.LessThanOrEqual(Vector512.Abs(baseValues), MaxFloat512),
            Vector512.GreaterThanOrEqual(baseValues, baseValues)), Vector512.GreaterThanOrEqual(baseValues, minimumV)),
            baseValues, alternateValues
            );
    }

    /// <summary>
    /// Assign to an array replacing values if the base is NaN
    /// </summary>
    /// <param name="dest">The place to store the results</param>
    /// <param name="baseValue">The original values</param>
    /// <param name="replacementValue">The values to replace them with if the base is NaN</param>
    public static void ReplaceIfNaN(float[] dest, float[] baseValue, float[] replacementValue)
    {
        int i = 0;
        if(Vector512.IsHardwareAccelerated)
        {
            for (; i < dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var b = Vector512.LoadUnsafe(ref baseValue[i]);
                var r = Vector512.LoadUnsafe(ref replacementValue[i]);
                Vector512.StoreUnsafe(
                    Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(b, b), b, r), 
                    ref dest[i]);
            }
        }
        if (Vector.IsHardwareAccelerated)
        {
            for (; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var b = new Vector<float>(baseValue, i);
                var r = new Vector<float>(replacementValue, i);
                Vector.ConditionalSelect(Vector.GreaterThanOrEqual(b, b), b, r).CopyTo(dest, i);
            }
        }
        for (; i < dest.Length; i++)
        {
            dest[i] = !float.IsNaN(baseValue[i]) ? baseValue[i] : replacementValue[i];
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="destIndex"></param>
    /// <param name="alternateValue"></param>
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReplaceIfNotFinite(float[] destination, int destIndex, float alternateValue, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            var altV = Vector512.Create(alternateValue);
            if (destIndex == 0)
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref destination[i]), altV), ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (!float.IsFinite(destination[i]))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref destination[i + destIndex]), altV), ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (!float.IsFinite(destination[i + destIndex]))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var altV = new Vector<float>(alternateValue);
            if (destIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(destination, i), altV)).CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (!float.IsFinite(destination[i]))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(destination, i + destIndex), altV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (!float.IsFinite(destination[i + destIndex]))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                if (!float.IsFinite(destination[i + destIndex]))
                {
                    destination[i + destIndex] = alternateValue;
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="destIndex"></param>
    /// <param name="alternateValue"></param>
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReplaceIfNotFinite(float[] destination, int destIndex, float[] source, int sourceIndex, float alternateValue, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            var altV = Vector512.Create(alternateValue);
            if (destIndex == 0 && sourceIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref source[i]), altV), ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    destination[i] = !float.IsFinite(source[i]) ? alternateValue : source[i];
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref source[i + sourceIndex]), altV), ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue : source[i + sourceIndex];
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var altV = new Vector<float>(alternateValue);
            if (destIndex == 0 && sourceIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(source, i), altV)).CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = !float.IsFinite(source[i]) ? alternateValue : source[i];
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(source, i + sourceIndex), altV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue : source[i + sourceIndex];
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue : source[i + sourceIndex];
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="destIndex"></param>
    /// <param name="alternateValue"></param>
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReplaceIfNotFinite(float[] destination, int destIndex, float[] source, int sourceIndex, float[] alternateValue, int altIndex, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            if (destIndex == 0 && sourceIndex == 0 && altIndex == 0)
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var altV = Vector512.LoadUnsafe(ref alternateValue[i]);
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref source[i]), altV), ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = !float.IsFinite(source[i]) ? alternateValue[i] : source[i];
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var altV = Vector512.LoadUnsafe(ref alternateValue[i + altIndex]);
                    Vector512.StoreUnsafe(SelectIfFinite(Vector512.LoadUnsafe(ref source[i + sourceIndex]), altV), ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue[i + altIndex] : source[i + sourceIndex];
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            if (destIndex == 0 && sourceIndex == 0 && altIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var altV = new Vector<float>(alternateValue, i);
                    (SelectIfFinite(new Vector<float>(source, i), altV)).CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = !float.IsFinite(source[i]) ? alternateValue[i] : source[i];
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var altV = new Vector<float>(alternateValue, i + altIndex);
                    (SelectIfFinite(new Vector<float>(source, i + sourceIndex), altV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue[i + altIndex] : source[i + sourceIndex];
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[i + destIndex] = !float.IsFinite(source[i + sourceIndex]) ? alternateValue[i + altIndex] : source[i + sourceIndex];
            }
        }
    }

    public static void ReplaceIfLessThanOrNotFinite(float[] destination, int destIndex, float alternateValue, float minimum, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            var altV = Vector512.Create(alternateValue);
            var minimumV = Vector512.Create(minimum);
            if (destIndex == 0)
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = SelectIfFiniteAndLessThan(Vector512.LoadUnsafe(ref destination[i]), altV, minimumV);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (float.IsInfinity(destination[i]) || !(destination[i] >= minimum))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var local = SelectIfFiniteAndLessThan(Vector512.LoadUnsafe(ref destination[i + destIndex]), altV, minimumV);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (float.IsInfinity(destination[i + destIndex]) || !(destination[i + destIndex] >= minimum))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var altV = new Vector<float>(alternateValue);
            var minimumV = new Vector<float>(minimum);
            if (destIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFiniteAndLessThan(new Vector<float>(destination, i), altV, minimumV)).CopyTo(destination, i);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (float.IsInfinity(destination[i]) || !(destination[i] >= minimum))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFiniteAndLessThan(new Vector<float>(destination, i + destIndex), altV, minimumV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (float.IsInfinity(destination[i + destIndex]) || !(destination[i + destIndex] >= minimum))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                if (float.IsInfinity(destination[i + destIndex]) || !(destination[i + destIndex] >= minimum))
                {
                    destination[i + destIndex] = alternateValue;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyGreaterThan(float[] data, int dataIndex, float rhs, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            var rhsV = Vector512.Create(rhs);
            if (dataIndex == 0)
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    if (Vector512.GreaterThanAny(Vector512.LoadUnsafe(ref data[i]), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (data[i] > rhs)
                    {
                        return true;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    if (Vector512.GreaterThanAny(Vector512.LoadUnsafe(ref data[i + dataIndex]), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (data[i + dataIndex] > rhs)
                    {
                        return true;
                    }
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var rhsV = new Vector<float>(rhs);
            if (dataIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if (Vector.GreaterThanAny(new Vector<float>(data, i), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (data[i] > rhs)
                    {
                        return true;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if (Vector.GreaterThanAny(new Vector<float>(data, i + dataIndex), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (data[i + dataIndex] > rhs)
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                if (data[i + dataIndex] > rhs)
                {
                    return true;
                }
            }
        }
        return false;
    }


    public static bool AreBoundedBy(float[] data, int dataIndex, float baseNumber, float maxVarriation, int length)
    {
        if(Vector512.IsHardwareAccelerated)
        {
            var baseV = Vector512.Create(baseNumber);
            var maxmumVariationV = Vector512.Create(maxVarriation);
            if (dataIndex == 0)
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    if (Vector512.GreaterThanAny(Vector512.Abs(Vector512.LoadUnsafe(ref data[i]) - baseV), maxmumVariationV))
                    {
                        return false;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (Math.Abs(data[i] - baseNumber) > maxVarriation)
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    if (Vector512.GreaterThanAny(Vector512.Abs(Vector512.LoadUnsafe(ref data[i + dataIndex]) - baseV), maxmumVariationV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector512<float>.Count); i < length; i++)
                {
                    if (Math.Abs(data[i + dataIndex] - baseNumber) > maxVarriation)
                    {
                        return false;
                    }
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var baseV = new Vector<float>(baseNumber);
            var maxmumVariationV = new Vector<float>(maxVarriation);
            if (dataIndex == 0)
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if (Vector.GreaterThanAny(Vector.Abs(new Vector<float>(data, i) - baseV), maxmumVariationV))
                    {
                        return false;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (Math.Abs(data[i] - baseNumber) > maxVarriation)
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if (Vector.GreaterThanAny(Vector.Abs(new Vector<float>(data, i + dataIndex) - baseV), maxmumVariationV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for (int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if (Math.Abs(data[i + dataIndex] - baseNumber) > maxVarriation)
                    {
                        return false;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                if (Math.Abs(data[i + dataIndex] - baseNumber) > maxVarriation)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Computes the Arithmetic Geometric mean for the given values.
    /// </summary>
    /// <param name="x">The first parameter vector. This parameter must be non negative!</param>
    /// <param name="y">The second parameter vector. This parameter must be non negative!</param>
    /// <seealso>
    ///     <cref>https://en.wikipedia.org/wiki/Arithmetic–geometric_mean</cref>
    /// </seealso>
    /// <returns>The AGM for each element in the parameters</returns>
    public static Vector<float> ArithmeticGeometricMean(Vector<float> x, Vector<float> y)
    {
        var half = new Vector<float>(0.5f);
        var a = half * (x + y);
        var g = Vector.SquareRoot(x * y);
        // 5 expansions seems to be sufficient for 32-bit floating point numbers
        for (int i = 0; i < 5; i++)
        {
            var tempA = half * (a + g);
            g = Vector.SquareRoot(a * g);
            a = tempA;
        }
        return a;
    }

    /// <summary>
    /// Computes the Arithmetic Geometric mean for the given values.
    /// </summary>
    /// <param name="x">The first parameter vector. This parameter must be non negative!</param>
    /// <param name="y">The second parameter vector. This parameter must be non negative!</param>
    /// <seealso>
    ///     <cref>https://en.wikipedia.org/wiki/Arithmetic–geometric_mean</cref>
    /// </seealso>
    /// <returns>The AGM for each element in the parameters</returns>
    public static Vector512<float> ArithmeticGeometricMean(Vector512<float> x, Vector512<float> y)
    {
        var half = Vector512.Create(0.5f);
        var a = half * (x + y);
        var g = Vector512.Sqrt(x * y);
        // 5 expansions seems to be sufficient for 32-bit floating point numbers
        for (int i = 0; i < 5; i++)
        {
            var tempA = half * (a + g);
            g = Vector512.Sqrt(a * g);
            a = tempA;
        }
        return a;
    }

    /// <summary>
    /// Computes the natural logarithm for each element in x
    /// </summary>
    /// <param name="x">The values to compute the logarithms of</param>
    /// <returns>The vector of logarithms</returns>
    /// <see>
    ///     <cref>https://en.wikipedia.org/wiki/Natural_logarithm</cref>
    /// </see>
    public static Vector<float> Log(Vector<float> x)
    {
        var two = new Vector<float>(2.0f);
        var pi = new Vector<float>(MathF.PI);
        var mTimesln2 = new Vector<float>(0.693147181f * 16.0f);
        var denom = new Vector<float>(4.0f) / (x * new Vector<float>(65536.0f));
        return (pi / (two * ArithmeticGeometricMean(Vector<float>.One, denom))) - mTimesln2;
    }

    /// <summary>
    /// Computes the natural logarithm for each element in x
    /// </summary>
    /// <param name="x">The values to compute the logarithms of</param>
    /// <returns>The vector of logarithms</returns>
    /// <see>
    ///     <cref>https://en.wikipedia.org/wiki/Natural_logarithm</cref>
    /// </see>
    public static Vector512<float> Log(Vector512<float> x)
    {
        var two = Vector512.Create(2.0f);
        var pi = Vector512.Create(MathF.PI);
        var mTimesln2 = Vector512.Create(0.693147181f * 16.0f);
        var denom = Vector512.Create(4.0f) / (x * Vector512.Create(65536.0f));
        return (pi / (two * ArithmeticGeometricMean(Vector512<float>.One, denom))) - mTimesln2;
    }

    /// <summary>
    /// Applies log(x) for each element in the array and saves it into the destination.
    /// </summary>
    /// <param name="destination">Where to save the results.</param>
    /// <param name="destIndex">An offset into the array to start saving.</param>
    /// <param name="x">The vector to take the log of.</param>
    /// <param name="xIndex">The offset into the array to start from.</param>
    /// <param name="length">The number of elements to convert.</param>
    public static void Log(float[] destination, int destIndex, float[] x, int xIndex, int length)
    {
        for (int i = 0; i < length; i++)
        {
            destination[i + destIndex] = MathF.Log(x[i + xIndex]);
        }
    }

    public static void Negate(float[] dest, float[] source)
    {
        int i = 0;
        if(Vector512.IsHardwareAccelerated)
        {
            for (; i < dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var local = Vector512.Negate(Vector512.LoadUnsafe(ref source[i]));
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
        }
        if(Vector.IsHardwareAccelerated)
        {
            for (; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                Vector.Negate(new Vector<float>(source, i)).CopyTo(dest, i);
            }
        }
        for (; i < dest.Length; i++)
        {
            dest[i] = -source[i];
        }
    }
}
