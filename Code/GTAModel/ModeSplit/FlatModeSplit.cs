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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
using Range = Datastructure.Range;

namespace TMG.GTAModel.ModeSplit;

public class FlatModeSplit : IInteractiveModeSplit
{
    [SubModelInformation(Required = false, Description = "Apply factors to the exponated utility of modes")]
    public ModeAdjustments Adjustments;

    [RootModule]
    public I4StepModel Root;

    [RunParameter("Simulation Time", "7:00 AM", typeof(Time), "The time that this mode split will be running as.")]
    public Time SimulationTime;

    private float CurrentInteractiveCategory;
    private float[] CurrentUtility;
    private bool InterativeMode;
    private bool LoadedAdjustments = false;
    private IModeChoiceNode[] Modes;
    private int NumberOfInteractiveCategories;

    private TreeData<float[][]>[] Results;
    private IZone[] Zones;

    private SparseArray<IZone> ZoneSystem;

    public string Name { get; set; }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public float ComputeUtility(IZone o, IZone d)
    {
        float sum = 0f;
        var flatO = ZoneSystem.GetFlatIndex(o.ZoneNumber);
        var flatD = ZoneSystem.GetFlatIndex(d.ZoneNumber);
        bool any = false;
        var zoneIndex = (flatO * Zones.Length + flatD) * Modes.Length;
        for (int mode = 0; mode < Modes.Length; mode++)
        {
            EnsureResult(flatO, mode);
            if (Modes[mode].Feasible(o, d, SimulationTime))
            {
                var res = Modes[mode].CalculateV(o, d, SimulationTime);
                if (!float.IsNaN(res))
                {
                    float v = (float)Math.Exp(res);
                    if (Adjustments != null)
                    {
                        v *= Adjustments.GiveAdjustment(o, d, mode, (int)CurrentInteractiveCategory);
                    }
                    CurrentUtility[zoneIndex + mode] = v;
                    sum += v;
                    any = true;
                }
            }
        }
        return any ? sum : float.NaN;
    }

    public void EndInterativeModeSplit()
    {
        Results = null;
        CurrentUtility = null;
    }

    public List<TreeData<float[][]>> ModeSplit(IEnumerable<SparseTwinIndex<float>> flowMatrix, int numberOfCategories)
    {
        if (Modes == null)
        {
            SetModes();
        }
        Progress = 0f;
        CurrentInteractiveCategory = 0;
        foreach (var matrix in flowMatrix)
        {
            AddModeSplit(matrix);
            CurrentInteractiveCategory++;
        }
        Progress = 1f;
        return CreateList();
    }

    public List<TreeData<float[][]>> ModeSplit(SparseTwinIndex<float> flowMatrix)
    {
        AddModeSplit(flowMatrix);
        return CreateList();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void StartNewInteractiveModeSplit(int numberOfInteractiveCategories)
    {
        NumberOfInteractiveCategories = numberOfInteractiveCategories;
        ZoneSystem = Root.ZoneSystem.ZoneArray;
        Zones = ZoneSystem.GetFlatData();
        InterativeMode = true;
        SetModes();
        InitializeResults();
        if (!LoadedAdjustments & Adjustments != null)
        {
            Adjustments.Load();
        }
    }

    private void AddModeSplit(SparseTwinIndex<float> matrix)
    {
        if (Results == null)
        {
            InitializeResults();
        }
        if (InterativeMode)
        {
            ProduceResultsForInteractive(matrix.GetFlatData());
        }
        else
        {
            throw new XTMFRuntimeException(this, "Only Interactive mode is supported!");
        }
    }

    private List<TreeData<float[][]>> CreateList()
    {
        var ret = new List<TreeData<float[][]>>(Results.Length);
        for (int i = 0; i < Results.Length; i++)
        {
            ret.Add(Results[i]);
        }
        EndInterativeModeSplit();
        return ret;
    }

    private void EnsureResult(int flatO, int mode)
    {
        if (Results[mode].Result[flatO] == null)
        {
            lock (Results)
            {
                Thread.MemoryBarrier();
                if (Results[mode].Result[flatO] == null)
                {
                    Results[mode].Result[flatO] = new float[Zones.Length];
                    Thread.MemoryBarrier();
                }
            }
        }
    }

    private void InitializeResults()
    {
        var numberOfZones = Zones.Length;
        var numberOfModes = Root.Modes.Count;
        if (CurrentUtility == null)
        {
            CurrentUtility = new float[numberOfZones * numberOfZones * numberOfModes];
        }
        // in all cases reset this value
        for (int i = 0; i < CurrentUtility.Length; i++)
        {
            CurrentUtility[i] = float.NaN;
        }
        if (Results == null)
        {
            Results = new TreeData<float[][]>[numberOfModes];
            for (int i = 0; i < Results.Length; i++)
            {
                Results[i] = new TreeData<float[][]>
                {
                    Result = new float[numberOfZones][]
                };
            }
        }
    }

    private void ProduceResultsForInteractive(float[][] flows)
    {
        Parallel.For(0, flows.Length, flatO =>
           {
               var row = flows[flatO];
               if (row == null) return;
               var numberOfModes = Modes.Length;
               for (int j = 0; j < row.Length; j++)
               {
                   var flow = flows[flatO][j];
                    // skip processing this OD if there are no trips between them
                    if (flow <= 0) continue;
                   var zoneIndex = (flatO * row.Length + j) * numberOfModes;
                    // get the sum
                    float sum = 0f;
                   bool any = false;
                   for (int mode = 0; mode < numberOfModes; mode++)
                   {
                       var cur = CurrentUtility[zoneIndex + mode];
                       if (!float.IsNaN(cur))
                       {
                           sum += cur;
                           any = true;
                       }
                   }
                   if (!any)
                   {
                       continue;
                   }
                    // procude probabilities
                    var factor = 1 / sum;
                   for (int mode = 0; mode < numberOfModes; mode++)
                   {
                       var temp = CurrentUtility[zoneIndex + mode] * factor;
                       if (!float.IsNaN(temp))
                       {
                           Results[mode].Result[flatO][j] += temp * flow;
                       }
                   }
               }
           });
        Progress = ((CurrentInteractiveCategory + 1) / NumberOfInteractiveCategories);
    }

    private void SetModes()
    {
        if (Modes == null)
        {
            Modes = new IModeChoiceNode[Root.Modes.Count];
            for (int i = 0; i < Modes.Length; i++)
            {
                Modes[i] = Root.Modes[i];
            }
        }
    }
}

public class ModeAdjustments : IModule
{
    [Parameter("Adjustment Matrix File", "Distribution/WorkModeAdjustments.csv", typeof(FileFromInputDirectory),
        "The file that contains the mode adjustments.  In CSV form (Occ,OriginPdStart,OriginPdEnd,DestinationPDStart,DesinstaionPDEnd,[1 column for each mode])")]
    public FileFromInputDirectory InputFile;

    [Parameter("Matrices Per Occupation", 20, "The number of matrices processed before switching occupation.")]
    public int MatriciesPerOccupation;

    [RunParameter("Number of Occupations", 4, "The number of different occupations for this model.")]
    public int NumberOfOccupations;

    [RunParameter("Occupation Start Index", 1, "The number for the first occupation.")]
    public int OccupationStartIndex;

    [RootModule]
    public I4StepModel Root;

    private Segment[][] Data;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public float GiveAdjustment(IZone origin, IZone destination, int mode, int currentMatrix)
    {
        var occNumber = currentMatrix / MatriciesPerOccupation;
        var oPD = origin.PlanningDistrict;
        var dPD = destination.PlanningDistrict;
        var row = Data[occNumber];
        var adjFactor = 1f;
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i].Origin.ContainsInclusive(oPD) & row[i].Destination.ContainsInclusive(dPD))
            {
                adjFactor *= row[i].ModificationForMode[mode];
            }
        }
        return adjFactor;
    }

    public void Load()
    {
        List<Segment>[] temp = new List<Segment>[NumberOfOccupations];
        for (int i = 0; i < temp.Length; i++)
        {
            temp[i] = [];
        }
        var numberOfModes = Root.Modes.Count;
        using (CsvReader reader = new(InputFile.GetFileName(Root.InputBaseDirectory)))
        {
            // burn header
            reader.LoadLine();
            while (!reader.EndOfFile)
            {
                if (reader.LoadLine() >= numberOfModes + 5)
                {
                    reader.Get(out int occ, 0);
                    reader.Get(out int os, 1);
                    reader.Get(out int oe, 2);
                    reader.Get(out int ds, 3);
                    reader.Get(out int de, 4);
                    float[] modeData = new float[numberOfModes];
                    for (int i = 0; i < modeData.Length; i++)
                    {
                        reader.Get(out modeData[i], 5 + i);
                    }
                    temp[occ - OccupationStartIndex].Add(new Segment
                    {
                        Origin = new Range(os, oe),
                        Destination = new Range(ds, de),
                        ModificationForMode = modeData
                    });
                }
            }
        }
        Data = new Segment[NumberOfOccupations][];
        for (int i = 0; i < Data.Length; i++)
        {
            Data[i] = [.. temp[i]];
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Unload()
    {
        Data = null;
    }

    private struct Segment
    {
        internal Range Destination;
        internal float[] ModificationForMode;
        internal Range Origin;
    }
}