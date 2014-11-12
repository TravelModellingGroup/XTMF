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
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"The Log-Sum Distribution module is designed to rapidly apply go through different
demographic categories and build demand matrices for them.  Primarily designed 
for building Place of Residence Place of Work models this module will work anywhere 
where you want a Logsum of the mode choice to represent the friction values.  There is an 
optional sub-module available for loading in K-Factors.  This module also supports using the 
GPU to enhance the processing time of the model." )]
    public class LogSumDistribution : IDemographicDistribution
    {
        [RunParameter( "Doubly Constrained", true, "Should we use a doubly constrained gravity model?" )]
        public bool DoublyConstrained;

        [RunParameter( "Max Error", 0.01f, "What should the maximum error be? (Between 0 and 1)" )]
        public float Epsilon;

        [RunParameter( "Correlation of Modes", 1f, "The correlation between the different modes. 1 means no correlation to 0 meaning perfect." )]
        public float ImpedianceParameter;

        [SubModelInformation( Description = "K-Factor Data Read, Optional", Required = false )]
        public IReadODData<float> KFactorDataReader;

        [RunParameter( "Max Iterations", 300, "How many iterations should we cut of the distribution at?" )]
        public int MaxIterations;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "Simulation Time", "7:00AM", typeof( Time ), "The time of day this will be simulating." )]
        public Time SimulationTime;

        [RunParameter( "Swap Attraction", false, "Switch attraction with production from generation." )]
        public bool SwapAttraction;

        [RunParameter( "Transpose Distribution", false, "Transpose the final result of the model." )]
        public bool Transpose;

        [RunParameter( "Use GPU", false, "Should we use the GPU to compute?" )]
        public bool UseGPU;

        /// <summary>
        /// Flat 2D Index = O * NumberOfZones + D
        /// </summary>
        private float[] KFactor;

        private int NumberOfZones;

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
            this.Progress = 0f;
            var ep = this.SwapAttraction ? attractions.GetEnumerator() : productions.GetEnumerator();
            var ea = this.SwapAttraction ? productions.GetEnumerator() : attractions.GetEnumerator();
            var ec = category.GetEnumerator();
            if ( this.KFactorDataReader != null )
            {
                this.LoadKFactors();
            }
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            foreach ( var ret in this.DoublyConstrained ?
                SolveDoublyConstrained( zones, ep, ea, ec ) : SolveSinglyConstrained( zones, ep, ea, ec ) )
            {
                if ( this.Transpose )
                {
                    TransposeMatrix( ret );
                }
                yield return ret;
            }
            this.KFactor = null;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private static void TransposeMatrix(SparseTwinIndex<float> ret)
        {
            var flatData = ret.GetFlatData();
            var length = flatData.Length;
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < i; j++ )
                {
                    var temp = flatData[i][j];
                    flatData[i][j] = flatData[j][i];
                    flatData[j][i] = temp;
                }
            }
        }

        private float[] ComputeFriction(IZone[] zones, IDemographicCategory cat, float[] production, float[] attraction, float[] friction)
        {
            var numberOfZones = zones.Length;
            float[] ret = friction == null ? new float[numberOfZones * numberOfZones] : friction;
            var rootModes = this.Root.Modes;
            var numberOfModes = rootModes.Count;
            // let it setup the modes so we can compute friction
            cat.InitializeDemographicCategory();
            try
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                {
                    int index = i * numberOfZones;
                    if ( production[i] == 0 )
                    {
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            ret[index++] = 0;
                        }
                        return;
                    }
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        if ( attraction != null && attraction[j] == 0 )
                        {
                            ret[index++] = 0;
                        }
                        else
                        {
                            var utility = 0f;
                            if ( !this.GatherAllUtility( zones[i], zones[j], out utility ) )
                            {
                                ret[index++] = 0;
                                //throw new XTMFRuntimeException( "There was no valid mode to travel between " + zones[i].ZoneNumber + " and " + zones[j].ZoneNumber );
                            }
                            else
                            {
                                ret[index++] = (float)Math.Pow( utility, this.ImpedianceParameter ) * ( this.KFactor != null ? (float)Math.Exp( this.KFactor[i * this.NumberOfZones + j] ) : 1f );
                            }
                        }
                    }
                } );
            }
            catch ( AggregateException e )
            {
                if ( e.InnerException is XTMFRuntimeException )
                {
                    throw new XTMFRuntimeException( e.InnerException.Message );
                }
                else
                {
                    throw new XTMFRuntimeException( e.InnerException.Message + "\r\n" + e.InnerException.StackTrace );
                }
            }
            // Use the Log-Sum from the V's as the impedence function
            return ret;
        }

        private void ComputeFriction(IZone[] zones, IDemographicCategory cat, float[][] friction)
        {
            var numberOfZones = zones.Length;
            var rootModes = this.Root.Modes;
            var numberOfModes = rootModes.Count;
            // let it setup the modes so we can compute friction
            cat.InitializeDemographicCategory();
            try
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                {
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        var destination = zones[j];
                        float utility;
                        if ( !this.GatherAllUtility( zones[i], zones[j], out utility ) )
                        {
                            throw new XTMFRuntimeException( "There was no valid mode to travel between " + zones[i].ZoneNumber + " and " + zones[j].ZoneNumber );
                        }
                        else
                        {
                            friction[i][j] = (float)Math.Pow( utility, this.ImpedianceParameter ) * ( this.KFactor != null ? (float)Math.Exp( this.KFactor[i * this.NumberOfZones + j] ) : 1f );
                        }
                    }
                } );
            }
            catch ( AggregateException e )
            {
                throw e.InnerException;
            }
        }

        private IEnumerable<SparseTwinIndex<float>> CPUDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            var frictionSparse = this.Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var friction = frictionSparse.GetFlatData();
            while ( ep.MoveNext() && ea.MoveNext() && ec.MoveNext() )
            {
                var production = ep.Current;
                var attraction = ea.Current;
                var cat = ec.Current;
                var ret = production.CreateSquareTwinArray<float>();
                this.ComputeFriction( zones, cat, friction );
                yield return new GravityModel( frictionSparse, ( p => this.Progress = p ), this.Epsilon, this.MaxIterations )
                    .ProcessFlow( production, attraction, production.ValidIndexArray() );
            }
        }

        private bool GatherAllUtility(IZone o, IZone d, out float utility)
        {
            var modes = this.Root.Modes;
            var length = modes.Count;
            var totalUtility = 0f;
            bool anyFeasible = false;
            for ( int i = 0; i < length; i++ )
            {
                float localUtility;
                if ( GatherAllUtility( modes[i], o, d, out localUtility ) )
                {
                    anyFeasible = true;
                    totalUtility += localUtility;
                }
            }
            utility = totalUtility;
            return anyFeasible;
        }

        private bool GatherAllUtility(IModeChoiceNode node, IZone o, IZone d, out float utility)
        {
            var cat = node as IModeCategory;
            if ( cat == null )
            {
                if ( node.Feasible( o, d, SimulationTime ) )
                {
                    utility = (float)Math.Exp( node.CalculateV( o, d, SimulationTime ) );
                    return !float.IsNaN( utility );
                }
            }
            else
            {
                // check to make sure that we are feasible
                if ( !node.Feasible( o, d, SimulationTime ) )
                {
                    utility = 0f;
                    return false;
                }
                // if we are feasible then go through and get the utility of our children
                float totalUtility = 0f;
                var length = cat.Children.Count;
                bool anyChildrenFeasible = false;
                for ( int i = 0; i < length; i++ )
                {
                    float res;
                    if ( GatherAllUtility( cat.Children[i], o, d, out res ) )
                    {
                        anyChildrenFeasible = true;
                        totalUtility += res;
                    }
                }
                if ( anyChildrenFeasible )
                {
                    utility = (float)( Math.Pow( totalUtility, cat.Correlation )
                        * Math.Exp( cat.CalculateCombinedV( o, d, SimulationTime ) ) );
                    return true;
                }
            }
            // if we got here then there were no feasible alternatives
            utility = 0f;
            return false;
        }

        private IEnumerable<SparseTwinIndex<float>> GPUDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            using ( MultiRunGPUGravityModel gm =
                        new MultiRunGPUGravityModel( zones.Length, ( p => this.Progress = p ), this.Epsilon, this.MaxIterations ) )
            {
                float[] friction = null;
                while ( ep.MoveNext() && ea.MoveNext() && ec.MoveNext() )
                {
                    var production = ep.Current;
                    var attraction = ea.Current;
                    var cat = ec.Current;
                    var ret = production.CreateSquareTwinArray<float>();
                    friction = this.ComputeFriction( zones, cat, production.GetFlatData(), attraction.GetFlatData(), friction );
                    yield return gm.ProcessFlow( friction, production, attraction );
                }
            }
        }

        private void LoadKFactors()
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            this.NumberOfZones = numberOfZones;
            this.KFactor = new float[numberOfZones * numberOfZones];
            for ( int i = 0; i < this.KFactor.Length; i++ )
            {
                this.KFactor[i] = 1f;
            }
            foreach ( var dataPoint in this.KFactorDataReader.Read() )
            {
                this.KFactor[dataPoint.O * numberOfZones + dataPoint.D] = dataPoint.Data;
            }
        }

        private IEnumerable<SparseTwinIndex<float>> SolveDoublyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            if ( this.UseGPU )
            {
                foreach ( var ret in GPUDoublyConstrained( zones, ep, ea, ec ) )
                {
                    yield return ret;
                }
            }
            else
            {
                foreach ( var ret in CPUDoublyConstrained( zones, ep, ea, ec ) )
                {
                    yield return ret;
                }
            }
        }

        private IEnumerable<SparseTwinIndex<float>> SolveSinglyConstrained(IZone[] zones, IEnumerator<SparseArray<float>> ep, IEnumerator<SparseArray<float>> ea, IEnumerator<IDemographicCategory> ec)
        {
            float[] friction = null;
            while ( ep.MoveNext() && ea.MoveNext() && ec.MoveNext() )
            {
                var production = ep.Current;
                var attraction = ea.Current;
                var cat = ec.Current;
                var ret = production.CreateSquareTwinArray<float>();
                friction = this.ComputeFriction( zones, cat, production.GetFlatData(), null, friction );
                yield return SinglyConstrainedGravityModel.Process( production, friction );
            }
        }
    }
}