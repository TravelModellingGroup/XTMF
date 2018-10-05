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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Common
{
    [ModuleInformation(Description = "This module is designed to load in a zone system and to provide that information to the rest of the model system.")]
    public sealed class ZoneRetriever : IZoneSystem, IDisposable
    {
        public IZone RoamingZone;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter("Zone File Name", "Zones.csv", "The location of the zone file.")]
        public string ZoneFileName;

        [RunParameter("Load Once", true, "Only load the zone system once.")]
        public bool LoadOnce;

        [RunParameter("Set Internal Distances", true, "Set the distances in the distance matrix to the values from the zones file.")]
        public bool SetInternalDistances;

        private SparseArray<IZone> AllZones;

        public SparseTwinIndex<float> Distances { get; private set; }

        public string Name { get; set; }

        public int NumberOfExternalZones
        {
            get { return 0; }
        }

        public int NumberOfInternalZones
        {
            get { return NumberOfZones; }
        }

        public int NumberOfZones
        {
            get
            {
                return AllZones.Top + 1;
            }
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(50, 150, 50); }
        }

        [RunParameter("Roaming Zone", -1, "The zone number of people who have a roaming place of work.")]
        public int RoamingZoneNumber
        {
            get;
            set;
        }

        public SparseArray<IZone> ZoneArray
        {
            get { return AllZones; }
        }

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
                            RoamingZone = new Zone(zoneNumber);
                        }
                        System.Threading.Thread.MemoryBarrier();
                    }
                }
                return RoamingZone;
            }
            return AllZones[zoneNumber];
        }

        public IZoneSystem GiveData()
        {
            return this;
        }

        public bool Loaded
        {
            get { return AllZones != null; }
        }

        [SubModelInformation(Required = false, Description = "Optional source to use for specifying the distances between zones.")]
        public IDataSource<SparseTwinIndex<float>> DistanceMatrix;

        public void LoadData()
        {
            if (!LoadOnce || !Loaded)
            {
                LoadZones();
                LoadReagions();
                LoadDistances();
            }
        }

        private void LoadZones()
        {
            var zoneFile = GetZoneFilePath();
            if (!File.Exists(zoneFile))
            {
                throw new XTMFRuntimeException(this, $"Unable to find a file with the path '{zoneFile}' to load zones from!");
            }
            List<IZone> zones = new List<IZone>(2500);
            var maxColumnSize = 0;
            using (var reader = new CsvReader(zoneFile, false))
            {
                // burn the header
                reader.LoadLine();
                while (reader.LoadLine(out var columns))
                {
                    maxColumnSize = Math.Max(maxColumnSize, columns);
                    if (columns >= 23)
                    {
                        reader.Get(out int zoneNumber, 0);
                        reader.Get(out int pd, 1);
                        reader.Get(out float population, 2);
                        reader.Get(out float x, 14);
                        reader.Get(out float y, 15);
                        reader.Get(out float internalDistance, 16);
                        reader.Get(out float parkingCost, 21);
                        zones.Add(new Zone(zoneNumber)
                        {
                            PlanningDistrict = pd,
                            Population = (int)Math.Round(population),
                            X = x,
                            Y = y,
                            InternalDistance = internalDistance,
                            ParkingCost = parkingCost
                        });
                    }
                }
            }
            // check for errors
            if(maxColumnSize > 0 && maxColumnSize < 23)
            {
                throw new XTMFRuntimeException(this, $"When reading the zones there was no row with 23 columns, the maximum size was {maxColumnSize}!");
            }
            if (zones.Count <= 0)
            {
                throw new XTMFRuntimeException(this, $"No zones were loaded when reading in the zone file from '{zoneFile}'!");
            }
            // make sure the zones are in order
            zones.Sort((first, second) => (first.ZoneNumber.CompareTo(second.ZoneNumber)));
            AllZones = SparseArray<IZone>.CreateSparseArray(zones.Select(z => z.ZoneNumber).ToArray(), zones.ToArray());
        }

        private string GetZoneFilePath()
        {
            var path = ZoneFileName;
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            return Path.Combine(Root.InputBaseDirectory, path);
        }

        [SubModelInformation(Required = false, Description = "A CSV File with Zone,Region.")]
        public FileLocation RegionFile;

        private void LoadReagions()
        {
            if (RegionFile != null)
            {
                var zoneArray = ZoneArray;
                var zones = zoneArray.GetFlatData();
                using (CsvReader reader = new CsvReader(RegionFile))
                {
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
        }


        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Dispose();
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

        /// <summary>
        /// This is passed into ZoneCache to create a zone from data
        /// </summary>
        /// <param name="zoneID">The zone that was asked for</param>
        /// <param name="data">The raw data from file</param>
        /// <returns>A new zone with this data</returns>
        private static IZone ConvertToZone(int zoneID, float[] data)
        {
            // Create this data from the information in the cache file
            return new Zone(zoneID, data);
        }

        private void LoadDistances()
        {
            if (DistanceMatrix == null)
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
            else
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
        }

        private string GetFullPath(string localPath)
        {
            if (!Path.IsPathRooted(localPath))
            {
                return Path.Combine(Root.InputBaseDirectory, localPath);
            }
            return localPath;
        }

        ~ZoneRetriever()
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
    }
}