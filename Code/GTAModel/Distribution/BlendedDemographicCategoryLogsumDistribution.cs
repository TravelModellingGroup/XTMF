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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel
{
    [ModuleInformation(Description = "If you are using Spatial Parameters, the first data point needs to be the impedance and the second needs to be the short trip factor if there is one.")]
    public class BlendedDemographicCategoryLogsumDistribution : IDemographicDistribution
    {
        [RunParameter("Allow Intrazonal", true, "Should we be allowed to make intrazonal linkages?")]
        public bool AllowIntrazonal;

        [RunParameter("Save Attraction", "", typeof(FileFromOutputDirectory), "The CSV filename to save the attraction per zones per category to.  Leave this blank to not save.")]
        public FileFromOutputDirectory AttractionFile;

        [RunParameter("Warm BalanceFactors", "", typeof(FileFromOutputDirectory), "The start of the name of the file (First Default = Balance.bin). If this is empty nothing will be saved.")]
        public FileFromOutputDirectory BalanceFactors;

        [RunParameter("Cull Small Values", false, "When building the distribution, remove the small distributions and move them out to other zones, for doubly constrained only.")]
        public bool CullSmallValues;

        [RunParameter("Max Error", 0.01f, "What should the maximum error be? (Between 0 and 1)")]
        public float Epsilon;

        [RunParameter("Correlation of Modes", 1f, "The correlation between the different modes. 1 means no correlation to 0 meaning perfect.  Only used if there is no Spatial Parameters.")]
        public float ImpedanceParameter;

        [SubModelInformation(Description = "K-Factor Data Read, Optional", Required = false)]
        public IODDataSource<float> KFactorDataReader;

        [RunParameter("Load Friction File Name", "", "The start of the name of the file (First Default = FrictionCache1.bin) to load for friction, leaving this empty will generate new friction.")]
        public string LoadFrictionFileName;

        [RunParameter("MaxFac", 0.5f, "The culling factor from the max value in the column or row.")]
        public float MaxFac;

        [RunParameter("Max Iterations", 300, "How many iterations should we cut of the distribution at?")]
        public int MaxIterations;

        [SubModelInformation(Description = "The Sets of demographic categories to blend together", Required = true)]
        public List<MultiBlendSet> MultiBlendSets;

        [ParentModel]
        public IPurpose Parent;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter("Save Friction File Name", "", "The start of the name of the file (First Default = FrictionCache1.bin). If this is empty nothing will be saved.")]
        public string SaveFrictionFileName;

        [RunParameter("Simulation Time", "7:00AM", typeof(Time), "The time of day this will be simulating.")]
        public Time SimulationTime;

        [RunParameter("Small Trip Length", 5000f, "If a trip is shorter than this and you are using spatial parameters then it will apply the short trip utility for that trip.")]
        public float SmallTripLength;

        [SubModelInformation(Description = "An optional source for gathering information to use for impedance parameters and for short trip utility modifiers.", Required = false)]
        public IODDataSource<float[]> SpatialParameters;

        [RunParameter("Swap Attraction", false, "Switch attraction with production from generation.")]
        public bool SwapAttraction;

        [RunParameter("Transpose Distribution", false, "Transpose the final result of the model.")]
        public bool Transpose;

        [RunParameter("trpfac", 0.01f, "The culling factor to apply to the labour force and employment.")]
        public float TripFac;

        [RunParameter("trpmin", 0.0001f, "The culling factor used as a minimum for the labour and employment.")]
        public float TripMin;

        [DoNotAutomate]
        protected IInteractiveModeSplit InteractiveModeSplit;

        private int CurrentMultiSetIndex;

        private int CurrentNumber;

        private int LastIteration = -1;

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

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
        {
            Progress = 0f;
            var ep = SwapAttraction ? attractions.GetEnumerator() : productions.GetEnumerator();
            var ea = SwapAttraction ? productions.GetEnumerator() : attractions.GetEnumerator();
            var ec = category.GetEnumerator();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            foreach (var ret in SolveDoublyConstrained(zones, ep, ea, ec))
            {
                if (Transpose)
                {
                    TransposeMatrix(ret);
                }
                yield return ret;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            InteractiveModeSplit = Parent.ModeSplit as IInteractiveModeSplit;
            if (InteractiveModeSplit == null)
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
               for (int j = 0; j < numberOfZones; j++)
               {
                   frictionRow[j] = float.NaN;
               }
           });
        }

        private static void SetupFrictionData(List<SparseArray<float>> productions, List<SparseArray<float>> attractions,
            List<IDemographicCategory> cats, MultiBlendSet multiset, float[][][] productionSet, float[][][] attractionSet,
            IDemographicCategory[][] multiCatSet)
        {
            int subsetIndex = -1;
            foreach (var blendSet in multiset.Subsets)
            {
                subsetIndex++;
                var set = blendSet.Set;
                var length = set.Count;
                int place = 0;
                int blendSetCount = 0;
                for (int i = 0; i < length; i++)
                {
                    for (int pos = set[i].Start; pos <= set[i].Stop; pos++)
                    {
                        blendSetCount++;
                    }
                }
                productionSet[subsetIndex] = new float[blendSetCount][];
                attractionSet[subsetIndex] = new float[blendSetCount][];
                multiCatSet[subsetIndex] = new IDemographicCategory[blendSetCount];
                for (int i = 0; i < length; i++)
                {
                    for (int pos = set[i].Start; pos <= set[i].Stop; pos++)
                    {
                        productionSet[subsetIndex][place] = productions[pos].GetFlatData();
                        attractionSet[subsetIndex][place] = attractions[pos].GetFlatData();
                        multiCatSet[subsetIndex][place] = cats[pos];
                        place++;
                    }
                }
            }
        }

        private static void SetupModeChoice(IDemographicCategory[][] cats, float[] ratio, IModeParameterDatabase mpd, int subset)
        {
            mpd.InitializeBlend();
            for (int j = 0; j < cats[subset].Length; j++)
            {
                mpd.SetBlendWeight(ratio[j]);
                cats[subset][j].InitializeDemographicCategory();
            }
            mpd.CompleteBlend();
        }

        private static void TransposeMatrix(SparseTwinIndex<float> ret)
        {
            var flatData = ret.GetFlatData();
            var length = flatData.Length;
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    var temp = flatData[i][j];
                    flatData[i][j] = flatData[j][i];
                    flatData[j][i] = temp;
                }
            }
        }

        private void CheckSaveFriction(float[][] friction)
        {
            if (!CullSmallValues)
            {
                if (!String.IsNullOrWhiteSpace(SaveFrictionFileName))
                {
                    SaveFriction(friction);
                }
            }
        }

        private void ComputeFriction(IZone[] zones, IDemographicCategory[][] cats, float[][][] productions, float[][][] attractions, float[][] friction, float[] production, float[] attraction)
        {
            var numberOfZones = zones.Length;
            bool loadedFriction = false;
            if (!String.IsNullOrWhiteSpace(LoadFrictionFileName))
            {
                LoadFriction(friction, -1);
                loadedFriction = true;
            }
            else
            {
                ClearFriction(friction, numberOfZones);
            }

            var mpd = Root.ModeParameterDatabase;
            float[] subsetRatios = new float[productions.Length];
            SumProductionAndAttraction(production, attraction, productions, attractions);
            for (int subset = 0; subset < cats.Length; subset++)
            {
                InteractiveModeSplit.StartNewInteractiveModeSplit(MultiBlendSets.Count);
                float[] ratio = new float[cats[subset].Length];
                for (int i = 0; i < numberOfZones; i++)
                {
                    ComputeSubsetRatios(i, subsetRatios, productions);
                    ProcessBlendsetRatio(i, ratio, productions[subset]);
                    // if there is no production for this origin we can just skip ahead for the next zone
                    if (production[i] == 0)
                    {
                        continue;
                    }
                    // if there is something here to process
                    SetupModeChoice(cats, ratio, mpd, subset);
                    GatherUtilities(zones, friction, attraction, numberOfZones, loadedFriction, subsetRatios, subset, i);
                }
                InteractiveModeSplit.EndInterativeModeSplit();
            }
            ConvertToFriction(friction, zones);
            CheckSaveFriction(friction);
        }

        private void ComputeSubsetRatios(int flatZone, float[] subsetRatios, float[][][] productions)
        {
            for (int subsetIndex = 0; subsetIndex < subsetRatios.Length; subsetIndex++)
            {
                double localTotal = 0f;
                var subset = productions[subsetIndex];
                for (int i = subset.Length - 1; i >= 0; i--)
                {
                    localTotal += subset[i][flatZone];
                }
                subsetRatios[subsetIndex] = (float)localTotal;
            }
            var sum = subsetRatios.Sum();
            if (sum <= 0) return;
            var normalFactor = 1 / sum;
            for (int subsetIndex = 0; subsetIndex < subsetRatios.Length; subsetIndex++)
            {
                subsetRatios[subsetIndex] *= normalFactor;
            }
        }

        private void ConvertToFriction(float[][] friction, IZone[] zones)
        {
            var distances = Root.ZoneSystem.Distances.GetFlatData();
            Parallel.For(0, friction.Length, delegate (int i)
           {
               var frictionRow = friction[i];
               for (int j = 0; j < frictionRow.Length; j++)
               {
                   if (frictionRow[j] == 0)
                   {
                       continue;
                   }
                   // if there was any utility
                   float[] data;
                   if ((!float.IsNaN(frictionRow[j]))
                       && (data = SpatialParameters.GetDataFrom(zones[i].ZoneNumber, zones[j].ZoneNumber, CurrentMultiSetIndex)) != null)
                   {
                       // apply the K-Factor and the small trip utilities to the friction
                       frictionRow[j] = (KFactorDataReader != null ? KFactorDataReader.GetDataFrom(zones[i].ZoneNumber, zones[j].ZoneNumber, CurrentMultiSetIndex) : 1.0f)
                          * frictionRow[j]
                          // now apply the small trip data
                          * (float)((distances[i][j] <= SmallTripLength ? Math.Exp(data[1]) : 1.0));
                   }
                   else
                   {
                       frictionRow[j] = 0f;
                   }
               }
           });
        }

        private IEnumerable<SparseTwinIndex<float>> CpuDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            float completed = 0f;
            var frictionSparse = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var productions = new List<SparseArray<float>>();
            var attractions = new List<SparseArray<float>>();
            var cats = new List<IDemographicCategory>();
            // We need to pre load all of our generations in order to handle blending properly
            while (ep.MoveNext() & ea.MoveNext() & ec.MoveNext())
            {
                productions.Add(ep.Current);
                attractions.Add(ea.Current);
                cats.Add(ec.Current);
            }
            SparseArray<float> production = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            SparseArray<float> attraction = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            CurrentMultiSetIndex = -1;
            foreach (var multiset in MultiBlendSets)
            {
                CurrentMultiSetIndex++;
                var numberOfSubsets = multiset.Subsets.Count;
                var productionSet = new float[numberOfSubsets][][];
                var attractionSet = new float[numberOfSubsets][][];
                var multiCatSet = new IDemographicCategory[numberOfSubsets][];
                SetupFrictionData(productions, attractions, cats, multiset, productionSet, attractionSet, multiCatSet);
                ComputeFriction(zones, multiCatSet, productionSet, attractionSet,
                    frictionSparse.GetFlatData(), production.GetFlatData(), attraction.GetFlatData());
                SparseArray<float> balanceFactors = GetWarmBalancingFactors(attraction, out string balanceFileName);
                if (CullSmallValues)
                {
                    var tempValues = new GravityModel(frictionSparse, null, Epsilon, MaxIterations)
                    .ProcessFlow(production, attraction, production.ValidIndexArray(), balanceFactors);
                    Cull(tempValues, frictionSparse.GetFlatData(), production.GetFlatData(), attraction.GetFlatData());
                    if (!String.IsNullOrWhiteSpace(SaveFrictionFileName))
                    {
                        SaveFriction(frictionSparse.GetFlatData());
                    }
                }
                // ReSharper disable once AccessToModifiedClosure
                yield return new GravityModel(frictionSparse, (p => Progress = (p * (1f / (MultiBlendSets.Count)) + (completed / (MultiBlendSets.Count)))), Epsilon, MaxIterations)
                    .ProcessFlow(production, attraction, production.ValidIndexArray(), balanceFactors);
                if (balanceFileName != null)
                {
                    SaveBalanceFactors(balanceFileName, balanceFactors);
                }
                completed += 1f;
            }
        }

        private void Cull(SparseTwinIndex<float> tempValues, float[][] friction, float[] production, float[] attraction)
        {
            var flatValues = tempValues.GetFlatData();
            var numberOfZones = flatValues.Length;
            var omax = new float[numberOfZones];
            var dmax = new float[numberOfZones];
            for (int i = 0; i < flatValues.Length; i++)
            {
                for (int j = 0; j < numberOfZones; j++)
                {
                    if (flatValues[i][j] >= omax[i])
                    {
                        omax[i] = flatValues[i][j];
                    }
                }
                for (int j = 0; j < numberOfZones; j++)
                {
                    if (flatValues[j][i] >= dmax[i])
                    {
                        dmax[i] = flatValues[j][i];
                    }
                }
            }
            Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int i)
           {
               for (int j = 0; j < numberOfZones; j++)
               {
                   if (TestCull(flatValues, i, j, omax, dmax, production, attraction))
                   {
                       friction[i][j] = 0f;
                   }
               }
           });
        }

        private void GatherUtilities(IZone[] zones, float[][] friction, float[] attraction, int numberOfZones, bool loadedFriction, float[] subsetRatios, int subset, int i)
        {
            if (loadedFriction)
            {
                Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int j)
               {
                   if (zones[j].RegionNumber <= 0) return;
                   if (Transpose)
                   {
                       InteractiveModeSplit.ComputeUtility(zones[j], zones[i]);
                   }
                   else
                   {
                       InteractiveModeSplit.ComputeUtility(zones[i], zones[j]);
                   }
               });
            }
            else
            {
                // if there are no people doing this, then we don't even need to compute this!
                if (subsetRatios[subset] <= 0f)
                {
                    return;
                }
                Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int j)
               {
                   if (((!AllowIntrazonal) & (i == j)) | zones[j].RegionNumber <= 0) return;
                   if (attraction != null && attraction[j] > 0)
                   {
                       float utility;
                       if (Transpose)
                       {
                           utility = InteractiveModeSplit.ComputeUtility(zones[j], zones[i]);
                       }
                       else
                       {
                           utility = InteractiveModeSplit.ComputeUtility(zones[i], zones[j]);
                       }
                       if (!float.IsNaN(utility))
                       {
                           var data = SpatialParameters.GetDataFrom(zones[i].ZoneNumber, zones[j].ZoneNumber, CurrentMultiSetIndex);
                           if (data != null)
                           {
                               // it is multiplication not addition
                               if (float.IsNaN(friction[i][j]))
                               {
                                   friction[i][j] = (float)Math.Pow(utility, data[0] * subsetRatios[subset]);
                               }
                               else
                               {
                                   friction[i][j] *= (float)Math.Pow(utility, data[0] * subsetRatios[subset]);
                               }
                           }
                       }
                   }
               });
            }
        }

        private string GetFrictionFileName(string baseName, int setNumber)
        {
            if (Root.CurrentIteration != LastIteration)
            {
                CurrentNumber = 0;
                LastIteration = Root.CurrentIteration;
            }
            return String.Concat(baseName, setNumber >= 0 ? setNumber : (CurrentNumber++), ".bin");
        }

        private SparseArray<float> GetWarmBalancingFactors(SparseArray<float> attraction, out string balanceFileName)
        {
            SparseArray<float> balanceFactors = null;
            if (BalanceFactors.ContainsFileName())
            {
                balanceFileName = BalanceFactors.GetFileName() + CurrentMultiSetIndex + ".bin";
                if (File.Exists(balanceFileName))
                {
                    balanceFactors = LoadBalanceFactors(balanceFileName);
                }
                else
                {
                    balanceFactors = attraction.CreateSimilarArray<float>();
                    var flatFactors = balanceFactors.GetFlatData();
                    // initialize the factors to 1
                    for (int i = 0; i < flatFactors.Length; i++)
                    {
                        flatFactors[i] = 1.0f;
                    }
                }
            }
            else
            {
                balanceFileName = null;
            }
            return balanceFactors;
        }

        private SparseArray<float> LoadBalanceFactors(string balanceFileName)
        {
            var ret = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            var flatRet = ret.GetFlatData();
            using (BinaryReader reader = new BinaryReader(File.OpenRead(balanceFileName)))
            {
                if (reader.BaseStream.Length < flatRet.Length * 4)
                {
                    throw new XTMFRuntimeException(this, "The balancing factor binary cache does not contain enough data!");
                }
                for (int i = 0; i < flatRet.Length; i++)
                {
                    flatRet[i] = reader.ReadSingle();
                }
            }
            return ret;
        }

        private void LoadFriction(float[][] ret, int setNumber)
        {
            try
            {
                Stream file = null;
                try
                {
                    file = File.OpenRead(GetFrictionFileName(LoadFrictionFileName, setNumber));
                    using BinaryReader reader = new BinaryReader(file);
                    file = null;
                    for (int i = 0; i < ret.Length; i++)
                    {
                        for (int j = 0; j < ret[i].Length; j++)
                        {
                            ret[i][j] = reader.ReadSingle();
                        }
                    }
                }
                finally
                {
                    if (file != null)
                    {
                        file.Dispose();
                    }
                }
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException(this, "Unable to load distribution cache file!\r\n" + e.Message);
            }
        }

        private void ProcessBlendsetRatio(int i, float[] ratio, float[][] productions)
        {
            var denom = 0f;
            for (int j = 0; j < ratio.Length; j++)
            {
                denom += productions[j][i];
            }
            if (denom != 0)
            {
                denom = 1 / denom;
            }
            for (int j = 0; j < ratio.Length; j++)
            {
                if (denom == 0)
                {
                    ratio[j] = 0;
                }
                else
                {
                    ratio[j] = productions[j][i] * denom;
                }
            }
        }

        private void SaveAttractionFile(float[] attraction)
        {
            bool first = !File.Exists(AttractionFile.GetFileName());
            using StreamWriter writer = new StreamWriter(AttractionFile.GetFileName(), true);
            if (first)
            {
                writer.WriteLine("Generation,Category,Zone,Attraction");
            }
            var startOfLine = Root.CurrentIteration + "," + CurrentMultiSetIndex + ",";
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            for (int i = 0; i < attraction.Length; i++)
            {
                writer.Write(startOfLine);
                writer.Write(zones[i].ZoneNumber);
                writer.Write(',');
                writer.WriteLine(attraction[i]);
            }
        }

        private void SaveBalanceFactors(string balanceFileName, SparseArray<float> balanceFactors)
        {
            var flat = balanceFactors.GetFlatData();
            using BinaryWriter writer = new BinaryWriter(File.OpenWrite(balanceFileName));
            for (int i = 0; i < flat.Length; i++)
            {
                writer.Write(flat[i]);
            }
        }

        private void SaveFriction(float[][] ret)
        {
            try
            {
                var fileName = GetFrictionFileName(SaveFrictionFileName, -1);
                var dirName = Path.GetDirectoryName(fileName);
                if (dirName == null)
                {
                    throw new XTMFRuntimeException(this, $"In {Name} we were unable to get the directory name from the file {fileName}!");
                }
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }
                Stream file = null;
                try
                {
                    file = File.OpenWrite(fileName);
                    using BinaryWriter writer = new BinaryWriter(file);
                    file = null;
                    for (int i = 0; i < ret.Length; i++)
                    {
                        for (int j = 0; j < ret[i].Length; j++)
                        {
                            writer.Write(ret[i][j]);
                        }
                    }
                }
                finally
                {
                    if (file != null)
                    {
                        file.Dispose();
                    }
                }
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException(this, "Unable to save distribution cache file!\r\n" + e.Message);
            }
        }

        private IEnumerable<SparseTwinIndex<float>> SolveDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            return CpuDoublyConstrained(zones, ep, ea, ec);
        }

        private void SumProductionAndAttraction(float[] production, float[] attraction, float[][][] productions, float[][][] attractions)
        {
            // i is the zone number
            for (int i = 0; i < production.Length; i++)
            {
                float productionSum = 0f;
                float attractionSum = 0f;
                // for each subset
                for (int subset = 0; subset < productions.Length; subset++)
                {
                    // for each blend set in the subset
                    for (int j = 0; j < productions[subset].Length; j++)
                    {
                        // add up the production
                        productionSum += productions[subset][j][i];
                        if (attraction != null)
                        {
                            attractionSum += attractions[subset][j][i];
                        }
                    }
                }
                // save the total
                production[i] = productionSum;
                if (attraction != null)
                {
                    attraction[i] = attractionSum;
                    // make sure attraction is always >= 0
                    if (attraction[i] < 0)
                    {
                        attraction[i] = 0;
                    }
                    else
                    {
                    }
                }
            }

            if (attraction != null)
            {
                if (AttractionFile.ContainsFileName())
                {
                    SaveAttractionFile(attraction);
                }
            }
        }

        private bool TestCull(float[][] flatValues, int o, int d, float[] omax, float[] dmax, float[] production, float[] attraction)
        {
            // do the quick test first
            if (flatValues[o][d] >= 1f) return false;
            float tmino = Math.Max(TripFac * production[o], TripMin);
            float tmind = Math.Max(TripFac * attraction[d], TripMin);

            var val = flatValues[o][d];
            return !((val >= omax[o] * MaxFac)
                | (val >= dmax[d] * MaxFac
                | (val >= tmino)
                | (val >= tmind)));
        }
    }
}