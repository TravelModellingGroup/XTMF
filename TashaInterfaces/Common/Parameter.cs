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
namespace Tasha.Common
{
    public class Parameter
    {
        public Parameter(object value, ParamType type)
        {
            ValType = type;
            Value = value;
            Description = "";
        }

        public Parameter(object value, ParamType type, string description)
            : this( value, type )
        {
            Description = description;
        }

        public Parameter(object value, string type)
        {
            if ( type == "Float" || type == "Double" )
            {
                ValType = ParamType.Float;
            }
            else if ( type == "File" )
            {
                ValType = ParamType.File;
            }
            else if ( type == "InputFile" )
            {
                ValType = ParamType.InputFile;
            }
            else if ( type == "Integer" || type == "Int" )
            {
                ValType = ParamType.Integer;
            }
            else if ( type == "OutputFile" )
            {
                ValType = ParamType.OutputFile;
            }
            else if ( type == "Directory" )
            {
                ValType = ParamType.Directory;
            }
            else if ( type == "CacheFile" )
            {
                ValType = ParamType.CacheFile;
            }
            else if ( type == "Bool" )
            {
                ValType = ParamType.Bool;
            }
            else if ( type == "String" )
            {
                ValType = ParamType.String;
            }

            Value = value;
        }

        public Parameter(object value)
        {
            Value = value;
            ValType = ParamType.String;
            Description = "";
        }

        public string Description { get; set; }

        public string SValue { get { return Value.ToString(); } }

        public ParamType ValType { get; private set; }

        public object Value { get; set; }
    }
}