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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.GTAModel
{
    public class HBOGeneration : DemographicCategoryGeneration
    {
        [RunParameter("Generation FileName", "", "The name of the file to save to, this will append the file. Leave blank to not save.")]
        public string GenerationOutputFileName;

        [SubModelInformation(Description = "Used to gather the daily generation rates", Required = true)]
        public IDataSource<SparseTriIndex<float>> LoadRates;

        [RunParameter("Planning Districts", true, "Is the data using planning districts?")]
        public bool UsesPlanningDistricts;

        internal bool LoadData = true;
        internal SparseTriIndex<float> Rates;

        public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            if ( LoadData && Rates == null )
            {
                LoadRates.LoadData();
                Rates = LoadRates.GiveData();
            }
            InitializeDemographicCategory();
            var flatProduction = production.GetFlatData();
            var numberOfIndexes = flatProduction.Length;

            // Compute the Production
            float totalProduction = 0;
            totalProduction = ComputeProduction( flatProduction, numberOfIndexes );
            SaveGenerationData( totalProduction );
            //The HBO Model does NOT include having an attraction component.  The distribution will handle this case.
            if ( LoadData )
            {
                Rates = null;
            }
        }

        public override bool RuntimeValidation(ref string error)
        {
            return base.RuntimeValidation( ref error );
        }

        private float ComputeProduction(float[] flatProduction, int numberOfZones)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
            {
                if ( ( zones[i].Population == 0 ) | ( zones[i].RegionNumber == 0 ) ) return;
                float temp = 0f;

                var zoneNumber = zones[i].ZoneNumber;
                var demographics = Root.Demographics;
                var spatialIndex = UsesPlanningDistricts ? zones[i].PlanningDistrict : zones[i].ZoneNumber;
                var ageProbabilies = demographics.AgeRates;
                var studentProbabilities = Root.Demographics.SchoolRates[zoneNumber];
                var empStatProbabilities = demographics.EmploymentStatusRates[zoneNumber];
                var occProbabilities = demographics.OccupationRates[zoneNumber];
                var population = zones[i].Population;
                var empStat = EmploymentStatusCategory[0].Start;
                foreach ( var ageRange in AgeCategoryRange )
                {
                    for ( int ageCat = ageRange.Start; ageCat <= ageRange.Stop; ageCat++ )
                    {
                        var ageProbability = ageProbabilies[zoneNumber, ageCat];
                        var empStatProbability = empStatProbabilities[ageCat, empStat];
                        if ( empStat == 0 )
                        {
                            var mobilityProbability = UnemployedMobilityProbability(Mobility[0].Start, demographics.NonWorkerVehicleRates[zoneNumber], ageCat, demographics.DriversLicenseRates[zoneNumber]);
                            var studentProbability = studentProbabilities[ageCat, empStat];
                            // if student
                            int hboType = 3;
                            temp += population * Rates[spatialIndex, ageCat, hboType] * ageProbability * empStatProbability * mobilityProbability * studentProbability;
                            // if not student
                            hboType = 4;
                            temp += population * Rates[spatialIndex, ageCat, hboType] * ageProbability * empStatProbability * mobilityProbability * ( 1 - studentProbability );
                        }
                        else
                        {
                            
                            foreach ( var occSet in OccupationCategory )
                            {
                                for ( int occ = occSet.Start; occ <= occSet.Stop; occ++ )
                                {
                                    var occProbability = occProbabilities[ageCat, empStat, occ];
                                    var mobilityProbability = EmployedMobilityProbability( Mobility[0].Start, empStat, occ ,
                                        demographics.WorkerVehicleRates[zoneNumber], ageCat, demographics.DriversLicenseRates[zoneNumber] );
                                    // we only need to add this in once because the probabilities are the same if you are a student or not
                                    temp += population * Rates[spatialIndex, ageCat, empStat] * ageProbability * mobilityProbability * empStatProbability * occProbability;
                                }
                            }
                        }
                    }
                }
                flatProduction[i] = temp;
            } );
            return flatProduction.Sum();
        }

        private static float EmployedMobilityProbability(int mobility, int emp, int occ, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
        {
            switch ( mobility )
            {
                case 0:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 0];
                case 1:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 1];

                case 2:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 2];
                case 3:
                    return ( dlicRate[age, emp] * ncars[1, occ, 0] );
                case 4:
                    return dlicRate[age, emp] * ncars[1, occ, 1];
                case 5:
                    return dlicRate[age, emp] * ncars[1, occ, 2];
                default:
                    throw new XTMFRuntimeException( "Unknown mobility type '" + mobility.ToString() + "'!" );
            }
        }

        private static float UnemployedMobilityProbability(int mobility, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
        {
            switch ( mobility )
            {
                case 0:
                    return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 0];
                case 1:
                    return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 1];
                case 2:
                    return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 2];
                case 3:
                    return dlicRate[age, 0] * ncars[1, age, 0];
                case 4:
                    return dlicRate[age, 0] * ncars[1, age, 1];
                case 5:
                    return dlicRate[age, 0] * ncars[1, age, 2];
                default:
                    throw new XTMFRuntimeException( "Unknown mobility type '" + mobility.ToString() + "'!" );
            }
        }

        private void SaveGenerationData(float totalProduction)
        {
            if ( !String.IsNullOrEmpty( GenerationOutputFileName ) )
            {
                bool first = !File.Exists( GenerationOutputFileName );
                // if the file name exists try to write to it, appending
                using (StreamWriter writer = new StreamWriter( GenerationOutputFileName, true ))
                {
                    if ( first )
                    {
                        writer.WriteLine( "Age,Employment,Occupation,Mobility,Total" );
                    }
                    writer.Write( AgeCategoryRange.ToString() );
                    writer.Write( ',' );
                    writer.Write( EmploymentStatusCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( OccupationCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( Mobility.ToString() );
                    writer.Write( ',' );
                    writer.WriteLine( totalProduction );
                }
            }
        }
    }
}