﻿/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Interfaces;

namespace XTMF;

public class ProjectModelSystem
{
    public string Name
    {
        get
        {
            return Root?.Name ?? _tempName ?? "Unnamed";
        }
        set
        {
            if (Root is not null)
            {
                Root.Name = value;
            }
            else
            {
                _tempName = value;
            }
        }
    }
    private string _tempName;
    public IModelSystemStructure Root { get; internal set; }
    public List<ILinkedParameter> LinkedParameters { get; internal set; }
    public List<IRegionDisplay> RegionDisplays { get; internal set; }
    public string Description { get; internal set; }
    public string GUID { get; internal set; }
    public DateTime LastModified { get; set; }

    public ProjectModelSystem()
    {
        
    }

    public bool IsLoaded { get; set; } = true;
}
