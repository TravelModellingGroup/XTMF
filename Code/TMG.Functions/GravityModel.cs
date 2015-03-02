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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;

namespace TMG.Functions
{
    public sealed class GravityModel
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
        private float MaxErrorChangePerIteration;

        public GravityModel(Func<int, int, double> frictionFunction, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100,
            float maxErrorChangePerIteration = 0.000100f)
        {
            Epsilon = epsilon;
            FrictionFunction = frictionFunction;
            MaxIterations = maxIterations;
            ProgressCallback = progressCallback;
            MaxErrorChangePerIteration = maxErrorChangePerIteration;
        }

        public GravityModel(SparseTwinIndex<float> friction, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100,
            float maxErrorChangePerIteration = 0.000100f)
        {
            Epsilon = epsilon;
            Friction = friction;
            MaxIterations = maxIterations;
            ProgressCallback = progressCallback;
            MaxErrorChangePerIteration = maxErrorChangePerIteration;
        }

        public SparseTwinIndex<float> ProcessFlow(SparseArray<float> O, SparseArray<float> D, int[] validIndexes, SparseArray<float> attractionStar = null)
        {
            int length = validIndexes.Length;
            Productions = O;
            Attractions = D;
            if(attractionStar == null)
            {
                AttractionsStar = D.CreateSimilarArray<float>();
            }
            else
            {
                AttractionsStar = attractionStar;
            }
            FlowMatrix = Productions.CreateSquareTwinArray<float>();
            if(Friction == null)
            {
                InitializeFriction(length);
            }
            var flatAttractionStar = AttractionsStar.GetFlatData();
            float[] oldTotal = new float[flatAttractionStar.Length];
            var flatAttractions = Attractions.GetFlatData();
            for(int i = 0; i < length; i++)
            {
                flatAttractionStar[i] = 1f;
                oldTotal[i] = flatAttractions[i];
            }
            int iteration = 0;
            float[] columnTotals = new float[length];
            do
            {
                if(ProgressCallback != null)
                {
                    // this doesn't go to 100%, but that is alright since when we end, the progress
                    // of the calling model should assume we hit 100%
                    ProgressCallback(iteration / (float)MaxIterations);
                }
                Array.Clear(columnTotals, 0, columnTotals.Length);
                ProcessFlow(columnTotals);
            } while((++iteration) < MaxIterations && !Balance(columnTotals, oldTotal));

            if(ProgressCallback != null)
            {
                ProgressCallback(1f);
            }
            // Rebalancing isn't actually doing anything productive
            //Rebalance( this.FlowMatrix, O );
            return FlowMatrix;
        }

        private bool Balance(float[] columnTotals, float[] oldTotal)
        {
            bool balanced = true;
            var flatAttractions = Attractions.GetFlatData();
            var flatFlows = FlowMatrix.GetFlatData();
            var flatAttractionStar = AttractionsStar.GetFlatData();
            int length = flatAttractions.Length;
            float ep = (float)Epsilon;
            Parallel.For(0, columnTotals.Length, (int i) =>
            {
                if(flatAttractions[i] > 0)
                {
                    var total = 1.0f / columnTotals[i];
                    if(!float.IsInfinity(total) & !float.IsNaN(total))
                    {
                        var residual = (float)(flatAttractions[i] * total);
                        if(Math.Abs(1 - residual) > ep)
                        {
                            balanced = false;
                        }
                        flatAttractionStar[i] *= residual;
                    }
                    else
                    {
                        flatAttractionStar[i] = 1.0f;
                    }
                }
            });
            return balanced;
        }

        private void InitializeFriction(int length)
        {
            Friction = Productions.CreateSquareTwinArray<float>();
            var flatFriction = Friction.GetFlatData();
            Parallel.For(0, length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
            {
                for(int j = 0; j < length; j++)
                {
                    flatFriction[i][j] = (float)FrictionFunction(Productions.GetSparseIndex(i), Attractions.GetSparseIndex(j));
                }
            });
        }

        private void ProcessFlow(float[] columnTotals)
        {
            Parallel.For(0, Productions.GetFlatData().Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                () => new float[columnTotals.Length],
                (int flatOrigin, ParallelLoopState state, float[] localTotals) =>
                {
                    float sumAF = 0;
                    var flatProductions = Productions.GetFlatData();
                    var flatFriction = Friction.GetFlatData();
                    var flatAStar = AttractionsStar.GetFlatData();
                    var flatAttractions = Attractions.GetFlatData();
                    var length = flatFriction.Length;
                    var flatFrictionRow = flatFriction[flatOrigin];
                    // check to see if there is no production, if not skip this
                    if(flatProductions[flatOrigin] > 0)
                    {
                        // if there is production continue on
                        for(int i = 0; i < flatFrictionRow.Length; i++)
                        {
                            sumAF += flatFrictionRow[i] * (flatAttractions[i] * flatAStar[i]);
                        }
                        sumAF = (1 / sumAF) * flatProductions[flatOrigin];
                        if(float.IsInfinity(sumAF) | float.IsNaN(sumAF))
                        {
                            // this needs to be 0f, otherwise we will be making the attractions have to be balanced higher
                            sumAF = 0f;
                        }
                        var flatFlowsRow = FlowMatrix.GetFlatData()[flatOrigin];
                        for(int i = 0; i < flatFlowsRow.Length; i++)
                        {
                            var temp = (flatFrictionRow[i] * (sumAF * flatAttractions[i] * flatAStar[i]));
                            temp = float.IsInfinity(temp) | float.IsNaN(temp) ? 0 : temp;
                            localTotals[i] += temp;
                            flatFlowsRow[i] = temp;
                        }
                    }
                    return localTotals;
                },
                (float[] localTotals) =>
                {
                    lock (columnTotals)
                    {
                        for(int i = 0; i < localTotals.Length; i++)
                        {
                            columnTotals[i] += localTotals[i];
                        }
                    }
                });
        }
    }
}