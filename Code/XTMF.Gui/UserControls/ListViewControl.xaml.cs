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
using XTMF.Gui.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ListViewControl.xaml
    /// </summary>
    public partial class ListViewControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty TitleTextDependencyProperty =
            DependencyProperty.Register("TitleText", typeof(string), typeof(ListViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SubTextDependencyProperty =
            DependencyProperty.Register("SubText", typeof(string), typeof(ListViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty StatusTextDependencyProperty =
            DependencyProperty.Register("StatusText", typeof(string), typeof(ListViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty BitmapIconDependencyProperty =
            DependencyProperty.Register("IsBitmapIcon", typeof(bool), typeof(ListViewControl), new PropertyMetadata(false));

        public static readonly DependencyProperty PathIconDependencyProperty =
            DependencyProperty.Register("IsPathIcon", typeof(bool), typeof(ListViewControl), new PropertyMetadata(true));

        public static readonly DependencyProperty IconPathDependencyProperty =
            DependencyProperty.Register("IconPath", typeof(Path), typeof(ListViewControl), new PropertyMetadata(null));

        public static readonly DependencyProperty IsSelectedDependencyProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(ListViewControl), new PropertyMetadata(true));

        public ListViewControl()
        {
            InitializeComponent();
        }

        public Path IconPath
        {
            get => (Path)GetValue(IconPathDependencyProperty);
            set => SetValue(IconPathDependencyProperty, value);
        }

        public bool IsBitmapIcon
        {
            get => (bool)GetValue(BitmapIconDependencyProperty);
            set => SetValue(BitmapIconDependencyProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedDependencyProperty);
            set => SetValue(IsSelectedDependencyProperty, value);
        }

        public bool IsPathIcon
        {
            get => (bool)GetValue(PathIconDependencyProperty);
            set => SetValue(PathIconDependencyProperty, value);
        }

        public string TitleText
        {
            get => (string)GetValue(TitleTextDependencyProperty);
            set
            {
                SetValue(TitleTextDependencyProperty, value);
                Title.Content = value;
            }
        }

        public string SubText
        {
            get => (string)GetValue(SubTextDependencyProperty);
            set => SetValue(SubTextDependencyProperty, value);
        }

        public string StatusText
        {
            get => (string)GetValue(StatusTextDependencyProperty);
            set
            {
                StatusTextLabel.Visibility = string.IsNullOrEmpty(value) ?
                    Visibility.Visible : Visibility.Collapsed;
                SetValue(StatusTextDependencyProperty, value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
