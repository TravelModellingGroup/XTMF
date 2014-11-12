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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
namespace TMG.Functions
{
    public static class GravityModel3D
    {
        public static float[] ProduceFlows(int maxIterations, float epsilon, float[] categoriesByOrigin, float[] destinations, float[] friction, int categories, int numberofZones)
        {

            var ret = new float[categories * numberofZones * numberofZones];
            var destinationStar = new float[numberofZones];
            for ( int i = 0; i < destinationStar.Length; i++ )
            {
                destinationStar[i] = destinations[i];
            }
            int iterations = 1;
            bool balanced;
            do
            {
                Apply( ret, categoriesByOrigin, friction, destinationStar, categories, numberofZones );
                balanced = Balance( ret, destinations, destinationStar, epsilon, categories, numberofZones );
            } while (iterations++ < maxIterations & !balanced);
            return ret;
        }

        public static float _CountRows(int numberOfZones, float[] ret, int categories, int i)
        {
            float total = 0.0f;
            for ( int k = 0; k < categories; k++ )
            {
                int index = ( k * numberOfZones * numberOfZones ) + ( i * numberOfZones );
                float local = 0.0f;
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    local += ret[index + j];
                }
                total += local;
            }
            return total;
        }

        private static void Apply(float[] ret, float[] categoriesByOrigin, float[] friction, float[] dStar, int categories, int numberOfZones)
        {
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int i) =>
            {
                for ( int k = 0; k < categories; k++ )
                {
                    var catByOrigin = categoriesByOrigin[i + k * numberOfZones];
                    if ( catByOrigin <= 0 ) continue;
                    int index = ( k * numberOfZones * numberOfZones ) + ( i * numberOfZones );
                    float sumAF = 0.0f;
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        sumAF += friction[index + j] * dStar[j];
                    }
                    if ( sumAF <= 0 ) continue;
                    var totalFromOrigin = catByOrigin / sumAF;
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        ret[index + j] = totalFromOrigin * friction[index + j] * dStar[j];
                    }
                }
            } );
        }

        private static bool Balance(float[] ret, float[] destinations, float[] destinationStar, float epsilon, int categories, int numberofZones)
        {
            bool balanced = true;
            Parallel.For( 0, numberofZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                () => true,
                (int j, ParallelLoopState state, bool localBalanced) =>
            {
                if ( destinations[j] <= 0 ) return localBalanced;
                float total = 0.0f;
                for ( int k = 0; k < categories; k++ )
                {
                    int categoryDestOffset = ( numberofZones * numberofZones * k ) + j;
                    for ( int i = 0; i < numberofZones; i++ )
                    {
                        total += ret[categoryDestOffset + i * numberofZones];
                    }
                }
                var residule = destinations[j] / total;
                if ( float.IsInfinity( residule ) )
                {
                    destinationStar[j] = destinations[j];
                    return false;
                }
                else
                {
                    if ( Math.Abs( residule - 1.0f ) > epsilon )
                    {
                        localBalanced = false;
                    }
                    destinationStar[j] = destinationStar[j] * residule;
                }
                return localBalanced;
            },
                (bool localBalanced) =>
            {
                if ( !localBalanced )
                {
                    balanced = false;
                }
            } );
            Thread.MemoryBarrier();
            return balanced;
        }
    }
}
