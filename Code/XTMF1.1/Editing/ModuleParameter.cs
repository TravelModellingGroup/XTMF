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

namespace XTMF
{
    public class ModuleParameter : IModuleParameter
    {
        public ModuleParameter(ParameterAttribute param, Type t)
        {
            this.Name = param.Name;
            this.Value = param.DefaultValue;
            this.Description = param.Description;
            this.OnField = param.AttachedToField;
            this.VariableName = param.VariableName;
            this.SystemParameter = !( param is RunParameterAttribute );
            this.QuickParameter = false;
            this.Type = t;
        }

        private ModuleParameter()
        {
        }

        /// <summary>
        /// Get the default value for this parameter
        /// </summary>
        /// <returns>The default value for the parameter</returns>
        internal object GetDefault()
        {
            if ( this.OnField )
            {
                var field = BelongsTo.Type.GetField( this.VariableName );
                if ( field == null ) return null;
                return GetDefault( field.GetCustomAttributes( typeof(ParameterAttribute), true ) );
            }
            else
            {
                var field = BelongsTo.Type.GetProperty( this.VariableName );
                if ( field == null ) return null;
                return GetDefault( field.GetCustomAttributes( typeof(ParameterAttribute), true ) );
            }
        }

        private object GetDefault(object[] v)
        {
            if ( v == null ) return null;
            for ( int i = 0; i < v.Length; i++ )
            {
                var parameter = v[i] as ParameterAttribute;
                if ( parameter != null )
                {
                    return parameter.DefaultValue;
                }
            }
            return null;
        }

        public IModelSystemStructure BelongsTo { get; internal set; }

        public string Description { get; set; }

        public string Name { get; private set; }

        public bool OnField { get; set; }

        public bool QuickParameter { get; set; }

        public bool SystemParameter { get; set; }

        public Type Type { get; private set; }

        public object Value { get; set; }

        public string VariableName { get; set; }

        public IModuleParameter Clone()
        {
            ModuleParameter copy = new ModuleParameter();
            copy.Name = this.Name;
            if ( this.Value is ICloneable )
            {
                copy.Value = ( this.Value as ICloneable ).Clone();
            }
            else
            {
                string error = null;
                // we can't have them referencing the same object or changing one will change the original
                if (Value != null)
                {
                    copy.Value = ArbitraryParameterParser.ArbitraryParameterParse(this.Type, this.Value.ToString(), ref error);
                }
                else
                {
                    copy.Value = null;
                }
            }
            copy.Description = this.Description;
            copy.VariableName = this.VariableName;
            copy.OnField = this.OnField;
            copy.SystemParameter = this.SystemParameter;
            copy.QuickParameter = this.QuickParameter;
            copy.BelongsTo = this.BelongsTo;
            copy.Type = this.Type;
            return copy;
        }
    }
}