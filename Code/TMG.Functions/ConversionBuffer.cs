/*
    Copyright 2016-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Runtime.InteropServices;

namespace TMG.Functions
{
    [StructLayout(LayoutKind.Explicit, Pack = 2)]
    public struct ConversionBuffer
    {
        private static readonly UIntPtr FloatType;
        private static readonly UIntPtr DoubleType;
        private static readonly UIntPtr ByteType;

        static ConversionBuffer()
        {
            unsafe
            {
                fixed (float* f = new float[1])
                {
                    var header = ((Header*)f) - 1;
                    FloatType = header->type;
                }
                fixed (double* d = new double[1])
                {
                    var header = ((Header*)d) - 1;
                    DoubleType = header->type;
                }
                fixed (byte* d = new byte[1])
                {
                    var header = ((Header*)d) - 1;
                    ByteType = header->type;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public UIntPtr type;
            public UIntPtr length;
        }
        [FieldOffset(0)]
        private readonly byte[] ByteData;
        [FieldOffset(0)]
        internal readonly float[] FloatData;
        [FieldOffset(0)]
        internal readonly double[] DoubleData;

        public ConversionBuffer(int bytes)
        {
            FloatData = null;
            DoubleData = null;
            ByteData = new byte[bytes];
        }

        public float[] FinalizeAsFloatArray(int size)
        {
            unsafe
            {
                fixed (float* f = FloatData)
                {
                    var header = ((Header*)f) - 1;
                    header->length = (UIntPtr)(size);
                    header->type = FloatType;
                }
            }
            return FloatData;
        }

        public float[] FinalizeAsDoubleArray(int size)
        {
            unsafe
            {
                fixed (double* f = DoubleData)
                {
                    var header = ((Header*)f) - 1;
                    header->length = (UIntPtr)(size);
                    header->type = DoubleType;
                }
            }
            return FloatData;
        }

        public byte[] GetByteBuffer(int size)
        {
            unsafe
            {
                fixed (double* f = DoubleData)
                {
                    var header = ((Header*)f) - 1;
                    header->length = (UIntPtr)(size);
                    header->type = ByteType;
                }
            }
            return ByteData;
        }

        public void FillFrom(Stream stream)
        {
            var data = ByteData;
            stream.Read(data, 0, data.Length);
        }
    }
}
