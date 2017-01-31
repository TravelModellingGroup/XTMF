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
using System.Text;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Generation
{
    public class PoRPoWGeneration : DemographicCategoryGeneration
    {
        public RangeSet AllAges;

        [RunParameter( "Attraction File Name", "", typeof( FileFromOutputDirectory ), "The name of the file to save the attractions per zone and demographic category to.  Leave blank to not save." )]
        public FileFromOutputDirectory AttractionFileName;

        [RunParameter( "Generation FileName", "", "The name of the file to save to, this will append the file. Leave blank to not save." )]
        public string GenerationOutputFileName;

        [SubModelInformation( Description = "Used to gather the rates of jobs taken by external workers", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadExternalJobsRates;

        [SubModelInformation( Description = "Used to gather the external worker rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadExternalWorkerRates;

        [SubModelInformation( Description = "Used to gather the work at home rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadWorkAtHomeRates;

        public SparseTriIndex<float> WorkIntrazonal;
        internal SparseTriIndex<float> ExternalJobs;
        internal SparseTriIndex<float> ExternalRates;
        internal bool LoadData = true;
        internal SparseTriIndex<float> WorkAtHomeRates;
        private float WorkAtHomeTotal;
        private float WorkIntrazonalTotal;

        public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            if ( LoadData )
            {
                LoadExternalWorkerRates.LoadData();
                LoadWorkAtHomeRates.LoadData();
                LoadExternalJobsRates.LoadData();
                ExternalRates = LoadExternalWorkerRates.GiveData();
                WorkAtHomeRates = LoadWorkAtHomeRates.GiveData();
                ExternalRates = LoadExternalJobsRates.GiveData();
            }
            var flatProduction = production.GetFlatData();
            var flatWah = new float[flatProduction.Length];
            var flatIntraZonal = new float[flatProduction.Length];
            float elfgta = CalculateElfGTA();
            var totalProduction = ComputeProduction( flatProduction, flatWah, flatIntraZonal );
            var totalAttraction = ComputeAttraction( attractions.GetFlatData() );

            ApplyWahAndIntrazonal( production.GetFlatData(), attractions.GetFlatData(), flatWah, flatIntraZonal, totalProduction, totalAttraction, elfgta );
            ApplyAgeCategoryFactor( production.GetFlatData(), attractions.GetFlatData() );
            Normalize( production.GetFlatData(), attractions.GetFlatData() );
            WriteGenerationFile( flatProduction.Sum(), attractions.GetFlatData().Sum() );
            WriteAttractionFile( production, attractions );
            if ( LoadData )
            {
                LoadExternalWorkerRates.UnloadData();
                LoadWorkAtHomeRates.UnloadData();
                LoadExternalJobsRates.UnloadData();
                WorkAtHomeRates = null;
                ExternalRates = null;
                ExternalJobs = null;
            }
        }

        public override void InitializeDemographicCategory()
        {
            // first learn what demographic category we should be in
            DemographicParameterSetIndex = GetDemographicIndex( AgeCategoryRange[0].Start,
                EmploymentStatusCategory[0].Start, Mobility[0].Start );
            // now we can generate
            base.InitializeDemographicCategory();
        }

        public override string ToString()
        {
            return String.Concat( "PoRPoW:Age='", AgeCategoryRange, "'Mob='", Mobility, "'Occ='",
                OccupationCategory, "'Emp='", EmploymentStatusCategory, "'" );
        }

        private void ApplyAgeCategoryFactor(float[] productions, float[] attractions)
        {
            var flatPopulation = Root.Population.Population.GetFlatData();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var internalWorkers = new float[zones.Length];
            var empRates = Root.Demographics.EmploymentStatusRates.GetFlatData();
            var occRates = Root.Demographics.OccupationRates.GetFlatData();
            var ageRates = Root.Demographics.AgeRates;
            // we know that in V2 there is only 1 age category per generation
            var age = AgeCategoryRange[0].Start;
            for ( int i = 0; i < zones.Length; i++ )
            {
                if ( productions[i] <= 0 )
                {
                    continue;
                }
                var emp = empRates[i];
                var occRate = occRates[i];
                var occ = OccupationCategory[0].Start;
                var occData = Root.Demographics.OccupationRates[zones[i].ZoneNumber];
                var empData = Root.Demographics.EmploymentStatusRates[zones[i].ZoneNumber];
                if ( occData == null | empData == null ) continue;
                // calculate all of the people fitting this category, before removing the ones that work externally
                float factor = 0f;
                foreach ( var ageSet in AllAges )
                {
                    for ( int j = ageSet.Start; j <= ageSet.Stop; j++ )
                    {
                        factor += ageRates[zones[i].ZoneNumber, j] * emp[j, 1] * occRate[j, 1, occ];
                    }
                }
                if ( factor <= 0 )
                {
                    productions[i] = 0;
                    continue;
                }
                // apply the age rate only relative to the other working ages
                productions[i] = productions[i] * ( ( ageRates[zones[i].ZoneNumber, age] * emp[age, 1] * occRate[age, 1, occ] ) / factor );
            }
        }

        private void ApplyWahAndIntrazonal(float[] flatProduction, float[] flatAttraction, float[] wah, float[] intrazonal, float totalProduction, float totalAttraction, float elfgta)
        {
            // Normalize the attractions
            var avggta = ( ( elfgta + totalAttraction ) / 2.0f );
            float attractionBalanceFactor = avggta / totalAttraction;
            float productionBalanceFactor = avggta / elfgta;
            // apply the ratio
            for ( int i = 0; i < flatAttraction.Length; i++ )
            {
                flatAttraction[i] *= attractionBalanceFactor;
                flatProduction[i] *= productionBalanceFactor;
                wah[i] *= productionBalanceFactor;
                intrazonal[i] *= productionBalanceFactor;
            }

            for ( int i = 0; i < flatAttraction.Length; i++ )
            {
                flatProduction[i] -= wah[i] + intrazonal[i];
                flatAttraction[i] -= wah[i] + intrazonal[i];
                if ( flatProduction[i] < 0 )
                {
                    flatProduction[i] = 0;
                }
                if ( flatAttraction[i] < 0 )
                {
                    flatAttraction[i] = 0;
                }
            }

            WorkAtHomeTotal = wah.Sum();
            WorkIntrazonalTotal = intrazonal.Sum();
        }

        private float CalculateElfGTA()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            var internalWorkers = new float[zones.Length];
            var ageRates = Root.Demographics.AgeRates;
            for ( int i = 0; i < zones.Length; i++ )
            {
                var occ = OccupationCategory[0].Start;
                var emp = 1;
                int planningDistrict = zones[i].PlanningDistrict;
                int zoneNumber = zones[i].ZoneNumber;
                var occData = Root.Demographics.OccupationRates[zoneNumber];
                var empData = Root.Demographics.EmploymentStatusRates[zoneNumber];
                if ( occData == null | empData == null ) continue;
                foreach ( var agesset in AllAges )
                {
                    for ( int age = agesset.Start; age <= agesset.Stop; age++ )
                    {
                        var employmentFactor = occData[age, emp, occ] * empData[age, emp];
                        
                        internalWorkers[i] += zones[i].Population * ageRates[zoneNumber, age] * employmentFactor
                            * ( 1 - ExternalRates[planningDistrict, age, emp] );
                    }
                }
            }

            return internalWorkers.Sum();
        }

        private float ComputeAttraction(float[] flatAttraction)
        {
            IZone[] zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            int numberOfZones = zones.Length;
            var demographics = Root.Demographics;
            var flatEmploymentRates = demographics.JobOccupationRates.GetFlatData();
            var flatJobTypes = demographics.JobTypeRates.GetFlatData();
            foreach ( var empRange in EmploymentStatusCategory )
            {
                for ( int emp = empRange.Start; emp <= empRange.Stop; emp++ )
                {
                    foreach ( var occRange in OccupationCategory )
                    {
                        for ( int occ = occRange.Start; occ <= occRange.Stop; occ++ )
                        {
                            for ( int i = 0; i < numberOfZones; i++ )
                            {
                                int pd = zones[i].PlanningDistrict;
                                if ( pd > 0 )
                                {
                                    var temp = flatEmploymentRates[i][emp][occ];
                                    temp *= flatJobTypes[i][emp];
                                    temp *= zones[i].Employment;
                                    temp *= ( 1f - ExternalJobs[zones[i].PlanningDistrict, emp, occ] );
                                    flatAttraction[i] += temp;
                                    if ( flatAttraction[i] < 0 )
                                    {
                                        throw new XTMFRuntimeException( "Zone " + zones[i].ZoneNumber + " had a negative attraction after computing the initial attraction. " + flatAttraction[i]
                                            + "\r\nOccupation Rates = " + flatEmploymentRates[i][emp][occ]
                                            + "\r\nEmplyoment Rates = " + flatJobTypes[i][emp]
                                            + "\r\nEmployment       = " + zones[i].Employment
                                            + "\r\nExt Employment   = " +
                                        ( 1f - ExternalJobs[zones[i].PlanningDistrict, emp, occ] ) );
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return flatAttraction.Sum();
        }

        private float ComputeProduction(float[] flatProduction, float[] flatWah, float[] flatIntrazonal)
        {
            int numberOfZones = flatProduction.Length;
            var flatPopulation = Root.Population.Population.GetFlatData();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            WorkAtHomeTotal = 0;
            var internalWorkers = new float[zones.Length];
            var ageRates = Root.Demographics.AgeRates;
            for ( int i = 0; i < zones.Length; i++ )
            {
                float tempWaH = 0f;
                float tempIntrazonal = 0f;
                var occ = OccupationCategory[0].Start;
                var emp = 1;
                var occData = Root.Demographics.OccupationRates[zones[i].ZoneNumber];
                var empData = Root.Demographics.EmploymentStatusRates[zones[i].ZoneNumber];
                if ( occData == null | empData == null ) continue;
                foreach ( var agesset in AllAges )
                {
                    for ( int age = agesset.Start; age <= agesset.Stop; age++ )
                    {
                        var employmentFactor = occData[age, emp, occ] * empData[age, emp];
                        // calculate all of the people fitting this category, before removing the ones that work externally
                        var containedPeople = zones[i].Population * ageRates[zones[i].ZoneNumber, age] * employmentFactor;
                        tempIntrazonal += containedPeople * WorkIntrazonal[zones[i].PlanningDistrict, occ, age];
                        tempWaH += containedPeople * WorkAtHomeRates[zones[i].PlanningDistrict, age, emp];
                        // now removce out the external workers
                        containedPeople *= ( 1 - ExternalRates[zones[i].PlanningDistrict, age, emp] );
                        flatProduction[i] += containedPeople;
                    }
                }
                flatWah[i] = tempWaH;
                flatIntrazonal[i] = tempIntrazonal;
            }

            // Sum everything
            float workAtHomeSum = 0,
                workIntrazonalSum = 0,
                productionSum = 0;

            for ( int i = 0; i < flatWah.Length; i++ )
            {
                workAtHomeSum += flatWah[i];
                workIntrazonalSum += flatIntrazonal[i];
                productionSum += flatProduction[i];
            }

            WorkAtHomeTotal = workAtHomeSum;
            WorkIntrazonalTotal = workIntrazonalSum;
            return productionSum;
        }

        private int GetDemographicIndex(int age, int employmentStatus, int mobility)
        {
            return ( age - 2 ) * 5 + mobility;
        }

        private void Normalize(float[] production, float[] attraction)
        {
            var factor = production.Sum() / attraction.Sum();
            for ( int i = 0; i < attraction.Length; i++ )
            {
                attraction[i] *= factor;
            }
        }

        private void WriteAttractionFile(SparseArray<float> productions, SparseArray<float> attractions)
        {
            if ( !AttractionFileName.ContainsFileName() )
            {
                return;
            }
            var flatAttractions = attractions.GetFlatData();
            var flatProduction = productions.GetFlatData();
            bool first = !File.Exists( AttractionFileName.GetFileName() );
            StringBuilder buildInside = new StringBuilder();
            buildInside.Append( ',' );
            buildInside.Append( AgeCategoryRange.ToString() );
            buildInside.Append( ',' );
            buildInside.Append( EmploymentStatusCategory.ToString() );
            buildInside.Append( ',' );
            buildInside.Append( OccupationCategory.ToString() );
            buildInside.Append( ',' );
            string categoryData = buildInside.ToString();
            using ( StreamWriter writer = new StreamWriter( AttractionFileName.GetFileName(), true ) )
            {
                if ( first )
                {
                    // if we are the first thing to generate, then write the header as well
                    writer.WriteLine( "Zone,Age,Employment,Occupation,Production,Attraction" );
                }
                for ( int i = 0; i < flatAttractions.Length; i++ )
                {
                    writer.Write( attractions.GetSparseIndex( i ) );
                    writer.Write( categoryData );
                    writer.Write( flatProduction[i] );
                    writer.Write( ',' );
                    writer.WriteLine( flatAttractions[i] );
                }
            }
        }

        private void WriteGenerationFile(float totalProduction, float totalAttraction)
        {
            if ( !String.IsNullOrEmpty( GenerationOutputFileName ) )
            {
                var dir = Path.GetDirectoryName( GenerationOutputFileName );
                DirectoryInfo info = new DirectoryInfo( dir );
                if ( !info.Exists )
                {
                    info.Create();
                }
                bool first = !File.Exists( GenerationOutputFileName );
                // if the file name exists try to write to it, appending
                using ( StreamWriter writer = new StreamWriter( GenerationOutputFileName, true ) )
                {
                    if ( first )
                    {
                        writer.WriteLine( "Age,Employment,Occupation,Production,Attraction,WAH,IntraZonal" );
                    }
                    writer.Write( AgeCategoryRange.ToString() );
                    writer.Write( ',' );
                    writer.Write( EmploymentStatusCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( OccupationCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( totalProduction );
                    writer.Write( ',' );
                    writer.Write( totalAttraction );
                    writer.Write( ',' );
                    writer.Write( WorkAtHomeTotal );
                    writer.Write( ',' );
                    writer.WriteLine( WorkIntrazonalTotal );
                }
            }
        }
    }
}