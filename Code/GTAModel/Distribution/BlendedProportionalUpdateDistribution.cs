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
using TMG.Functions;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel
{
    [ModuleInformation(Description = "This module provide the distribution for the school models and HBW/WBH")]
    public class BlendedProportionalUpdateDistribution : IDemographicDistribution
    {
        [SubModelInformation(Description = "The base data that we will fit against.", Required = false)]
        public
            List<IReadODData<float>> BaseData;

        [RunParameter("Load Friction File Name", "",
            "The start of the name of the file (First Default = FrictionCache1.bin) to load for friction, leaving this empty will generate new friction."
        )]
        public string LoadFrictionFileName;

        [SubModelInformation(Description = "The Sets of demographic categories to blend together", Required = true)]
        public List<MultiBlendSet> MultiBlendSets;

        [ParentModel]
        public IPurpose Parent;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter("Save Friction File Name", "",
            "The start of the name of the file (First Default = FrictionCache1.bin). If this is empty nothing will be saved."
        )]
        public string SaveFrictionFileName;

        [RunParameter("Transpose Distribution", false, "Transpose the final result of the model.")]
        public bool
            Transpose;

        [RunParameter("Production Ratios", false,
            "The production from generation contains a generation rate and attraction contains the total number of people."
        )]
        public bool UseProductionPercentages;

        [DoNotAutomate]
        protected IInteractiveModeSplit InteractiveModeSplit;

        private int CurrentNumber;

        private int LastIteration = -1;

        private int TotalBlendSets;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> production,
            IEnumerable<SparseArray<float>> attraction, IEnumerable<IDemographicCategory> category)
        {
            Progress = 0f;
            using (var ep = production.GetEnumerator())
            using (var ea = attraction.GetEnumerator())
            using (var eCat = category.GetEnumerator())
            {
                var zones = Root.ZoneSystem.ZoneArray;
                if (String.IsNullOrWhiteSpace(LoadFrictionFileName))
                {
                    if (BaseData.Count != MultiBlendSets.Count)
                    {
                        throw new XTMFRuntimeException("In " + Name +
                                                       " the number of BaseData entries is not the same as the number of Blend Sets!");
                    }
                }
                var productions = new List<SparseArray<float>>();
                var attractions = new List<SparseArray<float>>();
                var cats = new List<IDemographicCategory>();
                // We need to pre-load all of our generations in order to handle blending properly
                while (ep.MoveNext() && ea.MoveNext() && eCat.MoveNext())
                {
                    productions.Add(ep.Current);
                    attractions.Add(ea.Current);
                    cats.Add(eCat.Current);
                }
                int setNumber = -1;
                var ret = zones.CreateSquareTwinArray<float>();
                float[] p = new float[zones.GetFlatData().Length];
                CountTotalBlendSets();
                foreach (var multiset in MultiBlendSets)
                {
                    setNumber++;
                    var numberOfBlendSets = multiset.Subsets.Count;
                    var productionSet = new float[numberOfBlendSets][][];
                    var attractionSet = new float[numberOfBlendSets][][];
                    var catSet = new IDemographicCategory[numberOfBlendSets][];
                    SetupFrictionData(productions, attractions, cats, multiset, productionSet, attractionSet, catSet);
                    for (int subsetIndex = 0; subsetIndex < multiset.Subsets.Count; subsetIndex++)
                    {
                        SumProductionAndAttraction(p, productionSet[subsetIndex]);
                        bool loadedFriction = false;
                        // use the base data if we don't load in the friction base data
                        if (String.IsNullOrWhiteSpace(LoadFrictionFileName))
                        {
                            LoadInBaseData(ret, BaseData[setNumber]);
                        }
                        else
                        {
                            LoadFriction(ret.GetFlatData(), setNumber);
                            loadedFriction = true;
                        }
                        UpdateData(ret.GetFlatData(), p, catSet, productionSet, attractionSet, zones.GetFlatData(),
                            subsetIndex, loadedFriction);
                        yield return ret;
                    }
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            InteractiveModeSplit = Parent.ModeSplit as IInteractiveModeSplit;
            if (InteractiveModeSplit == null)
            {
                error = "In module '" + Name + "' it is required that the mode choice module to be of type IInteractiveModeSplit!";
                return false;
            }
            return true;
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

        private static void SetupModeSplit(IDemographicCategory[][] cats, int subset, IModeParameterDatabase mpd, float[] ratio)
        {
            mpd.InitializeBlend();
            for (int j = 0; j < cats[subset].Length; j++)
            {
                mpd.SetBlendWeight(ratio[j]);
                cats[subset][j].InitializeDemographicCategory();
            }
            mpd.CompleteBlend();
        }

        private static void TransposeMatrix(float[][] flatData)
        {
            for (int i = 0; i < flatData.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    var temp = flatData[i][j];
                    flatData[i][j] = flatData[j][i];
                    flatData[j][i] = temp;
                }
            }
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
            if (sum <= 0)
            {
                for (int subsetIndex = 0; subsetIndex < subsetRatios.Length; subsetIndex++)
                {
                    subsetRatios[subsetIndex] = 0;
                }
                return;
            }
            var normalFactor = 1 / sum;
            for (int subsetIndex = 0; subsetIndex < subsetRatios.Length; subsetIndex++)
            {
                subsetRatios[subsetIndex] *= normalFactor;
            }
        }

        private void CountTotalBlendSets()
        {
            TotalBlendSets = 0;
            for (int i = 0; i < MultiBlendSets.Count; i++)
            {
                TotalBlendSets += MultiBlendSets[i].Subsets.Count;
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

        private void LoadFriction(float[][] ret, int setNumber)
        {
            try
            {
                BinaryHelpers.ExecuteReader(reader =>
                   {
                       for (int i = 0; i < ret.Length; i++)
                       {
                           var row = ret[i];
                           for (int j = 0; j < row.Length; j++)
                           {
                               row[j] = reader.ReadSingle();
                           }
                       }
                   }, GetFrictionFileName(LoadFrictionFileName, setNumber));
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException("Unable to load distribution cache file!\r\n" + e.Message);
            }
        }

        private void LoadInBaseData(SparseTwinIndex<float> ret, IReadODData<float> data)
        {
            foreach (var point in data.Read())
            {
                ret[point.O, point.D] = point.Data;
            }
        }

        private void ProcessRatio(int flatZone, float[] ratio, float[] production, float[][] productions)
        {
            var denom = production[flatZone];
            if (denom != 0)
            {
                denom = 1 / denom;
            }
            for (int i = 0; i < ratio.Length; i++)
            {
                if (denom == 0)
                {
                    ratio[i] = 0;
                }
                else
                {
                    ratio[i] = productions[i][flatZone] * denom;
                }
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
                    throw new XTMFRuntimeException($"In {Name} we were unable to get the directory name from the file {fileName}!");
                }
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }
                BinaryHelpers.ExecuteWriter(writer =>
                   {
                       for (int i = 0; i < ret.Length; i++)
                       {
                           for (int j = 0; j < ret[i].Length; j++)
                           {
                               writer.Write(ret[i][j]);
                           }
                       }
                   }, fileName);
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException("Unable to save distribution cache file!\r\n" + e.Message);
            }
        }

        private void SumProductionAndAttraction(float[] production, float[][] productions)
        {
            for (int i = 0; i < production.Length; i++)
            {
                float productionSum = 0f;
                for (int j = 0; j < productions.Length; j++)
                {
                    productionSum += productions[j][i];
                }
                production[i] = productionSum;
            }
        }

        private void UpdateData(float[][] flatRet, float[] flatProd, IDemographicCategory[][] cats, float[][][] productions, float[][][] attractions, IZone[] zones, int subset, bool loadedFriction)
        {
            var numberOfZones = flatProd.Length;
            InteractiveModeSplit.StartNewInteractiveModeSplit(TotalBlendSets);
            var mpd = Root.ModeParameterDatabase;
            float[] subsetRatios = new float[productions.Length];
            float[] ratio = new float[cats[subset].Length];
            for (int i = 0; i < numberOfZones; i++)
            {
                var p = flatProd[i];
                float factor;
                if (p == 0)
                {
                    // if there is no production, clear out everything
                    for (int j = 0; j < numberOfZones; j++)
                    {
                        flatRet[i][j] = 0f;
                    }
                    continue;
                }
                if (UseProductionPercentages)
                {
                    var totalProduction = 0f;
                    factor = 0f;
                    for (int j = 0; j < attractions[subset].Length; j++)
                    {
                        totalProduction += productions[subset][j][i];
                    }
                    if (totalProduction <= 0)
                    {
                        for (int j = 0; j < numberOfZones; j++)
                        {
                            flatRet[i][j] = 0f;
                        }
                        continue;
                    }
                    // compute how much each one contributes to the total
                    // then sum it all up into our final factor
                    for (int j = 0; j < productions[subset].Length; j++)
                    {
                        ratio[j] = productions[subset][j][i] / totalProduction;
                        factor += productions[subset][j][i];
                    }
                    float retTotal = 0;
                    for (int j = 0; j < flatRet[i].Length; j++)
                    {
                        retTotal += flatRet[i][j];
                    }
                    factor = factor / retTotal;
                    // in this case we use the attraction since that is where the people
                    // are actually stored in this case
                    ComputeSubsetRatios(i, subsetRatios, attractions);
                    if (factor <= 0f)
                    {
                        for (int j = 0; j < numberOfZones; j++)
                        {
                            flatRet[i][j] = 0f;
                        }
                        continue;
                    }
                    // we actually still need to operate on the friction since it will be in totals
                    loadedFriction = false;
                }
                else
                {
                    var sum = 0f;
                    var retRow = flatRet[i];
                    // Gather the sum of all of the destinations from this origin
                    for (int j = 0; j < numberOfZones; j++)
                    {
                        sum += retRow[j];
                    }
                    // The rows should already be seeded however, if they are not
                    // just return since all of the values are zero anyway
                    if (sum <= 0)
                    {
                        throw new XTMFRuntimeException("In '" + Name + "' there was no attraction for zone " + zones[i].ZoneNumber);
                    }
                    ProcessRatio(i, ratio, flatProd, productions[subset]);
                    ComputeSubsetRatios(i, subsetRatios, productions);
                    // p is already the production of this subset
                    factor = p / sum;
                }
                SetupModeSplit(cats, subset, mpd, ratio);
                // make sure to only include the total factor of people that belong to this subset
                // now that we have the new factor we update the demand
                UpdateDemand(flatRet, zones, numberOfZones, loadedFriction, i, factor);
            }
            if (Transpose)
            {
                TransposeMatrix(flatRet);
            }
            if (!String.IsNullOrWhiteSpace(SaveFrictionFileName))
            {
                SaveFriction(flatRet);
            }
        }

        private void UpdateDemand(float[][] flatRet, IZone[] zones, int numberOfZones, bool loadedFriction, int i, float factor)
        {
            // now that we have the new factor we update the demand
            if (loadedFriction)
            {
                Parallel.For(0, numberOfZones, delegate (int j)
               {
                   if (Transpose)
                   {
                       InteractiveModeSplit.ComputeUtility(zones[j], zones[i]);
                   }
                   else
                   {
                       InteractiveModeSplit.ComputeUtility(zones[i], zones[j]);
                   }
               // we don't apply any factors here since they have already been taken into account
           });
            }
            else
            {
                Parallel.For(0, numberOfZones, delegate (int j)
               {
                   if (Transpose)
                   {
                       InteractiveModeSplit.ComputeUtility(zones[j], zones[i]);
                   }
                   else
                   {
                       InteractiveModeSplit.ComputeUtility(zones[i], zones[j]);
                   }
                   flatRet[i][j] *= factor;
               });
            }
        }
    }
}