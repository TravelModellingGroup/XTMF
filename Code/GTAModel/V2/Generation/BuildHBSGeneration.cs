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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Generation;

[ModuleInformation( Description
    = "This module is designed to at runtime generate a full set of TMG.GTAModel.V2.PoWGeneration and preload their data."
    + "  This class will then remove itself from the demographic category list."
    + "  Demographic indexes are based on the Durham Model." )]
public class BuildHBSGeneration : IDemographicCategoryGeneration
{
    [Parameter( "Ages", "1-6", typeof( RangeSet ), "The age category that this generation will be generating for." )]
    public RangeSet Ages;

    [SubModelInformation( Description = "Get the 24-hour trips rates for students, by zone [0], age [1], and employment status [2]. Employment status is assumed to be 0.", Required = true )]
    public IDataSource<SparseTriIndex<float>> LoadDailyRates;

    [SubModelInformation( Description = "Get the peak hour trips rates for students, by zone [0], age [1], and employment status [2]. Employment status is assumed to be 0.", Required = true )]
    public IDataSource<SparseTriIndex<float>> LoadTimeOfDayRates;

    [ParentModel]
    public IDemographicCategoyPurpose Parent;

    [RootModule]
    public IDemographic4StepModelSystemTemplate Root;

    [RunParameter( "Save Production To File", "", typeof( FileFromOutputDirectory ), "Leave this blank to not save, otherwise enter in the file name." )]
    public FileFromOutputDirectory SaveProduction;

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

    public void Generate(SparseArray<float> production, SparseArray<float> attractions)
    {
        // never gets called
        throw new XTMFRuntimeException(this, "For '" + Name + "' this generate method should never be called!" );
    }

    public void InitializeDemographicCategory()
    {
        // do nothing
    }

    public bool IsContained(IPerson person)
    {
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        LoadDailyRates.LoadData();
        LoadTimeOfDayRates.LoadData();
        GenerateChildren();
        LoadDailyRates.UnloadData();
        LoadTimeOfDayRates.UnloadData();
        return true;
    }

    private void AddNewGeneration(List<IDemographicCategoryGeneration> list, int age)
    {
        SchoolGeneration gen = new()
        {
            Root = Root,
            Age = age,
            SaveProduction = SaveProduction,
            StudentDailyRates = LoadDailyRates.GiveData(),
            StudentTimeOfDayRates = LoadTimeOfDayRates.GiveData()
        };
        list.Add( gen );
    }

    private void GenerateChildren()
    {
        // we need to generate our children here
        var list = Parent.Categories;
        list.Remove( this );
        foreach ( var set in Ages )
        {
            for ( int age = set.Start; age <= set.Stop; age++ )
            {
                AddNewGeneration( list, age );
            }
        }
    }
}