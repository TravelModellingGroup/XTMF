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
using System.Linq;
using System.IO.Compression;

namespace TMG.Emme;

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
        : this(zoneSystem.GetFlatData().Select(z => z.ZoneNumber).ToArray(), data)
    {
    }

    public EmmeMatrix(int[] zones, float[][] data)
    {
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
                row[j] = zones[j];
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
                throw new XTMFRuntimeException(null, "The matrix being saved to '" + fileLocation + "' contains NaN values!");
            }
        }
        FileStream file = null;
        try
        {
            file = new FileStream(fileLocation, FileMode.Create);
            Stream throughStream = (Path.GetExtension(fileLocation)
                ?.Equals(".gz", StringComparison.OrdinalIgnoreCase) == true) ?
                (Stream)new GZipStream(file, CompressionMode.Compress, false) : file;
            using var writer = new BinaryWriter(throughStream);
            file = null;
            SaveToStream(fileLocation, writer);
        }
        finally
        {
            file?.Dispose();
        }
    }

    private void SaveToStream(string fileLocation, BinaryWriter writer)
    {
        writer.Write(MagicNumber);
        writer.Write(Version);
        writer.Write((int)Type);
        writer.Write(Dimensions);
        for (int i = 0; i < Indexes.Length; i++)
        {
            writer.Write(Indexes[i].Length);
        }
        for (int i = 0; i < Indexes.Length; i++)
        {
            writer.Write(Indexes[i].AsSpan().ReinterpretSpan<int, byte>());
        }
        switch (Type)
        {
            case DataType.Float:
                writer.Write(FloatData.AsSpan().ReinterpretSpan<float, byte>());
                break;
            case DataType.Double:
                writer.Write(DoubleData.AsSpan().ReinterpretSpan<double, byte>());
                break;
            case DataType.SignedInteger:
                writer.Write(SignedIntData.AsSpan().ReinterpretSpan<int, byte>());
                break;
            case DataType.UnsignedInteger:
                writer.Write(UnsignedIntData.AsSpan().ReinterpretSpan<uint, byte>());
                break;
            default:
                throw new XTMFRuntimeException(null, $"When saving an EMME matrix to {fileLocation} we tried had no data to write!");
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
                    FloatData = new float[numberOfElements];
                    var byteSpan = FloatData.AsSpan().ReinterpretSpan<float, byte>();
                    baseStream.ReadExactly(byteSpan);
                }
                break;
            case DataType.Double:
                {
                    DoubleData = new double[numberOfElements];
                    var byteSpan = DoubleData.AsSpan().ReinterpretSpan<double, byte>();
                    baseStream.ReadExactly(byteSpan);
                }
                break;
            case DataType.SignedInteger:
                {
                    SignedIntData = new int[numberOfElements];
                    var byteSpan = SignedIntData.AsSpan().ReinterpretSpan<int, byte>();
                    baseStream.ReadExactly(byteSpan);
                }
                break;
            case DataType.UnsignedInteger:
                {
                    UnsignedIntData = new uint[numberOfElements];
                    var byteSpan = UnsignedIntData.AsSpan().ReinterpretSpan<uint, byte>();
                    baseStream.ReadExactly(byteSpan);
                }
                break;
        }
    }

    public bool IsValidHeader()
    {
        return MagicNumber == EmmeMagicNumber;
    }

}
