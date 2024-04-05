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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.GTAModel.V2.Generation;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
using Range = Datastructure.Range;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.V2.Distribution;

[ModuleInformation(
    Description = "This module provides the mobility split for the GTAModelV2 WCAT Model while linking the PoR to PoW for each OD pair."
    )]
// ReSharper disable once InconsistentNaming
public class V2PoRPoWDistribution : IDemographicDistribution
{
    [SubModelInformation(Description = "Provides the correlation factor for different spatial segments.", Required = true)]
    public FrictionAdjustments Correlation;

    [RunParameter("Max Error", 0.01f, "What should the maximum error be? (Between 0 and 1)")]
    public float Epsilon;

    [SubModelInformation(Description = "K-Factor Data Read, Optional", Required = true)]
    public IODDataSource<float> KFactorDataReader;

    [RunParameter("Max Iterations", 300, "How many iterations should we cut of the distribution at?")]
    public int MaxIterations;

    [RunParameter("Mobility Cache File Name", "Mobility/Cache", "The location to use when storing the mobility ")]
    public FileFromOutputDirectory MobilityCacheFileName;

    [SubModelInformation(Description = "The Sets of demographic categories to blend together", Required = true)]
    public List<MultiBlendSet> MultiBlendSets;

    [ParentModel]
    public IPurpose Parent;

    [RootModule]
    public IDemographic4StepModelSystemTemplate Root;

    [SubModelInformation(Required = false, Description = "Optionally save the friction data to file.")]
    public ISaveODDataSeries<float> SaveFriction;

    [RunParameter("Simulation Time", "7:00AM", typeof(Time), "The time of day this will be simulating.")]
    public Time SimulationTime;

    [SubModelInformation(Description = "Deals with the parameters coming from the WCat model.", Required = true)]
    public WCatParameters WCatParameters;

    [DoNotAutomate]
    protected IInteractiveModeSplit InteractiveModeSplit;

    private int CurrentMultiSetIndex;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get;
        set;
    }

    public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
    {
        SaveFriction?.Reset();
        Progress = 0f;
        WCatParameters.LoadData();
        var ep = productions.GetEnumerator();
        var ea = attractions.GetEnumerator();
        var ec = category.GetEnumerator();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var ret = CpuDoublyConstrained(zones, ep, ea, ec);
        return ret;
    }

    public bool RuntimeValidation(ref string error)
    {
        InteractiveModeSplit = Parent.ModeSplit as IInteractiveModeSplit;
        if(InteractiveModeSplit == null)
        {
            error = "In module '" + Name + "' we we require the mode choice for the purpose '" + Parent.PurposeName + "' to be of type IInteractiveModeSplit!";
            return false;
        }
        return true;
    }

    private static void ClearFriction(float[][] friction, int numberOfZones)
    {
        Parallel.For(0, numberOfZones, delegate (int i)
        {
            var frictionRow = friction[i];
            for(int j = 0; j < numberOfZones; j++)
            {
                frictionRow[j] = 1.0f;
            }
        });
    }

    private static void SetupFrictionData(List<SparseArray<float>> productions, List<SparseArray<float>> attractions,
        List<PoRPoWGeneration> cats, MultiBlendSet multiset, float[][][] productionSet, float[][][] attractionSet,
        PoRPoWGeneration[][] multiCatSet)
    {
        int subsetIndex = -1;
        foreach(var blendSet in multiset.Subsets)
        {
            subsetIndex++;
            var set = blendSet.Set;
            var length = set.Count;
            int place = 0;
            int blendSetCount = 0;
            for(int i = 0; i < length; i++)
            {
                for(int pos = set[i].Start; pos <= set[i].Stop; pos++)
                {
                    blendSetCount++;
                }
            }
            productionSet[subsetIndex] = new float[blendSetCount][];
            attractionSet[subsetIndex] = new float[blendSetCount][];
            multiCatSet[subsetIndex] = new PoRPoWGeneration[blendSetCount];
            for(int i = 0; i < length; i++)
            {
                for(int pos = set[i].Start; pos <= set[i].Stop; pos++)
                {
                    productionSet[subsetIndex][place] = productions[pos].GetFlatData();
                    attractionSet[subsetIndex][place] = attractions[pos].GetFlatData();
                    multiCatSet[subsetIndex][place] = cats[pos];
                    place++;
                }
            }
        }
    }

    private void CheckSaveFriction(float[][] friction)
    {
        SaveFriction?.SaveMatrix(friction);
    }

    private void ComputeFriction(IZone[] zones, PoRPoWGeneration[][] cats, float[][][] productions, float[][][] attractions, float[][] friction, float[] production, float[] attraction)
    {
        ClearFriction(friction, zones.Length);
        InteractiveModeSplit.StartNewInteractiveModeSplit(MultiBlendSets.Count);
        using (var mobilityStream = File.OpenWrite(MobilityCacheFileName.GetFileName() + CurrentMultiSetIndex + ".bin"))
        {
            float[] subsetRatios = new float[productions.Length];
            // 1 temp friction per mobility category
            float[][] tempMobility = new float[5][];
            for(int i = 0; i < tempMobility.Length; i++)
            {
                tempMobility[i] = new float[zones.Length];
            }
            SumProductionAndAttraction(production, attraction, productions, attractions);
            BlockingCollection<MemoryStream[]> toWrite = new(1);
            var writingTask = Task.Factory.StartNew(() => WriteMemoryStream(toWrite, mobilityStream), TaskCreationOptions.LongRunning);
            for(int subset = 0; subset < cats.Length; subset++)
            {
                for(int i = 0; i < zones.Length; i++)
                {
                    // if there is no production for this origin we can just skip ahead for the next zone
                    if(production[i] == 0)
                    {
                        for(int mobilityCategory = 0; mobilityCategory < tempMobility.Length; mobilityCategory++)
                        {
                            Array.Clear(tempMobility[mobilityCategory], 0, tempMobility[mobilityCategory].Length);
                        }
                    }
                    else
                    {
                        ComputeSubsetRatios(i, subsetRatios, productions);
                        for(int mobilityCategory = 0; mobilityCategory <= 4; mobilityCategory++)
                        {
                            SetupModeChoice(cats, subset, mobilityCategory);
                            // if there is something here to process
                            GatherModeChoiceUtilities(zones, tempMobility[mobilityCategory], attraction, subsetRatios[subset], i);
                        }
                    }
                    toWrite.Add(ProcessUtilities(tempMobility, friction, subsetRatios[subset], i));
                }
            }
            toWrite.CompleteAdding();
            ConvertToFriction(friction, zones);
            writingTask.Wait();
            mobilityStream.Flush();
        }
        InteractiveModeSplit.EndInterativeModeSplit();
        CheckSaveFriction(friction);
    }

    private void ComputeSubsetRatios(int flatZone, float[] subsetRatios, float[][][] productions)
    {
        Parallel.For(0, subsetRatios.Length, subsetIndex =>
        {
            double localTotal = 0f;
            var subset = productions[subsetIndex];
            for(int i = subset.Length - 1; i >= 0; i--)
            {
                localTotal += subset[i][flatZone];
            }
            subsetRatios[subsetIndex] = (float)localTotal;
        });
        var sum = 0f;
        for(int i = 0; i < subsetRatios.Length; i++)
        {
            sum += subsetRatios[i];
        }
        if(sum <= 0) return;
        var normalFactor = 1 / sum;
        for(int subsetIndex = 0; subsetIndex < subsetRatios.Length; subsetIndex++)
        {
            subsetRatios[subsetIndex] *= normalFactor;
        }
    }

    private void ConvertToFriction(float[][] friction, IZone[] zones)
    {
        Parallel.For(0, friction.Length, delegate (int i)
        {
            var row = friction[i];
            for(int j = 0; j < row.Length; j++)
            {
                // if there was any utility
                if(row[j] == 0)
                {
                    continue;
                }
                if(!float.IsNaN(row[j]))
                {
                    // apply the K-Factor and the small trip utilities to the friction
                    row[j] = KFactorDataReader.GetDataFrom(zones[i].ZoneNumber, zones[j].ZoneNumber, CurrentMultiSetIndex)
                        * (float)Math.Pow(row[j], Correlation.GiveAdjustment(zones[i], zones[j], CurrentMultiSetIndex));
                }
                else
                {
                    row[j] = 0f;
                }
            }
        });
    }

    private IEnumerable<SparseTwinIndex<float>> CpuDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
    {
        float completed = 0f;
        Correlation.Load();
        var frictionSparse = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        var productions = new List<SparseArray<float>>();
        var attractions = new List<SparseArray<float>>();
        var cats = new List<PoRPoWGeneration>();
        WCatParameters.LoadData();
        // We need to pre load all of our generations in order to handel blending properly
        while(ep.MoveNext() && ea.MoveNext() && ec.MoveNext())
        {
            productions.Add(ep.Current);
            attractions.Add(ea.Current);
            cats.Add(ec.Current as PoRPoWGeneration);
        }
        SparseArray<float> production = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
        SparseArray<float> attraction = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
        CurrentMultiSetIndex = -1;
        foreach(var multiset in MultiBlendSets)
        {
            CurrentMultiSetIndex++;
            var numberOfSubsets = multiset.Subsets.Count;
            var productionSet = new float[numberOfSubsets][][];
            var attractionSet = new float[numberOfSubsets][][];
            var multiCatSet = new PoRPoWGeneration[numberOfSubsets][];
            SetupFrictionData(productions, attractions, cats, multiset, productionSet, attractionSet, multiCatSet);
            ComputeFriction(zones, multiCatSet, productionSet, attractionSet,
                frictionSparse.GetFlatData(), production.GetFlatData(), attraction.GetFlatData());
            // ReSharper disable once AccessToModifiedClosure
            yield return new GravityModel(frictionSparse, (p => Progress = (p * (1f / (MultiBlendSets.Count)) + (completed / (MultiBlendSets.Count)))), Epsilon, MaxIterations)
                .ProcessFlow(production, attraction, production.ValidIndexArray());
            completed += 1f;
        }
        WCatParameters.UnloadData();
        Correlation.Unload();
    }

    private void GatherModeChoiceUtilities(IZone[] zones, float[] tempMobility, float[] attraction, float ratio, int i)
    {
        // if there are no people doing this, then we don't even need to compute this!
        if(ratio <= 0f)
        {
            for(int j = 0; j < tempMobility.Length; j++)
            {
                tempMobility[j] = 0;
            }
            return;
        }

        // get the region index
        Parallel.For(0, tempMobility.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int j)
        {
            if((i == j) | zones[j].RegionNumber <= 0 | attraction[j] <= 0)
            {
                tempMobility[j] = 0;
                return;
            }
            tempMobility[j] = (float)(WCatParameters.CalculateConstantV(zones[i], zones[j], SimulationTime)
                * (Math.Pow(InteractiveModeSplit.ComputeUtility(zones[i], zones[j]), WCatParameters.LSum)));
        });
    }

    private MemoryStream[] ProcessUtilities(float[][] tempMobility, float[][] friction, float subsetRatio, int i)
    {
        if(subsetRatio > 0)
        {
            // normalize the tempFriction
            Parallel.For(0, friction.Length, j =>
             {
                 float totalMobilityUtility = 0;
                 for(int mobCat = 0; mobCat < tempMobility.Length; mobCat++)
                 {
                     var temp = tempMobility[mobCat][j];
                     if(!(float.IsNaN(temp) | float.IsInfinity(temp)))
                     {
                         totalMobilityUtility += temp;
                     }
                 }
                 for(int mobCat = 0; mobCat < tempMobility.Length; mobCat++)
                 {
                     var mobilityProbability = tempMobility[mobCat][j] / totalMobilityUtility;
                     // if the ratio is not actually a number, divide it all up
                     if(!(mobilityProbability >= float.MinValue & mobilityProbability <= float.MaxValue))
                     {
                         mobilityProbability = 0;
                     }
                     tempMobility[mobCat][j] = mobilityProbability;
                 }
                 // Apply the ratio for this age group
                 friction[i][j] *= (float)Math.Pow(totalMobilityUtility, subsetRatio);
             });
        }
        friction[i][i] = 0;
        // build the streams in parallel
        MemoryStream[] stream = new MemoryStream[tempMobility.Length];
        Parallel.For(0, stream.Length, mobility =>
            {
                // Now store the mobility probabilities to the file
                byte[] temp = new byte[tempMobility[mobility].Length * sizeof(float)];
                Buffer.BlockCopy(tempMobility[mobility], 0, temp, 0, temp.Length);
                stream[mobility] = new MemoryStream(temp);
            });
        return stream;
    }

    private void SetupModeChoice(PoRPoWGeneration[][] cats, int subset, int mobilityCategory)
    {
        // this.CurrentMultiSet == Occupation [0,3] * NumberOfMobilityCategories + mobility Category
        WCatParameters.SetDemographicCategory(CurrentMultiSetIndex * 5 + mobilityCategory);
        cats[subset][0].Mobility = new RangeSet(new List<Range> { new(mobilityCategory, mobilityCategory)});
        cats[subset][0].InitializeDemographicCategory();
    }

    private void SumProductionAndAttraction(float[] production, float[] attraction, float[][][] productions, float[][][] attractions)
    {
        // i is the zone number
        Parallel.For(0, production.Length,
            (i) =>
            {
                float productionSum = 0f;
                float attractionSum = 0f;
                // for each subset
                for(int subset = 0; subset < productions.Length; subset++)
                {
                    var productionSubset = productions[subset];
                    var attractionSubset = attractions[subset];
                    // for each blend set in the subset
                    for(int j = 0; j < productions[subset].Length; j++)
                    {
                        // add up the production
                        productionSum += productionSubset[j][i];
                        if(attraction != null)
                        {
                            attractionSum += attractionSubset[j][i];
                        }
                    }
                }
                // save the total
                production[i] = productionSum;
                if(attraction != null)
                {
                    attraction[i] = attractionSum;
                    // make sure attraction is always >= 0
                    if(attraction[i] < 0)
                    {
                        attraction[i] = 0;
                    }
                }
            });
    }

    private void WriteMemoryStream(BlockingCollection<MemoryStream[]> writeInOrder, Stream writeToStream)
    {
        foreach(var streamSet in writeInOrder.GetConsumingEnumerable())
        {
            foreach(var stream in streamSet)
            {
                stream.WriteTo(writeToStream);
                // release the stream now that we are done with it
                stream.Dispose();
            }
            writeToStream.Flush();
        }
    }

    public class FrictionAdjustments : IModule
    {
        [Parameter("Adjustment Matrix File", "Distribution/WorkModeAdjustments.csv", typeof(FileFromInputDirectory),
            "The file that contains the mode adjustments.  In CSV form (Occ,OriginPdStart,OriginPdEnd,DestinationPDStart,DesinstaionPDEnd,[1 column for each mode])")]
        public FileFromInputDirectory InputFile;

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

        public Tuple<byte, byte, byte> ProgressColour => null;

        public float GiveAdjustment(IZone origin, IZone destination, int occupation)
        {
            var oPD = origin.PlanningDistrict;
            var dPD = destination.PlanningDistrict;
            var row = Data[0];
            var adjFactor = 1f;
            for(int i = 0; i < row.Length; i++)
            {
                if(row[i].Origin.ContainsInclusive(oPD) & row[i].Destination.ContainsInclusive(dPD))
                {
                    adjFactor *= row[i].ModificationForMode[occupation];
                }
            }
            return adjFactor;
        }

        public void Load()
        {
            List<Segment>[] temp = new List<Segment>[1];
            for(int i = 0; i < temp.Length; i++)
            {
                temp[i] = [];
            }
            var numberOfModes = 4;
            using (CsvReader reader = new(InputFile.GetFileName(Root.InputBaseDirectory)))
            {
                // burn header
                reader.LoadLine();
                while(!reader.EndOfFile)
                {
                    if(reader.LoadLine() >= numberOfModes + 5)
                    {
                        reader.Get(out int os, 1);
                        reader.Get(out int oe, 2);
                        reader.Get(out int ds, 3);
                        reader.Get(out int de, 4);
                        float[] modeData = new float[numberOfModes];
                        for(int i = 0; i < modeData.Length; i++)
                        {
                            reader.Get(out modeData[i], 5 + i);
                        }
                        temp[0].Add(new Segment
                        {
                            Origin = new Range(os, oe),
                            Destination = new Range(ds, de),
                            ModificationForMode = modeData
                        });
                    }
                }
            }
            Data = new Segment[1][];
            for(int i = 0; i < Data.Length; i++)
            {
                Data[i] = temp[i].ToArray();
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
}