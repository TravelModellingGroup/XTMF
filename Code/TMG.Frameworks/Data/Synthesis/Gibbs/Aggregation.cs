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
using Datastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMG.Input;
using XTMF;
namespace TMG.Frameworks.Data.Synthesis.Gibbs
{

    public class Aggregation : IModule
    {
        [RootModule]
        public GibbsSampler Root;

        public sealed class Join : IModule
        {
            [RootModule]
            public GibbsSampler Root;

            [ParentModel]
            public Aggregation Parent;

            public DataModule<string> SecondaryPool;

            private IConfiguration Config;

            public Join(IConfiguration config)
            {
                Config = config;
            }

            [SubModelInformation(Required = true, Description = "The attribute to bind to.")]
            public DataModule<string> PrimaryAttribute;

            [SubModelInformation(Required = true, Description = "")]
            public DataModule<string>[] SecondaryAttributes;

            [RunParameter("Random Seed", 12345, "")]
            public int RandomSeed;

            private Pool _SecondaryPool;

            private Attribute _PrimaryAttribute;

            private Attribute[] _SecondaryAttributes;

            [SubModelInformation(Required = true, Description = "Aggregation file")]
            public FileLocation AggregationFile;

            [SubModelInformation(Required = true, Description = "The location to save the combined form.")]
            public FileLocation SaveAggregationTo;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            [SubModelInformation(Required = false, Description = "An optional source to load the zone system from.  If left blank the Travel Demand Model Zone system will be used.")]
            public IDataSource<IZoneSystem> ZoneSystemSource;

            private IZoneSystem ZoneSystem;

            public void Execute()
            {
                var columns = _SecondaryAttributes.Select(a => Array.IndexOf(_SecondaryPool.Attributes, a)).ToArray();
                EnsureAllColumnsExist(columns);
                var factors = BuildFactors();
                var accepted = LoadAggregationFile(columns, factors);
                using (var writer = new StreamWriter(SaveAggregationTo))
                {
                    if (ZoneSystem != null)
                    {
                        writer.Write("Zone");
                        writer.Write(',');
                    }
                    writer.Write(Parent._PrimaryPool.Name);
                    for (int i = 0; i < _SecondaryPool.Attributes.Length; i++)
                    {
                        writer.Write(',');
                        writer.Write(_SecondaryPool.Attributes[i].Name);
                    }
                    writer.WriteLine();
                    var originalData = Parent._PrimaryPool.PoolChoices;
                    var baseHouseholdId = 1;
                    for (int zoneIndex = 0; zoneIndex < originalData.Length; zoneIndex++)
                    {
                        var candidatesByValue = SeperatePoolsToPrimaryAttributeValue(factors, columns, accepted, zoneIndex);
                        var r = new Random(RandomSeed);
                        string zoneString = null;
                        if(ZoneSystem != null)
                        {
                            zoneString = ZoneSystem.ZoneArray.GetFlatData()[zoneIndex].ZoneNumber.ToString();
                        }
                        var primaryAttributeColumn = Array.IndexOf(Parent._PrimaryPool.Attributes, _PrimaryAttribute);
                        for (int i = 0; i < originalData[zoneIndex].Length; i++)
                        {
                            var index = originalData[zoneIndex][i][primaryAttributeColumn];
                            var candidates = candidatesByValue[index];
                            if (candidates.Count > 0)
                            {
                                var candidate = candidates[(int)(r.NextDouble() * candidates.Count)];
                                if (ZoneSystem != null)
                                {
                                    writer.Write(zoneString);
                                    writer.Write(',');
                                }
                                writer.Write(baseHouseholdId + i);
                                for (int j = 0; j < candidate.Length; j++)
                                {
                                    writer.Write(',');
                                    writer.Write(candidate[j]);
                                }
                                writer.WriteLine();
                            }
                        }
                        baseHouseholdId += originalData[zoneIndex].Length;
                    }
                }
            }

            private List<List<int[]>> SeperatePoolsToPrimaryAttributeValue(int[] factors, int[] setIndirection, List<int>[] accepted, int zoneIndex)
            {
                return (from acceptedSet in accepted.AsParallel().AsOrdered()
                        select
                         (
                             from rep in _SecondaryPool.PoolChoices[zoneIndex].AsParallel().AsOrdered()
                             let setValue = GetIndex(rep, setIndirection, factors)
                             where acceptedSet.Any(set => setValue == set)
                             select rep
                         ).ToList()
                       ).ToList();
            }

            private static int GetIndex(int[] set, int[] setIndirection, int[] factors)
            {
                int index = 0;
                for (int i = 0; i < factors.Length; i++)
                {
                    index += set[setIndirection[i]] * factors[i];
                }
                return index;
            }

            private List<int>[] LoadAggregationFile(int[] columns, int[] factors)
            {
                var acceptedCombinations = new List<int>[_PrimaryAttribute.PossibleValues.Length];
                for (int i = 0; i < acceptedCombinations.Length; i++)
                {
                    acceptedCombinations[i] = new List<int>();
                }
                var expectedColumns = columns.Length + 1;
                using (var reader = new CsvReader(AggregationFile))
                {
                    reader.LoadLine();
                    int numberOfColumns;
                    /*
                     * Each line that is read in is an accepted combination 
                     * the first column is the primary attribute's index, the rest of the 
                     * secondary attributes in order
                    */
                    while (reader.LoadLine(out numberOfColumns))
                    {
                        if (numberOfColumns >= expectedColumns)
                        {
                            int primaryIndex;
                            int index = 0;
                            reader.Get(out primaryIndex, 0);
                            for (int i = 1; i < numberOfColumns; i++)
                            {
                                int secondaryIndex;
                                reader.Get(out secondaryIndex, i);
                                index += factors[i - 1] * secondaryIndex;
                            }
                            acceptedCombinations[primaryIndex].Add(index);
                        }
                    }
                }
                return acceptedCombinations;
            }

            private int[] BuildFactors()
            {
                var lengths = _SecondaryAttributes.Select(at => at.PossibleValues.Length).ToArray();
                int mul = 1;
                for (int i = lengths.Length - 1; i >= 0; i--)
                {
                    var length = lengths[i];
                    lengths[i] = mul;
                    mul *= length;
                }
                return lengths;
            }

            private void EnsureAllColumnsExist(int[] columns)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    if (columns[i] < 0)
                    {
                        throw new XTMFRuntimeException($"In '{Name}' an attribute named '{_SecondaryAttributes[i].Name}' was not found in the pool '{_SecondaryPool.Name}'!");
                    }
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                if ((_SecondaryPool = Root.Pools.FirstOrDefault(p => p.Name == SecondaryPool.Data)) == null)
                {
                    error = $"In '{Name}' we were unable to find a pool with the name '{SecondaryPool.Data}'.";
                    return false;
                }
                // this is safe because a parent's runtime validation will always execute first
                if ((_PrimaryAttribute = Parent._PrimaryPool.Attributes.FirstOrDefault(a => a.Name == PrimaryAttribute.Data)) == null)
                {
                    error = $"In '{Name} we were unable to find an attribute called '{PrimaryAttribute.Data}' in the pool '{Parent._PrimaryPool.Name}'.'";
                    return false;
                }
                _SecondaryAttributes = SecondaryAttributes.Select(s => _SecondaryPool.Attributes.FirstOrDefault(at => at.Name == s.Data)).ToArray();
                for (int i = 0; i < _SecondaryAttributes.Length; i++)
                {
                    if (_SecondaryAttributes[i] == null)
                    {
                        error = $"In '{Name}' we were unable to find an attribute named '{SecondaryAttributes[i].Data}' in the pool '{SecondaryPool.Name}'";
                        return false;
                    }
                }
                // Get the zone system from the travel demand model
                if (ZoneSystemSource != null)
                {
                    ZoneSystemSource.LoadData();
                    ZoneSystem = ZoneSystemSource.GiveData();
                }
                else
                {
                    IModelSystemStructure tdm;
                    if (Functions.ModelSystemReflection.GetRootOfType(Config, typeof(ITravelDemandModel), this, out tdm))
                    {
                        ZoneSystem = ((ITravelDemandModel)tdm.Module).ZoneSystem;
                        if (ZoneSystem != null && !ZoneSystem.Loaded)
                        {
                            ZoneSystem.LoadData();
                        }
                    }
                }
                return true;
            }
        }

        public DataModule<string> PrimaryPool;

        private Pool _PrimaryPool;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Description = "The joins to execute.")]
        public Join[] Joins;

        public void Execute()
        {
            foreach (var join in Joins)
            {
                join.Execute();
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ((_PrimaryPool = Root.Pools.FirstOrDefault(p => p.Name == PrimaryPool.Data)) == null)
            {
                error = $"In '{Name}' we were unable to find a pool with the name '{PrimaryPool.Data}'.";
                return false;
            }
            return true;
        }
    }
}
