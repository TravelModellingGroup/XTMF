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
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using XTMF;

namespace TMG.GTAModel.V2.Distribution
{
    public class V2ExternalDistribution : IDemographicDistribution
    {
        [SubModelInformation( Required = true, Description = "The rates for each PD that will produce an external trip (IE and EI)." )]
        public IDataSource<SparseTwinIndex<float>> DistributionRates;

        [SubModelInformation( Required = true, Description = "The observed external trips for the base year(1996)." )]
        public IDataSource<SparseTwinIndex<float>> ObservedExternalTrips;

        [ParentModel]
        public IDemographicCategoyPurpose Parent;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Save Location", "", "The location to save the data to, relative to the run directory.  Leave this blank to not save." )]
        public string SaveDistribution;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions,
            IEnumerable<IDemographicCategory> category)
        {
            var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            float[][] generationRates = ProduceNormalizedObservedData();
            SaveData.SaveMatrix( zones, generationRates, Path.Combine( SaveDistribution, "GenerationRates.csv" ) );
            Apply( ret, generationRates );
            if ( !String.IsNullOrWhiteSpace( SaveDistribution ) )
            {
                SaveData.SaveMatrix( ret, Path.Combine( SaveDistribution, "ExternalDistribution.csv" ) );
            }
            yield return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void Apply(SparseTwinIndex<float> ret, float[][] rates)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var data = ret.GetFlatData();
            Parallel.For( 0, data.Length, i =>
            {
                var row = data[i];
                var rateRow = rates[i];
                if ( zones[i].RegionNumber == 0 )
                {
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        row[j] = zones[i].Population * rateRow[j];
                    }
                }
                else
                {
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        row[j] = zones[j].Population * rateRow[j];
                    }
                }
            } );
        }

        private float[][] ProduceNormalizedObservedData()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            ObservedExternalTrips.LoadData();
            DistributionRates.LoadData();
            var distributionRates = ObservedExternalTrips.GiveData().GetFlatData();
            var generationRates = DistributionRates.GiveData();
            DistributionRates.UnloadData();
            ObservedExternalTrips.UnloadData();
            // EI
            Parallel.For( 0, distributionRates.Length, i =>
                {
                    if ( zones[i].RegionNumber == 0 )
                    {
                        var observedRates = distributionRates[i];
                        var sum = 0.0;
                        for ( int j = 0; j < observedRates.Length; j++ )
                        {
                            sum += observedRates[j];
                        }
                        if ( sum <= 0 )
                        {
                            return;
                        }
                        var factor = 1f / (float)sum;
                        if ( factor > 1.1f | factor < 0.7f )
                        {
                            factor = 1f;
                        }
                        for ( int j = 0; j < observedRates.Length; j++ )
                        {
                            observedRates[j] *= factor;
                            if ( observedRates[j] > 0 )
                            {
                                observedRates[j] = generationRates[zones[i].PlanningDistrict, zones[j].PlanningDistrict] * observedRates[j];
                            }
                        }
                    }
                } );
            // IE
            Parallel.For( 0, zones.Length, j =>
                {
                    var sum = 0.0;
                    for ( int i = 0; i < distributionRates.Length; i++ )
                    {
                        if ( zones[i].RegionNumber > 0 )
                        {
                            sum += distributionRates[i][j];
                        }
                    }
                    if ( sum <= 0 )
                    {
                        return;
                    }
                    var factor = 1f / (float)sum;
                    if ( factor > 1.1f | factor < 0.7f )
                    {
                        factor = 1f;
                    }
                    for ( int i = 0; i < distributionRates.Length; i++ )
                    {
                        if ( zones[i].RegionNumber > 0 )
                        {
                            distributionRates[i][j] *= factor;
                            if ( distributionRates[i][j] > 0 )
                            {
                                distributionRates[i][j] *= generationRates[zones[i].PlanningDistrict, zones[j].PlanningDistrict];
                            }
                        }
                    }
                } );
            return distributionRates;
        }
    }
}