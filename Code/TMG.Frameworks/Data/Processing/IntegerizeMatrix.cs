/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "This module is designed to take in a matrix and convert the matrix to contain only integer values. The values" +
    " will be remainders will be aggregated into Planning District to Planning District totals and redistributed respective to each contained zone's remainder.")]
public sealed class IntegerizeMatrix : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The matrix that we will integerize.")]
    public IDataSource<SparseTwinIndex<float>> MatrixToIntegerize;

    [RunParameter("Random Seed", 12345, "The random seed to use during integerization.")]
    public int RandomSeed;

    public bool Loaded => _data != null;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    private SparseTwinIndex<float> _data;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    private SparseTwinIndex<float> LoadBaseMatrix()
    {
        bool alreadyLoaded = MatrixToIntegerize.Loaded;
        if (!alreadyLoaded)
        {
            MatrixToIntegerize.LoadData();
        }
        var ret = MatrixToIntegerize.GiveData();
        if (!alreadyLoaded)
        {
            MatrixToIntegerize.UnloadData();
        }
        else
        {
            // if this was not freshly loaded and unloaded we need to make a copy
            ret = ret.Clone();
        }
        return ret;
    }

    /// <summary>
    /// Splits the integer portion of the matrix from the remainders.
    /// The rawMatrix will be integerized.
    /// </summary>
    /// <param name="rawMatrix">The matrix containing both the integer and remainder data</param>
    /// <returns>A new matrix containing the remainders</returns>
    private SparseTwinIndex<float> SplitIntegerAndRemainderMatrix(SparseTwinIndex<float> rawMatrix, out SparseTwinIndex<float> pdRemainderTotals)
    {
        var zoneSystem = Root.ZoneSystem.ZoneArray;
        var pdMatrix = TMG.Functions.ZoneSystemHelper.CreatePdTwinArray<float>(zoneSystem);
        var remainders = rawMatrix.CreateSimilarArray<float>();
        var rData = remainders.GetFlatData();
        var iData = rawMatrix.GetFlatData();
        // Split the integer and remainders while also accumulating the remainders into a PDxPD matrix
        System.Threading.Tasks.Parallel.For(0, rData.Length,
            () =>
            {
                return TMG.Functions.ZoneSystemHelper.CreatePdTwinArray<float>(zoneSystem);
            }
            , (int i, ParallelLoopState _, SparseTwinIndex<float> pdRemainders) =>
         {
             IZone[] flatZones = zoneSystem.GetFlatData();
             int pdI = pdRemainders.GetFlatIndex(flatZones[i].PlanningDistrict);
             var pdRow = pdRemainders.GetFlatData()[pdI];
             for (int j = 0; j < rData[i].Length; j++)
             {
                 int pdJ = pdRemainders.GetFlatIndex(flatZones[j].PlanningDistrict);
                 var original = iData[i][j];
                 iData[i][j] = (float)Math.Truncate(original);
                 rData[i][j] = original - iData[i][j];
                 pdRow[pdJ] += rData[i][j];
             }
             return pdRemainders;
         }, (SparseTwinIndex<float> pdRemainders) =>
         {
             var dest = pdMatrix.GetFlatData();
             var source = pdRemainders.GetFlatData();
             lock (pdMatrix)
             {
                 TMG.Functions.VectorHelper.Add(dest, dest, source);
             }
         });
        pdRemainderTotals = pdMatrix;
        return remainders;
    }

    public void LoadData()
    {
        var ret = LoadBaseMatrix();
        var remainders = SplitIntegerAndRemainderMatrix(ret, out var pdRemainders);
        // ret contains the integer portion of the matrix
        AssignIntegerRemainders(ret, remainders, pdRemainders);
        _data = ret;
    }

    struct ODPair
    {
        internal int Origin;
        internal int Destination;
    }

    private void AssignIntegerRemainders(SparseTwinIndex<float> integers, SparseTwinIndex<float> remainders, SparseTwinIndex<float> pdRemainders)
    {
        var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var pdIndexes = flatZones
            .Select(z => pdRemainders.GetFlatIndex(z.PlanningDistrict)).ToArray();

        // Create indexes to look for each PDxPD
        var pairs = pdRemainders.CreateSimilarArray<List<ODPair>>();
        for(int i = 0; i < flatZones.Length; i++)
        {
            for (int j = 0; j < flatZones.Length; j++)
            {
                var list = pairs.GetFlatData()[pdIndexes[i]][pdIndexes[j]];
                if (list == null)
                {
                    list = pairs.GetFlatData()[pdIndexes[i]][pdIndexes[j]] = new List<ODPair>(100);
                }
                list.Add(new ODPair() { Origin = i, Destination = j });
            }
        }
            
        var random = new Random(RandomSeed);
        var iData = integers.GetFlatData();
        var rData = remainders.GetFlatData();
        var pData = pdRemainders.GetFlatData();

        // Method to assign an additional trip to the integer matrix based on the remainders for the given pd of origin and destination
        void Assign(List<ODPair> zoneList, double pop, ref float pdTotal)
        {
            for (int z = 0; z < zoneList.Count; z++)
            {
                int i = zoneList[z].Origin;
                int j = zoneList[z].Destination;
                pop -= rData[i][j];
                if (pop <= 0)
                {
                    iData[i][j] += 1.0f;
                    pdTotal -= rData[i][j];
                    rData[i][j] = 0.0f;
                    return;
                }
            }
        }

        // Create the random seeds to work with for each PDxPD            
        var seeds = new int[pData.Length * pData.Length];
        for (int i = 0; i < seeds.Length; i++)
        {
            seeds[i] = random.Next(int.MinValue, int.MaxValue);
        }

        // Solve for each PDxPD
        System.Threading.Tasks.Parallel.For(0, pData.Length * pData.Length, (int index) =>
        {
            int i = index / pData.Length;
            int j = index % pData.Length;
            var r = new Random(seeds[i]);
            int toAssign = (int)Math.Round(pData[i][j]);
            var zoneList = pairs.GetFlatData()[i][j];
            for (int k = 0; k < toAssign; k++)
            {
                double pop = r.NextDouble() * pData[i][j];
                Assign(zoneList, pop, ref pData[i][j]);
            }
        });
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        _data = null;
    }
}
