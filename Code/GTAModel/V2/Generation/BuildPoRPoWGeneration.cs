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
using System.Collections.Generic;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Generation
{
    [ModuleInformation(Description
        = "This module is designed to at runtime generate a full set of TMG.GTAModel.V2.Generation.PoRPoWGeneration and preload their data."
        + "  This class will then remove itself from the demographic category list."
        + "  Demographic indexes are based on the Durham Model."
        + "  This model is developed to implement the City of Toronto model where mobility is generated internally.")]
    public class BuildPoRPoWGeneration : DemographicCategoryGeneration
    {
        [RunParameter("Attraction File Name", "", typeof(FileFromOutputDirectory), "The name of the file to save the attractions per zone and demographic category to.  Leave blank to not save.")]
        public FileFromOutputDirectory AttractionFileName;

        [RunParameter("Generation FileName", "", "The name of the file to save to, this will append the file. Leave blank to not save.")]
        public string GenerationOutputFileName;

        [SubModelInformation(Description = "Used to gather the rates of jobs taken by external workers", Required = true)]
        public IDataSource<SparseTriIndex<float>> LoadExternalJobsRates;

        [SubModelInformation(Description = "Used to gather the external worker rates", Required = true)]
        public IDataSource<SparseTriIndex<float>> LoadExternalWorkerRates;

        [SubModelInformation(Description = "Used to gather the work at home rates", Required = true)]
        public IDataSource<SparseTriIndex<float>> LoadWorkAtHomeRates;

        [SubModelInformation(Description = "Used to gather the work intra zonal", Required = true)]
        public IDataSource<SparseTriIndex<float>> LoadWorkIntraZonalRates;

        [RunParameter("Min Attraction", 0f, "The minimum amount of attraction.")]
        public float MinAttraction;

        [ParentModel]
        public IDemographicCategoyPurpose Parent;

        private SparseTriIndex<float> ExternalJobs;
        private SparseTriIndex<float> WorkAtHomeRates;
        private SparseTriIndex<float> WorkExternal;
        private SparseTriIndex<float> WorkIntraZonal;

        public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
        }

        public override bool RuntimeValidation(ref string error)
        {
            LoadData();
            GenerateChildren();
            return true;
        }

        private void AddNewGeneration(List<IDemographicCategoryGeneration> list, int occ, Range age, int employmentStatus)
        {
            var gen = new PoRPoWGeneration
            {
                Root = Root,
                LoadData = false,
                OccupationCategory = CreateRangeSet(occ),
                AgeCategoryRange = CreateRangeSet(age),
                EmploymentStatusCategory = CreateRangeSet(employmentStatus),
                ModeChoiceParameterSetIndex = ModeChoiceParameterSetIndex,
                Mobility = new RangeSet(new List<Range>() { new Range(0, 0) }),
                ExternalJobs = ExternalJobs,
                ExternalRates = WorkExternal,
                WorkIntrazonal = WorkIntraZonal,
                WorkAtHomeRates = WorkAtHomeRates,
                AllAges = new RangeSet(new List<Range>() { new Range(2, 5) }),
                GenerationOutputFileName = GenerationOutputFileName
            };
            gen.Name = Name + " - " + gen.ToString();
            gen.AttractionFileName = AttractionFileName;
            list.Add(gen);
        }

        private int ChildAgeIndex(int mobility)
        {
            switch (mobility)
            {
                case 0:
                    return 0;

                case 1:
                case 3:
                    return 1;

                case 2:
                case 4:
                default:
                    return 2;
            }
        }

        private RangeSet CreateRangeSet(int occ) => new RangeSet(new List<Range> { new Range(occ, occ) });

        private RangeSet CreateRangeSet(Range range) => new RangeSet(new List<Range> { range });

        private void GenerateChildren()
        {
            // we need to generate our children here
            var list = Parent.Categories;
            list.Remove(this);
            foreach (var occSet in OccupationCategory)
            {
                for (var occ = occSet.Start; occ <= occSet.Stop; occ++)
                {
                    foreach (var empSet in EmploymentStatusCategory)
                    {
                        for (var employmentStatus = empSet.Start; employmentStatus <= empSet.Stop; employmentStatus++)
                        {
                            foreach (var ageSet in AgeCategoryRange)
                            {
                                AddNewGeneration(list, occ, ageSet, employmentStatus);
                            }
                        }
                    }
                }
            }
        }

        private int GetDemographicIndex(int age, int employmentStatus, int mobility)
        {
            switch (age)
            {
                case 0:
                case 1:
                    {
                        return ChildAgeIndex(mobility);
                    }
                case 2:
                    {
                        int empOffset;
                        switch (employmentStatus)
                        {
                            case 0:
                            case 2:
                                empOffset = 0;
                                break;

                            case 1:
                            default:
                                empOffset = 3;
                                break;
                        }
                        return ChildAgeIndex(mobility) + 3 + empOffset;
                    }
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                default:
                    int ageOffset = ((age < 6 ? age : 6) - 3) * 12;
                    int employmentOffset = (employmentStatus == 1 ? 3 : 0);
                    int mobilityOffset = (mobility < 3 ? mobility : (mobility - 3) + 7);
                    return ageOffset + mobilityOffset + employmentOffset + 9;
            }
        }

        private void LoadData()
        {
            LoadWorkAtHomeRates.LoadData();
            LoadExternalWorkerRates.LoadData();
            LoadExternalJobsRates.LoadData();
            LoadWorkIntraZonalRates.LoadData();
            WorkExternal = LoadExternalWorkerRates.GiveData();
            WorkAtHomeRates = LoadWorkAtHomeRates.GiveData();
            ExternalJobs = LoadExternalJobsRates.GiveData();
            WorkIntraZonal = LoadWorkIntraZonalRates.GiveData();
            LoadWorkAtHomeRates.UnloadData();
            LoadExternalWorkerRates.UnloadData();
            LoadExternalJobsRates.UnloadData();
            LoadWorkIntraZonalRates.UnloadData();
        }
    }
}