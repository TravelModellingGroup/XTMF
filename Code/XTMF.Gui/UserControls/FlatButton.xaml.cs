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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using XTMF.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for FlatButton.xaml
    /// </summary>
    /// 
    public partial class FlatButton : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ToolTextTextDependencyProperty =
            DependencyProperty.Register("ToolText", typeof(string), typeof(FlatButton), new PropertyMetadata(null));

        public static readonly DependencyProperty IconPathDependencyProperty = DependencyProperty.Register("IconPath",
            typeof(Path), typeof(FlatButton), new PropertyMetadata(null));

        public event RoutedEventHandler Click;

        public FlatButton()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string ToolText
        {
            get => (string)GetValue(ToolTextTextDependencyProperty);
            set => SetValue(ToolTextTextDependencyProperty, value);
        }

        public Path IconPath
        {
            get => (Path)GetValue(IconPathDependencyProperty);
            set => SetValue(IconPathDependencyProperty, value);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e) => Click?.Invoke(sender, e);

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
