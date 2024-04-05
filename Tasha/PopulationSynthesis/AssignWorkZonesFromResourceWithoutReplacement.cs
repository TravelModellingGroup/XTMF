﻿/*
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
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    public class AssignWorkZonesFromResourceWithoutReplacement : ICalculation<ITashaPerson, IZone>
    {
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public sealed class OccupationData : IModule
        {
            public sealed class EmploymentStatus : IModule
            {
                [RootModule]
                public ITravelDemandModel Root;

                [SubModelInformation(Required = true, Description = "SparseTriIndex<float>")]
                public IResource Linkages;

                [SubModelInformation(Required = false, Description = "Save the worker linkages to file, skipped of not filled in.")]
                public FileLocation SaveLinkages;

                [SubModelInformation(Required = false, Description = "Save the worker categories to file, skipped of not filled in.")]
                public FileLocation SaveWorkerCategory;

                private SparseTriIndex<float> _linkages;
                private SparseTriIndex<float> _originalLinkages;

                private IZone[] Zones;
                private SparseArray<IZone> _zoneSystem;
                private SparseArray<float> _planningDistricts;
                private float[][] WorkerResults;
                /// <summary>
                /// [pd, type, workZone]
                /// </summary>
                private float[][][] _assigned;
                /// <summary>
                /// [pd, type, workZone]
                /// </summary>
                private float[][][] _totalEmployment;

                public void Load()
                {
                    _zoneSystem = Root.ZoneSystem.ZoneArray;
                    _planningDistricts = ZoneSystemHelper.CreatePdArray<float>(_zoneSystem);
                    Zones = _zoneSystem.GetFlatData();
                    if (SaveWorkerCategory != null)
                    {
                        WorkerResults = new float[3][];
                        for (int i = 0; i < WorkerResults.Length; i++)
                        {
                            WorkerResults[i] = new float[Zones.Length];
                        }
                    }
                    _linkages = Linkages.AcquireResource<SparseTriIndex<float>>();
                    CopyLinkages(_linkages);
                    SaveLinkagesToFile(_linkages.GetFlatData());
                    Linkages.ReleaseResource();
                }

                /// <summary>
                /// Copy the linkages in case they need to be reset.
                /// </summary>
                /// <param name="linkages"></param>
                private void CopyLinkages(SparseTriIndex<float> linkages)
                {
                    _originalLinkages = linkages.CreateSimilarArray<float>();
                    var flatLinkages = linkages.GetFlatData();
                    var flatOriginal = _originalLinkages.GetFlatData();
                    var numberofPDs = _planningDistricts.Count;
                    var zones = _zoneSystem.GetFlatData();
                    _totalEmployment = new float[numberofPDs][][];
                    _assigned = new float[numberofPDs][][];
                    for (int pd = 0; pd < numberofPDs; pd++)
                    {
                        _totalEmployment[pd] = new float[3][];
                        _assigned[pd] = new float[3][];
                    }
                    for (int i = 0; i < flatLinkages.Length; i++)
                    {
                        for (int pd = 0; pd < numberofPDs; pd++)
                        {
                            _totalEmployment[pd][i] = new float[flatLinkages[i].Length];
                            _assigned[pd][i] = new float[flatLinkages[i].Length];
                        }
                        for (int j = 0; j < flatLinkages[i].Length; j++)
                        {
                            Array.Copy(flatLinkages[i][j], flatOriginal[i][j], flatLinkages[i][j].Length);
                        }
                    }
                    for (int type = 0; type < flatLinkages.Length; type++)
                    {
                        for (int i = 0; i < zones.Length; i++)
                        {
                            var iPD = _planningDistricts.GetFlatIndex(zones[i].PlanningDistrict);
                            var empRow = _totalEmployment[iPD][type];
                            var linkRow = flatLinkages[type][i];
                            VectorHelper.Add(empRow, 0, empRow, 0, linkRow, 0, empRow.Length);
                        }
                    }
                }

                private void SaveLinkagesToFile(float[][][] data)
                {
                    if (SaveLinkages == null)
                    {
                        return;
                    }
                    SparseArray<IZone> zoneSystem = Root.ZoneSystem.ZoneArray;
                    var zones = zoneSystem.GetFlatData();
                    var saveData = new float[zones.Length][];
                    for (int i = 0; i < saveData.Length; i++)
                    {
                        saveData[i] = new float[zones.Length];
                        for (int j = 0; j < saveData[i].Length; j++)
                        {
                            float total = 0.0f;
                            for (int k = 0; k < data.Length; k++)
                            {
                                total += data[k][i][j];
                            }
                            saveData[i][j] = total;
                        }
                    }
                    SaveData.SaveMatrix(zones, saveData, SaveLinkages);
                }

                public void Unload()
                {
                    _linkages = null;
                    SaveHouseholdCategoryRecords();
                }

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    if (!Linkages.CheckResourceType<SparseTriIndex<float>>())
                    {
                        error = "In '" + Name + "' we were unable to load linkages because it is not of type SparseTriIndex<float>.  Please contact your model system provider.";
                        return false;
                    }
                    return true;
                }

                internal IZone ProduceResult(Random random, ITashaHousehold household, ITashaPerson person)
                {
                    var pop = (float)random.NextDouble();
                    var index = PickAZoneToSelect(pop, household, person, person.ExpansionFactor);
                    return Zones[index];
                }

                private float GetTotalLinkages(float[] row, float[] assigned, float[] total)
                {
                    int i = 0;
                    var acc = 0.0f;
                    var accV = Vector<float>.Zero;
                    var one = Vector<float>.One;
                    var zero = Vector<float>.Zero;
                    for (; i < row.Length - Vector<float>.Count; i+=Vector<float>.Count)
                    {
                        var eV = new Vector<float>(total, i);
                        var aV = new Vector<float>(assigned, i);
                        var ratio = VectorHelper.SelectIfFinite(aV / eV, Vector<float>.Zero);
                        accV += new Vector<float>(row, i) * Vector.Max((one - ratio), Vector<float>.Zero);
                    }
                    for (; i < row.Length; i++)
                    {
                        if (total[i] > 0)
                        {
                            acc += row[i] * Math.Max((1.0f - assigned[i] / total[i]), 0f);
                        }
                    }
                    return acc + Vector.Dot(accV, one);
                }

                private int PickAZoneToSelect(float pop, ITashaHousehold household, ITashaPerson person, float expansionFactor)
                {
                    var type = ClassifyHousehold(household, person);
                    var homeZoneIndex = _zoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
                    var row = _linkages.GetFlatData()[type][homeZoneIndex];
                    var pdIndex = _planningDistricts.GetFlatIndex(household.HomeZone.PlanningDistrict);
                    float[] assigned = _assigned[pdIndex][type];
                    float[] emp = _totalEmployment[pdIndex][type];
                    var totalLinkages = GetTotalLinkages(row, assigned, emp);
                    if (totalLinkages <= 0.0f)
                    {
                        Array.Copy(_originalLinkages.GetFlatData()[type][homeZoneIndex], row, row.Length);
                        totalLinkages = VectorHelper.Sum(row, 0, row.Length);
                        if (totalLinkages <= 0.0f)
                        {
                            throw new XTMFRuntimeException(this, $"A person living at zone {household.HomeZone.ZoneNumber} with worker category" +
                                $" {type + 1} tried to find an employment zone.  There was no aggregate data for any workers of this class however.  Please" +
                                $" update your worker categories and zonal residence files for this scenario!\r\n" +
                                $"HHLD#: {household.HouseholdId}");
                        }
                    }
                    // total linkages is greater than zero
                    pop *= totalLinkages;
                    float acc = 0.0f;
                    int index = 0;
                    for (; index < row.Length; index++)
                    {
                        var ratio = emp[index] > 0 ? (assigned[index] / emp[index]) : 1f;
                        acc += row[index] * Math.Max((1.0f - ratio), 0f);
                        if (pop < acc)
                        {
                            break;
                        }
                    }
                    // make sure it is bounded in case of rounding errors
                    if(index == row.Length)
                    {
                        for (index = row.Length - 1; index >= 0; index--)
                        {
                            if(row[index] > 0)
                            {
                                break;
                            }
                        }
                        // make sure we didn't run all the way back past the start of the array
                        if(index == -1)
                        {
                            throw new XTMFRuntimeException(this, $"After already checking that there was an available job, none were found!");
                        }
                    }
                    _assigned[pdIndex][type][index] += expansionFactor;
                    return index;
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
                    if (SaveWorkerCategory != null)
                    {
                        RecordHouseholdCategory(category, household.HomeZone.ZoneNumber, household.ExpansionFactor);
                    }
                    return category;
                }

                private void RecordHouseholdCategory(int category, int zoneNumber, float expansionFactor)
                {
                    var flatZoneIndex = Root.ZoneSystem.ZoneArray.GetFlatIndex(zoneNumber);
                    if (flatZoneIndex >= 0)
                    {
                        // this code is never executed in parallel
                        WorkerResults[category][flatZoneIndex] += expansionFactor;
                    }
                }

                private void SaveHouseholdCategoryRecords()
                {
                    if (SaveWorkerCategory != null && WorkerResults != null)
                    {
                        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                        using var writer = new StreamWriter(SaveWorkerCategory);
                        writer.WriteLine("Zone,Category,Total");
                        for (int i = 0; i < zones.Length; i++)
                        {
                            var zoneNumber = zones[i].ZoneNumber;
                            for (int cat = 0; cat < WorkerResults.Length; cat++)
                            {
                                writer.Write(zoneNumber);
                                writer.Write(',');
                                writer.Write(cat + 1);
                                writer.Write(',');
                                writer.WriteLine(WorkerResults[cat][i]);
                            }
                        }
                    }
                }
            }

            [SubModelInformation(Required = true)]
            public EmploymentStatus FullTime;

            [SubModelInformation(Required = true)]
            public EmploymentStatus PartTime;


            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }


            public void Load()
            {
                Console.WriteLine("Full-Time...");
                FullTime.Load();
                Console.WriteLine("Part-Time...");
                PartTime.Load();
            }

            public void Unload()
            {
                PartTime.Unload();
                FullTime.Unload();
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            internal IZone ProduceResult(Random random, ITashaPerson person, ITashaHousehold household)
            {
                switch (person.EmploymentStatus)
                {
                    case TTSEmploymentStatus.FullTime:
                        return FullTime.ProduceResult(random, household, person);
                    default:
                        return PartTime.ProduceResult(random, household, person);
                }
            }
        }
        [SubModelInformation(Required = true)]
        public OccupationData Professional;

        [SubModelInformation(Required = true)]
        public OccupationData General;

        [SubModelInformation(Required = true)]
        public OccupationData Sales;

        [SubModelInformation(Required = true)]
        public OccupationData Manufacturing;

        [RunParameter("Random Seed", 154321, "A seed used to generate random numbers.")]
        public int RandomSeed;

        private IZone _roamingZone;

        [RootModule]
        public ITravelDemandModel Root;

        public void Load()
        {
            _roamingZone = Root.ZoneSystem.Get(Root.ZoneSystem.RoamingZoneNumber);
            Console.WriteLine("Loading PoRPoW...");
            Console.WriteLine("Professional...");
            Professional.Load();
            Console.WriteLine("General...");
            General.Load();
            Console.WriteLine("Sales...");
            Sales.Load();
            Console.WriteLine("Manufacturing...");
            Manufacturing.Load();
            Console.WriteLine("Finished Loading PoRPoW");
        }

        [RunParameter("External Zone Ranges", "6000-9999", typeof(RangeSet), "The ranges that represent external zones.")]
        public RangeSet ExternalZones;

        private bool IsExternal(IZone employmentZone)
        {
            return employmentZone != null && 
                (employmentZone == _roamingZone || ExternalZones.Contains(employmentZone.ZoneNumber));
        }

        public IZone ProduceResult(ITashaPerson person)
        {
            // Gather the base data and create our random generator
            IZone empZone;
            if (IsExternal(empZone = person.EmploymentZone))
            {
                return empZone;
            }
            var household = person.Household;
            var random = new Random(RandomSeed * household.HouseholdId);
            switch (person.Occupation)
            {
                case Occupation.Office:
                    return General.ProduceResult(random, person, household);
                case Occupation.Retail:
                    return Sales.ProduceResult(random, person, household);
                case Occupation.Manufacturing:
                    return Manufacturing.ProduceResult(random, person, household);
                default:
                    return Professional.ProduceResult(random, person, household);
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {
            Professional.Unload();
            General.Unload();
            Sales.Unload();
            Manufacturing.Unload();
        }
    }
}
