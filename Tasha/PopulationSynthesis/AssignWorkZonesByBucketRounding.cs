/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Numerics;
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    public class AssignWorkZonesByBucketRounding : ICalculation<ITashaPerson, IZone>
    {

        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        private IZone _roamingZone;

        [RunParameter("External Zones", "6000-6999", typeof(RangeSet), "External employment zones previously loaded will not be overwritten.")]
        public RangeSet ExternalZones;

        [SubModelInformation(Required = true)]
        public OccupationData Professional;
        [SubModelInformation(Required = true)]
        public OccupationData General;
        [SubModelInformation(Required = true)]
        public OccupationData Sales;
        [SubModelInformation(Required = true)]
        public OccupationData Manufacturing;

        public void Load()
        {
            _roamingZone = Root.ZoneSystem.Get(Root.ZoneSystem.RoamingZoneNumber);
            Parallel.Invoke(
                () => Professional.Load(),
                () => General.Load(),
                () => Sales.Load(),
                () => Manufacturing.Load()
                );
        }

        public IZone ProduceResult(ITashaPerson data)
        {
            var empZone = data.EmploymentZone;
            if (empZone != null)
            {
                var number = empZone.ZoneNumber;
                if (ExternalZones.Contains(number) || empZone == _roamingZone)
                {
                    return empZone;
                }
            }
            switch (data.Occupation)
            {
                case Occupation.Professional:
                    return Professional.ProduceResult(data);
                case Occupation.Office:
                    return General.ProduceResult(data);
                case Occupation.Manufacturing:
                    return Manufacturing.ProduceResult(data);
                case Occupation.Retail:
                    return Sales.ProduceResult(data);
                default:
                    throw new XTMFRuntimeException(this, "Unknown occupation type!");
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {

        }

        public sealed class OccupationData : IModule
        {
            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

            public EmploymentStatus FullTime;
            public EmploymentStatus PartTime;

            internal void Load()
            {
                Parallel.Invoke(
                    () => FullTime.Load(),
                    () => PartTime.Load());
            }

            internal IZone ProduceResult(ITashaPerson data)
            {
                switch (data.EmploymentStatus)
                {
                    case TTSEmploymentStatus.FullTime:
                        return FullTime.ProduceResult(data);
                    case TTSEmploymentStatus.PartTime:
                        return PartTime.ProduceResult(data);
                    default:
                        throw new XTMFRuntimeException(this, "Unknown employment status!");
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            public sealed class EmploymentStatus : IModule
            {
                [RootModule]
                public ITravelDemandModel Root;

                private SparseArray<IZone> _zoneSystem;

                [SubModelInformation(Required = true, Description = "PoRPoW aggregate model")]
                public IDataSource<SparseTriIndex<float>> Linkages;

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

                /// <summary>
                /// [type, homeZone, choice]
                /// </summary>
                private int[][][] _choices;

                /// <summary>
                /// [type, homeZone]
                /// </summary>
                private int[][] _index;

                internal void Load()
                {
                    _zoneSystem = Root.ZoneSystem.ZoneArray;
                    Linkages.LoadData();
                    var results = Linkages.GiveData();
                    Linkages.UnloadData();
                    CreateIndexes();
                    CreateChoices(results.GetFlatData());
                }

                private void CreateIndexes()
                {
                    _index = new int[3][];
                    _choices = new int[3][][];
                    int numberOfZones = _zoneSystem.Count;
                    for (int i = 0; i < _index.Length; i++)
                    {
                        _index[i] = new int[numberOfZones];
                        _choices[i] = new int[numberOfZones][];
                    }
                }

                [RunParameter("Random Seed", 41614513, "The random seed to use for selection.")]
                public int RandomSeed;

                private void CreateChoices(float[][][] linkages)
                {
                    Parallel.For(0, 3, (int type) =>
                    {
                        Parallel.For(0, linkages[type].Length, (int i) =>
                        {
                            CreateChoices(linkages, type, i);
                        });
                    });
                }

                private void CreateChoices(float[][][] linkages, int type, int homeZoneIndex)
                {
                    var row = linkages[type][homeZoneIndex];
                    var temp = new int[row.Length];
                    var acc = 0.0f;
                    int j = 0;
                    var choices = new List<int>(200);
                    for (; j < temp.Length - Vector<float>.Count; j += Vector<float>.Count)
                    {
                        Vector.ConvertToInt32(new Vector<float>(row, j)).CopyTo(temp, j);
                    }
                    for (; j < temp.Length; j++)
                    {
                        temp[j] = (int)row[j];
                    }
                    var total = 0;
                    int lastWithEmp = 0;
                    int lastWithAnyEmp = 0;
                    for (j = 0; j < row.Length; j++)
                    {
                        var remander = row[j] - temp[j];
                        acc += remander;
                        if (acc >= 0.5f)
                        {
                            acc -= 1.0f;
                            ++temp[j];
                        }
                        if(row[j] > 0.0f)
                        {
                            lastWithAnyEmp = j;
                        }
                        if (temp[j] > 0)
                        {
                            lastWithEmp = j;
                        }
                        total += temp[j];
                    }
                    if(acc > 0)
                    {
                        ++temp[lastWithAnyEmp];
                        ++total;
                        lastWithEmp = lastWithAnyEmp;
                    }
                    unchecked
                    {
                        Random r = new Random(RandomSeed * total * homeZoneIndex * type);
                        while (total > 0)
                        {
                            var pop = r.Next(total);
                            var iacc = 0;
                            for (j = 0; j < temp.Length && j <= lastWithEmp; j++)
                            {
                                iacc += temp[j];
                                if (iacc >= pop)
                                {
                                    break;
                                }
                            }
                            j = Math.Min(j, lastWithEmp);
                            --temp[j];
                            choices.Add(j);
                            if (j == lastWithEmp && temp[j] == 0)
                            {
                                // If nothing else has employment this was the final iteration
                                for (int k = lastWithEmp - 1; k >= 0; k--)
                                {
                                    if (temp[k] > 0)
                                    {
                                        lastWithEmp = k;
                                        break;
                                    }
                                }
                            }
                            --total;
                        }
                        _choices[type][homeZoneIndex] = choices.ToArray();
                    }
                }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }

                private int ClassifyHousehold(ITashaHousehold household, ITashaPerson person)
                {
                    var numberOfLicenses = 0;
                    var numberOfVehicles = household.Vehicles.Length;
                    if (numberOfVehicles > 0)
                    {
                        var persons = household.Persons;
                        for (int i = 0; i < persons.Length; i++)
                        {
                            if (persons[i].Licence)
                            {
                                numberOfLicenses++;
                            }
                        }
                    }
                    int category = numberOfLicenses == 0 ? 0 : (numberOfVehicles < numberOfLicenses ? 1 : 2);
                    return category;
                }

                internal IZone ProduceResult(ITashaPerson data)
                {
                    ITashaHousehold household = data.Household;
                    var homeIndex = _zoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
                    var type = ClassifyHousehold(household, data);
                    var index = _index[type][homeIndex]++;
                    var homeRow = _choices[type][homeIndex];
                    var ret = _zoneSystem.GetFlatData()[_choices[type][homeIndex][index]];
                    if(_index[type][homeIndex] >= _choices[type][homeIndex].Length)
                    {
                        _index[type][homeIndex] = 0;
                    }
                    return ret;
                }
            }
        }
    }
}
