/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using TMG.Data;
using XTMF;
using TMG.Input;

namespace TMG.Frameworks.Data;

[ModuleInformation(Description = "This module is designed to load zonal mapping information from a CSV file where the first two columns are the zone number and then a number to categorize that zone to.")]
// ReSharper disable once InconsistentNaming
public sealed class LoadZoneMapFromCSV : IDataSource<ZoneMap>
{
    public bool Loaded => Data == null;

    [SubModelInformation(Required = true, Description = "The location to load the map file from. (Zone#,Mapping#)")]
    public FileLocation MapFileLocation;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    private ZoneMap Data;

    public ZoneMap GiveData()
    {
        return Data;
    }

    [RunParameter("Default Map Index", 0, "The index to give all of the zones that are not specified.")]
    public int DefaultMapIndex;

    [RootModule]
    public ITravelDemandModel Root;

    public void LoadData()
    {
        var zoneSystem = Root.ZoneSystem.ZoneArray;
        var zones = zoneSystem.GetFlatData();
        int[] map = new int[zones.Length];
        var defaultIndex = DefaultMapIndex;
        if (defaultIndex != 0)
        {
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = defaultIndex;
            }
        }
        using (var reader = new CsvReader(MapFileLocation))
        {
            // burn the header
            reader.LoadLine();
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 2)
                {
                    reader.Get(out int zoneNumber, 0);
                    reader.Get(out int mapIndex, 1);
                    var flatIndex = zoneSystem.GetFlatIndex(zoneNumber);
                    // make sure the zone exists within the zone system
                    if (flatIndex >= 0)
                    {
                        map[flatIndex] = mapIndex;
                    }
                    else
                    {
                        // check to see if everything just equals zero.  In this case it is likely excel adding some extra empty rows.
                        if (!(zoneNumber == 0 && mapIndex == 0))
                        {
                            throw new XTMFRuntimeException(this, "In '" + Name + "' while loading a zone number '" + zoneNumber + "' was found that is not included in the zone system!");
                        }
                    }
                }
            }
        }
        Data = ZoneMap.CreateZoneMap(zones, map);
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Data = null;
    }
}
