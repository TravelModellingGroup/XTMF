/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using System.Threading.Tasks;

namespace TMG.Estimation.AI;

[ModuleInformation(Description = @"Provides a hybrid between a PSO and a Gradient Descent algorithm.  First we use a PSO in order to find a near optimized point. 
Once we have a near optimal point we continue to explore the space with a the GD algorithm to further optimize.")]
// ReSharper disable once InconsistentNaming
public class HybridPSOGD : IEstimationAI
{
    [RootModule]
    public IEstimationHost Root;

    [RunParameter("Maximize", true, "Should we be trying to maximize (true) or minimize (false) the function?", Index = -3)]
    public bool Maximize;

    [RunParameter("Random Seed", 12345, "The random seed to use for the generation of the initial population.", Index = -2)]
    public int RandomSeed;

    /// <summary>
    /// Bounce strategies from the paper:
    /// "Handling boundary constraints for particle swarm optimization in high-dimensional search space." by Wei Chu et. al
    /// </summary>
    public enum BounceStrategy
    {
        Absorb,
        Reflect,
        RandomReallocation
    }

    [RunParameter("Bounce Strategy", "Reflect", typeof(BounceStrategy), "The strategy to deal with hitting minimum or maximum values.", Index = -1)]
    public BounceStrategy Bounce;

    [RunParameter("SwarmSize", "100", typeof(int), "The number of different particles to estimate with.", Index = 2)]
    public int SwarmSize;

    [RunParameter("Best Parameter Weight", "-0.2497974", typeof(float), "The weight of the particle's best parameter.", Index = 3)]
    public float BestParameterWeight;

    [RunParameter("Globally Optimal Weight", "2.99671", typeof(float), "The weight of the globally optimal parameter.", Index = 4)]
    public float OptimalWeight;

    [RunParameter("Generation Optimal Weight", "1.215541", typeof(float), "The weight of the globally optimal parameter.", Index = 5)]
    public float GenerationOptimalWeight;

    [RunParameter("Momentum", "0.002844917", typeof(float), "The carried velocity between iterations per particle.", Index = 6)]
    public float Momentum;

    [RunParameter("Movement Reduction", 0f, "Momentum *=  (1 - (MovementReduction * iteration/totalIterations))", Index = 7)]
    public float MovementReduction;

    [RunParameter("Max Initial Velocity", 0.25f, "The maximum initial velocity for the first generation of the optimization.", Index = 8)]
    public float MaxInitialVelocity;

    [RunParameter("Iterations to switch", 3, "The number of iterations before we will switch to the Gradient Descent algorithm if the Best Delta is not met.", Index = 9)]
    public int IterationsToSwitch;

    [RunParameter("Best Delta", 20.0f, "The amount of improvement we need between the set number of iterations before switching over to gradient descent.", Index = 10)]
    public float BestDelta;

    [RunParameter("Whisker Size", 0.001f, "The difference in relative parameter space between the kernel and the points to test.", Index = 11)]
    public float WhiskerSize;

    [RunParameter("Momentum Factor", 0.1f, "The factor applied to the continuation of the previous generations gradient on the current generation.", Index = 12)]
    public float MomentumFactor;

    [RunParameter("Error factor", 1f, "The factor to apply to the error term while running GD.", Index = 13)]
    public float ErrorFactor;

    [RunParameter("Report Kernel Movement", false, "Report to console the movement of the kernel.", Index = 14)]
    public bool ReportKernelMovement;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    private bool Pso = true;

    /// <summary>
    /// This represents a unique element in the estimation that keeps a local history as it moves
    /// through parameter space.
    /// </summary>
    private struct Particle
    {
        internal Job Job { get; set; }

        internal float BestValue;

        private bool Maximize;

        internal float[] BestParameters { get; set; }

        internal float[] Velocity;

        public Particle(HybridPSOGD us, Job job, bool maximize, Random random) : this()
        {
            Maximize = maximize;
            BestValue = maximize ? float.MinValue : float.MaxValue;
            Velocity = InitializeVelocity(us, job, random);
            Job = job;
            BestParameters = InitializeBestParameters(job);
        }

        private static float[] InitializeVelocity(HybridPSOGD us, Job job, Random random)
        {
            var parameters = job.Parameters;
            float[] velocity = new float[parameters.Length];
            // initialize all of the velocities to [-1,1] since we work in relative parameter space
            for (int i = 0; i < velocity.Length; i++)
            {
                velocity[i] = us.MaxInitialVelocity * (float)((random.NextDouble() * 2.0) - 1.0);
            }
            return velocity;
        }

        private static float[] InitializeBestParameters(Job job)
        {
            var parameters = job.Parameters;
            var copy = new float[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                copy[i] = parameters[i].Current;
            }
            return copy;
        }

        /// <summary>
        /// This function will update the best value and parameters if the current
        /// job has produced superior results
        /// </summary>
        internal void UpdateIfBest()
        {
            var currentValue = Job.Value;
            // if this value is better than anything we have seen before
            if (Maximize ? currentValue > BestValue : currentValue < BestValue)
            {
                var bestParameters = BestParameters;
                var currentParameters = Job.Parameters;
                BestValue = currentValue;
                for (int i = 0; i < bestParameters.Length; i++)
                {
                    bestParameters[i] = currentParameters[i].Current;
                }
            }
        }


        private static float RelativeDistance(ParameterSetting parameter, float ourValue, float otherValue)
        {
            return (otherValue - ourValue) / parameter.Size;
        }

        internal void UpdateVelocity(HybridPSOGD us, float[] globalBest, float[] bestInGeneration, Random r)
        {
            var parameters = us.Root.Parameters;
            var factor = 1.0f - (us.Root.CurrentIteration / (us.Root.TotalIterations - 1.0f) * us.MovementReduction);
            for (int i = 0; i < Velocity.Length; i++)
            {
                var bestParameterRandom = r.NextDouble();
                var optimalRandom = r.NextDouble();
                var current = Job.Parameters[i].Current;
                var globalBestV = us.BestParameterWeight * bestParameterRandom * RelativeDistance(parameters[i], current, BestParameters[i]);
                var localBestV = us.OptimalWeight * optimalRandom * RelativeDistance(parameters[i], current, globalBest[i]);
                var generationBestV = us.GenerationOptimalWeight * RelativeDistance(parameters[i], current, bestInGeneration[i]);
                // we step our velocity by apply a momentum to the old velocity and then applying the new with the rest of the fraction
                Velocity[i] = ((factor * us.Momentum * Velocity[i]) + (float)(globalBestV + localBestV + generationBestV));
            }
        }

        internal Job UpdatePosition(HybridPSOGD us, Random rand)
        {
            var temp = Job.Parameters;
            Job job = new()
            {
                Processed = false,
                ProcessedBy = null,
                Processing = false,
                Value = float.NaN
            };
            var parameters = job.Parameters = new ParameterSetting[temp.Length];
            for (int i = 0; i < temp.Length; i++)
            {
                parameters[i] = new ParameterSetting
                {
                    // we need to move in real parameter space instead of relative parameter space
                    Current = temp[i].Current + Velocity[i] * (temp[i].Size)
                };
                // clamp the value inside of parameter space
                if (parameters[i].Current < (parameters[i].Minimum = temp[i].Minimum))
                {
                    // reflect off the boundary instead of being absorbed
                    switch (us.Bounce)
                    {
                        case BounceStrategy.Reflect:
                            parameters[i].Current = Math.Min(temp[i].Minimum + (temp[i].Minimum - parameters[i].Current), temp[i].Maximum);
                            break;
                        case BounceStrategy.RandomReallocation:
                            parameters[i].Current = (float)(rand.NextDouble() * (temp[i].Maximum - temp[i].Minimum) + temp[i].Minimum);
                            break;
                        case BounceStrategy.Absorb:
                            parameters[i].Current = parameters[i].Minimum;
                            break;
                    }
                    if (us.Momentum > 0)
                    {
                        Velocity[i] = -Velocity[i];
                    }
                }
                if (parameters[i].Current > (parameters[i].Maximum = temp[i].Maximum))
                {
                    // reflect off the boundary instead of being absorbed
                    switch (us.Bounce)
                    {
                        case BounceStrategy.Reflect:
                            parameters[i].Current = Math.Max(temp[i].Maximum - (parameters[i].Current - temp[i].Maximum), temp[i].Minimum);
                            break;
                        case BounceStrategy.RandomReallocation:
                            parameters[i].Current = (float)(rand.NextDouble() * (temp[i].Maximum - temp[i].Minimum) + temp[i].Minimum);
                            break;
                        case BounceStrategy.Absorb:
                            parameters[i].Current = parameters[i].Maximum;
                            break;
                    }
                    if (us.Momentum > 0)
                    {
                        Velocity[i] = -Velocity[i];
                    }
                }
            }
            return (Job = job);
        }
    }

    private Particle[] Population;

    private List<Job> Jobs;

    public List<Job> CreateJobsForIteration()
    {
        if (Root.CurrentIteration == 0)
        {
            InitializeSwarm();
            CreateMomentum();
            IterationsSinceBest = 0;
            PreviousBest = Maximize ? float.MinValue : float.MaxValue;
        }
        else
        {
            if (Pso)
            {
                if (!CheckForSwitchToGd())
                {
                    UpdateSwarm();
                }
                else
                {
                    Jobs = MoveKernel(true);
                }
            }
            else
            {
                Jobs = MoveKernel(false);
            }
        }
        return Jobs;
    }

    int IterationsSinceBest;
    float PreviousBest;

    private bool CheckForSwitchToGd()
    {
        if (Maximize)
        {
            var mustBeat = PreviousBest + BestDelta;
            for (int i = 0; i < Population.Length; i++)
            {
                if (Population[i].BestValue >= mustBeat)
                {
                    PreviousBest = Population[i].BestValue;
                    mustBeat = Population[i].BestValue + BestDelta;
                    IterationsSinceBest = 0;
                }
            }
        }
        else
        {
            var mustBeat = PreviousBest - BestDelta;
            for (int i = 0; i < Population.Length; i++)
            {
                if (Population[i].BestValue <= mustBeat)
                {
                    PreviousBest = Population[i].BestValue;
                    mustBeat = Population[i].BestValue + BestDelta;
                    IterationsSinceBest = 0;
                }
            }
        }
        if (IterationsSinceBest >= IterationsToSwitch)
        {
            Pso = false;
            Console.WriteLine("Switched to Gradient Descent on iteration " + (Root.CurrentIteration + 1).ToString());
            return true;
        }
        IterationsSinceBest++;
        return false;
    }

    Random Random;

    /// <summary>
    /// Setup all of the members of the ParticleSwarm and initialize their positions.
    /// </summary>
    private void InitializeSwarm()
    {
        Random = new Random(RandomSeed);
        Pso = true;
        CreateInitialJobs();
        InitializePopulation();
    }

    /// <summary>
    /// Initializes the population with the given jobs
    /// </summary>
    private void InitializePopulation()
    {
        var population = new Particle[SwarmSize];
        for (int i = 0; i < population.Length; i++)
        {
            population[i] = new Particle(this, Jobs[i], Maximize, Random);
        }
        Population = population;
    }

    private void CreateInitialJobs()
    {
        Job[] jobs = new Job[SwarmSize];
        var random = new Random(RandomSeed);
        var parameters = Root.Parameters;
        for (int i = 0; i < jobs.Length; i++)
        {
            jobs[i] = GenerateRandomJob(parameters, random);
        }
        Jobs = new List<Job>(jobs);
    }

    private Job GenerateRandomJob(List<ParameterSetting> parameters, Random random)
    {
        var ret = CleanJob(parameters);
        for (int i = 0; i < ret.Parameters.Length; i++)
        {
            ret.Parameters[i].Current =
                ((ret.Parameters[i].Maximum - ret.Parameters[i].Minimum) * ((float)random.NextDouble()))
                + ret.Parameters[i].Minimum;
        }
        return ret;
    }

    private Job CleanJob(List<ParameterSetting> parameters)
    {
        var ret = new Job
        {
            Processed = false,
            ProcessedBy = null,
            Value = float.NaN,
            Processing = false,
            Parameters = new ParameterSetting[parameters.Count]
        };
        for (int i = 0; i < ret.Parameters.Length; i++)
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

    /// <summary>
    /// Update the swarm to new positions
    /// </summary>
    private void UpdateSwarm()
    {
        Parallel.For(0, Population.Length, i =>
        {
            // Update the current particle if it has seen the best parameter for itself so far
            Population[i].UpdateIfBest();
        });
        GetGlobalBest(out float[] globalBest, out float[] generationBest);
        // Now that we have the best, find the closest M to our best and update our position
        for (int i = 0; i < Population.Length; i++)
        {
            // Figure our who the closest neighbors are
            Population[i].UpdateVelocity(this, globalBest, generationBest, Random);
            Jobs[i] = Population[i].UpdatePosition(this, Random);
        }
    }

    private void GetGlobalBest(out float[] globalBest, out float[] generationBest)
    {
        int globalBestIndex = 0;
        int generationBestIndex = 0;
        if (Maximize)
        {
            for (int i = 1; i < Population.Length; i++)
            {
                if (Population[i].BestValue > Population[globalBestIndex].BestValue)
                {
                    globalBestIndex = i;
                }
                if (Population[i].Job.Value > Population[generationBestIndex].Job.Value)
                {
                    generationBestIndex = i;
                }
            }
        }
        else
        {
            for (int i = 1; i < Population.Length; i++)
            {
                if (Population[i].BestValue < Population[globalBestIndex].BestValue)
                {
                    globalBestIndex = i;
                }
                if (Population[i].Job.Value < Population[generationBestIndex].Job.Value)
                {
                    generationBestIndex = i;
                }
            }
        }
        globalBest = Population[globalBestIndex].BestParameters;
        generationBest = Population[generationBestIndex].Job.Parameters.Select(p => p.Current).ToArray();
    }

    float[] KernelMomentum;

    private void CreateMomentum()
    {
        var numberOfParameters = Root.Parameters.Count;
        var momentum = new float[numberOfParameters];
        KernelMomentum = momentum;
    }

    private List<Job> MoveKernel(bool first)
    {
        var ret = new List<Job>();
        var parameters = Root.Parameters;
        var oldJobs = Root.CurrentJobs;
        var kernel = first ?
            CreateJobFromBest()
            : Clone(oldJobs.Last());
        // Alter momentum
        if (!first)
        {
            UpdateMomentum(parameters, oldJobs);
            ApplyMomentum(kernel);
        }
        CreateWhiskers(ret, parameters, kernel);
        ret.Add(kernel);
        return ret;
    }

    private Job CreateJobFromBest()
    {
        var job = CleanJob(Root.Parameters);
        var parameters = job.Parameters;
        var values = Population[FindBestIndex()].BestParameters;
        for (int i = 0; i < values.Length; i++)
        {
            parameters[i].Current = values[i];
        }
        return job;
    }

    private int FindBestIndex()
    {
        var bestValue = Population[0].BestValue;
        int bestIndex = 0;
        if (Maximize)
        {
            for (int i = 1; i < Population.Length; i++)
            {
                if (Population[i].BestValue > bestValue)
                {
                    bestValue = Population[i].BestValue;
                    bestIndex = i;
                }
            }
        }
        else
        {
            for (int i = 1; i < Population.Length; i++)
            {
                if (Population[i].BestValue < bestValue)
                {
                    bestValue = Population[i].BestValue;
                    bestIndex = i;
                }
            }
        }
        return bestIndex;
    }

    private void CreateWhiskers(List<Job> ret, List<ParameterSetting> parameters, Job kernel)
    {
        for (int j = 0; j < parameters.Count; j++)
        {
            var delta = (kernel.Parameters[j].Maximum - kernel.Parameters[j].Minimum)
                * WhiskerSize;
            ret.Add(AddWisker(kernel, j, -delta));
            ret.Add(AddWisker(kernel, j, delta));
        }
    }

    private Job AddWisker(Job kernel, int j, float delta)
    {
        var whisker = Clone(kernel);
        whisker.Parameters[j].Current = kernel.Parameters[j].Current + delta;
        if (whisker.Parameters[j].Current > whisker.Parameters[j].Maximum)
        {
            whisker.Parameters[j].Current = whisker.Parameters[j].Maximum;
        }
        return whisker;
    }

    private Job Clone(Job job)
    {
        Job ret = new()
        {
            Processed = false,
            Processing = false,
            ProcessedBy = null,
            Value = float.NaN,
            Parameters = new ParameterSetting[job.Parameters.Length]
        };
        for (int i = 0; i < ret.Parameters.Length; i++)
        {
            ret.Parameters[i] = new ParameterSetting
            {
                Maximum = job.Parameters[i].Maximum,
                Minimum = job.Parameters[i].Minimum,
                Current = job.Parameters[i].Current,
                Names = job.Parameters[i].Names
            };
        }
        return ret;
    }

    private void ApplyMomentum(Job kernel)
    {
        var kernelParameters = kernel.Parameters;
        var momentum = KernelMomentum;
        for (int i = 0; i < kernelParameters.Length; i++)
        {
            var parameter = kernelParameters[i];
            parameter.Current += momentum[i] * (parameter.Maximum - parameter.Minimum);
            if (momentum[i] > 0)
            {
                if (parameter.Current > parameter.Maximum)
                {
                    parameter.Current = parameter.Maximum;
                }
            }
            else
            {
                if (parameter.Current < parameter.Minimum)
                {
                    parameter.Current = parameter.Minimum;
                }
            }
        }
    }

    private void UpdateMomentum(List<ParameterSetting> parameters, List<Job> oldJobs)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            var gradient = oldJobs[2 * i + 1].Value - oldJobs[2 * i].Value;
            gradient *= ErrorFactor;
            float kernelValue = oldJobs[oldJobs.Count - 1].Value;
            // if we want to minimize, go backwards
            if (!Maximize)
            {
                gradient = -gradient;
                if (oldJobs[2 * i + 1].Value >= kernelValue && oldJobs[2 * i].Value >= kernelValue)
                {
                    gradient = 0;
                }
            }
            else
            {
                if (oldJobs[2 * i + 1].Value <= kernelValue && oldJobs[2 * i].Value <= kernelValue)
                {
                    gradient = 0;
                }
            }
            KernelMomentum[i] = KernelMomentum[i] * MomentumFactor + gradient;
        }
        if (ReportKernelMovement)
        {
            var distanceFromPSOBest = Math.Round(Math.Sqrt(Population[FindBestIndex()].BestParameters.Zip(oldJobs[oldJobs.Count - 1].Parameters.Select(p => p.Current), (f, s) => (f - s) * (f - s)).Sum()), 8);
            double momentumDistance = Math.Round(Math.Sqrt(KernelMomentum.Sum(v => v * v)), 8);
            Console.WriteLine($"Distance from PSO {distanceFromPSOBest} and Momentum {momentumDistance}");
        }
    }

    public void IterationComplete()
    {
        // nothing to do here
    }

    public bool RuntimeValidation(ref string error)
    {
        if (SwarmSize <= 0)
        {
            error = "In '" + Name + "' the swarm size must be greater than 1!";
            return false;
        }
        return true;
    }
}
