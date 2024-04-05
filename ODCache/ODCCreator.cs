/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Datastructure;

[XmlRootAttribute("Settings")]
public class CacheGenerationInfo
{
    [XmlArray("FileInfo")]
    public List<DimensionInfo> CacheInfo;

    [XmlElement]
    public int Gap;

    [XmlElement]
    public int HighestZone;

    [XmlElement]
    public int Times;

    [XmlElement]
    public int Types;

    public CacheGenerationInfo()
    {
        CacheInfo = [];
    }

    public CacheGenerationInfo(int times, int types, int highestZone, int gap)
        : this()
    {
        Times = times;
        Types = types;
        HighestZone = highestZone;
        Gap = gap;
    }

    public void Add(DimensionInfo info)
    {
        CacheInfo.Add(info);
    }
}

public class DimensionInfo
{
    [XmlAttribute]
    public string FileName;

    [XmlAttribute]
    public bool Header;

    [XmlAttribute]
    public bool Is311;

    [XmlAttribute]
    public bool SaveInTimes;

    [XmlAttribute]
    public int TimeIndex;

    [XmlAttribute]
    public int TypeIndex;

    public DimensionInfo(string filename, int typeindex, int timeindex, bool is311, bool header, bool saveInTimes)
    {
        FileName = Path.GetFileName(filename);
        TypeIndex = typeindex;
        TimeIndex = timeindex;
        Is311 = is311;
        Header = header;
        SaveInTimes = saveInTimes;
    }

    public DimensionInfo()
    {
    }
}

/// <summary>
/// This class helps you create an ODCache file
/// </summary>
public class OdcCreator
{
    public int LoadingO;

    public int Times;

    public int Types;

    private int AmmountOfData;

    private float[][][] Data;

    private CacheGenerationInfo FilesLoaded;

    private bool[][] HasData;

    private int NumberOfSubs;

    /// <summary>Create a new ODC</summary>
    /// <param name="times">How many time periods to account for</param>
    /// <param name="highestZone"></param>
    /// <param name="types">How many different types of data to store per OD</param>
    /// <param name="gap"></param>
    public OdcCreator(int highestZone, int types, int times, int gap)
    {
        FilesLoaded = new CacheGenerationInfo(times, types, highestZone, gap);
        AmmountOfData = types * times;
        Data = new float[highestZone][][];
        HasData = new bool[highestZone][];
        Types = types;
        Times = times;
        HighestZone = highestZone;
    }

    public OdcCreator()
    {
    }

    public OdcCreator(string odcFile)
    {
        LoadFile(odcFile);
    }

    public int HighestZone { get; private set; }

    public static void CreateOdc(string fileName, string xmlTemplate, string dataDirectory)
    {
        XmlSerializer deserializer = new(typeof(CacheGenerationInfo));

        TextReader textReader = new StreamReader(xmlTemplate);

        CacheGenerationInfo info = (CacheGenerationInfo)deserializer.Deserialize(textReader);
        textReader.Close();

        OdcCreator odcCreator = new(info.HighestZone, info.Types, info.Times, info.Gap);

        foreach (var dimensionInfo in info.CacheInfo)
        {
            string fname = Path.Combine(dataDirectory, dimensionInfo.FileName);

            if (dimensionInfo.Is311)
            {
                odcCreator.LoadEmme2(fname, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
            }
            else
            {
                if (dimensionInfo.SaveInTimes)
                {
                    odcCreator.LoadCsvTimes(fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
                }
                else
                {
                    odcCreator.LoadCsvTypes(fname, dimensionInfo.Header, dimensionInfo.TimeIndex, dimensionInfo.TypeIndex);
                }
            }
        }
        odcCreator.Save(fileName, false);
    }

    /// <summary>
    /// Converts a csv file into odc.
    /// Multiple entries are stored as different types.
    /// </summary>
    /// <param name="csv">The CSV file to read</param>
    /// <param name="header">Does the CSV have a header?</param>
    /// <param name="offsetTimes">The offset into the times</param>
    /// <param name="offsetType">Should we offset the CSV's information in the types?</param>
    public void LoadCsvTimes(string csv, bool header, int offsetTimes, int offsetType)
    {
        // Gain access to the files
        StreamReader reader = new(new
            FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read,
            0x1000, FileOptions.SequentialScan));

        FilesLoaded.Add(new DimensionInfo(csv, offsetType, offsetTimes, false, header, true));

        string line;
        int injectIndex = Times * offsetType + offsetTimes;
        if (header) reader.ReadLine();
        // Read the line from the CSV
        while ((line = reader.ReadLine()) != null)
        {
            // Calculate where to store the data
            int pos, next;
            int o = FastParse.ParseInt(line, 0, (pos = line.IndexOf(',')));
            int d = FastParse.ParseInt(line, pos + 1, (next = line.IndexOf(',', pos + 1)));
            pos = next + 1;
            int length = line.Length;
            // if we haven't stored anything for this O before, load some memory for it now
            if (Data[o] == null)
            {
                Data[o] = new float[HighestZone][];
                HasData[o] = new bool[HighestZone];
                for (int k = HighestZone - 1; k >= 0; k--)
                {
                    Data[o][k] = new float[AmmountOfData];
                }
                for (int k = 0; k < HighestZone; k++) HasData[o][k] = false;
            }
            int entry = 0;
            for (int i = pos; i < length; i++)
            {
                if (line[i] == ',')
                {
                    float num = FastParse.ParseFixedFloat(line, pos, i - pos);
                    Data[o][d][injectIndex + entry] = num;
                    HasData[o][d] = true;
                    entry++;
                    pos = i + 1;
                }
            }
            if (pos < length)
            {
                Data[o][d][injectIndex + entry] = FastParse.ParseFixedFloat(line, pos, length - pos);
            }
        }
        // Close our access to the file streams
        reader.Close();
    }

    /// <summary>
    /// Converts a csv file into odc.
    /// </summary>
    /// <param name="csv">The CSV file to read</param>
    /// <param name="header">Does the CSV have a header?</param>
    public void LoadCsvTypes(string csv, bool header)
    {
        LoadCsvTypes(csv, header, 0, 0);
    }

    /// <param name="csv"></param>
    /// <param name="header">Does the CSV have a header?</param>
    /// <param name="offset">Should we offset the CSV's information in the types?</param>
    public void LoadCsvTypes(string csv, bool header, int offset)
    {
        LoadCsvTypes(csv, header, 0, offset);
    }

    /// <summary>
    /// Converts a csv file into odc.
    /// Multiple entries are stored as different types.
    /// </summary>
    /// <param name="csv">The CSV file to read</param>
    /// <param name="header">Does the CSV have a header?</param>
    /// <param name="offsetTimes">The offset into the times</param>
    /// <param name="offsetType">Should we offset the CSV's information in the types?</param>
    public void LoadCsvTypes(string csv, bool header, int offsetTimes, int offsetType)
    {
        // Gain access to the files
        StreamReader reader = new(new
            FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read,
            0x1000, FileOptions.SequentialScan));

        FilesLoaded.Add(new DimensionInfo(csv, offsetType, offsetTimes, false, header, false));

        string line;
        int injectIndex = Times * offsetType + offsetTimes;
        if (header) reader.ReadLine();
        // Read the line from the CSV

        while ((line = reader.ReadLine()) != null)
        {
            // Calculate where to store the data
            string[] data = line.Split(',');
            int o = int.Parse(data[0]);
            int d = int.Parse(data[1]);
            //int d = CsvParse.ParseInt(line, pos + 1, (next = line.IndexOf(',', pos + 1)));
            //pos = next + 1;
            //int length = line.Length;
            // if we haven't stored anything for this O before, load some memory for it now
            if (Data[o] == null)
            {
                Data[o] = new float[HighestZone][];
                for (int k = HighestZone - 1; k >= 0; k--)
                {
                    Data[o][k] = new float[AmmountOfData];
                }
                HasData[o] = new bool[HighestZone];
                for (int k = 0; k < HighestZone; k++) HasData[o][k] = false;
            }
            int entry = 0;
            for (int i = 2; i < data.Length; i++)
            {
                if (data[i] == "") break;

                Data[o][d][injectIndex + entry] = float.Parse(data[i]);
                HasData[o][d] = true;
                entry++;
            }
        }
        // Close our access to the file streams
        reader.Close();
    }

    /// <summary>
    ///  Loads the data from an emme2 311 file into a ODC
    /// </summary>
    /// <param name="emme2File">The emm2 file to read from</param>
    /// <param name="offset">The type offset to use</param>
    public void LoadEmme2(string emme2File, int offset)
    {
        LoadEmme2(emme2File, 0, offset);
    }

    /// <summary>
    ///  Loads the data from an emme2 311 file into a ODC
    /// </summary>
    /// <param name="emme2File">The emm2 file to read from</param>
    /// <param name="offsetTimes">The time offset to use</param>
    /// <param name="offsetType">The type offset to use</param>
    public void LoadEmme2(string emme2File, int offsetTimes, int offsetType)
    {
        string line;
        int pos;

        // do this because highest zone isn't high enough for array indexes
        HighestZone += 1;
        using StreamReader reader = new(new
            FileStream(emme2File, FileMode.Open, FileAccess.Read, FileShare.Read,
            0x1000, FileOptions.SequentialScan));
        FilesLoaded.Add(new DimensionInfo(emme2File, offsetType, offsetTimes, true, false, false));

        int injectIndex = Times * offsetType + offsetTimes;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length > 0 && line[0] == 'a') break;
        }
        while ((line = reader.ReadLine()) != null)
        {
            int length = line.Length;
            // don't read blank lines
            if (length < 7) continue;
            int o = FastParse.ParseFixedInt(line, 0, 7);
            if (o > Data.Length) continue;
            if (Data[o] == null)
            {
                Data[o] = new float[HighestZone][];
                for (int k = HighestZone - 1; k >= 0; k--)
                {
                    Data[o][k] = new float[AmmountOfData];
                }
                HasData[o] = new bool[HighestZone];
                for (int i = 0; i < HighestZone; i++) HasData[o][i] = false;
            }
            pos = 7;
            while (pos + 13 <= length)
            {
                int d = FastParse.ParseFixedInt(line, pos, 7);
                if (d < Data.Length)
                {
                    if (line[pos + 7] == ':')
                    {
                        Data[o][d][injectIndex] = FastParse.ParseFixedFloat(line, pos + 8, 5);
                    }
                    else
                    {
                        Data[o][d][injectIndex] = FastParse.ParseFixedFloat(line, pos + 8, 9);
                    }
                    HasData[o][d] = true;
                }
                pos += 13;
            }
        }
    }

    public void LoadFile(string odcFile)
    {
        OdCache cache = new(odcFile);
        FilesLoaded = new CacheGenerationInfo(cache.Times, cache.Types, cache.HighestZone, 5);
        Times = cache.Times;
        Types = cache.Types;
        AmmountOfData = Types * Times;
        HighestZone = cache.HighestZone;
        Data = new float[HighestZone][][];
        HasData = new bool[HighestZone][];
        cache.DumpToCreator(this);
        cache.Release();
    }

    /// <summary>
    /// Save the ODC to file
    /// </summary>
    /// <param name="fileName">The file to save this as.</param>
    /// <param name="xmlInfo"></param>
    public void Save(string fileName, bool xmlInfo)
    {
        if (xmlInfo)
        {
            SaveXmlInfo(fileName);
        }

        BinaryWriter writer = new(new
        FileStream(fileName, FileMode.Create, FileAccess.Write,
        FileShare.None, 0x10000, FileOptions.RandomAccess),
        Encoding.Default);
        //Write the primary header
        writer.Write(0);
        writer.Write(Times);
        writer.Write(Types);
        var version2DataSize = WriteVersion2Data(writer);
        Index[] gaps = CreateIndexes(Data);
        writer.Write(gaps.Length); // we will figure what this number is later
        LoadSubIndex(gaps, HasData); // find the gaps in the other direction
        Save(gaps, writer, version2DataSize);
        //complete
        writer.Seek(0, SeekOrigin.Begin);
        // write that this is a version 2 file
        writer.Write(2);
        writer.Close();
    }

    public void SaveXmlInfo(string fileName)
    {
        var fname = Path.GetFileNameWithoutExtension(fileName) ?? throw new IOException($"Unable to get the file name without extension of {fileName}!");
        var dirname = Path.GetDirectoryName(fileName) ?? throw new IOException($"Unable to get the directory name from {fileName}!");
        var xmlName = Path.Combine(dirname, fname) + ".xml";
        var serializer = new XmlSerializer(typeof(CacheGenerationInfo));
        using TextWriter textWriter = new StreamWriter(xmlName);
        serializer.Serialize(textWriter, FilesLoaded);
    }

    /// <summary>
    /// This is called to load data from an ODCache
    /// </summary>
    /// <param name="o"></param>
    /// <param name="d"></param>
    /// <param name="cache"></param>
    internal void Set(int o, int d, OdCache cache)
    {
        LoadingO = o;
        if (Data[o] == null)
        {
            Data[o] = new float[HighestZone][];
            HasData[o] = new bool[HighestZone];
            for (int k = HighestZone - 1; k >= 0; k--)
            {
                Data[o][k] = new float[AmmountOfData];
            }
            for (int k = 0; k < HighestZone; k++) HasData[o][k] = false;
        }
        HasData[o][d] = true;
        if (Data[o][d] == null)
        {
            Data[o][d] = new float[AmmountOfData];
        }
        int entry = 0;
        for (int time = 0; time < Times; time++)
        {
            for (int type = 0; type < Types; type++)
            {
                Data[o][d][entry] = cache[o, d, time, type];
                entry++;
            }
        }
    }

    protected virtual Dictionary<string, string> GetMetaData()
    {
        // we don't actually do anything here
        return null;
    }

    private Index[] CreateIndexes(float[][][] data)
    {
        int start = 0, numberOfZones = data.Length;
        List<Index> sections = new(40);
        for (int i = 0; i < numberOfZones; i++)
        {
            if (data[i] == null)
            {
                if (start != i)
                {
                    sections.Add(new Index() { Start = start, End = i - 1 });
                }
                start = i + 1;
            }
        }
        if (start != numberOfZones)
        {
            sections.Add(new Index() { Start = start, End = numberOfZones - 1 });
        }
        return [.. sections];
    }

    private bool DLoaded(Index i, bool[][] data, int d)
    {
        int zone;
        for (zone = i.Start; zone <= i.End; zone++)
        {
            if (data[zone][d])
            {
                return true;
            }
        }
        return false;
    }

    private void LoadSubIndex(Index[] index, bool[][] data)
    {
        int numberOfZones = data.Length;
        NumberOfSubs = 0;
        Parallel.For(0, index.Length, delegate (int segment)
       {
           List<Index> subIndex = new(50);
           int start = 0;
           for (int i = 0; i < numberOfZones; i++)
           {
               bool hasData = DLoaded(index[segment], data, i);
               if (!hasData)
               {
                   if (start != i)
                   {
                       subIndex.Add(new Index() { Start = start, End = i - 1 });
                       Interlocked.Increment(ref NumberOfSubs);
                   }
                   start = i + 1;
               }
           }
           if (start < numberOfZones)
           {
               subIndex.Add(new Index() { Start = start, End = numberOfZones - 1 });
               Interlocked.Increment(ref NumberOfSubs);
           }

           index[segment].SubIndex = [.. subIndex];
       });
    }

    private void Save(Index[] blocks, BinaryWriter writer, int versionDataSize)
    {
        // ForAll oIndex
        long indexLocation = 4 * sizeof(float) + versionDataSize;
        // the header + each o block [4] start [4] end [8] location +
        long subIndexLocation = (blocks.Length * Index.SizeOf) + indexLocation;
        //#of d blocks (sizeof(unit))
        long dataLocation = subIndexLocation + (blocks.Length * sizeof(uint)) + (NumberOfSubs * Index.SizeOf);
        long initDataLocation = dataLocation;
        foreach (var oBlock in blocks)
        {
            // Store(oIndex,startByte)
            writer.BaseStream.Position = indexLocation;
            writer.Write((uint)oBlock.Start);
            writer.Write((uint)oBlock.End);
            writer.Write(subIndexLocation);

            indexLocation = writer.BaseStream.Position;
            writer.BaseStream.Position = subIndexLocation;
            writer.Write((uint)oBlock.SubIndex.Length);
            foreach (var dBlock in oBlock.SubIndex)
            {
                writer.Write((uint)dBlock.Start);
                writer.Write((uint)dBlock.End);
                writer.Write(dataLocation);
                dataLocation += (dBlock.End - dBlock.Start + 1) *
                    (oBlock.End - oBlock.Start + 1) * AmmountOfData * sizeof(float);
            }
            subIndexLocation = writer.BaseStream.Position;
        }

        // Now Store data
        writer.BaseStream.Position = initDataLocation;
        for (int oBlock = 0; oBlock < blocks.Length; oBlock++)
        {
            for (int oIndex = blocks[oBlock].Start; oIndex <= blocks[oBlock].End; oIndex++)
            {
                foreach (var dBlock in blocks[oBlock].SubIndex)
                {
                    for (int dIndex = dBlock.Start; dIndex <= dBlock.End; dIndex++)
                    {
                        for (int k = 0; k < AmmountOfData; k++)
                        {
                            writer.Write(Data[oIndex][dIndex][k]);
                        }
                    }
                }
            }
        }
    }

    private void WriteMetaData(BinaryWriter writer, Dictionary<string, string> metaData)
    {
        // write the data to disk
        writer.Write(metaData.Count);
        foreach (var entry in metaData)
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }
    }

    private int WriteVersion2Data(BinaryWriter writer)
    {
        // write the total length int
        writer.Write(0);
        // include all of the version 2 information
        var start = writer.BaseStream.Position;
        // write the description to the stream
        var metaData = GetMetaData();
        if (metaData != null)
        {
            WriteMetaData(writer, metaData);
        }
        var length = (int)(writer.BaseStream.Position - start);
        writer.Seek(-length - sizeof(int), SeekOrigin.Current);
        writer.Write(length);
        // no + sizeof( int ) because we just wrote to the stream sizeof( int )
        writer.Seek(length, SeekOrigin.Current);
        // +4 because we also have the length stored in an int32
        return length + 4;
    }

    private struct Index
    {
        // [4] start, [4] end, [16] sub index location
        public static int SizeOf = 16;

        public int End;
        public int Start;
        public Index[] SubIndex;
    }
}