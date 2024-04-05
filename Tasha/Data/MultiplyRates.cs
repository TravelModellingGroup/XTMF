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
using TMG;
using XTMF;
using Datastructure;
namespace Tasha.Data;

[ModuleInformation(Description =
    @"This module is designed to multiply two rates together for each zone.")]
public class MultiplyRatesForZones : IDataSource<SparseArray<float>>
{
    private SparseArray<float> Data;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The rates to use for each planning district.")]
    public IResource FirstRateToApply;

    [SubModelInformation(Required = true, Description = "The rates to use for each planning district.")]
    public IResource SecondRateToApply;

    [RunParameter("First Rate PD", true, "Are the rates based on planning districts (true) or zones (false).")]
    public bool FirstRateBasedOnPD;

    [RunParameter("Second Rate PD", true, "Are the rates based on planning districts (true) or zones (false).")]
    public bool SecondRateBasedOnPD;

    [RunParameter("Save by PD", true, "Should we save our combined rate by PD?  If true then all rates are treated as if by PD!")]
    public bool SaveRatesBasedOnPD;

    public SparseArray<float> GiveData()
    {
        return Data;
    }

    public bool Loaded
    {
        get { return Data != null; }
    }

    public void LoadData()
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var zones = zoneArray.GetFlatData();
        var firstRate = FirstRateToApply.AcquireResource<SparseArray<float>>();
        var secondRate = SecondRateToApply.AcquireResource<SparseArray<float>>();
        SparseArray<float> data;
        if(SaveRatesBasedOnPD)
        {
            data = TMG.Functions.ZoneSystemHelper.CreatePdArray<float>(zoneArray);
            var pds = data.ValidIndexArray();
            for(int i = 0; i < pds.Length; i++)
            {
                var pd = pds[i];
                data[pd] = firstRate[pd] * secondRate[pd];
            }
        }
        else
        {
            // then we are outputting by zone
            data = zoneArray.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            for(int i = 0; i < flatData.Length; i++)
            {
                var pd = zones[i].PlanningDistrict;
                var zone = zones[i].ZoneNumber;
                flatData[i] = firstRate[FirstRateBasedOnPD ? pd : zone] * secondRate[SecondRateBasedOnPD ? pd : zone];
            }
        }
        Data = data;
    }

    public void UnloadData()
    {
        Data = null;
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool RuntimeValidation(ref string error)
    {
        if(!FirstRateToApply.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the first rates resource is not of type SparseArray<float>!";
            return false;
        }
        if(!SecondRateToApply.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the second rate resource is not of type SparseArray<float>!";
            return false;
        }
        if(SaveRatesBasedOnPD & !FirstRateBasedOnPD)
        {
            error = "In '" + Name + "', if you save rates by PD the input rates must be read in by PD.  The first rate is being read in by zone!";
            return false;
        }
        if(SaveRatesBasedOnPD & !SecondRateBasedOnPD)
        {
            error = "In '" + Name + "', if you save rates by PD the input rates must be read in by PD.  The second rate is being read in by zone";
            return false;
        }
        return true;
    }
}
