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
using System.Reflection;
using TMG.ParameterDatabase;
using XTMF;

namespace TMG.GTAModel.ParameterDatabase
{
    public class ParameterLink : IParameterLink
    {
        [RunParameter( "Mode Parameter Name", "", "The name of the parameter of the mode's module or Utility Component to bind to." )]
        public string ModeParameterName;

        [RunParameter( "Multiplier", 1f, "The amount to multiply against floating point parameters." )]
        public float Multiplier;

        [ParentModel]
        public IModeParameterAssignment Parent;

        [RootModule]
        public AdvancedModeParameterDatabase Root;

        protected object AssignTo;

        protected FieldInfo Field;

        protected PropertyInfo Property;

        private bool CurrentBlendBool = false;

        private double CurrentBlendNumber = 0;

        private int TypeIndex = 0;

        public string Name
        {
            get;
            set;
        }

        [RunParameter( "Parameter Name", "", "The name of the parameter from the mode choice file to bind to." )]
        public string ParameterName { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Assign(string value)
        {
            string error = null;
            object temp;
            if ( TypeIndex == 1 )
            {
                // do float assignment
                temp = float.Parse( value ) * this.Multiplier;
            }
            else if ( TypeIndex == 2 )
            {
                // do double assignment
                temp = double.Parse( value ) * this.Multiplier;
            }
            else
            {
                var t = ( this.Field == null ? this.Property.PropertyType : this.Field.FieldType );
                if ( ( temp = ArbitraryParameterParser.ArbitraryParameterParse( t, value, ref error ) ) == null )
                {
                    throw new XTMFRuntimeException( "Unable to convert value!" );
                }
            }

            if ( this.Field != null )
            {
                this.Field.SetValue( AssignTo, temp );
            }
            else
            {
                this.Property.SetValue( AssignTo, temp, null );
            }
        }

        public void BlendedAssignment(string value, float ammount)
        {
            var t = ( Field != null ? Field.FieldType : Property.PropertyType );
            if ( t == typeof( float ) )
            {
                double temp;
                if ( double.TryParse( value, out temp ) )
                {
                    // do float assignment
                    CurrentBlendNumber += temp * ammount;
                }
            }
            else if ( t == typeof( double ) )
            {
                double temp;
                if ( double.TryParse( value, out temp ) )
                {
                    // do float assignment
                    CurrentBlendNumber += temp * ammount;
                }
            }
            else if ( t == typeof( bool ) )
            {
                // take the "true'est value
                bool temp;
                if ( bool.TryParse( value, out temp ) )
                {
                    this.CurrentBlendBool = this.CurrentBlendBool | temp;
                }
            }
            else
            {
                string error = null;
                object temp;
                if ( ( temp = ArbitraryParameterParser.ArbitraryParameterParse( t, value, ref error ) ) == null )
                {
                    throw new XTMFRuntimeException( "Unable to convert value!" );
                }
                if ( this.Field != null )
                {
                    this.Field.SetValue( Parent.Mode, temp );
                }
                else
                {
                    this.Property.SetValue( Parent.Mode, temp, null );
                }
            }
        }

        public void FinishBlending()
        {
            var t = ( Field != null ? Field.FieldType : Property.PropertyType );
            if ( t == typeof( float ) )
            {
                float temp = (float)( this.CurrentBlendNumber * this.Multiplier );
                if ( this.Field != null )
                {
                    this.Field.SetValue( AssignTo, temp );
                }
                else
                {
                    this.Property.SetValue( AssignTo, temp, null );
                }
            }
            else if ( t == typeof( double ) )
            {
                double temp = this.CurrentBlendNumber * this.Multiplier;
                if ( this.Field != null )
                {
                    this.Field.SetValue( AssignTo, temp );
                }
                else
                {
                    this.Property.SetValue( AssignTo, temp, null );
                }
            }
            else if ( t == typeof( bool ) )
            {
                if ( this.Field != null )
                {
                    this.Field.SetValue( AssignTo, this.CurrentBlendBool );
                }
                else
                {
                    this.Property.SetValue( AssignTo, this.CurrentBlendBool, null );
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !LinkModeParameter( ref error ) )
            {
                return false;
            }
            var t = this.Field == null ? this.Property.PropertyType : this.Field.FieldType;
            if ( t == typeof( float ) )
            {
                TypeIndex = 1;
            }
            else if ( t == typeof( double ) )
            {
                TypeIndex = 2;
            }
            return true;
        }

        public void StartBlending()
        {
            CurrentBlendNumber = 0;
            CurrentBlendBool = false;
        }

        protected virtual bool LinkModeParameter(ref string error)
        {
            IModeChoiceNode mode = this.Parent.Mode;
            this.AssignTo = this.Parent.Mode;
            if ( mode == null )
            {
                error = "In '" + this.Parent.Name + "' it failed to present a mode for '" + this.Name + "'!";
                return false;
            }
            var modeType = mode.GetType();
            var parameterType = typeof( ParameterAttribute );
            foreach ( var field in modeType.GetFields() )
            {
                var attributes = field.GetCustomAttributes( parameterType, true );
                if ( attributes != null )
                {
                    for ( int i = 0; i < attributes.Length; i++ )
                    {
                        if ( ( attributes[i] as ParameterAttribute ).Name == this.ModeParameterName )
                        {
                            this.Field = field;
                            return true;
                        }
                    }
                }
            }

            foreach ( var field in modeType.GetProperties() )
            {
                var attributes = field.GetCustomAttributes( parameterType, true );
                if ( attributes != null )
                {
                    for ( int i = 0; i < attributes.Length; i++ )
                    {
                        if ( ( attributes[i] as ParameterAttribute ).Name == this.ModeParameterName )
                        {
                            this.Property = field;
                            return true;
                        }
                    }
                }
            }
            error = "We were unable to find a parameter in the mode '" + mode.ModeName + "' called '" + this.ModeParameterName + "'!";
            return false;
        }
    }
}