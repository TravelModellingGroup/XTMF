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
using System.Linq;
using System.Text;
using XTMF;
namespace TMG.Estimation
{
    public class GradientAI : IEstimationAI
    {
        [RootModule]
        public IEstimationHost Root;

        [RunParameter( "Kernels", 1, @"How many different points should we explore at the same time per generation. 
The total points explored will be the number of kernels*(#parameters * 2 + 1)." )]
        public int NumberOfKernels;

        [RunParameter( "Maximize", false, "Should we maximize the result or minimize it?" )]
        public bool Maximize;

        [RunParameter( "Error factor", 1f, "The factor to apply to the error when computing the gradients." )]
        public float ErrorFactor;

        [RunParameter( "Random Seed", 12345, "The initial position for the random number generator." )]
        public int RandomSeed;

        [RunParameter( "Whisker Size", 0.001f, "The difference in relative parameter space between the kernel and the points to test." )]
        public float WhiskerSize;

        [RunParameter( "Momentum Factor", 0.1f, "The factor applied to the continuation of the previous generations gradient on the current generation." )]
        public float MomentumFactor;

        private Random Random;

        float[][] KernelMomentum;


        public List<Job> CreateJobsForIteration()
        {
            if ( this.Root.CurrentIteration == 0 )
            {
                CreateMomentum();
                return CreateInitialJobs();
            }
            return MoveKernels();
        }

        private void CreateMomentum()
        {
            var numberOfParameters = this.Root.Parameters.Count;
            var momentum = new float[this.NumberOfKernels][];
            for ( int i = 0; i < momentum.Length; i++ )
            {
                momentum[i] = new float[numberOfParameters];
            }
            this.KernelMomentum = momentum;
        }

        private List<Job> MoveKernels()
        {
            var ret = new List<Job>();
            var parameters = this.Root.Parameters;
            var oldJobs = this.Root.CurrentJobs;
            for ( int i = 0; i < this.NumberOfKernels; i++ )
            {
                var kernelIndex = i * ( parameters.Count * 2 + 1 );
                var kernel = Clone( oldJobs[kernelIndex] );
                // Alter momentum
                UpdateMomentum( parameters, oldJobs, i, kernelIndex );
                ApplyMomentum( parameters, kernel, i );
                ret.Add( kernel );
                CreateWhiskers( ret, parameters, kernel );
            }
            return ret;
        }

        private void ApplyMomentum(List<ParameterSetting> parameters, Job kernel, int kernelNumber)
        {
            var kernelParameters = kernel.Parameters;
            var momentum = this.KernelMomentum[kernelNumber];
            for ( int i = 0; i < parameters.Count; i++ )
            {
                kernelParameters[i].Current += momentum[i] * ( kernelParameters[i].Maximum - kernelParameters[i].Minimum );
                if ( momentum[i] > 0 )
                {
                    if ( kernelParameters[i].Current > kernelParameters[i].Maximum )
                    {
                        kernelParameters[i].Current = kernelParameters[i].Maximum;
                    }
                }
                else
                {
                    if ( kernelParameters[i].Current < kernelParameters[i].Minimum )
                    {
                        kernelParameters[i].Current = kernelParameters[i].Minimum;
                    }
                }
            }
        }

        private void UpdateMomentum(List<ParameterSetting> parameters, List<Job> oldJobs, int i, int kernelIndex)
        {
            for ( int j = 0; j < parameters.Count; j++ )
            {
                var gradient = oldJobs[kernelIndex + 2 * j + 1].Value - oldJobs[kernelIndex + 2 * j].Value;
                gradient *= this.ErrorFactor;
                // if we want to minimize, go backwards
                if ( this.Maximize )
                {
                    gradient = -gradient;
                }
                this.KernelMomentum[i][j] = gradient * ( 1f - this.MomentumFactor )
                                            + this.KernelMomentum[i][j] * this.MomentumFactor;
            }
        }

        private Job Clone(Job job)
        {
            Job ret = new Job();
            ret.Processed = false;
            ret.Processing = false;
            ret.ProcessedBy = null;
            ret.Value = float.NaN;
            ret.Parameters = new ParameterSetting[job.Parameters.Length];
            for ( int i = 0; i < ret.Parameters.Length; i++ )
            {
                ret.Parameters[i] = new ParameterSetting();
                ret.Parameters[i].Maximum = job.Parameters[i].Maximum;
                ret.Parameters[i].Minimum = job.Parameters[i].Minimum;
                ret.Parameters[i].Current = job.Parameters[i].Current;
                ret.Parameters[i].Names = job.Parameters[i].Names;
            }
            return ret;
        }

        private void CreateWhiskers(List<Job> ret, List<ParameterSetting> parameters, Job kernel)
        {
            for ( int j = 0; j < parameters.Count; j++ )
            {
                var delta = ( kernel.Parameters[j].Maximum - kernel.Parameters[j].Minimum )
                    * this.WhiskerSize;
                ret.Add( AddWisker( parameters, kernel, j, -delta ) );
                ret.Add( AddWisker( parameters, kernel, j, delta ) );
            }
        }

        private List<Job> CreateInitialJobs()
        {
            var ret = new List<Job>();
            var parameters = this.Root.Parameters;
            for ( int i = 0; i < this.NumberOfKernels; i++ )
            {
                var kernel = GenerateRandomJob( parameters );
                ret.Add( kernel );
                CreateWhiskers( ret, parameters, kernel );
            }
            return ret;
        }

        private Job AddWisker(List<ParameterSetting> parameters, Job kernel, int j, float delta)
        {
            var whisker = Clone( kernel );
            whisker.Parameters[j].Current = kernel.Parameters[j].Current + delta;
            if ( whisker.Parameters[j].Current > whisker.Parameters[j].Maximum )
            {
                whisker.Parameters[j].Current = whisker.Parameters[j].Maximum;
            }
            return whisker;
        }

        private Job GenerateRandomJob(List<ParameterSetting> parameters)
        {
            var ret = CleanJob( parameters );
            for ( int i = 0; i < ret.Parameters.Length; i++ )
            {
                ret.Parameters[i].Current =
                    ( ( ret.Parameters[i].Maximum - ret.Parameters[i].Minimum ) * ( (float)this.Random.NextDouble() ) )
                    + ret.Parameters[i].Minimum;
            }
            return ret;
        }

        private static Job CleanJob(List<ParameterSetting> parameters)
        {
            var ret = new Job();
            ret.Processed = false;
            ret.ProcessedBy = null;
            ret.Value = float.NaN;
            ret.Processing = false;
            ret.Parameters = new ParameterSetting[parameters.Count];
            for ( int i = 0; i < ret.Parameters.Length; i++ )
            {
                ret.Parameters[i] = new ParameterSetting()
                {
                    Maximum = parameters[i].Maximum,
                    Minimum = parameters[i].Minimum,
                    Current = float.NaN
                };
            }
            return ret;
        }

        public void IterationComplete()
        {

        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            this.Random = new Random( this.RandomSeed );
            return true;
        }
    }
}
