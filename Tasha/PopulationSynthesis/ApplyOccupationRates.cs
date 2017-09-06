using System;
using Tasha.Common;
using XTMF;
using TMG;
using System.Threading.Tasks;
using Datastructure;
namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(
        Description =
        @"This module to designed to apply the occupation rates and assign them into the zonal information.")]
    public class ApplyOccupationRates : IPreIteration
    {
        [SubModelInformation(Required = true, Description = "PD,Occupation->Rate of occupation")]
        public IDataSource<SparseTwinIndex<float>> OccupationSplit;

        [SubModelInformation(Required = true, Description = "PD,Occupation->Rate of job occupation")]
        public IDataSource<SparseTwinIndex<float>> EmploymentSplit;

        [RootModule]
        public ITravelDemandModel Root;


        [RunParameter("FirstIerationOnly", "true", typeof(bool), "Should we only split the zonal employment and occupation data once?")]
        public bool FirstTimeOnly;


        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); } }

        public void Execute(int currentIeration, int maxIterations)
        {
            // if we should only run on the first iteration, then do so
            if ( FirstTimeOnly & currentIeration > 0)
            {
                return;
            }
            // If we are in the first iteration, or we don't care, load in our data and split
            var zoneSystem = Root.ZoneSystem.ZoneArray.GetFlatData();
            LoadInData(out SparseTwinIndex<float> occ, out SparseTwinIndex<float> emp );
            // apply the rates
            Parallel.For( 0, zoneSystem.Length, i =>
            {
                var zone = zoneSystem[i];
                var pd = zone.PlanningDistrict;
                var pop = zone.Population;
                var jobs = zone.TotalEmployment;
                zone.WorkProfessional = pop * occ[pd, 1];
                zone.WorkGeneral = pop * occ[pd, 2];
                zone.WorkRetail = pop * occ[pd, 3];
                zone.WorkManufacturing = pop * occ[pd, 4];

                zone.ProfessionalEmployment = jobs * emp[pd, 1];
                zone.GeneralEmployment = jobs * emp[pd, 2];
                zone.RetailEmployment = jobs * emp[pd, 3];
                zone.ManufacturingEmployment = jobs * emp[pd, 4];
            } );
        }

        private void LoadInData(out SparseTwinIndex<float> occ, out SparseTwinIndex<float> emp)
        {
            OccupationSplit.LoadData();
            EmploymentSplit.LoadData();
            occ = OccupationSplit.GiveData();
            emp = EmploymentSplit.GiveData();
            OccupationSplit.UnloadData();
            EmploymentSplit.UnloadData();
        }

        public void Load(int maxIterations)
        {
            // no need to run anything here
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
