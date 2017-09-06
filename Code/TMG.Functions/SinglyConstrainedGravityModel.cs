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
using XTMF;

namespace TMG.Functions
{
    public class SinglyConstrainedGravityModel
    {
        public static SparseTwinIndex<float> Process(SparseArray<float> production, float[] friction)
        {
            var ret = production.CreateSquareTwinArray<float>();
            var flatRet = ret.GetFlatData();
            var flatProduction = production.GetFlatData();
            var numberOfZones = flatProduction.Length;
            try
            {
                // Make all of the frictions to the power of E
                Parallel.For( 0, friction.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    delegate(int i)
                    {
                        friction[i] = (float)Math.Exp( friction[i] );
                    } );

                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    delegate(int i)
                    {
                        float sum = 0f;
                        var iIndex = i * numberOfZones;
                        // gather the sum of the friction
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            sum += friction[iIndex + j];
                        }
                        if ( sum <= 0 )
                        {
                            return;
                        }
                        sum = 1f / sum;
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            flatRet[i][j] = flatProduction[i] * ( friction[iIndex + j] * sum );
                        }
                    } );
            }
            catch ( AggregateException e )
            {
                if ( e.InnerException is XTMFRuntimeException )
                {
                    throw new XTMFRuntimeException(null, e.InnerException.Message );
                }
                else
                {
                    throw new XTMFRuntimeException(null, e.InnerException?.Message + "\r\n" + e.InnerException?.StackTrace );
                }
            }
            return ret;
        }
    }
}