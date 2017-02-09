/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using XTMF;
using TMG.Functions;

namespace TMG.Emme
{
    public struct EmmeMatrix
    {
        const uint EmmeMagicNumber = 0xC4D4F1B2;
        public enum DataType
        {
            Unknown = 0,
            Float = 1,
            Double = 2,
            SignedInteger = 3,
            UnsignedInteger = 4
        }
        public uint MagicNumber;
        public int Version;
        public DataType Type;
        public int Dimensions;
        public int[][] Indexes;
        public float[] FloatData;
        public double[] DoubleData;
        public int[] SignedIntData;
        public uint[] UnsignedIntData;

        public EmmeMatrix(BinaryReader reader) : this()
        {
            MagicNumber = reader.ReadUInt32();
            if (!IsValidHeader())
            {
                return;
            }
            Version = reader.ReadInt32();
            Type = (DataType)reader.ReadInt32();
            Dimensions = reader.ReadInt32();
            Indexes = new int[Dimensions][];
            for (int i = 0; i < Indexes.Length; i++)
            {
                Indexes[i] = new int[reader.ReadInt32()];
            }
            for (int i = 0; i < Indexes.Length; i++)
            {
                var row = Indexes[i];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = reader.ReadInt32();
                }
            }
            LoadData(reader.BaseStream);
        }

        public EmmeMatrix(SparseArray<IZone> zoneSystem, float[][] data)
        {
            var zones = zoneSystem.GetFlatData();
            MagicNumber = EmmeMagicNumber;
            Version = 1;
            Type = DataType.Float;
            Dimensions = 2;
            float[] temp = new float[zones.Length * zones.Length];
            Indexes = new int[2][];
            for (int i = 0; i < Indexes.Length; i++)
            {
                var row = Indexes[i] = new int[zones.Length];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = zones[j].ZoneNumber;
                }
            }
            for (int i = 0; i < data.Length; i++)
            {
                Array.Copy(data[i], 0, temp, i * zones.Length, zones.Length);
            }
            FloatData = temp;
            DoubleData = null;
            SignedIntData = null;
            UnsignedIntData = null;
        }

        public void Save(string fileLocation, bool checkForNaN)
        {
            if (checkForNaN)
            {
                bool any = false;
                switch (Type)
                {
                    case DataType.Float:
                        for (int i = 0; i < FloatData.Length; i++)
                        {
                            if (float.IsNaN(FloatData[i]))
                            {
                                any = true;
                                break;
                            }
                        }
                        break;
                    case DataType.Double:
                        for (int i = 0; i < DoubleData.Length; i++)
                        {
                            if (double.IsNaN(DoubleData[i]))
                            {
                                any = true;
                                break;
                            }
                        }
                        break;
                }
                if (any)
                {
                    throw new XTMFRuntimeException("The matrix being saved to '" + fileLocation + "' contains NaN values!");
                }
            }
            FileStream file = null;
            try
            {
                file = new FileStream(fileLocation, FileMode.Create);
                using (var writer = new BinaryWriter(file))
                {
                    file = null;
                    writer.Write(MagicNumber);
                    writer.Write(Version);
                    writer.Write((int)Type);
                    writer.Write(Dimensions);
                    byte[] temp = null;
                    for (int i = 0; i < Indexes.Length; i++)
                    {
                        writer.Write(Indexes[i].Length);
                    }
                    for (int i = 0; i < Indexes.Length; i++)
                    {
                        if (temp == null || temp.Length != Indexes[i].Length)
                        {
                            temp = new byte[sizeof(int) * Indexes[i].Length];
                        }
                        Buffer.BlockCopy(Indexes[i], 0, temp, 0, temp.Length);
                        writer.Write(temp, 0, temp.Length);
                    }
                    switch (Type)
                    {
                        case DataType.Float:
                            temp = new byte[FloatData.Length * sizeof(float)];
                            Buffer.BlockCopy(FloatData, 0, temp, 0, temp.Length);
                            break;
                        case DataType.Double:
                            temp = new byte[FloatData.Length * sizeof(double)];
                            Buffer.BlockCopy(DoubleData, 0, temp, 0, temp.Length);
                            break;
                        case DataType.SignedInteger:
                            temp = new byte[FloatData.Length * sizeof(int)];
                            Buffer.BlockCopy(SignedIntData, 0, temp, 0, temp.Length);
                            break;
                        case DataType.UnsignedInteger:
                            temp = new byte[FloatData.Length * sizeof(uint)];
                            Buffer.BlockCopy(UnsignedIntData, 0, temp, 0, temp.Length);
                            break;
                    }
                    if (temp == null)
                    {
                        throw new XTMFRuntimeException($"When saving an EMME matrix to {fileLocation} we tried had no data to write!");
                    }
                    writer.Write(temp);
                }
            }
            finally
            {
                file?.Dispose();
            }
        }



        private void LoadData(Stream baseStream)
        {
            int numberOfElements = 1;
            for (int i = 0; i < Indexes.Length; i++)
            {
                numberOfElements *= Indexes[i].Length;
            }
            switch (Type)
            {
                case DataType.Float:
                    {
                        var bytes = numberOfElements * sizeof(float);
                        var cb = new ConversionBuffer(bytes);
                        cb.FillFrom(baseStream);
                        FloatData = cb.FinalizeAsFloatArray(numberOfElements);
                    }
                    break;
                case DataType.Double:
                    {
                        var temp = new double[numberOfElements];
                        var raw = new byte[numberOfElements * sizeof(double)];
                        baseStream.Read(raw, 0, raw.Length);
                        Buffer.BlockCopy(raw, 0, temp, 0, raw.Length);
                        DoubleData = temp;
                    }
                    break;
                case DataType.SignedInteger:
                    {
                        var temp = new int[numberOfElements];
                        var raw = new byte[numberOfElements * sizeof(int)];
                        baseStream.Read(raw, 0, raw.Length);
                        Buffer.BlockCopy(raw, 0, temp, 0, raw.Length);
                        SignedIntData = temp;
                    }
                    break;
                case DataType.UnsignedInteger:
                    {
                        var temp = new uint[numberOfElements];
                        var raw = new byte[numberOfElements * sizeof(uint)];
                        baseStream.Read(raw, 0, raw.Length);
                        Buffer.BlockCopy(raw, 0, temp, 0, raw.Length);
                        UnsignedIntData = temp;
                    }
                    break;
            }
        }

        public bool IsValidHeader()
        {
            return MagicNumber == EmmeMagicNumber;
        }

    }
}
