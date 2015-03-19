/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TMG.Functions
{
    /// <summary>
    /// This class is designed to help facilitate the use of the SIMD instructions available in
    /// modern .Net.
    /// </summary>
    public static class VectorHelper
    {
        // Dummy code to get the JIT to startup with SIMD
        static Vector<float> _Unused;

        /// <summary>
        /// A vector containing the maximum value of a float
        /// </summary>
        private static Vector<float> MaxFloat;

        static VectorHelper()
        {
            _Unused = Vector<float>.One;
            MaxFloat = new Vector<float>(float.MaxValue);
        }

        /// <summary>
        /// Check to see if Vector code is allowed
        /// </summary>
        /// <returns>If true, then SIMD is enabled.</returns>
        public static bool IsHardwareAccelerated { get { return Vector.IsHardwareAccelerated; } }

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
        public static float VectorSum(float[] array, int startIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            int endIndex = startIndex + length;
            // copy everything we can do inside of a vector
            for(int i = 0; i <= endIndex - Vector<float>.Count; i += Vector<float>.Count)
            {
                acc += new Vector<float>(array, i + startIndex);
            }
            // copy the remainder
            for(int i = endIndex - (endIndex % Vector<float>.Count); i < endIndex; i++)
            {
                remainderSum += array[i];
            }
            return remainderSum + Sum(ref acc);
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
        public static float VectorAbsDiffAverage(float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - Vector<float>.Count;
                for(int i = 0; i <= highestForVector; i += Vector<float>.Count)
                {
                    acc += Vector.Abs(new Vector<float>(first, i) - new Vector<float>(second, i));
                }
            }
            else
            {
                int highestForVector = length - Vector<float>.Count + firstIndex;
                int s = secondIndex;
                for(int f = 0; f <= highestForVector; f += Vector<float>.Count)
                {
                    acc += Vector.Abs(new Vector<float>(first, f) - new Vector<float>(second, s));
                    s += Vector<float>.Count;
                }
            }
            // copy the remainder
            for(int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                remainderSum += Math.Abs(first[i + firstIndex] - second[i + secondIndex]);
            }
            return remainderSum + Sum(ref acc) / length;
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
        public static float VectorAbsDiffMax(float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            var remainderMax = 0.0f;
            var vectorMax = Vector<float>.Zero;
            if((firstIndex | secondIndex) == 0)
            {
                int highestForVector = length - Vector<float>.Count;
                for(int i = 0; i <= highestForVector; i += Vector<float>.Count)
                {
                    vectorMax = Vector.Max(Vector.Abs(new Vector<float>(first, i) - new Vector<float>(second, i)), vectorMax);
                }
            }
            else
            {
                int highestForVector = length - Vector<float>.Count + firstIndex;
                int s = secondIndex;
                for(int f = 0; f <= highestForVector; f += Vector<float>.Count)
                {
                    vectorMax = Vector.Max(Vector.Abs(new Vector<float>(first, f) - new Vector<float>(second, s)), vectorMax);
                    s += Vector<float>.Count;
                }
            }
            // copy the remainder
            for(int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                remainderMax = Math.Max(remainderMax, Math.Abs(first[i + firstIndex] - second[i + secondIndex]));
            }
            float[] temp = new float[Vector<float>.Count];
            for(int i = 0; i < temp.Length; i++)
            {
                remainderMax = Math.Max(temp[i], remainderMax);
            }
            return remainderMax;
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
        public static float VectorSquareDiff(float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var diff = new Vector<float>(first, i) - new Vector<float>(second, i);
                    acc += diff * diff;
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    var diff = first[i] - second[i];
                    remainderSum += diff * diff;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var diff = new Vector<float>(first, i + firstIndex) - new Vector<float>(second, i + secondIndex);
                    acc += diff * diff;
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    var diff = first[i + firstIndex] - second[i + secondIndex];
                    remainderSum += diff * diff;
                }
            }
            return remainderSum + Sum(ref acc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorMultiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            if((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) * new Vector<float>(second, i))
                        .CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = first[i] * second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex))
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorDivide(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            if((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) / new Vector<float>(second, i))
                        .CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = first[i] / second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) / new Vector<float>(second, i + secondIndex))
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] / second[i + secondIndex];
                }
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
        public static void VectorMultiply(float[] destination, int destIndex, float[] first, int firstIndex, float scalar, int length)
        {
            Vector<float> scalarV = new Vector<float>(scalar);
            if((destIndex | firstIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) * scalarV)
                        .CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = first[i] * scalar;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) * scalarV)
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * scalar;
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
        public static void VectorMultiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex,
            float[] third, int thirdIndex, float[] fourth, int fourthIndex, int length)
        {
            if((destIndex | firstIndex | secondIndex | thirdIndex | fourthIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) * new Vector<float>(second, i)
                        * new Vector<float>(third, i) * new Vector<float>(fourth, i))
                        .CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = first[i] * second[i] * third[i] * fourth[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex)
                        * new Vector<float>(third, i + thirdIndex) * new Vector<float>(fourth, i + fourthIndex))
                        .CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * fourth[i + fourthIndex];
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
        /// <param name="length">The amount of data to multiply</param>
        /// <returns>The sum of all of the multiplies</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VectorMultiplyAndSum(float[] destination, int destIndex, float[] first, int firstIndex,
            float[] second, int secondIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = (new Vector<float>(first, i) * new Vector<float>(second, i));
                    acc += local;
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += destination[i] = first[i] * second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex));
                    acc += local;
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Sum(ref acc);
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
        public static float VectorMultiplyAndSum(float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if((firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    acc += (new Vector<float>(first, i) * new Vector<float>(second, i));
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += first[i] * second[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    acc += (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex));
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex];
                }
            }
            return remainderSum + Sum(ref acc);
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
        public static float VectorMultiply3AndSum(float[] first, int firstIndex, float[] second, int secondIndex,
            float[] third, int thirdIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            if((firstIndex | secondIndex | thirdIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = (new Vector<float>(first, i) * new Vector<float>(second, i) * new Vector<float>(third, i));
                    acc += local;
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += first[i] * second[i] * third[i];
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * new Vector<float>(third, i + thirdIndex));
                    acc += local;
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    remainderSum += first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex];
                }
            }
            return remainderSum + Sum(ref acc);
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
        public static void VectorMultiply2Scalar1AndColumnSum(float[] destination, int destIndex, float[] first, int firstIndex,
            float[] second, int secondIndex, float scalar, float[] columnSum, int columnIndex, int length)
        {
            Vector<float> scalarV = new Vector<float>(scalar);
            if((destIndex | firstIndex | secondIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i) * new Vector<float>(second, i) * scalarV;
                    (new Vector<float>(columnSum, i) + local).CopyTo(columnSum, i);
                    local.CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * scalarV;
                    (new Vector<float>(columnSum, i + columnIndex) + local).CopyTo(columnSum, i + columnIndex);
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
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
        /// <param name="length">The amount of data to multiply</param>
        /// <returns>The sum of all of the multiplies</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorMultiply3Scalar1AndColumnSum(float[] destination, int destIndex, float[] first, int firstIndex,
            float[] second, int secondIndex, float[] third, int thirdIndex, float scalar, float[] columnSum, int columnIndex, int length)
        {
            Vector<float> scalarV = new Vector<float>(scalar);
            if((destIndex | firstIndex | secondIndex | thirdIndex | columnIndex) == 0)
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i) * new Vector<float>(second, i) * new Vector<float>(third, i) * scalarV;
                    (new Vector<float>(columnSum, i) + local).CopyTo(columnSum, i);
                    local.CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i] += (destination[i] = first[i] * second[i] * third[i] * scalar);
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var local = new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * new Vector<float>(third, i + thirdIndex) * scalarV;
                    (new Vector<float>(columnSum, i + columnIndex) + local).CopyTo(columnSum, i + columnIndex);
                    local.CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    columnSum[i + columnIndex] += (destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex] * scalar);
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
        public static void VectorAdd(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            if((destIndex | firstIndex | secondIndex) == 0)
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i) + new Vector<float>(second, i)).CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i] = first[i] + second[i];
                }
            }
            else
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (new Vector<float>(first, i + firstIndex) + new Vector<float>(second, i + secondIndex)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] + second[i + secondIndex];
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
            Vector<float> max = new Vector<float>(float.MaxValue);
            //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
            return Vector.ConditionalSelect(
                Vector.BitwiseAnd(Vector.LessThanOrEqual(Vector.Abs(baseValues), max), Vector.Equals(baseValues, baseValues)),
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
        public static Vector<float> SelectIfFiniteAndLessThan(Vector<float> baseValues, Vector<float> alternateValues, Vector<float> minimumV)
        {
            //If it is greater than the maximum value it is infinite, if it is not equal to itself it is NaN
            return Vector.ConditionalSelect(
                Vector.BitwiseAnd(Vector.BitwiseAnd(Vector.LessThanOrEqual(Vector.Abs(baseValues), MaxFloat), Vector.Equals(baseValues, baseValues)),Vector.GreaterThanOrEqual(baseValues, minimumV)),
                baseValues, alternateValues
                );
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
            var altV = new Vector<float>(alternateValue);
            if(destIndex == 0)
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(destination, i), altV)).CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(float.IsNaN(destination[i]) || float.IsInfinity(destination[i]))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFinite(new Vector<float>(destination, i + destIndex), altV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(float.IsNaN(destination[i + destIndex]) || float.IsInfinity(destination[i + destIndex]))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }

        public static void ReplaceIfLessThanOrNotFinite(float[] destination, int destIndex, float alternateValue, float minimum, int length)
        {
            var altV = new Vector<float>(alternateValue);
            var minimumV = new Vector<float>(minimum);
            if(destIndex == 0)
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFiniteAndLessThan(new Vector<float>(destination, i), altV, minimumV)).CopyTo(destination, i);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(float.IsInfinity(destination[i]) || !(destination[i] >= minimum))
                    {
                        destination[i] = alternateValue;
                    }
                }
            }
            else
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    (SelectIfFiniteAndLessThan(new Vector<float>(destination, i + destIndex), altV, minimumV)).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(float.IsInfinity(destination[i + destIndex]) || !(destination[i + destIndex] >= minimum))
                    {
                        destination[i + destIndex] = alternateValue;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyGreaterThan(float[] data, int dataIndex, float rhs, int length)
        {
            var rhsV = new Vector<float>(rhs);
            if(dataIndex == 0)
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if(Vector.GreaterThanAny(new Vector<float>(data, i), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(data[i] > rhs)
                    {
                        return true;
                    }
                }
            }
            else
            {
                for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    if(Vector.GreaterThanAny(new Vector<float>(data, i + dataIndex), rhsV))
                    {
                        return true;
                    }
                }
                // copy the remainder
                for(int i = length - (length % Vector<float>.Count); i < length; i++)
                {
                    if(data[i + dataIndex] > rhs)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
