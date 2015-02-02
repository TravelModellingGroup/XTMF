/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for DocumentationControl.xaml
    /// </summary>
    public partial class DocumentationControl : UserControl
    {


        public Type Type
        {
            get { return (Type)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public string TypeNameText { get { var t = Type; return t == null ? "No Type!" : t.Name; } }


        // Using a DependencyProperty as the backing store for Type.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(Type), typeof(DocumentationControl), new PropertyMetadata(null, OnTypeChanged));



        public string ModuleName
        {
            get { return (string)GetValue(ModuleNameProperty); }
            set { SetValue(ModuleNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ModuleName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModuleNameProperty =
            DependencyProperty.Register("ModuleName", typeof(string), typeof(DocumentationControl), new PropertyMetadata(""));

        public string ModuleNamespace
        {
            get { return (string)GetValue(ModuleNamespaceProperty); }
            set { SetValue(ModuleNamespaceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ModuleName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModuleNamespaceProperty =
            DependencyProperty.Register("ModuleNamespace", typeof(string), typeof(DocumentationControl), new PropertyMetadata(""));

        public string ModuleDescription
        {
            get { return (string)GetValue(ModuleDescriptionProperty); }
            set { SetValue(ModuleDescriptionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ModuleName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ModuleDescriptionProperty =
            DependencyProperty.Register("ModuleDescription", typeof(string), typeof(DocumentationControl), new PropertyMetadata(""));



        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newType = e.NewValue as Type;
            var us = d as DocumentationControl;
            if(newType == null)
            {
                us.ModuleName = "No Type Loaded";
                us.ModuleNamespace = String.Empty;
                us.ModuleDescription = String.Empty;
            }
            else
            {
                us.ModuleName = newType.Name;
                us.ModuleNamespace = newType.FullName;
                us.ModuleDescription = GetDescription(newType);
                SetDescription(us, us.ModuleDescription);
            }
        }

        private static string GetDescription(Type type)
        {
            var attributes = type.GetCustomAttributes(true);
            string description = "No Description";
            foreach(var at in attributes)
            {
                var info = at as ModuleInformationAttribute;
                if(info != null)
                {
                    description = info.Description;
                    break;
                }
            }
            return description;
        }

        private static void SetDescription(DocumentationControl window, string description)
        {
            StringBuilder builder = new StringBuilder();
            window.Browser.Visibility = Visibility.Collapsed;
            builder.Append(@"<!DOCTYPE html>
<html>
<head><meta http-equiv='X-UA-Compatible' content='IE=edge' /> </head><body style='background-color: #ffffff; color:#000;'>");
            builder.Append(description);
            builder.Append("</body></html>");
            window.ModuleDescription = builder.ToString();
            window.Browser.NavigateToString(window.ModuleDescription);
        }

        public DocumentationControl()
        {
            this.DataContext = this;
            InitializeComponent();
        }
    }
}
