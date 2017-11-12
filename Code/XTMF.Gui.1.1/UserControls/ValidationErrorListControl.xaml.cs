/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XTMF.Gui.Annotations;

namespace XTMF.Gui.UserControls 
{
    /// <summary>
    /// Interaction logic for ListViewControl.xaml
    /// </summary>
    public partial class ValidationErrorListControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ErrorStringDependencyProperty = 
            DependencyProperty.Register("ErrorString", typeof(string), typeof(ValidationErrorListControl), new PropertyMetadata("Test"));

        public static readonly DependencyProperty ModuleNameDependencyProperty =
            DependencyProperty.Register("ModuleName", typeof(string), typeof(ValidationErrorListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty IsSelectedDependencyProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(ValidationErrorListControl), new PropertyMetadata(true));

        public ValidationErrorListControl()
        {
            InitializeComponent();
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedDependencyProperty);
            set => SetValue(IsSelectedDependencyProperty, value);
        }

        public string ErrorString
        {
            get => (string)GetValue(ErrorStringDependencyProperty);
            set
            {
                SetValue(ErrorStringDependencyProperty, value);
                ErrorStringTextBlock.Text = value;
            }
        }

        public string ModuleName
        {
            get => (string)GetValue(ModuleNameDependencyProperty);
            set
            { 
                SetValue(ModuleNameDependencyProperty, value);
                ModuleNameLabel.Content = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ModuleNameLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }
    }
}
