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

namespace TMG.GTAModel
{
    public sealed class PowGeneration : DemographicCategoryGeneration
    {
        [SubModelInformation( Description = "Used to gather the daily generation rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadDailyRates;

        [SubModelInformation( Description = "Used to gather the period generation rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadTimeOfDayRates;

        [RunParameter( "Planning Districts", true, "Is the data using planning districts?" )]
        public bool UsesPlanningDistricts;

        internal SparseTriIndex<float> DailyRates;

        internal bool LoadData = true;
        internal SparseTriIndex<float> TimeOfDayRates;

        [SubModelInformation( Required = true, Description = "A resource used for storing the results of PoRPoW generation [zone][empStat,Mobility,ageCategory]" )]
        public IResource WorkerData;

        override public void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            if ( LoadData )
            {
                if ( DailyRates == null )
                {
                    this.LoadDailyRates.LoadData();
                    this.DailyRates = this.LoadDailyRates.GiveData();
                }
                if ( TimeOfDayRates == null )
                {
                    this.LoadTimeOfDayRates.LoadData();
                    this.TimeOfDayRates = this.LoadTimeOfDayRates.GiveData();
                }
            }
            var flatProduction = production.GetFlatData();
            var flatAttraction = attractions.GetFlatData();

            var numberOfIndexes = flatAttraction.Length;

            // Compute the Production and Attractions
            ComputeProduction( flatProduction, flatAttraction, numberOfIndexes );

            //We do not normalize the attraction
            if ( LoadData )
            {
                this.LoadDailyRates.UnloadData();
                this.LoadTimeOfDayRates.UnloadData();
                DailyRates = null;
                TimeOfDayRates = null;
            }
        }

        private void ComputeProduction(float[] flatProduction, float[] flatAttraction, int numberOfZones)
        {
            var flatPopulation = this.Root.Population.Population.GetFlatData();
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            int emp = this.EmploymentStatusCategory[0].Start;
            var rateEmp = emp;
            var mob = this.Mobility[0].Start;
            var age = this.AgeCategoryRange[0].Start;
            var occ = this.OccupationCategory[0].Start;
            var workerData = this.WorkerData.AcquireResource<SparseArray<SparseTriIndex<float>>>().GetFlatData();
            var test = workerData[0];
            if ( !test.GetFlatIndex( ref emp, ref mob, ref age ) )
            {
                throw new XTMFRuntimeException( "In " + this.Name + " we were unable to find a place to store our data (" + emp + "," + mob + "," + age + ")" );
            }
            for ( int i = 0; i < flatProduction.Length; i++ )
            {
                var population = zones[i].Population;
                var zoneNumber = this.UsesPlanningDistricts ? zones[i].PlanningDistrict : zones[i].ZoneNumber;
                // production is the generation rate
                if ( population <= 0 | zones[i].RegionNumber == 0 )
                {
                    flatProduction[i] = 0;
                    flatAttraction[i] = 0;
                    continue;
                }
                var workersOfCatInZone = workerData[i].GetFlatData()[emp][mob][age];
                flatProduction[i] = workersOfCatInZone * this.DailyRates[zoneNumber, rateEmp, occ] * this.TimeOfDayRates[zoneNumber, rateEmp, occ];
                flatAttraction[i] = workersOfCatInZone;
            }
        }
    }
}