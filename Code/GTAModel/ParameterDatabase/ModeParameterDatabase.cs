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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"ModeParameterDatabase provides the ability to load in a set of parameters from file, 
link them to the variables inside of the nested modes for Travel Demand Models and 
assign the values at runtime.  GTAModel uses this in order to switch the parameters and feasibilities for 
its mode when switching context between different demographic categories."
        )]
    public class ModeParameterDatabase : IModeParameterDatabase
    {
        [RunParameter( "Database File", "ModeChoiceParameters.csv", "A file containing all of the parameters to be used for each parameter set." )]
        public string DatabaseFile;

        [RunParameter( "Ignore Bad Parameters", false, "Should we continue loading parameters if a column does not have an associated parameter?" )]
        public bool IgnoreBadParameters;

        [RootModule]
        public I4StepModel Root;

        private int _NumberOfParameterSets = -1;

        private bool Loaded = false;

        /// <summary>
        /// The root for each parameter set
        /// </summary>
        private ParameterSetStructureRoot RootNode;

        public string Name
        {
            get;
            set;
        }

        public int NumberOfParameterSets
        {
            get
            {
                // make sure that we are loaded
                if ( !Loaded )
                {
                    Load();
                }
                return this._NumberOfParameterSets;
            }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void ApplyParameterSet(int parameterSetIndex, int demographicIndex)
        {
            // make sure that we are loaded
            if ( !Loaded )
            {
                this.Load();
            }
            if ( parameterSetIndex < 0 || parameterSetIndex >= this._NumberOfParameterSets )
            {
                throw new XTMFRuntimeException( "The parameter set requested does not exist.\r\nRequested:"
                    + parameterSetIndex + "\r\nTotal Sets:" + this._NumberOfParameterSets );
            }
            this.RootNode.Apply( parameterSetIndex );
        }

        public void CompleteBlend()
        {
            throw new NotImplementedException();
        }

        public void InitializeBlend()
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            var dbf = this.GetFullPath( this.DatabaseFile );
            if ( !File.Exists( dbf ) )
            {
                error = "The file '" + dbf + "' does not exist.\r\nPlease include it and try again.";
                return false;
            }
            return true;
        }

        public void SetBlendWeight(float p)
        {
            throw new NotImplementedException();
        }

        private static ParameterSetStructure GetStructure(string mode, ParameterSetStructure parameterSetStructure)
        {
            if ( parameterSetStructure.Mode.ModeName == mode )
            {
                return parameterSetStructure;
            }
            if ( parameterSetStructure.Children != null )
            {
                for ( int i = 0; i < parameterSetStructure.Children.Length; i++ )
                {
                    var res = GetStructure( mode, parameterSetStructure.Children[i] );
                    if ( res != null ) return res;
                }
            }
            return null;
        }

        private Parameter[] CreateParameterMapping(string[] header)
        {
            Parameter[] ret = new Parameter[header.Length];
            for ( int i = 0; i < ret.Length; i++ )
            {
                var endOfMode = header[i].IndexOf( '.' );
                string mode;
                if ( endOfMode == -1 )
                {
                    continue;
                }
                else
                {
                    mode = header[i].Substring( 0, endOfMode );
                    ParameterSetStructure coRespondingStructure = GetStructure( mode );
                    if ( coRespondingStructure == null ) continue;
                    FieldInfo field;
                    PropertyInfo property;
                    if ( StoreProperty( coRespondingStructure.Mode, header[i].Substring( endOfMode + 1 ), out field, out property ) )
                    {
                        if ( coRespondingStructure.Parameters == null )
                        {
                            coRespondingStructure.Parameters = new Parameter[1];
                        }
                        else
                        {
                            var temp = coRespondingStructure.Parameters;
                            coRespondingStructure.Parameters = new Parameter[coRespondingStructure.Parameters.Length + 1];
                            Array.Copy( temp, coRespondingStructure.Parameters, temp.Length );
                        }
                        // store the new parameter in our return list and in the recursive structure
                        ret[i] = coRespondingStructure.Parameters[coRespondingStructure.Parameters.Length - 1] = new Parameter()
                        {
                            Field = field,
                            Property = property,
                            Values = new List<object>()
                        };
                    }
                }
            }
            return ret;
        }

        private void CreateStructure()
        {
            var modes = this.Root.Modes;
            this.RootNode = new ParameterSetStructureRoot()
            {
                Children = CreateStructure( modes )
            };
        }

        private ParameterSetStructure[] CreateStructure(List<IModeChoiceNode> modes)
        {
            // First create all of our children
            var children = new ParameterSetStructure[modes.Count];
            for ( int i = 0; i < children.Length; i++ )
            {
                var mode = modes[i];
                var cat = mode as IModeCategory;
                children[i] = new ParameterSetStructure();
                children[i].Mode = mode;
                if ( cat != null )
                {
                    children[i].Children = CreateStructure( cat.Children );
                }
            }
            return children;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private ParameterSetStructure GetStructure(string mode)
        {
            for ( int i = 0; i < this.RootNode.Children.Length; i++ )
            {
                var res = GetStructure( mode, this.RootNode.Children[i] );
                if ( res != null ) return res;
            }
            return null;
        }

        private void Load()
        {
            this.Loaded = true;
            this.CreateStructure();
            // read in the file
            this.ReadInFile();
        }

        private void ReadInFile()
        {
            var dbf = this.GetFullPath( this.DatabaseFile );
            try
            {
                using ( StreamReader reader = new StreamReader( dbf ) )
                {
                    // First read the header, we will need that data to store in the mode parameters
                    var headerLine = reader.ReadLine();
                    if ( headerLine == null )
                    {
                        throw new XTMFRuntimeException( "The file \"" + this.DatabaseFile + "\" does not contain any data to load parameters from!" );
                    }
                    string[] header = headerLine.Split( ',' );
                    var numberOfParameters = header.Length;
                    string[] modeName = new string[numberOfParameters];
                    Parameter[] parameterMapping = CreateParameterMapping( header );
                    string line;
                    int numberOfLines = 0;
                    while ( ( line = reader.ReadLine() ) != null )
                    {
                        var parameters = line.Split( ',' );
                        if ( parameters.Length < numberOfParameters ) continue;
                        numberOfLines++;
                        for ( int i = 0; i < numberOfParameters; i++ )
                        {
                            if ( parameterMapping[i] == null ) continue;
                            Type t = parameterMapping[i].Field != null ? parameterMapping[i].Field.FieldType
                                : parameterMapping[i].Property.PropertyType;
                            if ( t == typeof( float ) )
                            {
                                parameterMapping[i].Values.Add( float.Parse( parameters[i] ) );
                            }
                            else if ( t == typeof( double ) )
                            {
                                parameterMapping[i].Values.Add( double.Parse( parameters[i] ) );
                            }
                            else if ( t == typeof( bool ) )
                            {
                                parameterMapping[i].Values.Add( bool.Parse( parameters[i] ) );
                            }
                            else if ( t == typeof( int ) )
                            {
                                parameterMapping[i].Values.Add( int.Parse( parameters[i] ) );
                            }
                        }
                    }
                    this._NumberOfParameterSets = numberOfLines;
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "The file '" + dbf + "' does not exist or is not accessable!" );
            }
        }

        private bool StoreProperty(IModeChoiceNode selectedMode, string parameterName, out FieldInfo field, out PropertyInfo property)
        {
            // Search for a field or property that has an attribute with this name
            field = null;
            property = null;
            var modeType = selectedMode.GetType();
            foreach ( var f in modeType.GetProperties() )
            {
                // search the attributes
                var attributes = f.GetCustomAttributes( true );
                foreach ( var at in attributes )
                {
                    // if we find an attribute from XTMF
                    ParameterAttribute parameter;
                    if ( ( parameter = ( at as XTMF.ParameterAttribute ) ) != null )
                    {
                        // Check to see if this is our parameter
                        if ( parameter.Name == parameterName )
                        {
                            property = f;
                            return true;
                        }
                    }
                }
            }
            foreach ( var f in modeType.GetFields() )
            {
                // search the attributes
                var attributes = f.GetCustomAttributes( true );
                foreach ( var at in attributes )
                {
                    // if we find an attribute from XTMF
                    ParameterAttribute parameter;
                    if ( ( parameter = ( at as XTMF.ParameterAttribute ) ) != null )
                    {
                        // Check to see if this is our parameter
                        if ( parameter.Name == parameterName )
                        {
                            field = f;
                            return true;
                        }
                    }
                }
            }
            if ( !IgnoreBadParameters )
            {
                // If we get here then we did not find it!
                throw new XTMFRuntimeException( "We were unable to find a parameter with the name \"" + parameterName + "\" in the mode " + selectedMode.ModeName );
            }
            return false;
        }

        private struct ParameterSetStructureRoot
        {
            internal ParameterSetStructure[] Children;

            internal void Apply(int parameterIndex)
            {
                if ( Children != null )
                {
                    foreach ( var child in Children )
                    {
                        child.Apply( parameterIndex );
                    }
                }
            }
        }

        private class Parameter
        {
            internal FieldInfo Field;
            internal PropertyInfo Property;
            internal List<object> Values;

            internal void Apply(IModeChoiceNode mode, int parameterIndex)
            {
                if ( this.Field != null )
                {
                    this.Field.SetValue( mode, Values[parameterIndex] );
                }
                else
                {
                    this.Property.SetValue( mode, Values[parameterIndex], null );
                }
            }
        }

        private class ParameterSetStructure
        {
            internal ParameterSetStructure[] Children;
            internal IModeChoiceNode Mode;
            internal Parameter[] Parameters;

            internal void Apply(int parameterIndex)
            {
                if ( this.Parameters != null )
                {
                    for ( int i = 0; i < this.Parameters.Length; i++ )
                    {
                        this.Parameters[i].Apply( this.Mode, parameterIndex );
                    }
                }
                if ( Children != null )
                {
                    foreach ( var child in Children )
                    {
                        child.Apply( parameterIndex );
                    }
                }
            }
        }
    }
}