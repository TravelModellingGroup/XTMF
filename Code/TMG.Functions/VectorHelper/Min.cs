/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Functions;

public static partial class VectorHelper
{
    public static void Min(float[] dest, float[] source, float scalar)
    {
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var constant = Vector512.Create(scalar);

            // copy everything we can do inside of a vector
            for (; i <= source.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var dynamic = Vector512.LoadUnsafe(ref source[i]);
                var local = Vector512.Min(constant, dynamic);
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            Vector<float> constant = new(scalar);
            // copy everything we can do inside of a vector
            for (; i <= source.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var dynamic = new Vector<float>(source, i);
                Vector.Min(constant, dynamic).CopyTo(dest, i);
            }
        }

        for (; i < dest.Length; i++)
        {
            dest[i] = MathF.Min(source[i], scalar);
        }
    }

    public static void Min(float[] dest, float[] first, float[] second)
    {
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {

            // copy everything we can do inside of a vector
            for (; i <= first.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var firstV = Vector512.LoadUnsafe(ref first[i]);
                var secondV = Vector512.LoadUnsafe(ref second[i]);
                var local = Vector512.Min(firstV, secondV);
                Vector512.StoreUnsafe(local, ref dest[i]);
            }
        }
        else if (Vector.IsHardwareAccelerated)
        {
            // copy everything we can do inside of a vector
            for (; i <= first.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var firstV = new Vector<float>(first, i);
                var secondV = new Vector<float>(second, i);
                Vector.Min(firstV, secondV).CopyTo(dest, i);
            }
        }
        for (; i < dest.Length; i++)
        {
            dest[i] = MathF.Min(first[i], second[i]);
        }
    }

    public static float Min(float[] vector)
    {
        var ret = float.NegativeInfinity;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var retV = Vector512.Create(float.PositiveInfinity);
            // copy everything we can do inside of a vector
            for (; i <= vector.Length - Vector512<float>.Count; i += Vector512<float>.Count)
            {
                var firstV = Vector512.LoadUnsafe(ref vector[i]);
                retV = Vector512.Min(retV, firstV);
            }
            Vector256<float> half = Vector256.Min(retV.GetUpper(), retV.GetLower());
            Vector128<float> quarter = Vector128.Min(half.GetUpper(), half.GetLower());
            ret = MathF.Min(MathF.Min(quarter[0], quarter[1]), MathF.Min(quarter[2], quarter[3]));
        }
        else if (Vector.IsHardwareAccelerated)
        {
            var retV = new Vector<float>(float.PositiveInfinity);
            // copy everything we can do inside of a vector
            for (; i <= vector.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var firstV = new Vector<float>(vector, i);
                retV = Vector.Min(retV, firstV);
            }
            for (int j = 0; j <= Vector<float>.Count; j++)
            {
                ret = MathF.Min(ret, retV[j]);
            }
        }

        for (; i < vector.Length; i++)
        {
            ret = MathF.Min(ret, vector[i]);
        }
        return ret;
    }
}
