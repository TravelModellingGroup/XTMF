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
            this.Epsilon = epsilon;
            this.FrictionFunction = frictionFunction;
            this.MaxIterations = maxIterations;
            this.ProgressCallback = progressCallback;
            this.MaxErrorChangePerIteration = maxErrorChangePerIteration;
        }

        public GravityModel(SparseTwinIndex<float> friction, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100,
            float maxErrorChangePerIteration = 0.000100f)
        {
            this.Epsilon = epsilon;
            this.Friction = friction;
            this.MaxIterations = maxIterations;
            this.ProgressCallback = progressCallback;
            this.MaxErrorChangePerIteration = maxErrorChangePerIteration;
        }

        public SparseTwinIndex<float> ProcessFlow(SparseArray<float> O, SparseArray<float> D, int[] validIndexes, SparseArray<float> attractionStar = null)
        {
            int length = validIndexes.Length;
            this.Productions = O;
            this.Attractions = D;
            if ( attractionStar == null )
            {
                this.AttractionsStar = D.CreateSimilarArray<float>();
            }
            else
            {
                this.AttractionsStar = attractionStar;
            }
            this.FlowMatrix = this.Productions.CreateSquareTwinArray<float>();
            if ( this.Friction == null )
            {
                InitializeFriction( length );
            }
            var flatAttractionStar = this.AttractionsStar.GetFlatData();
            float[] oldTotal = new float[flatAttractionStar.Length];
            var flatAttractions = this.Attractions.GetFlatData();
            for ( int i = 0; i < length; i++ )
            {
                flatAttractionStar[i] = 1f;
                oldTotal[i] = flatAttractions[i];
            }
            int iteration = 0;
            do
            {
                if ( this.ProgressCallback != null )
                {
                    // this doesn't go to 100%, but that is alright since when we end, the progress
                    // of the calling model should assume we hit 100%
                    this.ProgressCallback( iteration / (float)MaxIterations );
                }
                Parallel.For( 0, length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                {
                    this.ProcessFlow( i );
                } );
            } while ( ( ++iteration ) < MaxIterations && !this.Balance(oldTotal) );

            if ( this.ProgressCallback != null )
            {
                this.ProgressCallback( 1f );
            }
            // Rebalancing isn't actually doing anything productive
            //Rebalance( this.FlowMatrix, O );
            return this.FlowMatrix;
        }

        private bool Balance(float[] oldTotal)
        {
            bool balanced = true;
            var flatAttractions = Attractions.GetFlatData();
            var flatFlows = FlowMatrix.GetFlatData();
            var flatAttractionStar = AttractionsStar.GetFlatData();
            int length = flatAttractions.Length;
            float ep = (float)this.Epsilon;
            Parallel.For( 0, length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int j)
            {
                double total = 0;
                if ( flatAttractions[j] == 0 ) return;
                for ( int i = 0; i < length; i++ )
                {
                    total += flatFlows[i][j];
                }
                float residule;
                /*if ( Math.Abs( total - oldTotal[j] ) > this.MaxErrorChangePerIteration )
                {
                    balanced = false;
                }*/
                total = 1 / total;
                residule = (float)( flatAttractions[j] * total );
                if ( !double.IsInfinity( total ) & !double.IsNaN( total ) )
                {
                    if ( Math.Abs( 1 - residule ) > ep )
                    {
                        balanced = false;
                    }
                    flatAttractionStar[j] *= residule;
                }
                else
                {
                    flatAttractionStar[j] = 1.0f;
                }
            } );
            return balanced;
        }

        private void InitializeFriction(int length)
        {
            this.Friction = this.Productions.CreateSquareTwinArray<float>();
            var flatFriction = this.Friction.GetFlatData();
            Parallel.For( 0, length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    for ( int j = 0; j < length; j++ )
                    {
                        flatFriction[i][j] = (float)this.FrictionFunction( this.Productions.GetSparseIndex( i ), this.Attractions.GetSparseIndex( j ) );
                    }
                } );
        }

        private void ProcessFlow(int flatOrigin)
        {
            float sumAF = 0;
            var flatProductions = this.Productions.GetFlatData();
            var flatFriction = this.Friction.GetFlatData();
            var flatAStar = this.AttractionsStar.GetFlatData();
            var flatAttractions = this.Attractions.GetFlatData();
            var length = flatFriction.Length;
            var flatFrictionRow = flatFriction[flatOrigin];
            // check to see if there is no production
            if ( flatProductions[flatOrigin] <= 0 )
            {
                return;
            }
            // if there is production just continue on
            for ( int i = 0; i < flatFrictionRow.Length; i++ )
            {
                sumAF += flatFrictionRow[i] * ( flatAttractions[i] * flatAStar[i] );
            }
            sumAF = ( 1 / sumAF ) * flatProductions[flatOrigin];
            if (float.IsInfinity( sumAF ) | float.IsNaN( sumAF ) )
            {
                // this needs to be 0f, otherwise we will be making the attractions have to be balanced higher
                sumAF = 0f;
            }
            var flatFlowsRow = this.FlowMatrix.GetFlatData()[flatOrigin];
            for ( int i = 0; i < flatFlowsRow.Length; i++ )
            {
                var temp = ( flatFrictionRow[i] * ( sumAF * flatAttractions[i] * flatAStar[i] ) );
                flatFlowsRow[i] = float.IsInfinity( temp ) | float.IsNaN( temp ) ? 0 : temp;
            }
        }

        private void Rebalance(SparseTwinIndex<float> sparseTwinIndex, SparseArray<float> O)
        {
            var data = sparseTwinIndex.GetFlatData();
            var original = O.GetFlatData();
            Parallel.For( 0, original.Length, (int i) =>
                {
                    var row = data[i];
                    var sum = 0.0;
                    if ( original[i] == 0 )
                    {
                        return;
                    }
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        sum += row[j];
                    }
                    var factor = (float)( original[i] / sum );
                    if ( float.IsNaN( factor ) || float.IsInfinity( factor ) )
                    {
                        return;
                    }
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        row[j] *= factor;
                    }
                } );
        }
    }
}