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

        static VectorHelper()
        {
            _Unused = Vector<float>.One;
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
        private static float Sum(this Vector<float> v)
        {
            float[] tempSpace = new float[Vector<float>.Count];
            var sum = 0.0f;
            v.CopyTo(tempSpace);
            for(int i = 0; i < tempSpace.Length; i++)
            {
                sum += tempSpace[i];
            }
            return sum;
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
            return remainderSum + acc.Sum();
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
            return remainderSum + acc.Sum();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorMultiply(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
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
        public static float VectorMultiplyAndSum(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
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
            return remainderSum + acc.Sum();
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
            // copy everything we can do inside of a vector
            for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var local = (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex));
                acc += local;
            }
            // copy the remainder
            for(int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                remainderSum += first[i + firstIndex] * second[i + secondIndex];
            }
            return remainderSum + acc.Sum();
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
        public static float VectorMultiply3AndSum(float[] first, int firstIndex, float[] second, int secondIndex, float[] third, int thirdIndex, int length)
        {
            var remainderSum = 0.0f;
            var acc = Vector<float>.Zero;
            // copy everything we can do inside of a vector
            for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var local = (new Vector<float>(first, i + firstIndex) * new Vector<float>(second, i + secondIndex) * new Vector<float>(second, i + thirdIndex));
                acc += local;
            }
            // copy the remainder
            for(int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                remainderSum += first[i + firstIndex] * second[i + secondIndex] * third[i + thirdIndex];
            }
            return remainderSum + acc.Sum();
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
            for(int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
            {
                (new Vector<float>(first, i + firstIndex) + new Vector<float>(second, i + secondIndex)).CopyTo(destination, i + destIndex);
            }
            // copy the remainder
            for(int i = length - (length % Vector<float>.Count); i < length; i++)
            {
                destination[i + destIndex] = first[i + firstIndex] * second[i + secondIndex];
            }
        }
    }
}
