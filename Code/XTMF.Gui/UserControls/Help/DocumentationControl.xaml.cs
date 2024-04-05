/*
    Copyright 2015-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XTMF.Gui.Annotations;
using Brush = System.Drawing.Brush;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for DocumentationControl.xaml
    /// </summary>
    public partial class DocumentationControl : UserControl
    {

        public Type Type
        {
            get => (Type)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public string ModuleName
        {
            get => (string)GetValue(ModuleNameProperty);
            set => SetValue(ModuleNameProperty, value);
        }

        public string ModuleNamespace
        {
            get => (string)GetValue(ModuleNamespaceProperty);
            set => SetValue(ModuleNamespaceProperty, value);
        }

        public string ModuleDescription
        {
            get => (string)GetValue(ModuleDescriptionProperty);
            set => SetValue(ModuleDescriptionProperty, value);
        }

        public Parameter[] ModuleParameters
        {
            get => (Parameter[])GetValue(ModuleParametersProperty);
            set => SetValue(ModuleParametersProperty, value);
        }

        public SubModule[] ModuleSubmodules
        {
            get => (SubModule[])GetValue(ModuleSubmodulesProperty);
            set => SetValue(ModuleSubmodulesProperty, value);
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(Type), typeof(DocumentationControl), new PropertyMetadata(null, OnTypeChanged));

        public static readonly DependencyProperty ModuleNameProperty =
            DependencyProperty.Register("ModuleName", typeof(string), typeof(DocumentationControl), new FrameworkPropertyMetadata("No module loaded", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModuleNamespaceProperty =
            DependencyProperty.Register("ModuleNamespace", typeof(string), typeof(DocumentationControl), new FrameworkPropertyMetadata("No module loaded", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModuleDescriptionProperty =
            DependencyProperty.Register("ModuleDescription", typeof(string), typeof(DocumentationControl), new FrameworkPropertyMetadata("No module loaded", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModuleParametersProperty = 
            DependencyProperty.Register("ModuleParameters", typeof(Parameter[]), typeof(DocumentationControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModuleSubmodulesProperty =
            DependencyProperty.Register("ModuleSubmodules", typeof(SubModule[]), typeof(DocumentationControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public string TypeNameText { get { var t = Type; return t == null ? "No Type!" : t.Name; } }
        public DocumentationControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newType = e.NewValue as Type;
            var us = d as DocumentationControl;
            if (newType == null)
            {
                us.ModuleName = "No Type Loaded";
                us.ModuleNamespace = string.Empty;
                us.ModuleDescription = string.Empty;
                us.ModuleParameters = null;
                us.ModuleSubmodules = null;
            }
            else
            {
                us.ModuleName = newType.Name;
                us.ModuleNamespace = newType.FullName;
                SetDescription(us, GetDescription(newType), newType);
                us.ModuleParameters = GetParameters(newType);
                us.ModuleSubmodules = GetSubmodules(newType);
            }
        }

        private static string GetDescription(Type type)
        {
            var attributes = type.GetCustomAttributes(true);
            string description = "No Description";
            foreach (var at in attributes)
            {
                if (at is ModuleInformationAttribute info)
                {
                    description = info.Description;
                    break;
                }
            }
            return description;
        }

        private static void SetDescription(DocumentationControl window, string description, Type module)
        {
            window.ModuleDescription = description;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Parameter[] GetParameters(Type type)
        {
            var list = Project.GetParameters(type);
            if (list == null)
            {
                return null;
            }
            var length = list.Parameters.Count;
            Parameter[] ret = new Parameter[length];
            for (int i = 0; i < length; i++)
            {
                ret[i] = new Parameter
                {
                    Type = list.Parameters[i].Type == null ? "No Type" : ConvertTypeName(list.Parameters[i].Type),
                    Name = list.Parameters[i].Name,
                    Description = list.Parameters[i].Description
                };
            }
            return ret;
        }

        private static SubModule[] GetSubmodules(Type type)
        {
            List<SubModule> submodules = [];
            var fields = type.GetFields();
            foreach (var f in fields)
            {
                var attributes = f.GetCustomAttributes(true);
                submodules.AddRange(attributes.OfType<SubModelInformation>().Select(param => new SubModule
                {
                    Name = f.Name,
                    Description = param.Description,
                    Type = ConvertTypeName(f.FieldType)
                }));
            }
            var properties = type.GetProperties();
            foreach (var f in properties)
            {
                var attributes = f.GetCustomAttributes(true);
                if (attributes != null)
                {
                    submodules.AddRange(attributes.OfType<SubModelInformation>().Select(param => new SubModule
                    {
                        Name = f.Name,
                        Description = param.Description,
                        Type = ConvertTypeName(f.PropertyType)
                    }));
                }
            }
            return submodules.ToArray();
        }

        private static string ConvertTypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            StringBuilder builder = new();
            builder.Append(type.Name, 0, type.Name.IndexOf('`'));
            builder.Append('<');
            var inside = type.GetGenericArguments();
            var first = true;
            foreach (var t in inside)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                builder.Append(t.Name);
            }
            builder.Append('>');
            return builder.ToString();
        }   
    }
    public class Parameter : INotifyPropertyChanged
    {
        public string Description { get; internal set; }

        public string Name { get; internal set; }

        public string Type { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SubModule : INotifyPropertyChanged
    {
        public string Description { get; internal set; }

        public string Name { get; internal set; }

        public bool Required { get; internal set; }

        public string Type { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
