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
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel
{
    public class ProportionalUpdateDistribution : IDemographicDistribution
    {
        [SubModelInformation( Description = "The base data that we will fit against.", Required = false )]
        public List<IReadODData<float>> BaseData;

        [RootModule]
        public ITravelDemandModel Root;

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
            get { return null; }
        }

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
        {
            var eProd = productions.GetEnumerator();
            var eBaseData = this.BaseData.GetEnumerator();
            var eCat = category.GetEnumerator();
            var zones = this.Root.ZoneSystem.ZoneArray;
            if ( BaseData.Count != category.Count() )
            {
                throw new XTMFRuntimeException( "In " + this.Name + " the number of BaseData entries is not the same as the number of demographic categories!" );
            }
            while ( eProd.MoveNext() && eBaseData.MoveNext() && eCat.MoveNext() )
            {
                var prod = eProd.Current;
                var data = eBaseData.Current;
                var cat = eCat.Current;

                // Setup everything for this category
                cat.InitializeDemographicCategory();
                var ret = zones.CreateSquareTwinArray<float>();
                LoadInBaseData( ret, data );
                UpdateData( ret, prod );
                yield return ret;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void LoadInBaseData(SparseTwinIndex<float> ret, IReadODData<float> data)
        {
            try
            {
                Parallel.ForEach( data.Read(), delegate(ODData<float> point)
                {
                    ret[point.O, point.D] = point.Data;
                } );
            }
            catch ( AggregateException e )
            {
                if ( e.InnerException is XTMFRuntimeException )
                {
                    throw new XTMFRuntimeException( e.Message );
                }
                else
                {
                    throw new XTMFRuntimeException( e.Message + "\r\n" + e.StackTrace );
                }
            }
        }

        private void UpdateData(SparseTwinIndex<float> ret, SparseArray<float> productions)
        {
            var flatProd = productions.GetFlatData();
            var flatRet = ret.GetFlatData();
            var numberOfZones = flatProd.Length;
            try
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                {
                    var p = flatProd[i];
                    if ( p == 0 )
                    {
                        // if there is no production, clear out the data
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            flatRet[i][j] = 0;
                        }
                        return;
                    }
                    var sum = 0f;
                    // Gather the sum of all of the destinations from this origin
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        sum += flatRet[i][j];
                    }
                    // The rows should already be seeded however, if they are not
                    // just return since all of the values are zero anyway
                    if ( sum == 0 )
                    {
                        return;
                    }
                    // Calculate the new balance factor
                    var factor = p / sum;
                    // now that we have the new factor we update the demand
                    for ( int j = 0; j < numberOfZones; j++ )
                    {
                        flatRet[i][j] *= factor;
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
        }
    }
}