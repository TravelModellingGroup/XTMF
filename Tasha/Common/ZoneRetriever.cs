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
using System.Threading.Tasks;
using Datastructure;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Common
{
    public sealed class ZoneRetriever : IZoneSystem, IDisposable
    {
        [RunParameter("Highest Zone Number", 7150, "The highest numbered zone.")]
        public int HighestZoneNumber;

        public IZone RoamingZone;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter("Zone Cache File", "Zones.zfc", "The file name of the zone data")]
        public string ZoneCacheFile;

        [RunParameter("Zone File Name", "Zones.csv", "The location of the zone file.")]
        public string ZoneFileName;

        [Parameter("Zones With Employment Data", "0", "LowerBound-UpperBound")]
        public string ZonesWithEmploymentData;

        [RunParameter("Regenerate", true, "Should we regenerate the cache file every time?")]
        public bool Regenerate;

        [RunParameter("Load Once", true, "Only load the zone system once.")]
        public bool LoadOnce;

        private SparseArray<IZone> AllZones;

        private Pair<int, int>[] EmploymentDataRange;
        private object LoadingLock = new object();

        public ZoneRetriever()
        {
        }

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

        public void Generate()
        {
            SparseZoneCreator creator = new SparseZoneCreator(HighestZoneNumber + 1, 22);
            creator.LoadCSV(GetFullPath(ZoneFileName), true);
            creator.Save(GetFullPath(ZoneCacheFile));
        }

        public IZone Get(int ZoneNumber)
        {
            if(ZoneNumber == RoamingZoneNumber)
            {
                if(RoamingZone == null)
                {
                    lock (this)
                    {
                        System.Threading.Thread.MemoryBarrier();
                        if(RoamingZone == null)
                        {
                            RoamingZone = new Zone(ZoneNumber);
                        }
                        System.Threading.Thread.MemoryBarrier();
                    }
                }
                return RoamingZone;
            }
            return AllZones[ZoneNumber];
        }

        public IZoneSystem GiveData()
        {
            return this;
        }

        public bool Loaded
        {
            get { return AllZones != null; }
        }

        public void LoadData()
        {
            if(!LoadOnce || !Loaded)
            {
                initEmpDataRange();
                var cacheFileName = GetFullPath(ZoneCacheFile);
                if(CheckIfWeNeedToRegenerateCache(cacheFileName))
                {
                    Generate();
                }
                using (var cache = new ZoneCache<IZone>(cacheFileName, ConvertToZone))
                {
                    AllZones = cache.StoreAll();
                    cache.Dispose();
                }
                ComputeDistances();
                LoadReagions();
            }
        }

        private bool CheckIfWeNeedToRegenerateCache(string cacheFileName)
        {
            if(Regenerate) return true;
            return !File.Exists(cacheFileName);
        }

        [SubModelInformation(Required = false, Description = "A CSV File with Zone,Region.")]
        public FileLocation RegionFile;

        private void LoadReagions()
        {
            if(RegionFile != null)
            {
                var zoneArray = ZoneArray;
                var zones = zoneArray.GetFlatData();
                using (CsvReader reader = new CsvReader(RegionFile))
                {
                    // burn header
                    reader.LoadLine(out int columns);
                    // read the rest
                    while(reader.LoadLine(out columns))
                    {
                        if(columns < 2) continue;
                        reader.Get(out int zoneNumber, 0);
                        reader.Get(out int regionNumber, 1);
                        int index = zoneArray.GetFlatIndex(zoneNumber);
                        if(index >= 0)
                        {
                            zones[index].RegionNumber = regionNumber;
                        }
                        else
                        {
                            throw new XTMFRuntimeException("In '" + Name + "' we found a zone '" + zoneNumber + "' while reading in the regions that does not exist in the zone system!");
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
            var zfn = GetFullPath(ZoneFileName);
            var zcn = GetFullPath(ZoneCacheFile);
            if(!File.Exists(zfn) && !File.Exists(zcn))
            {
                error = string.Format("Both the zone file \"{0}\" and cache file \"{1}\" do not exist!", zfn, zcn);
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Dispose(true);
        }

        public bool ZoneHasEmploymentData(IZone zone)
        {
            foreach(var pair in EmploymentDataRange)
            {
                if(pair.First > zone.ZoneNumber)
                    return false;

                if(pair.First <= zone.ZoneNumber && pair.Second >= zone.ZoneNumber)
                    return true;
            }

            return false;
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
        /// <param name="ZoneID">The zone that was asked for</param>
        /// <param name="data">The raw data from file</param>
        /// <returns>A new zone with this data</returns>
        private static IZone ConvertToZone(int ZoneID, float[] data)
        {
            // Create this data from the information in the cache file
            return new Zone(ZoneID, data);
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
                for(int j = 0; j < length; j++)
                {
                    row[j] = (i == j) ? zones[i].InternalDistance
                        : CalcDistance(zones[i], zones[j]);
                }
            });
            Distances = distances;
        }

        private string GetFullPath(string localPath)
        {
            if(!Path.IsPathRooted(localPath))
            {
                return Path.Combine(Root.InputBaseDirectory, localPath);
            }
            return localPath;
        }

        private void initEmpDataRange()
        {
            List<Pair<int, int>> empDataRange = new List<Pair<int, int>>();
            string sRange = ZonesWithEmploymentData;
            string[] ranges = sRange.Split(',');
            foreach(var r in ranges)
            {
                string[] range = r.Split('-');
                if(range.Length == 1)
                {
                    empDataRange.Add(new Pair<int, int>(int.Parse(range[0]), int.Parse(range[0])));
                }
                else if(range.Length == 2)
                {
                    empDataRange.Add(new Pair<int, int>(int.Parse(range[0]), int.Parse(range[1])));
                }
            }
            EmploymentDataRange = empDataRange.ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool all)
        {
            if(!LoadOnce)
            {
                AllZones = null;
            }
        }
    }
}