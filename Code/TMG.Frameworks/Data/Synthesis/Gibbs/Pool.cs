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
using System.IO;
using System.Linq;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Synthesis.Gibbs
{

    public class Pool : IModule
    {
        [SubModelInformation(Description = "The conditionals to apply.")]
        public Conditional[] Conditionals;

        [SubModelInformation(Description = "The attributes for the pool")]
        public Attribute[] Attributes;

        [RunParameter("Random Seed", 12345, "A seed to fix the random generator to ensure multiple runs will give the same results.")]
        public int RandomSeed;

        [RunParameter("Size", 1000, "The number of elements to create.")]
        public int SizeToGenerate;

        [RunParameter("Segment Size", 100, "The population to process for each pool segment. (Orders parallel processing.)")]
        public int SegmentSize;

        [RunParameter("Iterations Before Accept", 100, "How many iterations should we spin before we accept a solution?")]
        public int IterationsBeforeAccept;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public int[][][] PoolChoices;

        [SubModelInformation(Required = false, Description = "Set this to save a copy for the pool.")]
        public FileLocation Dump;

        private IConfiguration Config;

        public Pool(IConfiguration config)
        {
            Config = config;
        }

        public void GeneratePool()
        {
            if (ZoneSystem == null)
            {
                PoolChoices = new int[1][][];
                GenerateForZone(-1);

            }
            else
            {
                var zones = ZoneSystem.ZoneArray.GetFlatData();
                PoolChoices = new int[zones.Length][][];
                for (int zone = 0; zone < zones.Length; zone++)
                {
                    GenerateForZone(zone);
                }
            }
        }

        private void GenerateForZone(int zone)
        {
            // load in the data we need to process our conditionals
            System.Threading.Tasks.Parallel.For(0, Conditionals.Length, i =>
            {
                Conditionals[i].LoadConditionalsData(0);
            });
            // once we have the required data process the pool segments
            var poolSegments = new PoolSegment[(int)Math.Ceiling((float)SizeToGenerate / SegmentSize)];
            Random r = new(RandomSeed);
            for (int i = 0; i < poolSegments.Length; i++)
            {
                poolSegments[i] = new PoolSegment(this, r.Next());
            }
            System.Threading.Tasks.Parallel.For(0, poolSegments.Length, i =>
            {
                poolSegments[i].ProcessSegment(SegmentSize);
            });
            CopyResults(poolSegments, zone);
            if (Dump != null)
            {
                Save(Dump, zone);
            }
        }

        private void CopyResults(PoolSegment[] poolSegments, int zoneIndex)
        {
            int startIndex = 0;
            var poolRow = PoolChoices[zoneIndex < 0 ? 0 : zoneIndex] = new int[poolSegments.Sum(seg => seg.Result.Length)][];
            for (int i = 0; i < poolSegments.Length; i++)
            {
                var localResults = poolSegments[i].Result;
                int length = localResults.Length;
                Array.Copy(localResults, 0, poolRow, startIndex, length);
                startIndex += length;
            }
        }

        private void Save(FileLocation dump, int zoneIndex)
        {
            var poolRow = PoolChoices[zoneIndex < 0 ? 0 : zoneIndex];
            using var writer = new StreamWriter(dump, zoneIndex > 0);
            if (zoneIndex < 0)
            {
                writer.WriteLine(string.Join(",", Attributes.Select(a => AddQuotes(a.Name))));
                for (int i = 0; i < poolRow.Length; i++)
                {
                    var row = poolRow[i];
                    writer.Write(row[0]);
                    for (int j = 1; j < row.Length; j++)
                    {
                        writer.Write(',');
                        writer.Write(row[j]);
                    }
                    writer.WriteLine();
                }
            }
            else
            {
                //write header
                if (zoneIndex == 0)
                {
                    writer.Write("Zone,");
                    writer.WriteLine(string.Join(",", Attributes.Select(a => AddQuotes(a.Name))));
                }
                var zoneNumber = ZoneSystem.ZoneArray.GetFlatData()[zoneIndex].ZoneNumber.ToString();
                for (int i = 0; i < poolRow.Length; i++)
                {
                    var row = poolRow[i];
                    writer.Write(zoneNumber);
                    writer.Write(',');
                    writer.Write(row[0]);
                    for (int j = 1; j < row.Length; j++)
                    {
                        writer.Write(',');
                        writer.Write(row[j]);
                    }
                    writer.WriteLine();
                }
            }
        }

        private static string AddQuotes(string inner)
        {
            return $"\"{inner}\"";
        }

        [SubModelInformation(Required = false, Description = "An optional source to load the zone system from.  If left blank the Travel Demand Model Zone system will be used.")]
        public IDataSource<IZoneSystem> ZoneSystemSource;

        private IZoneSystem ZoneSystem;

        public bool RuntimeValidation(ref string error)
        {
            // Get the zone system from the travel demand model
            if (ZoneSystemSource != null)
            {
                ZoneSystemSource.LoadData();
                ZoneSystem = ZoneSystemSource.GiveData();
            }
            else
            {
                if (Functions.ModelSystemReflection.GetRootOfType(Config, typeof(ITravelDemandModel), this, out IModelSystemStructure tdm))
                {
                    ZoneSystem = ((ITravelDemandModel)tdm.Module).ZoneSystem;
                    if (ZoneSystem != null && !ZoneSystem.Loaded)
                    {
                        ZoneSystem.LoadData();
                    }
                }
            }
            // ZoneSystem can still be null at the end of this
            return true;
        }
    }

}
