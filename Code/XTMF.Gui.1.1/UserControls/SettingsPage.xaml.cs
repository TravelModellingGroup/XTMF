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
using System.ComponentModel;
using System.Linq;
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
using XTMF;
namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : UserControl
    {

        private IConfiguration Configuration;

        public SettingsPage(Configuration configuration)
        {
            Configuration = configuration;
            DataContext = new SettingsModel(configuration);
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(ProjectDirectoryBox);
        }

        public void Close()
        {
            ((SettingsModel)DataContext).Unbind();
        }

        public class SettingsModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private string _ProjectDirectory;
            public string ProjectDirectory
            {
                get
                {
                    return _ProjectDirectory;
                }
                set
                {
                    if (_ProjectDirectory != value)
                    {
                        string error = null;
                        if (Configuration.SetProjectDirectory(value, ref error))
                        {
                            _ProjectDirectory = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ProjectDirectory");
                            Configuration.Save();
                        }
                    }
                }
            }

            private string _ModelSystemDirectory;
            public string ModelSystemDirectory
            {
                get
                {
                    return _ModelSystemDirectory;
                }
                set
                {
                    if (_ModelSystemDirectory != value)
                    {
                        string error = null;
                        if (Configuration.SetModelSystemDirectory(value, ref error))
                        {
                            _ModelSystemDirectory = value;
                            Configuration.ModelSystemDirectory = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ModelSystemDirectory");
                            Configuration.Save();
                        }
                    }
                }
            }

            private int _HostPort;
            public int HostPort
            {
                get
                {
                    return _HostPort;
                }
                set
                {
                    if (_HostPort != value)
                    {
                        _HostPort = value;
                        Configuration.HostPort = _HostPort;
                        ModelHelper.PropertyChanged(PropertyChanged, this, "HostPort");
                        Configuration.Save();
                    }
                }
            }

            private Configuration Configuration;

            public SettingsModel(Configuration config)
            {
                Configuration = config;
                _ProjectDirectory = config.ProjectDirectory;
                _ModelSystemDirectory = config.ModelSystemDirectory;
                _HostPort = config.HostPort;
                Configuration.PropertyChanged += Configuration_PropertyChanged;
            }

            private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                ProjectDirectory = Configuration.ProjectDirectory;
                ModelSystemDirectory = Configuration.ModelSystemDirectory;
                HostPort = Configuration.HostPort;
            }

            internal void Unbind()
            {
                Configuration.PropertyChanged -= Configuration_PropertyChanged;
            }
        }

        private void HintedTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                var element = sender as UIElement;
                if (element != null)
                {
                    switch (e.Key)
                    {
                        case Key.Down:
                        case Key.Enter:
                            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                            e.Handled = true;
                            break;
                        case Key.Up:
                            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        private void Select_ModelSystemDirectory(object sender, RoutedEventArgs e)
        {
            var dir = MainWindow.OpenDirectory();
            if(dir != null)
            {
                ((SettingsModel)DataContext).ModelSystemDirectory = dir;
            }
        }

        private void Select_ProjectDirectory(object sender, RoutedEventArgs e)
        {
            var dir = MainWindow.OpenDirectory();
            if (dir != null)
            {
                ((SettingsModel)DataContext).ProjectDirectory = dir;
            }
        }
    }
}
