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
using TMG.Input;
using XTMF;

namespace TMG.Estimation.AI
{
    [ModuleInformation(Description =
        "This module is highly experimental not ready for external use!  Its goal is to help provide a relative for multi modal PSO.")]
    public class GravityOptimization : IEstimationAI
    {
        [RootModule]
        public IEstimationHost Root;
        public string Name { get; set; }

        public float Progress { get { return 0f; } }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        [RunParameter("Increase of Energy", 0.001f, "The amount of energy to increase a particle if it doesn't hit its best value")]
        public float IncreaseOfEnergy;

        [SubModelInformation(Required = false, Description = "Save the changes to stars for each generation.")]
        public FileLocation StarLog;

        private struct Particle
        {
            internal float[] Position;
            internal float[] Velocity;
            internal float BestFitness;
            internal float CurrentFitness;
            internal float[] WeightToStars;
            internal float Energy;
        }


        internal int CurrentStarNumber = 1;

        private struct Star
        {
            internal int StarNumber;
            internal float[] Position;
            internal float PreviousMass;
            internal float CurrentMass;
            internal float Fitness;
            internal bool Living;
        }

        private Particle[] Particles;
        private Star[] Stars;

        [RunParameter("Particles", 100, "The number of particles for the system.")]
        public int NumberOfParticles;

        [RunParameter("Stars", 5, "The number of stars for the system.")]
        public int NumberOfStars;

        [RunParameter("NicheDistance", 0.5f, "The minimum distance between two stars before they destroy each other.")]
        public float NicheDistance;

        private Random Random;

        public List<Job> CreateJobsForIteration()
        {
            var iteration = Root.CurrentIteration;
            UpdateParticleFitness(iteration);
            switch(iteration)
            {
                case 0:
                    return GenerateInitialConditions();
                case 1:
                    CreateTheFirstStars();
                    break;
            }
            return GenerateNextGeneration();
        }

        private class ParticleCompare : IComparable<ParticleCompare>
        {
            internal int Index;
            internal float Fitness;

            public ParticleCompare(int index, float fitness)
            {
                Index = index;
                Fitness = fitness;
            }
            public int CompareTo(ParticleCompare other)
            {
                if(Fitness < other.Fitness)
                {
                    return -1;
                }
                else if(Fitness > other.Fitness)
                {
                    return 1;
                }
                return 0;
            }
        }

        private void CreateTheFirstStars()
        {
            ParticleCompare[] compareParticles = BuildCompareParticles();
            int nextIndex = 0;
            for(int i = 0; i < Stars.Length; i++)
            {
                if(nextIndex >= compareParticles.Length)
                {
                    for(int j = i; j < Stars.Length; j++)
                    {
                        Stars[j].Position = new float[Parameters.Length];
                        Stars[j].Fitness = Maximize ? float.MinValue : float.MaxValue;
                        Stars[j].Living = false;
                        Stars[j].CurrentMass = 0.0f;
                        Stars[j].PreviousMass = 0.0f;
                    }
                    break;
                }

                Particle particle = Particles[compareParticles[nextIndex++].Index];
                var nextPosition = particle.Position;
                // if there is a collision we will need to try again
                if(CheckForCollisions(nextPosition, i))
                {
                    i--;
                    continue;
                }
                Stars[i].Position = nextPosition.Clone() as float[];
                Stars[i].Fitness = particle.CurrentFitness;
                Stars[i].CurrentMass = 0.0f;
                Stars[i].PreviousMass = 0.0f;
                Stars[i].Living = true;
                Stars[i].StarNumber = CurrentStarNumber++;
            }
            ComputeParticleWeight();
            UpdateMass();
        }

        private ParticleCompare[] BuildCompareParticles()
        {
            var compareParticles = new ParticleCompare[Particles.Length];
            for(int i = 0; i < compareParticles.Length; i++)
            {
                compareParticles[i] = new ParticleCompare(i, Particles[i].CurrentFitness);
            }
            Array.Sort(compareParticles);
            if(Maximize)
            {
                Array.Reverse(compareParticles);
            }
            return compareParticles;
        }

        private bool CheckForCollisions(float[] nextPosition, int lastStar)
        {
            for(int i = 0; i < lastStar; i++)
            {
                if(Distance(nextPosition, Stars[i].Position) <= NicheDistance)
                {
                    return true;
                }
            }
            return false;
        }

        private float Distance(float[] nextPosition, float[] position)
        {
            float total = 0.0f;
            for(int i = 0; i < nextPosition.Length; i++)
            {
                var d = (nextPosition[i] - position[i]) / Parameters[i].Size;
                total += d * d;
            }
            return (float)Math.Sqrt(total);
        }

        private List<Job> GenerateNextGeneration()
        {
            // make it so
            ComputeParticleWeight();
            // The stars will have already been made so now we can compute our attraction to the stars
            UpdateStars(out bool starsChanged);
            if (StarLog != null)
            {
                SaveStarState();
            }
            if(starsChanged)
            {
                // Update the weight to the stars given the stars could have been destroyed
                ComputeParticleWeight();
            }
            MoveParticles(ConfineToBounds);
            // at the end just convert the particles to jobs
            return GenerateJobs();
        }

        private void SaveStarState()
        {
            var generation = Root.CurrentIteration;
            var exists = File.Exists(StarLog);
            using StreamWriter writer = new(StarLog, true);
            if (!exists)
            {
                writer.Write("Generation,Star,Mass");
                foreach (var parameter in Parameters)
                {
                    foreach (var name in parameter.Names)
                    {
                        writer.Write(',');
                        writer.Write(name);
                    }
                }
                writer.WriteLine();
            }
            for (int i = 0; i < Stars.Length; i++)
            {
                writer.Write(generation);
                writer.Write(',');
                writer.Write(Stars[i].StarNumber);
                writer.Write(',');
                writer.Write(Stars[i].CurrentMass);
                var position = Stars[i].Position;
                for (int j = 0; j < position.Length; j++)
                {
                    for (int k = 0; k < Parameters[j].Names.Length; k++)
                    {
                        writer.Write(',');
                        writer.Write(Stars[i].Position[j]);
                    }
                }
                writer.WriteLine();
            }
        }

        private void MoveParticles(bool confineToBounds)
        {
            for(int i = 0; i < Particles.Length; i++)
            {
                var starRandom = (float)Random.NextDouble();
                float[] position = Particles[i].Position;
                float[] veclocity = Particles[i].Velocity;
                for(int j = 0; j < position.Length; j++)
                {
                    var currentPull = 0.0f;
                    for(int k = 0; k < Stars.Length; k++)
                    {
                        if(Stars[k].Living)
                        {
                            currentPull += starRandom * (Stars[k].Position[j] - position[j]) * Particles[i].WeightToStars[k] * StarWeight;
                        }
                    }
                    currentPull += (((float)Random.NextDouble()) * 2.0f - 1.0f) * Particles[i].Energy * RandomWeight * Parameters[j].Size;
                    currentPull = currentPull * (1.0f - ParticleMomentum) + veclocity[j] * ParticleMomentum;
                    position[j] += currentPull;
                    veclocity[j] = currentPull;
                    if(confineToBounds)
                    {
                        if(position[j] > Parameters[j].Maximum)
                        {
                            position[j] = Parameters[j].Maximum;
                            veclocity[j] = -veclocity[j];
                        }
                        else if(position[j] < Parameters[j].Minimum)
                        {
                            position[j] = Parameters[j].Minimum;
                            veclocity[j] = -veclocity[j];
                        }
                    }
                }
            }
        }

        [RunParameter("Confine To Bounds", true, "Limit the search to the given bounds as defined by the parameters.")]
        public bool ConfineToBounds;

        [RunParameter("StarWeight", 2.3f, "The weight applied by a star to the particle's position.")]
        public float StarWeight;

        [RunParameter("RandomWeight", 0.001f, "The weight applied to each parameter in a particle at each iteration.")]
        public float RandomWeight;

        [RunParameter("Particle Momentum", 0.3f, "The momentum to carry for the particle between generations.")]
        public float ParticleMomentum;

        private void UpdateStars(out bool starsChanged)
        {
            UpdateMass();
            // organize the particles in order by their fitness so that the best one comes first
            var compareParticles = BuildCompareParticles();
            var distances = new float[Stars.Length];
            for(int i = 0; i < compareParticles.Length; i++)
            {
                bool processNext = false;
                var fitness = compareParticles[i].Fitness;
                var particlePosition = Particles[compareParticles[i].Index].Position;
                // find the closest star
                int closestStar = -1;
                for(int j = 0; j < Stars.Length; j++)
                {
                    if(Stars[j].Living)
                    {
                        distances[j] = Distance(Stars[j].Position, particlePosition);
                        if(closestStar < 0 || distances[j] < distances[closestStar])
                        {
                            closestStar = j;
                        }
                        if(Maximize ? fitness > Stars[j].Fitness : fitness < Stars[j].Fitness)
                        {
                            processNext = true;
                        }
                    }
                }
                // check to see if it is within the star's niche
                if(closestStar >= 0)
                {
                    if(distances[closestStar] < NicheDistance)
                    {
                        // see if we should just move the star
                        if(Maximize ? fitness > Stars[closestStar].Fitness : fitness < Stars[closestStar].Fitness)
                        {
                            Array.Copy(particlePosition, Stars[closestStar].Position, particlePosition.Length);
                            Stars[closestStar].Fitness = fitness;
                            DestroyStarsThatAreTooClose(closestStar);
                        }
                    }
                    else
                    {
                        // if it isn't within any stars Niche distance
                        int unlivingIndex = -1;
                        for(int k = 0; k < Stars.Length; k++)
                        {
                            // if there is a star that isn't living
                            if(Stars[k].Living == false)
                            {
                                unlivingIndex = k;
                                break;
                            }
                        }
                        if(unlivingIndex >= 0)
                        {
                            Array.Copy(particlePosition, Stars[unlivingIndex].Position, particlePosition.Length);
                            Stars[unlivingIndex].Fitness = fitness;
                            Stars[unlivingIndex].Living = true;
                            Stars[unlivingIndex].StarNumber = CurrentStarNumber++;
                            DestroyStarsThatAreTooClose(unlivingIndex);
                            ComputeParticleWeight();
                            break;
                        }
                        // if all of the stars are living
                        // see if we should just move the closest star
                        if(Maximize ? fitness > Stars[closestStar].Fitness : fitness < Stars[closestStar].Fitness)
                        {
                            Array.Copy(particlePosition, Stars[closestStar].Position, particlePosition.Length);
                            Stars[closestStar].Fitness = fitness;
                            DestroyStarsThatAreTooClose(closestStar);
                        }
                    }
                }
                if(!processNext)
                {
                    break;
                }
            }
            starsChanged = false;
            // update the mass at the end
            for(int i = 0; i < Stars.Length; i++)
            {
                Stars[i].PreviousMass = Stars[i].CurrentMass;
            }
        }

        private void UpdateMass()
        {
            float[] fitnessToStar = new float[Stars.Length];
            float[] weightToStar = new float[Stars.Length];
            float totalFitness = 0.0f;
            for(int i = 0; i < Particles.Length; i++)
            {
                var fitness = Particles[i].CurrentFitness;
                float[] weightRow = Particles[i].WeightToStars;
                for(int j = 0; j < weightRow.Length; j++)
                {
                    float weight = weightRow[j];
                    totalFitness += fitnessToStar[j] += fitness * weight;
                    weightToStar[j] += weight;
                }
            }
            int best = -1;
            for(int i = 0; i < Stars.Length; i++)
            {
                fitnessToStar[i] = fitnessToStar[i] / weightToStar[i];
                if(Stars[i].Living && (best < 0 || (Maximize ? fitnessToStar[i] > fitnessToStar[best] : fitnessToStar[i] < fitnessToStar[best])))
                {
                    best = i;
                }
            }
            var previousFactor = PreviousMassAverageWeight;
            var nextFactor = Degeneration - previousFactor;
            for(int i = 0; i < Stars.Length; i++)
            {
                if(Stars[i].Living)
                {
                    Stars[i].CurrentMass = Stars[i].PreviousMass * previousFactor;
                    if(Maximize)
                    {
                        Stars[i].CurrentMass += (fitnessToStar[i] / totalFitness) * nextFactor;
                    }
                    else
                    {
                        Stars[i].CurrentMass += ((totalFitness - fitnessToStar[i]) / totalFitness) * nextFactor;
                    }
                    // only update if there is a difference
                    if(float.IsInfinity(Stars[i].CurrentMass) | float.IsNaN(Stars[i].CurrentMass))
                    {
                        Stars[i].CurrentMass = Stars[i].PreviousMass;
                    }
                }
            }
        }

        private void DestroyStarsThatAreTooClose(int closestStar)
        {
            for(int k = 0; k < Stars.Length; k++)
            {
                // ignore ourselves and stars that are dead
                if((closestStar == k) | !Stars[k].Living)
                {
                    continue;
                }
                var distance = Distance(Stars[closestStar].Position, Stars[k].Position);
                // check to see if two stars collide and destroy the star that isn't as good
                if(distance < NicheDistance)
                {
                    if(Maximize ? Stars[k].Fitness <= Stars[closestStar].Fitness : Stars[k].Fitness >= Stars[closestStar].Fitness)
                    {
                        Stars[k].Living = false;
                    }
                    else
                    {
                        Stars[closestStar].Living = false;
                    }
                    break;
                }
            }
        }

        private void ComputeParticleWeight()
        {
            Parallel.For(0, Particles.Length, i =>
            {
                var total = 0.0f;
                for(int j = 0; j < Stars.Length; j++)
                {
                    if(Stars[j].Living)
                    {
                        total += (Particles[i].WeightToStars[j] = (float)Math.Exp((MassFactor * Stars[j].PreviousMass) * (float)Math.Pow(Distance(Particles[i].Position, Stars[j].Position), DistanceFactor)));
                        // if this happens we are at the point of the star
                        if(float.IsInfinity(total) | float.IsNaN(total))
                        {
                            for(int k = 0; k < Stars.Length; k++)
                            {
                                Particles[i].WeightToStars[k] = j == k ? 1.0f : 0.0f;
                            }
                            total = 1.0f;
                            break;
                        }
                    }
                    else
                    {
                        Particles[i].WeightToStars[j] = float.NaN;
                    }
                }
                if(total > 0)
                {
                    total = 1.0f / total;
                    for(int j = 0; j < Particles[i].WeightToStars.Length; j++)
                    {
                        Particles[i].WeightToStars[j] *= total;
                        if(float.IsInfinity(Particles[i].WeightToStars[j]) | float.IsNaN(Particles[i].WeightToStars[j]))
                        {
                            for(int k = 0; k < Stars.Length; k++)
                            {
                                Particles[i].WeightToStars[k] = j == k ? 1.0f : 0.0f;
                            }
                            break;
                        }
                    }
                }
            });
        }

        [RunParameter("MassFactor", 0.0f, "The factor to apply to the mass of a star while computing the particles attraction.")]
        public float MassFactor;

        [RunParameter("Previous Mass Average Weight", 0.5f, "The weight to use for computing the next mass for the stars. This value will be applied to the previous iterations mass and (1-it) will be applied to the current generation")]
        public float PreviousMassAverageWeight;

        [RunParameter("Star Degeneration", 0.5f, "The rate at which a star that does not gain mass dies out.")]
        public float Degeneration;

        [RunParameter("DistanceFactor", -2.0f, "The exponential factor to apply to the distance to get the 'inverse relationship' between the attractiveness of the star given its distance to the particle.")]
        public float DistanceFactor;

        private ParameterSetting[] Parameters;

        [RunParameter("Random Seed", 12345, "The random seed for the AI.")]
        public int RandomSeed;

        [RunParameter("Maximize", true, "Should we be maximizing (true) or minimizing (false) the fitness function?")]
        public bool Maximize;


        private List<Job> GenerateInitialConditions()
        {
            Parameters = Root.Parameters.ToArray();
            // Setup the memory for the particles and the stars
            Particles = new Particle[NumberOfParticles];
            Stars = new Star[NumberOfStars];
            Random = new Random(RandomSeed);
            for(int p = 0; p < Particles.Length; p++)
            {
                Particles[p].Energy = 0;
                Particles[p].CurrentFitness = Maximize ? float.MinValue : float.MaxValue;
                Particles[p].BestFitness = Maximize ? float.MinValue : float.MaxValue;
                Particles[p].Position = new float[Parameters.Length];
                Particles[p].Velocity = new float[Parameters.Length];
                Particles[p].WeightToStars = new float[NumberOfStars];
                // we do this after to help memory locality
                for(int i = 0; i < Particles[p].Position.Length; i++)
                {
                    Particles[p].Position[i] = ((float)Random.NextDouble() * Parameters[i].Size) + Parameters[i].Minimum;
                    // a random number between -1 and 1
                    Particles[p].Velocity[i] = (float)Random.NextDouble() * 2.0f - 1.0f;
                }
            }
            return GenerateJobs();
        }

        private void UpdateParticleFitness(int iteration)
        {
            if(iteration <= 0) return;
            // Update the fitnesses
            var jobs = Root.CurrentJobs;
            for(int i = 0; i < Particles.Length; i++)
            {
                Particles[i].CurrentFitness = jobs[i].Value;
                if(Maximize)
                {
                    var currentBest = Math.Max(Particles[i].BestFitness, Particles[i].CurrentFitness);
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if(currentBest != Particles[i].BestFitness)
                    {
                        Particles[i].Energy = 0;
                        Particles[i].BestFitness = currentBest;
                    }
                    else
                    {
                        Particles[i].Energy += IncreaseOfEnergy;
                    }
                }
                else
                {
                    float currentBest = Math.Min(Particles[i].BestFitness, Particles[i].CurrentFitness);
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if(currentBest != Particles[i].BestFitness)
                    {
                        Particles[i].Energy = 0;
                        Particles[i].BestFitness = currentBest;
                    }
                    else
                    {
                        Particles[i].Energy += IncreaseOfEnergy;
                    }
                }
            }
        }

        public void IterationComplete()
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            if(NumberOfParticles <= 0)
            {
                error = "In '" + Name + "' we need to have at least one particle!";
                return false;
            }
            if(NumberOfStars <= 0)
            {
                error = "In '" + Name + "' we need to have at least one star!";
                return false;
            }
            if(NumberOfParticles < NumberOfStars)
            {
                error = "In '" + Name + "' there must be more particles than stars!";
                return false;
            }
            return true;
        }

        private List<Job> PreviousGeneration = [];
        private List<Job> GenerateJobs()
        {
            List<Job> ret = PreviousGeneration;
            if(ret.Count == 0)
            {
                for(int i = 0; i < Particles.Length; i++)
                {
                    var parameters = Particles[i].Position;
                    var job = new Job()
                    {
                        Parameters = Clone(),
                    };
                    job.Processed = false;
                    job.ProcessedBy = null;
                    job.Processing = false;
                    job.Value = float.NaN;
                    ret.Add(job);
                    var row = job.Parameters;
                    for(int j = 0; j < row.Length; j++)
                    {
                        row[j].Current = parameters[j];
                    }
                }
            }
            else
            {
                for(int i = 0; i < Particles.Length; i++)
                {
                    var parameters = Particles[i].Position;
                    var job = ret[i];
                    job.Processed = false;
                    job.ProcessedBy = null;
                    job.Processing = false;
                    job.Value = float.NaN;
                    ret[i] = job;
                    var row = job.Parameters;
                    for(int j = 0; j < row.Length; j++)
                    {
                        row[j].Current = parameters[j];
                    }
                }
            }
            return ret;
        }

        private ParameterSetting[] Clone()
        {
            return Parameters.Select(p => new ParameterSetting()
            {
                Maximum = p.Maximum,
                Minimum = p.Minimum,
                Names = p.Names,
                Current = p.Current,
                NullHypothesis = p.NullHypothesis
            }).ToArray();
        }
    }
}
