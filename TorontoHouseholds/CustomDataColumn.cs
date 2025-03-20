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
using Datastructure;
using System;
using Tasha.Common;
using XTMF;

namespace TMG.Tasha;

[ModuleInformation(Description = "Used for specifying extra attributes for different data types.  Values must be floating point.")]
public sealed class CustomDataColumn : IModule
{
    [RunParameter("Variable Name", "", "The name of the variable to assign to.")]
    public string VariableName;

    [RunParameter("Column Index", -1, "The 0 indexed column to read from.")]
    public int ColumnIndex;

    public enum DataType
    {
        Float = 0,
        String = 1,
        Character = 2,
        Integer = 3,
    }

    [RunParameter("Data Type", DataType.Float, "The type of data to read in.")]
    public DataType ColumnDataType = DataType.Float;

    public void ReadIntoAttachable(IAttachable attachable, CsvReader reader)
    {
        switch(ColumnDataType)
        {
            case DataType.Float:
                {
                    reader.Get(out float temp, ColumnIndex);
                    attachable.Attach(VariableName, temp);
                }
                break;
            case DataType.String:
                {
                    reader.Get(out string temp, ColumnIndex);
                    attachable.Attach(VariableName, temp);
                }
                break;
            case DataType.Character:
                {
                    reader.Get(out char temp, ColumnIndex);
                    attachable.Attach(VariableName, temp);
                }
                break;
            case DataType.Integer:
                {
                    reader.Get(out int temp, ColumnIndex);
                    attachable.Attach(VariableName, temp);
                }
                break;
            default:
                throw new XTMFRuntimeException(this, "Unrecognized DataType.");
        }
    }

    public string Name { get; set; } = string.Empty;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);



    public bool RuntimeValidation(ref string error)
    {
        if(ColumnIndex < 0)
        {
            error = "Column Index must be greater than or equal to 0.";
            return false;
        }
        return true;
    }
}
