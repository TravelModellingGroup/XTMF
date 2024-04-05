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

using System.Collections.Generic;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Generation;

[ModuleInformation( Description
    = "This module is designed to at runtime generate a full set of TMG.GTAModel.Generation.PoRPoWGeneration and pre-load their data."
    + "  This class will then remove itself from the demographic category list."
    + "  Demographic indexes are based on the Durham Model." )]
// ReSharper disable once InconsistentNaming
public class BuildPoRPoWGeneration : DemographicCategoryGeneration
{
    [RunParameter( "All Ages", "2-6", typeof( RangeSet ), "All of the working ages in the model system." )]
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

    [RunParameter( "Min Attraction", 0f, "The minimum amount of attraction." )]
    public float MinAttraction;

    [ParentModel]
    public IDemographicCategoyPurpose Parent;

    [RunParameter( "Planning Districts", true, "The data for worker rates are using planning districts instead of zones." )]
    public bool UsePlanningDistricts;

    [SubModelInformation( Required = true, Description = "A resource used for storing the results of PoRPoW generation [zone][empStat,Mobility,ageCategory]" )]
    public IResource WorkerData;

    private SparseTriIndex<float> ExternalJobs;
    private SparseTriIndex<float> WorkAtHomeRates;
    private SparseTriIndex<float> WorkExternal;

    public override void Generate(SparseArray<float> production, SparseArray<float> attractions)
    {
    }

    public override bool RuntimeValidation(ref string error)
    {
        if ( !WorkerData.CheckResourceType<SparseArray<SparseTriIndex<float>>>() )
        {
            error = "In " + Name + " the worker data is not of type SparseArray<SparseTriIndex<float>>!";
            return false;
        }
        LoadData();
        GenerateChildren();
        return true;
    }

    private void AddNewGeneration(List<IDemographicCategoryGeneration> list, int occ, Range age, int employmentStatus, int mobility)
    {
        PoRPoWGeneration gen = new()
        {
            Root = Root,
            LoadData = false,
            UsePlanningDistricts = UsePlanningDistricts,
            OccupationCategory = CreateRangeSet(occ),
            AgeCategoryRange = CreateRangeSet(age),
            EmploymentStatusCategory = CreateRangeSet(employmentStatus),
            Mobility = CreateRangeSet(mobility),
            ModeChoiceParameterSetIndex = ModeChoiceParameterSetIndex,
            DemographicParameterSetIndex = GetDemographicIndex(age.Start, employmentStatus, mobility),
            ExternalJobs = ExternalJobs,
            ExternalRates = WorkExternal,
            WorkAtHomeRates = WorkAtHomeRates,
            GenerationOutputFileName = GenerationOutputFileName
        };
        gen.Name = Name + " - " + gen;
        gen.AttractionFileName = AttractionFileName;
        gen.AllAges = AllAges;
        gen.WorkerData = WorkerData;
        list.Add( gen );
    }

    private int ChildAgeIndex(int mobility)
    {
        switch ( mobility )
        {
            case 0:
            case 3:
                return 0;
            case 1:
            case 2:
                return 1;
            default:
                return 2;
        }
    }

    private RangeSet CreateRangeSet(int occ)
    {
        var set = new List<Range>
        {
            new(occ, occ)
        };
        return new RangeSet( set );
    }

    private RangeSet CreateRangeSet(Range range)
    {
        var set = new List<Range>
        {
            range
        };
        return new RangeSet( set );
    }

    private void GenerateChildren()
    {
        // we need to generate our children here
        var list = Parent.Categories;
        list.Remove( this );
        foreach ( var occSet in OccupationCategory )
        {
            for ( int occ = occSet.Start; occ <= occSet.Stop; occ++ )
            {
                foreach ( var empSet in EmploymentStatusCategory )
                {
                    for ( int employmentStatus = empSet.Start; employmentStatus <= empSet.Stop; employmentStatus++ )
                    {
                        foreach ( var mobilitySet in Mobility )
                        {
                            for ( int mobility = mobilitySet.Start; mobility <= mobilitySet.Stop; mobility++ )
                            {
                                foreach ( var ageSet in AgeCategoryRange )
                                {
                                    AddNewGeneration( list, occ, ageSet, employmentStatus, mobility );
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private int GetDemographicIndex(int age, int employmentStatus, int mobility)
    {
        switch ( age )
        {
            case 0:
            case 1:
                {
                    return ChildAgeIndex( mobility );
                }
            case 2:
                {
                    int empOffset;
                    switch ( employmentStatus )
                    {
                        case 0:
                        case 2:
                            empOffset = 0;
                            break;

                        default:
                            empOffset = 3;
                            break;
                    }
                    return ChildAgeIndex( mobility ) + 3 + empOffset;
                }
            default:
                int ageOffset = ( ( age < 6 ? age : 6 ) - 3 ) * 12;
                int employmentOffset = ( employmentStatus == 1 ? 3 : 0 );
                int mobilityOffset = ( mobility < 3 ? mobility : ( mobility - 3 ) + 6 );
                return ageOffset + mobilityOffset + employmentOffset + 9;
        }
    }

    private void LoadData()
    {
        LoadWorkAtHomeRates.LoadData();
        LoadExternalWorkerRates.LoadData();
        LoadExternalJobsRates.LoadData();
        WorkExternal = LoadExternalWorkerRates.GiveData();
        WorkAtHomeRates = LoadWorkAtHomeRates.GiveData();
        ExternalJobs = LoadExternalJobsRates.GiveData();
        LoadWorkAtHomeRates.UnloadData();
        LoadExternalWorkerRates.UnloadData();
        LoadExternalJobsRates.UnloadData();
    }
}