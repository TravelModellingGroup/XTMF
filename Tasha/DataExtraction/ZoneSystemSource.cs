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
using TMG;
namespace Tasha.DataExtraction;

[ModuleInformation(Description=
    "This module is designed to allow for the storage of a zone system as a resource."
    )]
public class ZoneSystemSource : IDataSource<IZoneSystem>
{
    [SubModelInformation(Required=true, Description="The zone system to store.")]
    public IZoneSystem ZoneSystem;

    public IZoneSystem GiveData()
    {
        return ZoneSystem;
    }

    public bool Loaded
    {
        get { return ZoneSystem != null; }
    }

    public void LoadData()
    {
        ZoneSystem.LoadData();
    }

    public void UnloadData()
    {
        ZoneSystem.UnloadData();
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
        return true;
    }
}
