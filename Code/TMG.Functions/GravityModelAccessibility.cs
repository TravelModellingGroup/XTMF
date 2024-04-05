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
using System;
using System.Threading.Tasks;
using Datastructure;

namespace TMG.Functions;

public sealed class GravityModelAccessibility
{
    private SparseArray<float> Attractions;
    private SparseArray<float> AttractionsStar;
    private float Epsilon;
    private SparseTwinIndex<float> FlowMatrix;
    private SparseTwinIndex<float> Friction;
    private Func<int, int, double> FrictionFunction;
    private int MaxIterations;
    private SparseArray<float> Productions;
    private Action<float> ProgressCallback;

    public GravityModelAccessibility(Func<int, int, double> frictionFunction, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100)
    {
        Epsilon = epsilon;
        FrictionFunction = frictionFunction;
        MaxIterations = maxIterations;
        ProgressCallback = progressCallback;
    }

    public GravityModelAccessibility(SparseTwinIndex<float> friction, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100)
    {
        Epsilon = epsilon;
        Friction = friction;
        MaxIterations = maxIterations;
        ProgressCallback = progressCallback;
    }

    public SparseArray<float> ProcessAccessibility(SparseArray<float> o, SparseArray<float> d, int[] validIndexes, SparseArray<float> attractionStar = null)
    {
        int length = validIndexes.Length;
        Productions = o;
        Attractions = d;
        if (attractionStar == null)
        {
            AttractionsStar = d.CreateSimilarArray<float>();
        }
        else
        {
            AttractionsStar = attractionStar;
        }
        FlowMatrix = Productions.CreateSquareTwinArray<float>();
        if (Friction == null)
        {
            InitializeFriction(length);
        }
        var flatAttractionStar = AttractionsStar.GetFlatData();
        float[] oldTotal = new float[flatAttractionStar.Length];
        var flatAttractions = Attractions.GetFlatData();
        for (int i = 0; i < length; i++)
        {
            flatAttractionStar[i] = 1f;
            oldTotal[i] = flatAttractions[i];
        }
        int iteration = 0;
        float[] columnTotals = new float[length];
        bool balanced;
        do
        {
            // this doesn't go to 100%, but that is alright since when we end, the progress
            // of the calling model should assume we hit 100%
            ProgressCallback?.Invoke(iteration / (float)MaxIterations);
            Array.Clear(columnTotals, 0, columnTotals.Length);
            VectorProcessFlow(columnTotals, FlowMatrix.GetFlatData());
            balanced = Balance(columnTotals);
        } while ((++iteration) < MaxIterations && !balanced);

        ProgressCallback?.Invoke(1f);
        var logsums = AttractionsStar.CreateSimilarArray<float>();
        ComputeAccessibility(logsums.GetFlatData());
        return logsums;
    }

    private bool Balance(float[] columnTotals)
    {
        var flatAttractions = Attractions.GetFlatData();
        var flatAttractionStar = AttractionsStar.GetFlatData();
        float ep = Epsilon;
        VectorHelper.Divide(columnTotals, 0, flatAttractions, 0, columnTotals, 0, columnTotals.Length);
        VectorHelper.Multiply(flatAttractionStar, 0, flatAttractionStar, 0, columnTotals, 0, flatAttractionStar.Length);
        VectorHelper.ReplaceIfNotFinite(flatAttractionStar, 0, 1.0f, flatAttractionStar.Length);
        return VectorHelper.AreBoundedBy(columnTotals, 0, 1.0f, ep, columnTotals.Length);
    }

    private void InitializeFriction(int length)
    {
        Friction = Productions.CreateSquareTwinArray<float>();
        var flatFriction = Friction.GetFlatData();
        Parallel.For(0, length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
            delegate (int i)
        {
            for (int j = 0; j < length; j++)
            {
                flatFriction[i][j] = (float)FrictionFunction(Productions.GetSparseIndex(i), Attractions.GetSparseIndex(j));
            }
        });
    }

    private void VectorProcessFlow(float[] columnTotals, float[][] flatFlows)
    {
        Parallel.For(0, Productions.GetFlatData().Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => new float[columnTotals.Length],
            (flatOrigin, state, localTotals) =>
        {
            var flatProductions = Productions.GetFlatData();
            // check to see if there is no production, if not skip this
            if (flatProductions[flatOrigin] > 0)
            {
                var flatFriction = Friction.GetFlatData();
                var flatAStar = AttractionsStar.GetFlatData();
                var flatAttractions = Attractions.GetFlatData();
                var flatFrictionRow = flatFriction[flatOrigin];
                var sumAf = VectorHelper.Multiply3AndSum(flatFrictionRow, 0, flatAttractions, 0, flatAStar, 0, flatFriction.Length);
                sumAf = (flatProductions[flatOrigin] / sumAf);
                if (float.IsInfinity(sumAf) | float.IsNaN(sumAf))
                {
                    // this needs to be 0f, otherwise we will be making the attractions have to be balanced higher
                    sumAf = 0f;
                }
                VectorHelper.Multiply3Scalar1AndColumnSum(flatFlows[flatOrigin], 0, flatFrictionRow, 0, flatAttractions, 0, flatAStar, 0, sumAf, localTotals, 0, flatFriction.Length);
            }
            return localTotals;
        },
        localTotals =>
        {
            lock (columnTotals)
            {
                VectorHelper.Add(columnTotals, 0, columnTotals, 0, localTotals, 0, columnTotals.Length);
            }
        });
    }

    private void ComputeAccessibility(float[] logsums)
    {

        Parallel.For(0, Productions.GetFlatData().Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (flatOrigin) =>
            {
                var flatProductions = Productions.GetFlatData();
                // check to see if there is no production, if not skip this
                var flatFriction = Friction.GetFlatData();
                var flatAStar = AttractionsStar.GetFlatData();
                var flatAttractions = Attractions.GetFlatData();
                var flatFrictionRow = flatFriction[flatOrigin];
                var sumAf = VectorHelper.Multiply3AndSum(flatFrictionRow, 0, flatAttractions, 0, flatAStar, 0, flatFriction.Length);
                logsums[flatOrigin] = (float)Math.Log(sumAf);
            });
    }
}