/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Functions
{
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

        static VectorHelper()
        {
            MaxFloat = new Vector<float>(float.MaxValue);
        }

        /// <summary>
        /// Add up the elements in the vector
        /// </summary>
        /// <param name="v">The vector to sum</param>
        /// <returns>The sum of the elements in the vector</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sum(ref Vector<float> v)
        {
            // shockingly to myself this is actually faster than doing a copy to an array
            // and manually computing the sum
            return Vector.Dot(v, Vector<float>.One);
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
                float[] temp = new float[Vector<float>.Count];
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
            {
                int i = 0;
                var vValue = new Vector<float>(value);
                for (; i < dest.Length - Vector<float>.Count; i++)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> scalarV = new Vector<float>(scalar);
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
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> scalarV = new Vector<float>(scalar);
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
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                if (dest.Length != rhs.Length)
                {
                    throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
                }
                Vector<float> zero = Vector<float>.Zero;
                Vector<float> one = Vector<float>.One;
                Vector<float> vValue = new Vector<float>(lhs);
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
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                if (dest.Length != lhs.Length)
                {
                    throw new ArgumentException("The size of the arrays are not the same!", nameof(dest));
                }
                Vector<float> zero = Vector<float>.Zero;
                Vector<float> one = Vector<float>.One;
                Vector<float> vValue = new Vector<float>(rhs);
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
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> half = new Vector<float>(0.5f);
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
        /// Assign to an array replacing values if the base is NaN
        /// </summary>
        /// <param name="dest">The place to store the results</param>
        /// <param name="baseValue">The original values</param>
        /// <param name="replacementValue">The values to replace them with if the base is NaN</param>
        public static void ReplaceIfNaN(float[] dest, float[] baseValue, float[] replacementValue)
        {
            int i = 0;
            for (; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var b = new Vector<float>(baseValue, i);
                var r = new Vector<float>(replacementValue, i);
                Vector.ConditionalSelect(Vector.GreaterThanOrEqual(b, b), b, r).CopyTo(dest, i);
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
            if (Vector.IsHardwareAccelerated)
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
                        if (float.IsNaN(destination[i]) || float.IsInfinity(destination[i]))
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
                        if (float.IsNaN(destination[i + destIndex]) || float.IsInfinity(destination[i + destIndex]))
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
                    if (float.IsNaN(destination[i + destIndex]) || float.IsInfinity(destination[i + destIndex]))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }

        public static void ReplaceIfLessThanOrNotFinite(float[] destination, int destIndex, float alternateValue, float minimum, int length)
        {
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            if (Vector.IsHardwareAccelerated)
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
            // This is still using scalar operations until we have an accurate replacement that runs faster when vectorized
            if (Vector.IsHardwareAccelerated)
            {
                if ((destIndex | xIndex) == 0)
                {
                    int i = 0;
                    for (; i < length; i++)
                    {
                        destination[i] = (float)Math.Exp(x[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        destination[i + destIndex] = (float)Math.Exp(x[i + xIndex]);
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    destination[i + destIndex] = (float)Math.Exp(x[i + xIndex]);
                }
            }
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
            var pi = new Vector<float>((float)Math.PI);
            var mTimesln2 = new Vector<float>(0.693147181f * 16.0f);
            var denom = new Vector<float>(4.0f) / (x * new Vector<float>(65536.0f));
            return (pi / (two * ArithmeticGeometricMean(Vector<float>.One, denom))) - mTimesln2;
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
                destination[i + destIndex] = (float)Math.Log(x[i + xIndex]);
            }
        }

        public static void Negate(float[] dest, float[] source)
        {
            int i = 0;
            for (; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                Vector.Negate(new Vector<float>(source, i)).CopyTo(dest, i);
            }
            for (; i < dest.Length; i++)
            {
                dest[i] = -source[i];
            }
        }

    }
}
