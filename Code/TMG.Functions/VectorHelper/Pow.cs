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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TMG.Functions
{
    public static partial class VectorHelper
    {
        public static void Pow(float[] flat, float[] lhs, float rhs)
        {
            // Vectorize this when possible
            for (int i = 0; i < flat.Length; i++)
            {
                flat[i] = (float)Math.Pow(lhs[i], rhs);
            }
        }

        public static void Pow(float[] flat, float lhs, float[] rhs)
        {
            // Vectorize this when possible
            for (int i = 0; i < flat.Length; i++)
            {
                flat[i] = (float)Math.Pow(lhs, rhs[i]);
            }
        }

        public static void Pow(float[] flat, float[] lhs, float[] rhs)
        {
            // Vectorize this when possible
            for (int i = 0; i < flat.Length; i++)
            {
                flat[i] = (float)Math.Pow(lhs[i], rhs[i]);
            }
        }

        public static void Pow(float[][] flat, float[][] lhs, float[][] rhs)
        {
            // Vectorize this when possible
            Parallel.For(0, flat.Length, (int i) =>
            {
                Pow(flat[i], lhs[i], rhs[i]);
            });
        }

        public static void Pow(float[][] flat, float[][] lhs, float rhs)
        {
            // Vectorize this when possible
            Parallel.For(0, flat.Length, (int i) =>
            {
                Pow(flat[i], lhs[i], rhs);
            });
        }

        public static void Pow(float[][] flat, float lhs, float[][] rhs)
        {
            // Vectorize this when possible
            Parallel.For(0, flat.Length, (int i) =>
            {
                Pow(flat[i], lhs, rhs[i]);
            });
        }
    }
}
