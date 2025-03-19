/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Tasha;

[ModuleInformation(Description = "Used for specifying extra attributes for different data types.  Values must be floating point.")]
public sealed class CustomDataColumn : IModule
{
    [RunParameter("Variable Name", "", "The name of the variable to assign to.")]
    public string VariableName;

    [RunParameter("Column Index", -1, "The 0 indexed column to read from.")]
    public int ColumnIndex;

    public string Name { get; set; } = string.Empty;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
