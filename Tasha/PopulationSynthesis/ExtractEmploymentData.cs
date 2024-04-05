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
using Datastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis;

[ModuleInformation(Description = "Create all of the data required for estimating PoRPoW from household records.")]
public class ExtractEmploymentData : IPostHousehold
{
    [RootModule]
    public ITashaRuntime Root;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    [RunParameter("External PDs", "0", typeof(RangeSet), "The set of planning districts that are considered external.")]
    public RangeSet ExternalPDs;

    /// <summary>
    /// [Emp][Occ][pdhome][pdemp]
    /// </summary>
    private float[][][] _zonalResidence;
    private float[][][][][] _zonalWorkerCategories;
    private float[][][] _zonalEmployment;


    private SparseArray<float> _pds;
    private SparseArray<IZone> _zones;

    public void IterationStarting(int iteration)
    {
        var zoneSystem = _zones = Root.ZoneSystem.ZoneArray;
        var pdMap = _pds = ZoneSystemHelper.CreatePdArray<float>(zoneSystem);
        var numberOfPds = pdMap.GetFlatData().Length;
        var numberOfZones = zoneSystem.GetFlatData().Length;
        // 4 employment statuses
        _zonalResidence = new float[4][][];
        _zonalWorkerCategories = new float[4][][][][];
        _zonalEmployment = new float[4][][];
        for (int emp = 0; emp < _zonalWorkerCategories.Length; emp++)
        {
            // 4 occupation categories
            _zonalResidence[emp] = new float[4][];
            _zonalEmployment[emp] = new float[4][];
            _zonalWorkerCategories[emp] = new float[4][][][];
            for (int occ = 0; occ < _zonalWorkerCategories[emp].Length; occ++)
            {
                _zonalResidence[emp][occ] = new float[numberOfZones];
                _zonalEmployment[emp][occ] = new float[numberOfZones];
                _zonalWorkerCategories[emp][occ] = new float[numberOfZones][][];
                for (int i = 0; i < _zonalWorkerCategories[emp][occ].Length; i++)
                {
                    _zonalWorkerCategories[emp][occ][i] = new float[numberOfZones][];
                    for (int j = 0; j < _zonalWorkerCategories[emp][occ][i].Length; j++)
                    {
                        _zonalWorkerCategories[emp][occ][i][j] = new float[3];
                    }
                }

            }
        }
    }

    public void Execute(ITashaHousehold household, int iteration)
    {
        var homeZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
        foreach (var person in household.Persons)
        {
            int emp = GetEmp(person);
            int occ = GetOcc(person);
            if (ValidPerson(emp, occ))
            {
                var workerCategory = GetWorkerCategory(person, household);
                var empZone = person.EmploymentZone;
                if (empZone == null) continue;
                var flatEmpZone = _zones.GetFlatIndex(empZone.ZoneNumber);
                float expansionFactor = person.ExpansionFactor;
                _zonalEmployment[emp][occ][flatEmpZone] += expansionFactor;
                if (!ExternalPDs.Contains(empZone.PlanningDistrict))
                {
                    _zonalResidence[emp][occ][homeZone] += expansionFactor;
                }
                _zonalWorkerCategories[emp][occ][homeZone][flatEmpZone][workerCategory] += expansionFactor;
            }
        }
    }

    private int GetWorkerCategory(ITashaPerson person, ITashaHousehold household)
    {
        var cars = household.Vehicles?.Length ?? 0;
        if (!person.Licence || cars == 0) return 0;
        return household.Persons.Count(p => p.Licence) > cars ? 1 : 2;
    }

    static bool ValidPerson(int emp, int occ)
    {
        return (emp >= 0) & (occ >= 0);
    }

    private int GetOcc(ITashaPerson person)
    {
        switch (person.Occupation)
        {
            case Occupation.Professional:
                return 0;
            case Occupation.Office:
                return 1;
            case Occupation.Retail:
                return 2;
            case Occupation.Manufacturing:
                return 3;
            default:
                return -1;
        }
    }

    private int GetEmp(ITashaPerson person)
    {
        switch (person.EmploymentStatus)
        {
            case TTSEmploymentStatus.FullTime:
                return 0;
            case TTSEmploymentStatus.PartTime:
                return 1;
            case TTSEmploymentStatus.WorkAtHome_FullTime:
                return 2;
            case TTSEmploymentStatus.WorkAtHome_PartTime:
                return 3;
            default:
                return -1;
        }
    }

    public void IterationFinished(int iteration)
    {
        WriteTotalEmployment();
        WriteEmpOccRates();
        WriteWorkAtHomeRates();
        WriteExternalRates();
        WriteLinkObservations();
        WriteWorkerCategories();
        WriteZonalResidence();
    }


    [SubModelInformation(Required = false, Description = "The location to save the summation of employment by zone.")]
    public FileLocation TotalEmploymentByZone;

    private void WriteTotalEmployment()
    {
        if (TotalEmploymentByZone == null)
        {
            return;
        }
        var flatZones = _zones.GetFlatData();
        using var writer = new StreamWriter(TotalEmploymentByZone);
        writer.WriteLine("Zone,Employment");
        for (int i = 0; i < flatZones.Length; i++)
        {
            if (!ExternalPDs.Contains(flatZones[i].PlanningDistrict))
            {
                writer.Write(flatZones[i].ZoneNumber);
                writer.Write(',');
                var acc = 0.0f;
                for (int emp = 0; emp < _zonalEmployment.Length; emp++)
                {
                    for (int occ = 0; occ < _zonalEmployment[emp].Length; occ++)
                    {
                        acc += _zonalEmployment[emp][occ][i];
                    }
                }
                writer.WriteLine(acc);
            }
        }
    }

    [SubModelInformation(Required = false, Description = "The location to save the rates of each full-time and part-time by occupation.")]
    public FileLocation EmpOccRateDir;

    private void EnsureDirectory(string path)
    {
        DirectoryInfo dir = new(path);
        if (!dir.Exists)
        {
            dir.Create();
        }
    }

    [RunParameter("Compute Rates At a PD Level", true, "Should the rates by generated by zone or by planning district?")]
    public bool ComputeRatesAtAPDLevel;

    private void WriteEmpOccRates()
    {
        if (EmpOccRateDir == null)
        {
            return;
        }
        var results = ComputeEmpOccRates();
        string dir = EmpOccRateDir;
        EnsureDirectory(dir);
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                using var writer = new StreamWriter(Path.Combine(dir, GetPrefix(emp, occ) + ".csv"));
                writer.WriteLine("Zone,Rate");
                for (int i = 0; i < zones.Length; i++)
                {
                    var zoneNumber = zones[i].ZoneNumber;
                    var value = (ComputeRatesAtAPDLevel
                        ? results[_pds.GetFlatIndex(zones[i].PlanningDistrict)] : results[i])[emp * 4 + occ];
                    if (value > 0.0)
                    {
                        writer.Write(zoneNumber);
                        writer.Write(',');
                        writer.WriteLine(value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// [Zone/PD][emp,occ](8)
    /// </summary>
    /// <returns></returns>
    private float[][] ComputeEmpOccRates()
    {
        var flatZones = _zones.GetFlatData();
        // we first compute everything by zone
        var ret = new float[(ComputeRatesAtAPDLevel ? _pds.Count : flatZones.Length)][];
        for (int i = 0; i < ret.Length; i++)
        {
            ret[i] = new float[8];
        }
        // Pass 1: Total everything
        if (ComputeRatesAtAPDLevel)
        {
            for (int i = 0; i < flatZones.Length; i++)
            {
                var currentPD = _pds.GetFlatIndex(flatZones[i].PlanningDistrict);
                for (int emp = 0; emp < 2; emp++)
                {
                    for (int occ = 0; occ < 4; occ++)
                    {
                        ret[currentPD][emp * 4 + occ] += _zonalEmployment[emp][occ][i] + _zonalEmployment[emp + 2][occ][i];
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < flatZones.Length; i++)
            {
                for (int emp = 0; emp < 2; emp++)
                {
                    for (int occ = 0; occ < 4; occ++)
                    {
                        ret[i][emp * 4 + occ] += _zonalEmployment[emp][occ][i] + _zonalEmployment[emp + 2][occ][i];
                    }
                }
            }
        }
        // Pass 2: generate rates from data
        for (int i = 0; i < ret.Length; i++)
        {
            var acc = 0.0f;
            for (int j = 0; j < ret[i].Length; j++)
            {
                acc += ret[i][j];
            }
            if (acc > 0)
            {
                for (int j = 0; j < ret[i].Length; j++)
                {
                    ret[i][j] /= acc;
                }
            }
        }
        return ret;
    }

    [SubModelInformation(Required = false, Description = "The directory to store the work at home job rates.")]
    public FileLocation WorkAtHomeRateDir;

    private void WriteWorkAtHomeRates()
    {
        if (WorkAtHomeRateDir == null)
        {
            return;
        }
        string dir = WorkAtHomeRateDir;
        EnsureDirectory(dir);
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                using var writer = new StreamWriter(Path.Combine(dir, GetPrefix(emp, occ) + ".csv"));
                writer.WriteLine("Zone,Rate");
                if (ComputeRatesAtAPDLevel)
                {
                    var totals = new float[_pds.Count];
                    var wah = new float[_pds.Count];
                    // pass 1: fill out our totals and wah for the occ/emp aggregated to pd
                    for (int i = 0; i < _zonalEmployment[emp][occ].Length; i++)
                    {
                        // assign both totals and WaH
                        var index = _pds.GetFlatIndex(zones[i].PlanningDistrict);
                        wah[index] += _zonalEmployment[emp + 2][occ][i];
                        totals[index] += _zonalEmployment[emp][occ][i] + _zonalEmployment[emp + 2][occ][i];
                    }
                    // pass 2:
                    for (int i = 0; i < _zonalEmployment[emp][occ].Length; i++)
                    {
                        var index = _pds.GetFlatIndex(zones[i].PlanningDistrict);
                        if (totals[index] > 0)
                        {
                            writer.Write(zones[i].ZoneNumber);
                            writer.Write(',');
                            writer.WriteLine(wah[index] / totals[index]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _zonalEmployment[emp][occ].Length; i++)
                    {
                        var wah = _zonalEmployment[emp + 2][occ][i];
                        var total = _zonalEmployment[emp][occ][i] + wah;
                        var value = total <= 0.0f ? 0 : _zonalEmployment[emp + 2][occ][i] / total;
                        if (total > 0.0f)
                        {
                            writer.Write(zones[i].ZoneNumber);
                            writer.Write(',');
                            writer.WriteLine();
                        }
                    }
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "The directory to write the external worker rates to.")]
    public FileLocation ExternalRateDir;

    private void WriteExternalRates()
    {
        if (ExternalRateDir == null)
        {
            return;
        }
        string dir = ExternalRateDir;
        EnsureDirectory(dir);
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                using var writer = new StreamWriter(Path.Combine(dir, GetPrefix(emp, occ) + ".csv"));
                writer.WriteLine("Zone,Rate");
                if (ComputeRatesAtAPDLevel)
                {
                    var totals = new float[_pds.Count];
                    var external = new float[_pds.Count];
                    for (int workZone = 0; workZone < zones.Length; workZone++)
                    {
                        var workIndex = _pds.GetFlatIndex(zones[workZone].PlanningDistrict);
                        for (int homeZone = 0; homeZone < zones.Length; homeZone++)
                        {
                            var isExternal = ExternalPDs.Contains(zones[homeZone].PlanningDistrict);
                            for (int workerCategory = 0; workerCategory < 3; workerCategory++)
                            {
                                var links = _zonalWorkerCategories[emp][occ][homeZone][workZone][workerCategory];
                                if (isExternal)
                                {
                                    external[workIndex] += links;
                                }
                                totals[workIndex] += links;
                            }
                        }
                    }
                    for (int i = 0; i < zones.Length; i++)
                    {
                        var index = _pds.GetFlatIndex(zones[i].PlanningDistrict);
                        if (totals[index] > 0)
                        {
                            writer.Write(zones[i].ZoneNumber);
                            writer.Write(',');
                            writer.WriteLine(external[index] / totals[index]);
                        }
                    }
                }
                else
                {
                    for (int workZone = 0; workZone < zones.Length; workZone++)
                    {
                        var total = 0.0f;
                        var external = 0.0f;
                        if (!ExternalPDs.Contains(zones[workZone].PlanningDistrict))
                        {
                            for (int homeZone = 0; homeZone < zones.Length; homeZone++)
                            {
                                var isExternal = ExternalPDs.Contains(zones[homeZone].PlanningDistrict);
                                for (int workerCategory = 0; workerCategory < 3; workerCategory++)
                                {
                                    var links = _zonalWorkerCategories[emp][occ][workZone][homeZone][workerCategory];
                                    if (isExternal)
                                    {
                                        external += links;
                                    }
                                    total += links;
                                }
                            }
                        }
                        if (total > 0)
                        {
                            writer.Write(zones[workZone].ZoneNumber);
                            writer.Write(',');
                            writer.WriteLine(external / total);
                        }
                    }
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "The directory to write the observed links by worker class to.")]
    public FileLocation ObservedLinkRateDir;

    private void WriteLinkObservations()
    {
        if (ObservedLinkRateDir == null)
        {
            return;
        }
        string dir = ObservedLinkRateDir;
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                for (int k = 0; k < 3; k++)
                {
                    var subdir = Path.Combine(dir, k.ToString());
                    EnsureDirectory(subdir);
                    using var writer = new StreamWriter(Path.Combine(subdir, GetPrefix(emp, occ) + ".csv"));
                    writer.WriteLine("Home,Work,Links");
                    for (int homeZone = 0; homeZone < zones.Length; homeZone++)
                    {
                        if (!ExternalPDs.Contains(zones[homeZone].PlanningDistrict))
                        {
                            for (int workZone = 0; workZone < zones.Length; workZone++)
                            {
                                if (!ExternalPDs.Contains(zones[workZone].PlanningDistrict))
                                {
                                    var value = _zonalWorkerCategories[emp][occ][homeZone][workZone][k];
                                    if (value > 0.0f)
                                    {
                                        writer.Write(zones[homeZone].ZoneNumber);
                                        writer.Write(',');
                                        writer.Write(zones[workZone].ZoneNumber);
                                        writer.Write(',');
                                        writer.WriteLine(value);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "The directory to write worker category rates by zone to.  This is always by zone.")]
    public FileLocation WorkerCategoryDir;

    private void WriteWorkerCategories()
    {
        if (WorkerCategoryDir == null)
        {
            return;
        }
        string dir = WorkerCategoryDir;
        EnsureDirectory(dir);
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                using var writer = new StreamWriter(Path.Combine(dir, GetPrefix(emp, occ) + ".csv"));
                writer.WriteLine("HomeZone,WorkerCategory,Split");
                for (int homeZone = 0; homeZone < zones.Length; homeZone++)
                {
                    if (!ExternalPDs.Contains(zones[homeZone].PlanningDistrict))
                    {

                        float wc0 = 0.0f, wc1 = 0.0f, wc2 = 0.0f;
                        for (int workZone = 0; workZone < zones.Length; workZone++)
                        {
                            if (!ExternalPDs.Contains(zones[workZone].PlanningDistrict))
                            {
                                var row = _zonalWorkerCategories[emp][occ][homeZone][workZone];
                                wc0 += row[0];
                                wc1 += row[1];
                                wc2 += row[2];
                            }
                        }
                        var total = wc0 + wc1 + wc2;
                        if (total > 0.0f)
                        {
                            var zi = zones[homeZone].ZoneNumber;
                            if (wc0 > 0.0f)
                            {
                                writer.Write(zi); writer.Write(','); writer.Write(1); writer.Write(','); writer.WriteLine(wc0 / total);
                            }
                            if (wc1 > 0.0f)
                            {
                                writer.Write(zi); writer.Write(','); writer.Write(2); writer.Write(','); writer.WriteLine(wc1 / total);
                            }
                            if (wc2 > 0.0f)
                            {
                                writer.Write(zi); writer.Write(','); writer.Write(3); writer.Write(','); writer.WriteLine(wc2 / total);
                            }
                        }
                    }
                }
            }
        }
    }

    [SubModelInformation(Required = false, Description = "The directory to write zonal residence by zone to.  This is always by zone.")]
    public FileLocation ZonalResidenceDir;

    private void WriteZonalResidence()
    {
        if (ZonalResidenceDir == null)
        {
            return;
        }
        string dir = ZonalResidenceDir;
        EnsureDirectory(dir);
        var zones = _zones.GetFlatData();
        for (int emp = 0; emp < 2; emp++)
        {
            for (int occ = 0; occ < 4; occ++)
            {
                using var writer = new StreamWriter(Path.Combine(dir, GetPrefix(emp, occ) + ".csv"));
                writer.WriteLine("HomeZone,Workers");
                for (int i = 0; i < zones.Length; i++)
                {
                    if (!ExternalPDs.Contains(zones[i].PlanningDistrict))
                    {
                        writer.Write(zones[i].ZoneNumber);
                        writer.Write(',');
                        writer.WriteLine(_zonalResidence[emp][occ][i]);
                    }
                }
            }
        }
    }

    string GetPrefix(int emp, int occ)
    {
        if (emp >= 2) throw new XTMFRuntimeException(this, "Emp can't be greater than or equal to 2!");
        if (occ >= 4) throw new XTMFRuntimeException(this, "Occ can't be greater than or equal to 4!");
        return
            (occ < 2 ? (occ == 0 ? "P" : "G") : (occ == 2 ? "S" : "M"))
            + (emp == 0 ? "F" : "P");
    }

    public void Load(int maxIterations)
    {
    }

    public bool RuntimeValidation(ref string error)
    {
        if (Root.Parallel)
        {
            error = $"{Name} detected the model system is running in parallel, this is not supported by this module!";
            return false;
        }
        return true;
    }
}
