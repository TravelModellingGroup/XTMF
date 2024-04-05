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
using XTMF;

namespace Tasha;

/// <summary>
/// An extension of the Tasha Genetic Estimation System to include more modern techniques
///
/// The following code is based on:
/// Real-parameter evolutionary multimodal optimization — A survey of the state-of-the-art
/// Swagatam Dasa, Sayan Maity a, Bo-Yang Qub, P.N. Suganthan b,∗
/// a Department of Electronics and Telecommunication Engg., Jadavpur University, Kolkata 700 032, India
/// b School of Electrical and Electronic Engineering, Nanyang Technological University, Singapore 639798, Singapore
///
/// Swarm and Evolutionary Computation
/// Volume 1, Issue 2, June 2011, Pages 71–88
/// </summary>
public class AdvancedModeChoiceEstimationHost : ModeChoiceEstimationHost
{
    [RunParameter( "Distance", 2f, "The distance between parameters to consider a niche" )]
    public float Distance;

    [RunParameter( "Niche Capacity", 10, "The max number of population continuing in a niche" )]
    public int NicheCapacity;

    [RunParameter( "Percent Distance", false, "Use the percent of difference between parameters instead of raw values." )]
    public bool PercentDistance;

    public AdvancedModeChoiceEstimationHost(IConfiguration config)
        : base( config )
    {
    }

    protected override void GenerateNextGeneration()
    {
        Clearing();
        // the base will sort the population again before performing its selection
        base.GenerateNextGeneration();
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
        int populationSize = PopulationSize;
        // sort the population
        Array.Sort( Population, new CompareParameterSet() );
        for ( int i = 0; i < populationSize; i++ )
        {
            int win = 0;
            if ( Population[i].Value > float.MinValue )
            {
                win = 1;
            }
            for ( int j = i + 1; j < populationSize; j++ )
            {
                if ( ( Population[j].Value > float.MinValue ) && ComputeDistance( i, j ) <= Distance )
                {
                    if ( win < NicheCapacity )
                    {
                        win++;
                    }
                    else
                    {
                        Population[j].Value = float.MinValue;
                    }
                }
            }
        }
    }

    private float ComputeDistance(int first, int second)
    {
        double distance = 0;
        var firstParameters = Population[first].Parameters;
        var secondParameters = Population[second].Parameters;
        var numberOfParameters = firstParameters.Length;
        for ( int i = 0; i < numberOfParameters; i++ )
        {
            float unit;
            if ( PercentDistance )
            {
                unit = ( firstParameters[i].Current - secondParameters[i].Current ) / ( firstParameters[i].Stop - firstParameters[i].Start );
            }
            else
            {
                unit = firstParameters[i].Current - secondParameters[i].Current;
            }
            distance = distance + ( unit * unit );
        }
        return (float)Math.Sqrt( distance );
    }
}