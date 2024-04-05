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
using XTMF;

// ReSharper disable once CheckNamespace
namespace TMG.Estimation;

// ReSharper disable once InconsistentNaming
public class GeneticAI : IEstimationAI
{
    [RunParameter( "Population Size", 1000, "The number of different runs to do per generation." )]
    public int PopulationSize;

    [RunParameter( "Reseed Size", 10, "The number of parameters to generate randomly per generation." )]
    public int ReseedSize;

    [RunParameter( "Cross Exponent", 2.2f, "The exponent used for selecting the parameters to breed." )]
    public float CrossExponent;

    [RunParameter( "Max Mutation", 0.4f, "The maximum amount (in 0 to 1) that a parameter can be mutated" )]
    public float MaxMutationPercent;

    [RunParameter( "Mutation Exponent", 2f, "The exponent used for mutation" )]
    public float MutationExponent;

    [RunParameter( "Mutation Probability", 3.1f, "The number of mutations per gene. The remainder will be applied with a probability." )]
    public float MutationProbability;

    [RunParameter( "Distance", 2f, "The distance between parameters to consider a niche" )]
    public float Distance;

    [RunParameter( "Niche Capacity", 10, "The max number of population continuing in a niche" )]
    public int NicheCapacity;

    [RunParameter( "Percent Distance", false, "Use the percent of difference between parameters instead of raw values." )]
    public bool PercentDistance;

    [RunParameter( "Random Seed", 12345, "The random seed to use for this AI.." )]
    public int RandomSeed;

    [RootModule]
    public IEstimationHost Host;

    private Random Random;

    [RunParameter( "Maximize", true, "Should this AI try to maximize or minimize the values?" )]
    public bool Maximize;


    public List<Job> CreateJobsForIteration()
    {
        if ( Host.CurrentIteration == 0 )
        {
            return CreateInitialPopulation();
        }
        return EvolvePopulation();
    }

    private List<Job> EvolvePopulation()
    {
        var parameters = Host.Parameters;
        var ret = new List<Job>( PopulationSize );
        var oldPopulation = Host.CurrentJobs;
        // reorder the old jobs so they are all sorted
        if ( !Maximize )
        {
            FlipSigns( oldPopulation );
        }
        Clearing();
        for ( int i = 0; i < PopulationSize - ReseedSize; i++ )
        {
            var job = CleanJob( parameters );
            // process this job
            int firstIndex = Select();
            int secondIndex = Select();
            if ( secondIndex == firstIndex )
            {
                secondIndex++;
            }
            if ( secondIndex >= PopulationSize )
            {
                secondIndex = 0;
            }
            CrossGenes( job.Parameters, oldPopulation[firstIndex].Parameters, oldPopulation[secondIndex].Parameters );
            Mutate( job.Parameters );
            //now add it to the jobs to execute for this iteration
            ret.Add( job );
        }
        for ( int i = 0; i < ReseedSize; i++ )
        {
            ret.Add( GenerateRandomJob( parameters ) );
        }
        return ret;
    }

    private void FlipSigns(List<Job> oldPopulation)
    {
        for ( int i = 0; i < oldPopulation.Count; i++ )
        {
            oldPopulation[i].Value = -oldPopulation[i].Value;
        }
    }

    private void Mutate(ParameterSetting[] ret)
    {
        var numberOfParameters = ret.Length;
        // see if we will have an addition mutation randomly
        int numberOfMutations = ( MutationProbability - (int)MutationProbability ) > Random.NextDouble() ? (int)MutationProbability + 1 : (int)MutationProbability;
        for ( int i = 0; i < numberOfMutations; i++ )
        {
            int index = (int)( Random.NextDouble() * numberOfParameters );

            // figure out how large this dimension of parameter space is
            var space = ret[index].Maximum - ret[index].Minimum;

            // do an exponential push
            var mutation = (float)( Math.Pow( Random.NextDouble(), MutationExponent ) * ( space * MaxMutationPercent ) );

            // move the current position
            ret[index].Current += ( Random.NextDouble() > 0.5 ? mutation : -mutation );

            // Make sure that it does past the edges
            if ( ret[index].Current < ret[index].Minimum )
            {
                ret[index].Current = ret[index].Minimum;
            }
            else if ( ret[index].Current > ret[index].Maximum )
            {
                ret[index].Current = ret[index].Maximum;
            }
        }
    }

    private void CrossGenes(ParameterSetting[] ret, ParameterSetting[] firstParent, ParameterSetting[] secondParent)
    {
        for ( int i = 0; i < ret.Length; i++ )
        {
            if ( Random.NextDouble() > 0.5 )
            {
                ret[i].Current = firstParent[i].Current;
            }
            else
            {
                ret[i].Current = secondParent[i].Current;
            }
        }
    }

    private int Select()
    {
        return (int)( Math.Pow( Random.NextDouble(), CrossExponent ) * PopulationSize );
    }

    /// <summary>
    /// Based on:
    /// Petrowski
    /// A. Pétrowski, A clearing procedure as a niching method for genetic
    /// algorithms, in: Proceedings of Third IEEE International Conference on
    /// Evolutionary Computation, ICEC’96, IEEE Press, Piscataway, NJ, 1996,
    /// pp. 798–803
    /// </summary>
    private void Clearing()
    {
        var oldJobs = Host.CurrentJobs;
        int populationSize = PopulationSize;
        // sort the population
        oldJobs.Sort( new CompareParameterSet() );
        for ( int i = 0; i < populationSize; i++ )
        {
            int win = 0;
            if ( oldJobs[i].Value > float.MinValue )
            {
                win = 1;
            }
            for ( int j = i + 1; j < populationSize; j++ )
            {
                if ( ( oldJobs[j].Value > float.MinValue ) && ComputeDistance( i, j ) <= Distance )
                {
                    if ( win < NicheCapacity )
                    {
                        win++;
                    }
                    else
                    {
                        oldJobs[j].Value = float.MinValue;
                    }
                }
            }
        }
    }

    private float ComputeDistance(int first, int second)
    {
        double distance = 0;
        var oldJobs = Host.CurrentJobs;
        var firstParameters = oldJobs[first].Parameters;
        var secondParameters = oldJobs[second].Parameters;
        var numberOfParameters = firstParameters.Length;
        for ( int i = 0; i < numberOfParameters; i++ )
        {
            float unit;
            if ( PercentDistance )
            {
                unit = ( firstParameters[i].Current - secondParameters[i].Current ) / ( firstParameters[i].Maximum - firstParameters[i].Minimum );
            }
            else
            {
                unit = firstParameters[i].Current - secondParameters[i].Current;
            }
            distance = distance + ( unit * unit );
        }
        return (float)Math.Sqrt( distance );
    }

    protected class CompareParameterSet : IComparer<Job>
    {
        public int Compare(Job x, Job y)
        {
            // we want it in desc order (highest @ 0)
            if ( x.Value < y.Value ) return 1;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if ( x.Value == y.Value )
            {
                return 0;
            }
            return -1;
        }
    }

    private Job GenerateRandomJob(List<ParameterSetting> parameters)
    {
        var ret = CleanJob( parameters );
        for ( int i = 0; i < ret.Parameters.Length; i++ )
        {
            ret.Parameters[i].Current =
                ( ( ret.Parameters[i].Maximum - ret.Parameters[i].Minimum ) * ( (float)Random.NextDouble() ) )
                + ret.Parameters[i].Minimum;
        }
        return ret;
    }

    private Job CleanJob(List<ParameterSetting> parameters)
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

    private List<Job> CreateInitialPopulation()
    {
        var parameters = Host.Parameters;
        var ret = new List<Job>( PopulationSize );
        for ( int i = 0; i < PopulationSize; i++ )
        {
            ret.Add( GenerateRandomJob( parameters ) );
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
        if ( ReseedSize > PopulationSize )
        {
            error = "You can not reseed more than the size of the population!";
            return false;
        }
        Random = new Random( RandomSeed );
        return true;
    }
}
