/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using Datastructure;
using System;
using XTMF;

namespace TMG.Frameworks.Data.DataTypes;

[ModuleInformation(Description = "Gives a vector containing a selected zone system attribute for each TAZ.")]
public sealed class ZoneSystemVectorFromZoneSystem : IDataSource<SparseArray<float>>
{

    public enum FillData
    {
        ZoneNumber,
        PlanningDistrict,
        Region,
        FlatIndex
    }

    [RunParameter("Fill With", nameof(FillData.PlanningDistrict), typeof(FillData), "The type of data to fill into the zone system vector.")]
    public FillData FillWith;

    [SubModelInformation(Required = true, Description = "The zone system to get the planning districts from.")]
    public IDataSource<IZoneSystem> ZoneSystem;

    private SparseArray<float> _data;

    public SparseArray<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        var wasLoaded = ZoneSystem.Loaded;
        if (!wasLoaded)
        {
            ZoneSystem.LoadData();
        }
        var zones = ZoneSystem.GiveData().ZoneArray;
        var flatZones = zones.GetFlatData();
        var ret = zones.CreateSimilarArray<float>();
        var flatRet = ret.GetFlatData();
        switch (FillWith)
        {
            case FillData.PlanningDistrict:
                {
                    for (int i = 0; i < flatZones.Length; i++)
                    {
                        flatRet[i] = flatZones[i].PlanningDistrict;
                    }
                }
                break;
            case FillData.Region:
                {
                    for (int i = 0; i < flatZones.Length; i++)
                    {
                        flatRet[i] = flatZones[i].RegionNumber;
                    }
                }
                break;
            case FillData.ZoneNumber:
                {
                    for (int i = 0; i < flatZones.Length; i++)
                    {
                        flatRet[i] = flatZones[i].ZoneNumber;
                    }
                }
                break;
            case FillData.FlatIndex:
                {
                    for (int i = 0; i < flatZones.Length; i++)
                    {
                        flatRet[i] = i;
                    }
                }
                break;
        }

        _data = ret;
    }

    public void UnloadData()
    {
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if(!Enum.IsDefined<FillData>(FillWith))
        {
            error = "FillWith is not a valid FillData value!";
            return false;
        }
        return true;
    }

}
