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
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for DocumentationWindow.xaml
    /// </summary>
    public partial class DocumentationWindow : Window
    {
        public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register( "ModuleDescription", typeof( string ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( "No Description", FrameworkPropertyMetadataOptions.AffectsRender ) );

        public static readonly DependencyProperty ModuleNameProperty = DependencyProperty.Register( "ModuleName", typeof( string ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( "No Module Selected", FrameworkPropertyMetadataOptions.AffectsRender ) );

        public static readonly DependencyProperty ModuleNamespaceProperty = DependencyProperty.Register( "ModuleNamespace", typeof( string ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( "", FrameworkPropertyMetadataOptions.AffectsRender ) );

        public static readonly DependencyProperty ModuleParametersProperty = DependencyProperty.Register( "ModuleParameters", typeof( Parameter[] ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender ) );

        public static readonly DependencyProperty ModuleProperty = DependencyProperty.Register( "Module", typeof( IModelSystemStructure ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( OnModuleChanged, OnCoerceModuleChanged ) );

        public static readonly DependencyProperty ModuleSubmodulesProperty = DependencyProperty.Register( "ModuleSubmodules", typeof( SubModule[] ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender ) );

        public static readonly DependencyProperty ModuleTypeProperty = DependencyProperty.Register( "ModuleType", typeof( Type ), typeof( DocumentationWindow ),
            new FrameworkPropertyMetadata( OnModuleTypeChanged, OnCoerceModuleChanged ) );

        public DocumentationWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            this.Browser.Navigated += Browser_Navigated;
        }

        void Browser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            this.Browser.Visibility = System.Windows.Visibility.Visible;
        }

        public IModelSystemStructure Module
        {
            get { return (IModelSystemStructure)GetValue( ModuleProperty ); }
            set { SetValue( ModuleProperty, value ); }
        }

        public string ModuleDescription
        {
            get { return (string)GetValue( ModuleDescriptionProperty ); }
            set { SetValue( ModuleDescriptionProperty, value ); }
        }

        public string ModuleName
        {
            get { return (string)GetValue( ModuleNameProperty ); }
            set { SetValue( ModuleNameProperty, value ); }
        }

        public string ModuleNamespace
        {
            get { return (string)GetValue( ModuleNamespaceProperty ); }
            set { SetValue( ModuleNamespaceProperty, value ); }
        }

        public Parameter[] ModuleParameters
        {
            get { return (Parameter[])GetValue( ModuleParametersProperty ); }
            set { SetValue( ModuleParametersProperty, value ); }
        }

        public SubModule[] ModuleSubmodules
        {
            get { return (SubModule[])GetValue( ModuleSubmodulesProperty ); }
            set { SetValue( ModuleSubmodulesProperty, value ); }
        }

        public Type ModuleType
        {
            get { return (Type)GetValue( ModuleTypeProperty ); }
            set { SetValue( ModuleTypeProperty, value ); }
        }

        private static string ConvertTypeName(Type type)
        {
            if ( !type.IsGenericType )
            {
                return type.Name;
            }
            else
            {
                StringBuilder builder = new StringBuilder();
                builder.Append( type.Name, 0, type.Name.IndexOf( '`' ) );
                builder.Append( '<' );
                var inside = type.GetGenericArguments();
                bool first = true;
                foreach ( var t in inside )
                {
                    if ( !first )
                    {
                        builder.Append( ',' );
                    }
                    first = false;
                    builder.Append( t.Name );
                }
                builder.Append( '>' );
                return builder.ToString();
            }
        }

        private static string GetDescription(Type type)
        {
            var attributes = type.GetCustomAttributes( true );
            string description = "No Description";
            foreach ( var at in attributes )
            {
                var info = at as ModuleInformationAttribute;
                if ( info != null )
                {
                    description = info.Description;
                    break;
                }
            }
            return description;
        }

        private static Parameter[] GetParameters(Type type)
        {
            List<Parameter> parameters = new List<Parameter>();
            var fields = type.GetFields();
            foreach ( var f in fields )
            {
                var attributes = f.GetCustomAttributes( true );
                if ( attributes != null )
                {
                    foreach ( var a in attributes )
                    {
                        var param = a as XTMF.ParameterAttribute;
                        if ( param != null )
                        {
                            parameters.Add( new Parameter()
                            {
                                Name = param.Name,
                                Description = param.Description,
                                Type = ConvertTypeName( f.FieldType )
                            } );
                        }
                    }
                }
            }
            var properties = type.GetProperties();
            foreach ( var f in properties )
            {
                var attributes = f.GetCustomAttributes( true );
                if ( attributes != null )
                {
                    foreach ( var a in attributes )
                    {
                        var param = a as XTMF.ParameterAttribute;
                        if ( param != null )
                        {
                            parameters.Add( new Parameter()
                            {
                                Name = param.Name,
                                Description = param.Description,
                                Type = ConvertTypeName( f.PropertyType )
                            } );
                        }
                    }
                }
            }
            return parameters.ToArray();
        }

        private static Parameter[] GetParameters(IModelSystemStructure mss)
        {
            var list = mss.Parameters;
            if ( list == null )
            {
                return null;
            }
            var length = list.Parameters.Count;
            Parameter[] ret = new Parameter[length];
            for ( int i = 0; i < length; i++ )
            {
                ret[i] = new Parameter()
                {
                    Type = list.Parameters[i].Type == null ? "No Type" : ConvertTypeName( list.Parameters[i].Type ),
                    Name = list.Parameters[i].Name,
                    Description = list.Parameters[i].Description
                };
            }
            return ret;
        }

        private static SubModule[] GetSubmodules(Type type)
        {
            List<SubModule> submodules = new List<SubModule>();
            var fields = type.GetFields();
            foreach ( var f in fields )
            {
                var attributes = f.GetCustomAttributes( true );
                if ( attributes != null )
                {
                    foreach ( var a in attributes )
                    {
                        var param = a as XTMF.SubModelInformation;
                        if ( param != null )
                        {
                            submodules.Add( new SubModule()
                            {
                                Name = f.Name,
                                Description = param.Description,
                                Type = ConvertTypeName( f.FieldType )
                            } );
                        }
                    }
                }
            }
            var properties = type.GetProperties();
            foreach ( var f in properties )
            {
                var attributes = f.GetCustomAttributes( true );
                if ( attributes != null )
                {
                    foreach ( var a in attributes )
                    {
                        var param = a as XTMF.SubModelInformation;
                        if ( param != null )
                        {
                            submodules.Add( new SubModule()
                            {
                                Name = f.Name,
                                Description = param.Description,
                                Type = ConvertTypeName( f.PropertyType )
                            } );
                        }
                    }
                }
            }
            return submodules.ToArray();
        }

        private static SubModule[] GetSubmodules(IModelSystemStructure mss)
        {
            var list = mss.Children;
            if ( list == null )
            {
                return null;
            }
            var length = list.Count;
            SubModule[] ret = new SubModule[length];
            for ( int i = 0; i < length; i++ )
            {
                ret[i] = new SubModule()
                {
                    Type = list[i].ParentFieldType == null ? "Unknown" : ConvertTypeName( list[i].ParentFieldType ),
                    Name = list[i].ParentFieldName,
                    Description = list[i].Description,
                    Required = list[i].Required
                };
            }
            return ret;
        }

        private static object OnCoerceModuleChanged(DependencyObject source, object e)
        {
            return e;
        }

        private static void OnModuleChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var window = source as DocumentationWindow;
            var value = e.NewValue as IModelSystemStructure;
            ProcessNewType( window, value == null ? null : value.Type );
        }

        private static void OnModuleTypeChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var window = source as DocumentationWindow;
            var value = e.NewValue as Type;
            ProcessNewType( window, value );
        }

        private static void ProcessNewType(DocumentationWindow window, Type type)
        {
            if ( type == null )
            {
                window.ModuleName = "No Module Loaded";
                window.ModuleNamespace = String.Empty;
                SetDescription( window, String.Empty );
                window.ModuleParameters = null;
                window.ModuleSubmodules = null;
            }
            else
            {
                window.ModuleName = type.Name;
                window.ModuleNamespace = type.FullName;
                SetDescription( window, GetDescription( type ) );
                window.ModuleParameters = GetParameters( type );
                window.ModuleSubmodules = GetSubmodules( type );
            }
        }

        private static void SetDescription(DocumentationWindow window, string description)
        {
            StringBuilder builder = new StringBuilder();
            window.Browser.Visibility = Visibility.Collapsed;
            builder.Append( @"<!DOCTYPE html>
<html>
<head><meta http-equiv='X-UA-Compatible' content='IE=edge' /> </head><body style='background-color: #303030; color:#ffffff;'>" );
            builder.Append( description );
            builder.Append( "</body></html>" );
            window.ModuleDescription = builder.ToString();
            window.Browser.NavigateToString( window.ModuleDescription );
        }

        private void Browser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            // When we try to go to a link cancel it.
            //e.Cancel = true;
        }

        public class Parameter
        {
            public string Description { get; internal set; }

            public string Name { get; internal set; }

            public string Type { get; internal set; }
        }

        public class SubModule
        {
            public string Description { get; internal set; }

            public string Name { get; internal set; }

            public bool Required { get; internal set; }

            public string Type { get; internal set; }
        }
    }
}