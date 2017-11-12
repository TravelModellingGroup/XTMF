/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Threading;
using XTMF.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for StringRequestOverlay.xaml
    /// </summary>
    public partial class StringRequestOverlay : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty StringEntryCompleteDependencyProperty =
            DependencyProperty.Register("StringEntryComplete", typeof(RoutedEventHandler), typeof(StringRequestOverlay), new PropertyMetadata(null));

        public static readonly DependencyProperty StringEntryValueDependencyProperty =
            DependencyProperty.Register("EntryValue", typeof(string), typeof(StringRequestOverlay), new PropertyMetadata(null));

        public static readonly DependencyProperty DescriptionDependencyProperty =
            DependencyProperty.Register("Description", typeof(string), typeof(StringRequestOverlay), new PropertyMetadata(null));

        public static readonly DependencyProperty ExtraInfoDependencyProperty =
            DependencyProperty.Register("ExtraInfo", typeof(string), typeof(StringRequestOverlay), new PropertyMetadata(null));

        public StringRequestOverlay()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Reset()
        {
            ((FrameworkElement)Parent).Visibility = Visibility.Collapsed;
            Visibility = Visibility.Collapsed;
            StringInput.Clear();
            ClearListeners();

        }

        public string StringEntryValue
        {
            get => StringInput.Text;
            set => SetValue(StringEntryValueDependencyProperty, value);
        }

        public String ExtraInfo
        {
            get => (string)GetValue(ExtraInfoDependencyProperty);
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    ExtraInfoLabel.Visibility = Visibility.Visible;
                    ExtraInfoLabel.Content = value;
                }
                else
                {
                    ExtraInfoLabel.Visibility = Visibility.Collapsed;
                }
                SetValue(ExtraInfoDependencyProperty, value);
            }
        }

        public string Description
        {
            get => (string)GetValue(DescriptionDependencyProperty);
            set
            {
                SetValue(DescriptionDependencyProperty, value);
                DescriptionLabel.Content = value;
            }
        }

        public RoutedEventHandler StringEntryComplete
        {
            get => (RoutedEventHandler)GetValue(StringEntryCompleteDependencyProperty);
            set => SetValue(StringEntryCompleteDependencyProperty, value);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ClearListeners()
        {
            if (StringEntryComplete != null)
            {
                foreach (Delegate d in StringEntryComplete.GetInvocationList())
                {
                    if (StringEntryComplete != null)
                    {
                        StringEntryComplete -= d as RoutedEventHandler;
                    }
                }
            }
        }

        private void FlatButton_OnClick(object sender, RoutedEventArgs e) => StringEntryComplete?.Invoke(this, new RoutedEventArgs());

        private void StringInput_OnKeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    StringEntryComplete?.Invoke(this, new RoutedEventArgs());
                    break;
                case Key.Escape:
                    Reset();
                    e.Handled = true;
                    break;
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    Keyboard.Focus(StringInput);
                }, DispatcherPriority.Render);
            }
        }
    }
}
