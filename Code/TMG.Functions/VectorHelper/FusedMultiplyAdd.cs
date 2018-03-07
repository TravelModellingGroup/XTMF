/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;

namespace TMG.Functions
{
    public static partial class VectorHelper
    {
        /// <summary>
        /// Dest[i] = hls[i] * rhs[i] + add
        /// </summary>
        public static void FusedMultiplyAdd(float[] dest, float[] lhs, float[] rhs, float add)
        {
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                var vAdd = new Vector<float>(add);
                for (i = 0; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var l = new Vector<float>(lhs, i);
                    var r = new Vector<float>(rhs, i);
                    (l * r + vAdd).CopyTo(dest, i);
                }
                for (; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs[i] + add;
                }
            }
            else
            {
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs[i] + add;
                }
            }
        }

        /// <summary>
        /// Dest[i] = hls[i] * rhs + add
        /// </summary>
        public static void FusedMultiplyAdd(float[] dest, float[] lhs, float rhs, float add)
        {
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                var vAdd = new Vector<float>(add);
                var r = new Vector<float>(rhs);
                for (i = 0; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var l = new Vector<float>(lhs, i);

                    (l * r + vAdd).CopyTo(dest, i);
                }
                for (; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs + add;
                }
            }
            else
            {
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs + add;
                }
            }
        }

        /// <summary>
        /// Dest[i] = hls[i] * rhs + add[i]
        /// </summary>
        public static void FusedMultiplyAdd(float[] dest, float[] lhs, float rhs, float[] add)
        {
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                var r = new Vector<float>(rhs);
                for (i = 0; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var l = new Vector<float>(lhs, i);
                    var vAdd = new Vector<float>(add, i);
                    (l * r + vAdd).CopyTo(dest, i);
                }
                for (; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs + add[i];
                }
            }
            else
            {
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs + add[i];
                }
            }
        }

        /// <summary>
        /// Dest[i] = hls[i] * rhs[i] + add[i]
        /// </summary>
        public static void FusedMultiplyAdd(float[] dest, float[] lhs, float[] rhs, float[] add)
        {
            if (Vector.IsHardwareAccelerated)
            {
                int i;
                for (i = 0; i < dest.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var l = new Vector<float>(lhs, i);
                    var r = new Vector<float>(rhs, i);
                    var vAdd = new Vector<float>(add, i);
                    (l * r + vAdd).CopyTo(dest, i);
                }
                for (; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs[i] + add[i];
                }
            }
            else
            {
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = lhs[i] * rhs[i] + add[i];
                }
            }
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add
        /// </summary>
        public static void FusedMultiplyAdd(float[][] dest, float[][] lhs, float[][] rhs, float add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add
        /// </summary>
        public static void FusedMultiplyAdd(float[][] dest, float[][] lhs, float rhs, float add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add[i][j]
        /// </summary>
        public static void FusedMultiplyAdd(float[][] dest, float[][] lhs, float rhs, float[][] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add[i][j]
        /// </summary>
        public static void FusedMultiplyAdd(float[][] dest, float[][] lhs, float[][] rhs, float[][] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add[i]
        /// </summary>
        public static void FusedMultiplyAddVerticalAdd(float[][] dest, float[][] lhs, float[][] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs + add[j]
        /// </summary>
        public static void FusedMultiplyAddHorizontalAdd(float[][] dest, float[][] lhs, float rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs + add[i]
        /// </summary>
        public static void FusedMultiplyAddVerticalAdd(float[][] dest, float[][] lhs, float rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i][j] + add[j]
        /// </summary>
        public static void FusedMultiplyAddHorizontalAdd(float[][] dest, float[][] lhs, float[][] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i] + add
        /// </summary>
        public static void FusedMultiplyAddVerticalRhs(float[][] dest, float[][] lhs, float[] rhs, float add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[j] + add
        /// </summary>
        public static void FusedMultiplyAddHorizontalRhs(float[][] dest, float[][] lhs, float[] rhs, float add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i] + add[i][j]
        /// </summary>
        public static void FusedMultiplyAddVerticalRhs(float[][] dest, float[][] lhs, float[] rhs, float[][] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[j] + add[i][j]
        /// </summary>
        public static void FusedMultiplyAddHorizontalRhs(float[][] dest, float[][] lhs, float[] rhs, float[][] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i] + add[i]
        /// </summary>
        public static void FusedMultiplyAddVerticalRhsVerticalAdd(float[][] dest, float[][] lhs, float[] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[j] + add[i]
        /// </summary>
        public static void FusedMultiplyAddHorizontalRhsVerticalAdd(float[][] dest, float[][] lhs, float[] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add[i]);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[i] + add[j]
        /// </summary>
        public static void FusedMultiplyAddVerticalRhsHorizontalAdd(float[][] dest, float[][] lhs, float[] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs[i], add);
            });
        }

        /// <summary>
        /// Dest[i][j] = hls[i][j] * rhs[j] + add[j]
        /// </summary>
        public static void FusedMultiplyAddHorizontalRhsHorizontalAdd(float[][] dest, float[][] lhs, float[] rhs, float[] add)
        {
            Parallel.For(0, dest.Length, i =>
            {
                FusedMultiplyAdd(dest[i], lhs[i], rhs, add);
            });
        }
    }
}
