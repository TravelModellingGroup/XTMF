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

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;

namespace TMG.Functions;

public static partial class VectorHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Divide(float[] destination, int destIndex, float[] first, int firstIndex, float[] second, int secondIndex, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var s = Vector512.LoadUnsafe(ref second[i]);
                    var local = (f / s);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] / second[i];
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
                    var local = (f / s);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] / second[i + secondIndex];
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            if ((destIndex | firstIndex | secondIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);
                    var s = new Vector<float>(second, i);
                    (f / s).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] / second[i];
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
                    (f / s).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] / second[i + secondIndex];
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[destIndex + i] = first[firstIndex + i] / second[secondIndex + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Divide(float[] destination, int destIndex, float[] first, int firstIndex, float denominator, int length)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var s = Vector512.Create(denominator);
            if ((destIndex | firstIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i]);
                    var local = (f / s);
                    Vector512.StoreUnsafe(local, ref destination[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] / denominator;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var f = Vector512.LoadUnsafe(ref first[i + firstIndex]);
                    var local = (f / s);
                    Vector512.StoreUnsafe(local, ref destination[i + destIndex]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] / denominator;
                }
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var s = new Vector<float>(denominator);
            if ((destIndex | firstIndex) == 0)
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i);

                    (f / s).CopyTo(destination, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i] = first[i] / denominator;
                }
            }
            else
            {
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var f = new Vector<float>(first, i + firstIndex);
                    (f / s).CopyTo(destination, i + destIndex);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    destination[i + destIndex] = first[i + firstIndex] / denominator;
                }
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                destination[destIndex + i] = first[firstIndex + i] / denominator;
            }
        }
    }

    public static void Divide(float[][] destination, float numerator, float[][] denominator)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var n = Vector512.Create(numerator);
                var dest = destination[row];
                var length = dest.Length;
                var denom = denominator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var d = Vector512.LoadUnsafe(ref denom[i]);
                    var local = (n / d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = numerator / denom[i];
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                Vector<float> n = new(numerator);
                var dest = destination[row];
                var length = dest.Length;
                var denom = denominator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var d = new Vector<float>(denom, i);
                    (n / d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = numerator / denom[i];
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = numerator / denominator[i][j];
                }
            });
        }
    }

    public static void Divide(float[][] destination, float[][] numerator, float denominator)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var d = Vector512.Create(denominator);
                var dest = destination[row];
                var length = dest.Length;
                var num = numerator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var n = Vector512.LoadUnsafe(ref num[i]);
                    var local = (n / d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] / denominator;
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                Vector<float> d = new(denominator);
                var dest = destination[row];
                var length = dest.Length;
                var num = numerator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var n = new Vector<float>(num, i);
                    (n / d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] / denominator;
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = numerator[i][j] / denominator;
                }
            });
        }
    }

    public static void Divide(float[][] destination, float[][] numerator, float[][] denominator)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var dest = destination[row];
                var length = dest.Length;
                var num = numerator[row];
                var denom = denominator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector512<float>.Count; i += Vector512<float>.Count)
                {
                    var n = Vector512.LoadUnsafe(ref num[i]);
                    var d = Vector512.LoadUnsafe(ref denom[i]);
                    var local = (n / d);
                    Vector512.StoreUnsafe(local, ref dest[i]);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] / denom[i];
                }
            });
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Parallel.For(0, destination.Length, row =>
            {
                var dest = destination[row];
                var length = dest.Length;
                var num = numerator[row];
                var denom = denominator[row];
                // copy everything we can do inside of a vector
                int i = 0;
                for (; i <= length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var n = new Vector<float>(num, i);
                    var d = new Vector<float>(denom, i);
                    (n / d).CopyTo(dest, i);
                }
                // copy the remainder
                for (; i < length; i++)
                {
                    dest[i] = num[i] / denom[i];
                }
            });
        }
        else
        {
            Parallel.For(0, destination.Length, i =>
            {
                for (int j = 0; j < destination[i].Length; j++)
                {
                    destination[i][j] = numerator[i][j] / denominator[i][j];
                }
            });
        }
    }

    public static void Divide(float[] dest, float[] lhs, float rhs)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> rhsV = Vector512.Create(rhs);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var lhsV = Vector512.LoadUnsafe(ref lhs[i]);
                var local = (lhsV / rhsV);
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
            // copy the remainder
            for (; i < lhs.Length; i++)
            {
                dest[i] = lhs[i] / rhs;
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> rhsV = new(rhs);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var lhsV = new Vector<float>(lhs, i);
                (lhsV / rhsV).CopyTo(dest, i);
            }
            // copy the remainder
            for (; i < lhs.Length; i++)
            {
                dest[i] = lhs[i] / rhs;
            }
        }
        else
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = lhs[i] / rhs;
            }
        }
    }

    public static void Divide(float[] dest, float lhs, float[] rhs)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var lhsV = Vector512.Create(lhs);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var rhsV = Vector512.LoadUnsafe(ref rhs[i]);
                var local = (lhsV / rhsV);
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
            // copy the remainder
            for (; i < rhs.Length; i++)
            {
                dest[i] = lhs / rhs[i];
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> lhsV = new(lhs);

            // copy everything we can do inside of a vector
            int i = 0;
            for (; i <= dest.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var rhsV = new Vector<float>(rhs, i);
                (lhsV / rhsV).CopyTo(dest, i);
            }
            // copy the remainder
            for (; i < rhs.Length; i++)
            {
                dest[i] = lhs / rhs[i];
            }
        }
        else
        {
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = lhs / rhs[i];
            }
        }
    }
}
