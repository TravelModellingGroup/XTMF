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
using System.IO;
using System.Linq;
using Datastructure;
using TMG.Functions;
using XTMF;

namespace TMG.GTAModel.V2.Distribution;

public class V2NWSDistribution : IDemographicDistribution
{
    [SubModelInformation( Description = "Base year zonal observations.", Required = true )]
    public IDataSource<SparseTwinIndex<float>> BaseYearObservations;

    [RunParameter( "Maximim Error", 0.005f, "The maximum error for each zone allowed, where 0.01 would be 1%." )]
    public float MaximumError;

    [RunParameter( "Max Iterations", 10, "The maximum number of iterations to run for.  This will cut off the calculation even if the maximum error has not been reached." )]
    public int MaxIterations;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter( "Save Location", "", "The location to save the data to, relative to the run directory.  Leave this blank to not save." )]
    public string SaveDistribution;

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

    public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
    {
        // This computation ignores all of the productions and attractions
        var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        BaseYearObservations.LoadData();
        var o = new float[zones.Length];
        var d = new float[zones.Length];
        ApplyBaseRates( zones, o, d );
        // make O and D equal to the same thing, averaged in the middle of the two
        Balance( o, d );
        Fratar.Run( ret.GetFlatData(), o, d, BaseYearObservations.GiveData().GetFlatData(), MaximumError, MaxIterations );
        BaseYearObservations.UnloadData();
        if ( !String.IsNullOrWhiteSpace( SaveDistribution ) )
        {
            SaveData.SaveMatrix( ret, Path.Combine( SaveDistribution, "NWSDistribution.csv" ) );
        }
        yield return ret;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private static void ApplyBaseRates(IZone[] zones, float[] o, float[] d)
    {
        for ( int i = 0; i < zones.Length; i++ )
        {
            var pd = zones[i].PlanningDistrict;
            if ( pd <= 0 ) continue;
            if ( pd < 17 )
            {
                o[i] = 69.634f + 0.054551f * zones[i].Population + 0.012107f * zones[i].TotalEmployment;
                d[i] = 117.10f + 0.032995f * zones[i].Population + 0.043112f * zones[i].TotalEmployment;
            }
            else if ( pd > 40 )
            {
                o[i] = 0.065295f * zones[i].Population + 0.037936f * zones[i].TotalEmployment;
                d[i] = 34.204f + 0.047985f * zones[i].Population + 0.040603f * zones[i].TotalEmployment;
            }
            else
            {
                o[i] = 0.078684f * zones[i].Population + 0.033756f * zones[i].TotalEmployment;
                d[i] = 13.457f + 0.058827f * zones[i].Population + 0.053694f * zones[i].TotalEmployment;
            }
        }
    }

    private static void Balance(float[] baseO, float[] baseD)
    {
        var sumOfO = baseO.Sum();
        var sumOfD = baseD.Sum();
        var average = ( sumOfO + sumOfD ) / 2f;
        var oFactor = average / sumOfO;
        var dFactor = average / sumOfD;
        for ( int i = 0; i < baseO.Length; i++ )
        {
            baseO[i] *= oFactor;
            baseD[i] *= dFactor;
        }
    }
}