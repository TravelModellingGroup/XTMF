/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Threading.Tasks;

namespace TMG.Functions;

public static class Fratar
{
    public static void Run(float[][] ret, float[] o, float[] d, float[][] baseYearObservations, float maximumError, int maxIterations)
    {
        float[] dStar = new float[o.Length];
        float[][] originalNormalizedProbabilities = NormalizeObservations( baseYearObservations );
        for ( int i = 0; i < dStar.Length; i++ )
        {
            dStar[i] = 1f;
        }
        for ( int i = 0; i < maxIterations; i++ )
        {
            Apply( ret, o, dStar, originalNormalizedProbabilities );
            if ( CheckError( ret, d, dStar, maximumError ) )
            {
                return;
            }
        }
    }

    private static void Apply(float[][] ret, float[] o, float[] dStar, float[][] obsProb)
    {
        Parallel.For( 0, o.Length, i =>
            {
                if ( o[i] <= 0 )
                {
                    return;
                }
                var sum = 0.0;
                for ( int j = 0; j < o.Length; j++ )
                {
                    sum += dStar[j] * obsProb[i][j];
                }
                // normalize the factors
                var factor = 1f / (float)sum;
                if ( !float.IsInfinity( factor ) )
                {
                    for ( int j = 0; j < o.Length; j++ )
                    {
                        ret[i][j] = o[i] * dStar[j] * obsProb[i][j] * factor;
                    }
                }
            } );
    }

    private static bool CheckError(float[][] ret, float[] d, float[] dStar, float maximumError)
    {
        bool nonePastMaxError = true;
        Parallel.For( 0, d.Length, j =>
            {
                if ( d[j] <= 0 ) return;
                var total = 0.0;
                for ( int i = 0; i < d.Length; i++ )
                {
                    total += ret[i][j];
                }
                total = 1.0 / total;
                if ( !double.IsInfinity( total ) )
                {
                    var factor = d[j] * (float)total;
                    if ( Math.Abs( 1 - factor ) > maximumError )
                    {
                        nonePastMaxError = false;
                    }
                    dStar[j] *= factor;
                }
            } );
        return nonePastMaxError;
    }

    private static float[][] NormalizeObservations(float[][] baseYearObservations)
    {
        var ret = new float[baseYearObservations.Length][];
        Parallel.For( 0, baseYearObservations.Length, i =>
        {
            ret[i] = new float[baseYearObservations.Length];
            var total = baseYearObservations[i].Sum();
            // if there are no trips, there are no probabilities
            if ( total <= 0 )
            {
                return;
            }
            var normalizingFactor = 1f / total;
            for ( int j = 0; j < ret[i].Length; j++ )
            {
                ret[i][j] = baseYearObservations[i][j] * normalizingFactor;
            }
        } );
        return ret;
    }
}