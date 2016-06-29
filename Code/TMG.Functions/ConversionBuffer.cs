/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TMG.Functions
{
    [StructLayout(LayoutKind.Explicit, Pack = 2)]
    public struct ConversionBuffer
    {
        private static UIntPtr FloatType;
        private static UIntPtr DoubleType;
        private static UIntPtr ByteType;

        static ConversionBuffer()
        {
            unsafe
            {
                fixed (float* f = new float[1])
                {
                    Header* header = ((Header*)f) - 1;
                    FloatType = header->type;
                }
                fixed (double* d = new double[1])
                {
                    Header* header = ((Header*)d) - 1;
                    DoubleType = header->type;
                }
                fixed (byte* d = new byte[1])
                {
                    Header* header = ((Header*)d) - 1;
                    DoubleType = header->type;
                }
            }
        }

        private unsafe byte[] ConvertToByteArray(void* f, int length)
        {
            Header* header = ((Header*)f) - 1;
            header->length = (UIntPtr)(length);
            header->type = ByteType;
            return ByteData;
        }

        private unsafe byte[] ConvertToFloatArray(void* f, int length)
        {
            Header* header = ((Header*)f) - 1;
            header->length = (UIntPtr)(length);
            header->type = FloatType;
            return ByteData;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public UIntPtr type;
            public UIntPtr length;
        }
        [FieldOffset(0)]
        private byte[] ByteData;
        [FieldOffset(0)]
        internal float[] FloatData;
        [FieldOffset(0)]
        internal double[] DoubleData;

        public ConversionBuffer(int bytes)
        {
            FloatData = null;
            DoubleData = null;
            ByteData = new byte[bytes];
        }

        private ConversionBuffer(float[] array)
        {
            DoubleData = null;
            ByteData = null;
            FloatData = array;
        }

        public float[] FinalizeAsFloatArray(int size)
        {
            unsafe
            {
                fixed (float* f = FloatData)
                {
                    Header* header = ((Header*)f) - 1;
                    header->length = (UIntPtr)(size);
                    header->type = FloatType;
                }
            }
            return FloatData;
        }

        public float[] FinalizeAsDoubleArray(int numberOfElements)
        {
            unsafe
            {
                fixed (double* f = DoubleData)
                {
                    Header* header = ((Header*)f) - 1;
                    header->length = (UIntPtr)(numberOfElements);
                    header->type = FloatType;
                }
            }
            return FloatData;
        }

        public byte[] GetByteBuffer()
        {
            return ByteData;
        }

        public void FillFrom(Stream stream)
        {
            var data = ByteData;
            stream.Read(data, 0, data.Length);
        }
    }
}
