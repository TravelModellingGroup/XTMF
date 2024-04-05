/*
    Copyright 2018-2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

// Ignore Spelling: Intrazonal

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Datastructure;
using XTMF;
using TMG.Input;

namespace TMG.Emme.Utilities;

[ModuleInformation(Description = "Load a zone system from an EMME network package.")]
public sealed class ZoneSystemFromNWP : IZoneSystem, IDisposable
{
    [RunParameter("Load Once", true, "Only load the zone system once.")]
    public bool LoadOnce;

    [RunParameter("Set Internal Distances", true, "Set the distances in the distance matrix to the values from the zones file.")]
    public bool SetInternalDistances;

    [RunParameter("Roaming Zone", 8888, "The zone number of people who have a roaming place of work.")]
    public int RoamingZoneNumber { get; set; }

    [SubModelInformation(Required = true, Description = "The network package to use to build the zone system.")]
    public FileLocation NWPLocation;

    public IZone RoamingZone;

    public int NumberOfExternalZones => 0;

    public int NumberOfInternalZones => NumberOfZones;

    public int NumberOfZones => AllZones.Top + 1;

    private SparseArray<IZone> AllZones;

    public SparseArray<IZone> ZoneArray => AllZones;

    public SparseTwinIndex<float> Distances { get; private set; }

    public string Name { get; set; }

    public IZone Get(int zoneNumber)
    {
        if (zoneNumber == RoamingZoneNumber)
        {
            if (RoamingZone == null)
            {
                lock (this)
                {
                    System.Threading.Thread.MemoryBarrier();
                    if (RoamingZone == null)
                    {
                        RoamingZone = new Zone(zoneNumber, -1, -1);
                    }
                    System.Threading.Thread.MemoryBarrier();
                }
            }
            return RoamingZone;
        }
        return AllZones[zoneNumber];
    }

    public IZoneSystem GiveData() => this;

    public bool Loaded => AllZones != null;

    public float Progress => throw new NotImplementedException();

    public Tuple<byte, byte, byte> ProgressColour => throw new NotImplementedException();

    public void LoadData()
    {
        if (!LoadOnce || !Loaded)
        {
            LoadZones();
            Parallel.Invoke(
                () => LoadRegions(),
                () => LoadPDs(),
                () => LoadIntrazonalDistance(),
                () => LoadParkingCosts(),
                () => LoadPopulation());
            // Distances requires Intrazonal distances
            LoadDistances();
        }
    }

    private void LoadZones()
    {
        if(!File.Exists(NWPLocation))
        {
            throw new XTMFRuntimeException(this, $"Unable to find a network package at the location '{NWPLocation.GetFilePath()}'!");
        }
        using (ZipArchive archive = new(File.OpenRead(NWPLocation), ZipArchiveMode.Read, false))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.Name.Equals("base.211", StringComparison.InvariantCultureIgnoreCase))
                {
                    using var reader = new StreamReader(entry.Open());
                    string line = null;
                    const string nodesMarker = "t nodes";
                    while ((line = reader.ReadLine()) != null && line != nodesMarker) ;
                    if (line != nodesMarker)
                    {
                        throw new XTMFRuntimeException(this, $"While reading in a zone system from {NWPLocation.GetFilePath()} we did not encounter the start of nodes!");
                    }
                    // burn the header
                    reader.ReadLine();
                    var seperators = new char[] { ' ', '\t' };
                    List<Zone> zones = new(2500);
                    while ((line = reader.ReadLine()) != null && line[0] != 't')
                    {
                        // ignore blank lines
                        if (line.Length > 2)
                        {
                            // if it is a centroid
                            if (line[0] == 'a' && line[1] == '*')
                            {
                                var split = line.Split(seperators, StringSplitOptions.RemoveEmptyEntries);
                                if (split.Length < 3)
                                {
                                    throw new XTMFRuntimeException(this, $"Failed to load the line '{line}' when reading in zones.");
                                }
                                if (!(int.TryParse(split[1], out int zoneNumber)
                                    && float.TryParse(split[2], out float x)
                                    && float.TryParse(split[3], out float y)))
                                {
                                    throw new XTMFRuntimeException(this, $"Failed to load the line '{line}' when reading in zones.");
                                }
                                zones.Add(new Zone(zoneNumber, x, y));
                            }
                        }
                    }
                    zones.Sort((z1, z2) => z1.ZoneNumber.CompareTo(z2.ZoneNumber));
                    AllZones = SparseArray<IZone>.CreateSparseArray(zones.Select(z => z.ZoneNumber).ToArray(), zones.ToArray());
                    return;
                }
            }
        }
        throw new XTMFRuntimeException(this, $"The network package located at '{NWPLocation.GetFilePath()}' did not contain a base.211 file to load the zones from!");
    }

    [SubModelInformation(Required = false, Description = "Optional source to use for specifying the distances between zones.")]
    public IDataSource<SparseTwinIndex<float>> DistanceMatrix;

    private void LoadDistances()
    {
        if (DistanceMatrix == null)
        {
            ComputeDistances();
        }
        else
        {
            LoadDistancesFromFile();
        }
    }

    private void LoadDistancesFromFile()
    {
        if (!DistanceMatrix.Loaded)
        {
            DistanceMatrix.LoadData();
            Distances = DistanceMatrix.GiveData();
            DistanceMatrix.UnloadData();
        }
        else
        {
            Distances = DistanceMatrix.GiveData();
        }
        var flatDistances = Distances.GetFlatData();
        if (SetInternalDistances)
        {
            var flatZones = ZoneArray.GetFlatData();
            for (int i = 0; i < flatDistances.Length; i++)
            {
                flatDistances[i][i] = flatZones[i].InternalDistance;
            }
        }
    }

    private void ComputeDistances()
    {
        var distances = ZoneArray.CreateSquareTwinArray<float>();
        var flatDistnaces = distances.GetFlatData();
        var zones = ZoneArray.GetFlatData();
        var length = zones.Length;
        Parallel.For(0, flatDistnaces.Length, delegate (int i)
        {
            var row = flatDistnaces[i];
            for (int j = 0; j < length; j++)
            {
                row[j] = (i == j) ? zones[i].InternalDistance
                    : CalcDistance(zones[i], zones[j]);
            }
        });
        Distances = distances;
    }

    /// <summary>
    /// Calculate the distance between two zones
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="destination"></param>
    /// <returns>The distance is meters</returns>
    private static float CalcDistance(IZone origin, IZone destination)
    {
        var deltaX = origin.X - destination.X;
        var deltaY = origin.Y - destination.Y;
        return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    [SubModelInformation(Required = false, Description = "A CSV File with Zone,Region.")]
    public FileLocation RegionFile;

    private void LoadRegions()
    {
        if (RegionFile != null)
        {
            var zoneArray = ZoneArray;
            var zones = zoneArray.GetFlatData();
            if(!File.Exists(RegionFile))
            {
                throw new XTMFRuntimeException(this, $"The file containing region information was not found '{RegionFile.GetFilePath()}'!");
            }
            using CsvReader reader = new(RegionFile);
            // burn header
            reader.LoadLine(out int columns);
            // read the rest
            while (reader.LoadLine(out columns))
            {
                if (columns < 2) continue;
                reader.Get(out int zoneNumber, 0);
                reader.Get(out int regionNumber, 1);
                int index = zoneArray.GetFlatIndex(zoneNumber);
                if (index >= 0)
                {
                    zones[index].RegionNumber = regionNumber;
                }
                else
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the regions that does not exist in the zone system!");
                }
            }
        }
    }


    [SubModelInformation(Required = false, Description = "A CSV File with Zone,PlanningDistrict.")]
    public FileLocation PDFile;

    private void LoadPDs()
    {
        if (PDFile != null)
        {
            var zoneArray = ZoneArray;
            var zones = zoneArray.GetFlatData();
            if (!File.Exists(PDFile))
            {
                throw new XTMFRuntimeException(this, $"The file containing planning district information was not found '{PDFile.GetFilePath()}'!");
            }
            using CsvReader reader = new(PDFile);
            // burn header
            reader.LoadLine(out int columns);
            // read the rest
            while (reader.LoadLine(out columns))
            {
                if (columns < 2) continue;
                reader.Get(out int zoneNumber, 0);
                reader.Get(out int pdNumber, 1);
                int index = zoneArray.GetFlatIndex(zoneNumber);
                if (index >= 0)
                {
                    zones[index].PlanningDistrict = pdNumber;
                }
                else
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the planning districts that does not exist in the zone system!");
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "A CSV File with HomeZone,Population.")]
    public FileLocation PopulationFile;

    private void LoadPopulation()
    {
        if (PopulationFile != null)
        {
            var zoneArray = ZoneArray;
            var zones = zoneArray.GetFlatData();
            if (!File.Exists(PopulationFile))
            {
                throw new XTMFRuntimeException(this, $"The file containing population information was not found '{PopulationFile.GetFilePath()}'!");
            }
            using CsvReader reader = new(PopulationFile);
            // burn header
            reader.LoadLine(out int columns);
            // read the rest
            while (reader.LoadLine(out columns))
            {
                if (columns < 2) continue;
                reader.Get(out int zoneNumber, 0);
                reader.Get(out float population, 1);
                int index = zoneArray.GetFlatIndex(zoneNumber);
                if (index >= 0)
                {
                    zones[index].Population = (int)population;
                }
                else
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the population that does not exist in the zone system!");
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "A CSV File with Zone,IntrazonalDistance. Where the distance is in metres.")]
    public FileLocation IntrazonalDistanceFile;

    private void LoadIntrazonalDistance()
    {
        if (IntrazonalDistanceFile != null)
        {
            var zoneArray = ZoneArray;
            var zones = zoneArray.GetFlatData();
            if (!File.Exists(IntrazonalDistanceFile))
            {
                throw new XTMFRuntimeException(this, $"The file containing intrazonal distance information was not found '{IntrazonalDistanceFile.GetFilePath()}'!");
            }
            using CsvReader reader = new(IntrazonalDistanceFile);
            // burn header
            reader.LoadLine(out int columns);
            // read the rest
            while (reader.LoadLine(out columns))
            {
                if (columns < 2) continue;
                reader.Get(out int zoneNumber, 0);
                reader.Get(out float intraDistance, 1);
                int index = zoneArray.GetFlatIndex(zoneNumber);
                if (index >= 0)
                {
                    zones[index].InternalDistance = (int)intraDistance;
                }
                else
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the intrazonal distances that does not exist in the zone system!");
                }
            }
        }
    }


    [SubModelInformation(Required = false, Description = "A CSV File with Zone,ParkingCost. Where the cost is in $/hour")]
    public FileLocation ParkingCostFile;

    private void LoadParkingCosts()
    {
        if (ParkingCostFile != null)
        {
            var zoneArray = ZoneArray;
            var zones = zoneArray.GetFlatData();
            if (!File.Exists(ParkingCostFile))
            {
                throw new XTMFRuntimeException(this, $"The file containing parking cost information was not found '{ParkingCostFile.GetFilePath()}'!");
            }
            using CsvReader reader = new(ParkingCostFile);
            // burn header
            reader.LoadLine(out int columns);
            // read the rest
            while (reader.LoadLine(out columns))
            {
                if (columns < 2) continue;
                reader.Get(out int zoneNumber, 0);
                reader.Get(out float parkingCost, 1);
                int index = zoneArray.GetFlatIndex(zoneNumber);
                if (index >= 0)
                {
                    zones[index].ParkingCost = (int)parkingCost;
                }
                else
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the parking costs that does not exist in the zone system!");
                }
            }
        }
    }

    public void UnloadData()
    {
        Dispose();
    }

    ~ZoneSystemFromNWP()
    {
        LocalDispose();
    }

    public void Dispose()
    {
        LocalDispose();
        GC.SuppressFinalize(this);
    }

    private void LocalDispose()
    {
        if (!LoadOnce)
        {
            AllZones = null;
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private sealed class Zone : IZone
    {
        /// <summary>
        /// Creates a new zone to represent the roaming zone
        /// </summary>
        /// <param name="zoneID"></param>
        public Zone(int zoneID, float x, float y)
        {
            ZoneNumber = zoneID;
            X = x;
            Y = y;
        }

        /// <summary>
        /// ratio of arterial road km to total road km
        /// </summary>
        public float ArterialRoadRatio { get; set; }

        /// <summary>
        /// total zonal employment
        /// </summary>
        public float Employment { get; set; }

        /// <summary>
        /// employment - general office / clerical
        /// </summary>
        public float GeneralEmployment { get; set; }

        /// <summary>
        /// The area that this zone represents
        /// </summary>
        public float InternalArea { get; set; }

        /// <summary>
        /// Average distance within this zone
        /// </summary>
        public float InternalDistance { get; set; }

        /// <summary>
        /// intersection density (# intersections/total road km)
        /// </summary>
        public float IntrazonalDensity { get; set; }

        /// <summary>
        /// employment - manufacturing/construction/trades
        /// </summary>
        public float ManufacturingEmployment { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float OtherActivityLevel { get; set; }

        /// <summary>
        /// How much does parking cost within this zone
        /// </summary>
        public float ParkingCost { get; set; }

        /// <summary>
        /// The planning district the zone is in
        /// </summary>
        public int PlanningDistrict { get; set; }

        /// <summary>
        /// total zonal population for the zone
        /// </summary>
        public int Population { get; set; }

        /// <summary>
        /// employment - professional management technical
        /// </summary>
        public float ProfessionalEmployment { get; set; }

        public int RegionNumber
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        public float RetailActivityLevel { get; set; }

        /// <summary>
        /// employment - retail sales and service
        /// </summary>
        public float RetailEmployment { get; set; }

        public float TotalEmployment
        {
            get
            {
                return GeneralEmployment + ManufacturingEmployment + ProfessionalEmployment + RetailEmployment;
            }

            set
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// employment - We don't know exactly what type it is
        /// </summary>
        public float UnknownEmployment { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkActivityLevel { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkGeneral { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkManufacturing { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkProfessional { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkRetail { get; set; }

        /// <summary>
        ///
        /// </summary>
        public float WorkUnknown { get; set; }

        /// <summary>
        /// X Position of this zone
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y Position of this Zone
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// The ID for the zone
        /// </summary>
        public int ZoneNumber { get; }

        public override int GetHashCode() => ZoneNumber.GetHashCode();

        public override string ToString() => ZoneNumber.ToString();
    }
}
