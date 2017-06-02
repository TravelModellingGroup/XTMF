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

namespace TMG.NetworkEstimation
{
    public class AdvancedGeneticNetworkEstimationHost : GeneticNetworkEstimationHost
    {
        [RunParameter( "Distance", 2f, "The distance between parameters to consider a niche" )]
        public float Distance;

        [RunParameter( "Niche Capacity", 10, "The max number of population continuing in a niche" )]
        public int NicheCapacity;

        [RunParameter( "Percent Distance", false, "Use the percent of difference between parameters instead of raw values." )]
        public bool PercentDistance;

        protected override void GenerateNextGeneration()
        {
            Clearing();
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
                if ( Population[i].Value < float.MaxValue )
                {
                    win = 1;
                }
                for ( int j = i + 1; j < populationSize; j++ )
                {
                    if ( ( Population[j].Value < float.MaxValue ) && ComputeDistance( i, j ) <= Distance )
                    {
                        if ( win < NicheCapacity )
                        {
                            win++;
                        }
                        else
                        {
                            Population[j].Value = float.MaxValue;
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
}