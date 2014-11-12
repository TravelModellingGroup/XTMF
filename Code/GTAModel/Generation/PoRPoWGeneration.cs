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
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Generation
{
    public class PoRPoWGeneration : DemographicCategoryGeneration
    {
        [RunParameter( "All Ages", "2-8", typeof( RangeSet ), "All of the working ages in the model system." )]
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

        [RunParameter( "Planning Districts", true, "The data for worker rates are using planning districts instead of zones." )]
        public bool UsePlanningDistricts;

        [SubModelInformation( Required = true, Description = "A resource used for storing the results of PoRPoW generation [zone][empStat,Mobility,ageCategory]" )]
        public IResource WorkerData;

        internal SparseTriIndex<float> ExternalJobs;
        internal SparseTriIndex<float> ExternalRates;
        internal bool LoadData = true;
        internal SparseTriIndex<float> WorkAtHomeRates;
        private float WorkAtHomeTotal;

        public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            if ( LoadData )
            {
                this.LoadExternalWorkerRates.LoadData();
                this.LoadWorkAtHomeRates.LoadData();
                this.LoadExternalJobsRates.LoadData();
                this.ExternalRates = this.LoadExternalWorkerRates.GiveData();
                this.WorkAtHomeRates = this.LoadWorkAtHomeRates.GiveData();
                this.ExternalRates = this.LoadExternalJobsRates.GiveData();
            }
            var flatProduction = production.GetFlatData();
            var flatWah = new float[flatProduction.Length];
            var totalProduction = ComputeProduction( flatProduction, flatWah );
            var totalAttraction = ComputeAttraction( attractions.GetFlatData() );

            Normalize( production.GetFlatData(), attractions.GetFlatData(), totalProduction, totalAttraction );
            totalAttraction = RemoveWAHFromAttraction( attractions.GetFlatData(), flatWah );
            StoreProductionData( production );
            WriteGenerationFile( totalProduction, totalAttraction );
            WriteAttractionFile( attractions );
            if ( LoadData )
            {
                this.LoadExternalWorkerRates.UnloadData();
                this.LoadWorkAtHomeRates.UnloadData();
                this.LoadExternalJobsRates.UnloadData();
                WorkAtHomeRates = null;
                ExternalRates = null;
                ExternalJobs = null;
            }
        }

        private void StoreProductionData(SparseArray<float> production)
        {
            var age = this.AgeCategoryRange[0].Start;
            var mob = this.Mobility[0].Start;
            var emp = this.EmploymentStatusCategory[0].Start;
            var data = this.WorkerData.AquireResource<SparseArray<SparseTriIndex<float>>>();
            var flatData = data.GetFlatData();
            var test = flatData[0];
            var flatProduction = production.GetFlatData();
            if ( !test.GetFlatIndex( ref emp, ref mob, ref age ) )
            {
                throw new XTMFRuntimeException( "In " + this.Name + " we were unable to find a place to store our data (" + emp + "," + mob + "," + age + ")" );
            }
            int i = 0;
            try
            {
                for ( ; i < flatProduction.Length; i++ )
                {
                    flatData[i].GetFlatData()[emp][mob][age] = flatProduction[i];
                }
            }
            catch
            {
                throw new XTMFRuntimeException( "Failed Yo!" );
            }
        }

        public override string ToString()
        {
            return String.Concat( "PoRPoW:Age='", this.AgeCategoryRange, "'Mob='", this.Mobility, "'Occ='",
                this.OccupationCategory, "'Emp='", this.EmploymentStatusCategory, "'" );
        }

        private float ApplyMobilityProbability(int emp, int occ, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
        {
            switch ( this.Mobility[0].Start )
            {
                case 0:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 0];
                case 1:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 1];
                case 2:
                    return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 2];
                case 3:
                    return dlicRate[age, emp] * ncars[1, occ, 0];
                case 4:
                    return dlicRate[age, emp] * ncars[1, occ, 1];
                case 5:
                    return dlicRate[age, emp] * ncars[1, occ, 2];
            }
            return 0;
        }

        private float ComputeAttraction(float[] flatAttraction)
        {
            IZone[] zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            int numberOfZones = zones.Length;
            var demographics = this.Root.Demographics;
            var flatEmploymentRates = demographics.JobOccupationRates.GetFlatData();
            var flatJobTypes = demographics.JobTypeRates.GetFlatData();
            foreach ( var empRange in this.EmploymentStatusCategory )
            {
                for ( int emp = empRange.Start; emp <= empRange.Stop; emp++ )
                {
                    foreach ( var occRange in this.OccupationCategory )
                    {
                        for ( int occ = occRange.Start; occ <= occRange.Stop; occ++ )
                        {
                            Parallel.For( 0, numberOfZones, delegate(int i)
                            {
                                int pd = zones[i].PlanningDistrict;
                                var temp = flatEmploymentRates[i][emp][occ];
                                temp *= flatJobTypes[i][emp];
                                temp *= zones[i].Employment;
                                temp *= ( 1f - ( this.UsePlanningDistricts ?
                                    this.ExternalJobs[zones[i].PlanningDistrict, emp, occ]
                                    : this.ExternalJobs[zones[i].ZoneNumber, emp, occ] ) );
                                flatAttraction[i] += temp;
                                if ( flatAttraction[i] < 0 )
                                {
                                    throw new XTMFRuntimeException( "Zone " + zones[i].ZoneNumber + " had a negative attraction after computing the initial attraction. " + flatAttraction[i]
                                        + "\r\nOccupation Rates = " + flatEmploymentRates[i][emp][occ]
                                        + "\r\nEmployment Rates = " + flatJobTypes[i][emp]
                                        + "\r\nEmployment       = " + zones[i].Employment
                                        + "\r\nExt Employment   = " +
                                    ( 1f - ( this.UsePlanningDistricts ?
                                    this.ExternalJobs[zones[i].PlanningDistrict, emp, occ]
                                    : this.ExternalJobs[zones[i].ZoneNumber, emp, occ] ) ) );
                                }
                            } );
                        }
                    }
                }
            }
            return flatAttraction.Sum();
        }

        private float ComputeProduction(float[] flatProduction, float[] flatWah)
        {
            int numberOfZones = flatProduction.Length;
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            this.WorkAtHomeTotal = 0;
            System.Threading.Tasks.Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    if ( zones[i].Population == 0 | zones[i].RegionNumber == 0 ) return;
                    float productionTemp = 0f;
                    var zoneNumber = this.UsePlanningDistricts ? zones[i].PlanningDistrict : zones[i].ZoneNumber;
                    var tempWaH = 0f;
                    var ageRates = this.Root.Demographics.AgeRates;
                    var empRates = this.Root.Demographics.EmploymentStatusRates.GetFlatData()[i];
                    var occRates = this.Root.Demographics.OccupationRates.GetFlatData()[i];
                    var dlicRate = this.Root.Demographics.DriversLicenseRates.GetFlatData()[i];
                    var emp = this.EmploymentStatusCategory[0].Start;
                    if ( emp == 0 )
                    {
                        throw new XTMFRuntimeException( "Trying to get PoRPoW for non-employed people!" );
                    }
                    var occ = this.OccupationCategory[0].Start;
                    var ncars = this.Root.Demographics.WorkerVehicleRates.GetFlatData()[i];
                    foreach ( var aSet in this.AgeCategoryRange )
                    {
                        for ( int age = aSet.Start; age <= aSet.Stop; age++ )
                        {
                            var catFactor = ageRates[zones[i].ZoneNumber, age] * empRates[age, emp] * occRates[age, emp, occ]
                            * ApplyMobilityProbability( emp, occ, ncars, age, dlicRate );
                            var nonExternalWorkers = zones[i].Population * catFactor * ( 1 - this.ExternalRates[zoneNumber, age, emp] );
                            var wah = nonExternalWorkers * WorkAtHomeRates[zoneNumber, age, emp];
                            if ( nonExternalWorkers < wah )
                            {
                                throw new XTMFRuntimeException( "We ended up having a negative for zone " + zones[i].ZoneNumber );
                            }
                            productionTemp += nonExternalWorkers - wah;
                            tempWaH += wah;
                        }
                    }
                    flatProduction[i] = productionTemp;
                    flatWah[i] = tempWaH;
                } );
            this.WorkAtHomeTotal = flatWah.Sum();
            return flatProduction.Sum();
        }

        private void Normalize(float[] flatProduction, float[] flatAttraction, float totalProduction, float totalAttraction)
        {
            // Normalize the attractions
            float productionAttractionRatio;
            if ( totalAttraction != 0 )
            {
                productionAttractionRatio = ( totalProduction + this.WorkAtHomeTotal ) / totalAttraction; // inverse totalAttraction to save on divisions
            }
            else
            {
                productionAttractionRatio = ( totalProduction + this.WorkAtHomeTotal ) / flatProduction.Length;
            }
            // apply the ratio
            for ( int i = 0; i < flatAttraction.Length; i++ )
            {
                flatAttraction[i] *= productionAttractionRatio;
            }
        }

        private float RemoveWAHFromAttraction(float[] attraction, float[] flatWah)
        {
            for ( int i = 0; i < attraction.Length; i++ )
            {
                attraction[i] -= flatWah[i];
            }
            this.WorkAtHomeTotal = flatWah.Sum();
            return attraction.Sum();
        }

        private void WriteAttractionFile(SparseArray<float> attractions)
        {
            if ( !this.AttractionFileName.ContainsFileName() )
            {
                return;
            }
            var flatAttractions = attractions.GetFlatData();
            bool first = !File.Exists( this.AttractionFileName.GetFileName() );
            StringBuilder buildInside = new StringBuilder();
            buildInside.Append( ',' );
            buildInside.Append( this.AgeCategoryRange.ToString() );
            buildInside.Append( ',' );
            buildInside.Append( this.EmploymentStatusCategory.ToString() );
            buildInside.Append( ',' );
            buildInside.Append( this.OccupationCategory.ToString() );
            buildInside.Append( ',' );
            buildInside.Append( this.Mobility.ToString() );
            buildInside.Append( ',' );
            string categoryData = buildInside.ToString();
            using ( StreamWriter writer = new StreamWriter( this.AttractionFileName.GetFileName(), true ) )
            {
                if ( first )
                {
                    // if we are the first thing to generate, then write the header as well
                    writer.WriteLine( "Zone,Age,Employment,Occupation,Mobility,Attraction" );
                }
                for ( int i = 0; i < flatAttractions.Length; i++ )
                {
                    writer.Write( attractions.GetSparseIndex( i ) );
                    writer.Write( categoryData );
                    writer.WriteLine( flatAttractions[i] );
                }
            }
        }

        private void WriteGenerationFile(float totalProduction, float totalAttraction)
        {
            if ( !String.IsNullOrEmpty( this.GenerationOutputFileName ) )
            {
                bool first = !File.Exists( this.GenerationOutputFileName );
                // make sure the directory exists
                var dir = Path.GetDirectoryName( GenerationOutputFileName );
                if ( !String.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
                {
                    Directory.CreateDirectory( dir );
                }
                // if the file name exists try to write to it, appending
                using ( StreamWriter writer = new StreamWriter( this.GenerationOutputFileName, true ) )
                {
                    if ( first )
                    {
                        writer.WriteLine( "Age,Employment,Occupation,Mobility,Production,Attraction,WAH" );
                    }
                    writer.Write( this.AgeCategoryRange.ToString() );
                    writer.Write( ',' );
                    writer.Write( this.EmploymentStatusCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( this.OccupationCategory.ToString() );
                    writer.Write( ',' );
                    writer.Write( this.Mobility.ToString() );
                    writer.Write( ',' );
                    writer.Write( totalProduction );
                    writer.Write( ',' );
                    writer.Write( totalAttraction );
                    writer.Write( ',' );
                    writer.WriteLine( this.WorkAtHomeTotal );
                }
            }
        }
    }
}