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

namespace Tasha.Common;

/// <summary>
/// This attribute should be placed over any variable in a plugin
/// that should be appear in the associated directory in the configuration
/// file
/// </summary>
[AttributeUsage( AttributeTargets.Field )]
public class ConfigParamAttribute : Attribute
{
    public ConfigParamAttribute(ParamType type, object defaultVal)
    {
        DefaultValue = defaultVal;
        Type = type;
        Description = "";
    }

    public ConfigParamAttribute(ParamType type, object defaultVal, string description)
        : this( type, defaultVal )
    {
        Description = description;
    }

    public object DefaultValue { get; private set; }

    public string Description { get; private set; }

    public ParamType Type { get; private set; }
}