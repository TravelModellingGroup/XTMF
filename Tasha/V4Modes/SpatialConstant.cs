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
using XTMF;
using Datastructure;
using TMG.Functions;
using TMG;

namespace Tasha.V4Modes;

[ModuleInformation(Description = "This module provides the ability to apply constants to a utility function depending on the trip origin and destinations of the trip.")]
public sealed class SpatialConstant : IModule
{
    [RunParameter("Origins", "", typeof(RangeSet), "The rangeset for the accepted origins")]
    public RangeSet Origins;

    [RunParameter("Destinations", "", typeof(RangeSet), "The rangeset for the accepted destinations")]
    public RangeSet Destinations;

    [RunParameter("Constant", 0f, "The constant to apply if the if is within these origins.")]
    public float Constant;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}

[ModuleInformation(Description = "This module provides the ability to apply constants to the utility function of modes depending on the time of day, origin, and destination for the trip.")]
public sealed class TimePeriodSpatialConstant : IModule
{
    [RootModule]
    public ITravelDemandModel Root;

    public SpatialConstant[] SpatialConstants;

    private SparseTwinIndex<float> PlanningDistrictConstants;

    [RunParameter("Start Time", "6:00AM", typeof(Time), "The start time for this time period, inclusive.", Index = 0)]
    public Time StartTime;

    [RunParameter("End Time", "9:00AM", typeof(Time), "The end time for this time period, exclusive.", Index = 1)]
    public Time EndTime;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool RuntimeValidation(ref string error)
    {
        if(StartTime >= EndTime)
        {
            error = "In '" + Name + "' the Start Time is greater than or the same to the end time!";
            return false;
        }
        return true;
    }

    public void BuildMatrix()
    {
        //build the region constants
        var planningDistricts = ZoneSystemHelper.CreatePdArray<float>(Root.ZoneSystem.ZoneArray);
        var pdIndexes = planningDistricts.ValidIndexArray();
        PlanningDistrictConstants = planningDistricts.CreateSquareTwinArray<float>();
        var data = PlanningDistrictConstants.GetFlatData();
        for(int i = 0; i < data.Length; i++)
        {
            for(int j = 0; j < data[i].Length; j++)
            {
                data[i][j] = GetPDConstant(pdIndexes[i], pdIndexes[j]);
            }
        }
    }

    private float GetPDConstant(int originRegion, int destinationRegion)
    {
        for(int i = 0; i < SpatialConstants.Length; i++)
        {
            if(SpatialConstants[i].Origins.Contains(originRegion) && SpatialConstants[i].Destinations.Contains(destinationRegion))
            {
                return SpatialConstants[i].Constant;
            }
        }
        return 0f;
    }

    public float GetConstant(int pdO, int pdD)
    {
        return PlanningDistrictConstants[pdO, pdD];
    }
}
