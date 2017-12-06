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
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Controllers;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : UserControl
    {

        private readonly IConfiguration _configuration;

        public SettingsPage(Configuration configuration)
        {
            _configuration = configuration;
            DataContext = new SettingsModel(configuration);
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            if (MainWindow.Us.IsNonDefaultConfig)
            {
                NonStandardConfigLabel.Visibility = Visibility.Visible;
                ConfigLocationLabel.Content = MainWindow.Us.ConfigurationFilePath;
                ConfigLocationLabel.Visibility = Visibility.Visible;
                CreateLocalConfigButton.Visibility = Visibility.Collapsed;
                if (MainWindow.Us.IsLocalConfig)
                {
                    DeleteConfigLabel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CreateLocalConfigButton.Visibility = Visibility.Visible;
                DeleteConfigLabel.Visibility = Visibility.Collapsed;
            }
            foreach (var theme in MainWindow.Us.ThemeController.Themes)
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem
                {
                    Content = theme.Name,
                    Tag = theme
                };
                ThemeComboBox.Items.Add(comboBoxItem);
            }
            if (((Configuration) _configuration).Theme != null)
            {
                foreach (ComboBoxItem item in ThemeComboBox.Items)
                {
                    if ((string)item.Content == ((Configuration) _configuration).Theme)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }
            else
            {
                ThemeComboBox.SelectedIndex = 0;
            } 
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e) => Keyboard.Focus(ProjectDirectoryBox);

        public void Close() => ((SettingsModel)DataContext).Unbind();

        public class SettingsModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private string _projectDirectory;
            public string ProjectDirectory
            {
                get => _projectDirectory;
                set
                {
                    if (_projectDirectory != value)
                    {
                        string error = null;
                        if (Configuration.SetProjectDirectory(value, ref error))
                        {
                            _projectDirectory = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ProjectDirectory");
                            Configuration.Save();
                        }
                        else
                        {
                            MessageBox.Show(MainWindow.Us, "Unable to save project directory: " + error, "Unable to set directory", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

            private string _modelSystemDirectory;
            public string ModelSystemDirectory
            {
                get => _modelSystemDirectory;
                set
                {
                    if (_modelSystemDirectory != value)
                    {
                        string error = null;
                        if (Configuration.SetModelSystemDirectory(value, ref error))
                        {
                            _modelSystemDirectory = value;
                            Configuration.ModelSystemDirectory = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ModelSystemDirectory");
                            Configuration.Save();
                        }
                        else
                        {
                            MessageBox.Show(MainWindow.Us, "Unable to save model system directory: " + error, "Unable to set directory", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

            private int _hostPort;
            public int HostPort
            {
                get => _hostPort;
                set
                {
                    if (_hostPort != value)
                    {
                        _hostPort = value;
                        Configuration.HostPort = _hostPort;
                        ModelHelper.PropertyChanged(PropertyChanged, this, "HostPort");
                        Configuration.Save();
                    }
                }
            }

            private readonly Configuration Configuration;

            public SettingsModel(Configuration config)
            {
                Configuration = config;
                _projectDirectory = config.ProjectDirectory;
                _modelSystemDirectory = config.ModelSystemDirectory;
                _hostPort = config.HostPort;
                Configuration.PropertyChanged += Configuration_PropertyChanged;
            }

            private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                ProjectDirectory = Configuration.ProjectDirectory;
                ModelSystemDirectory = Configuration.ModelSystemDirectory;
                HostPort = Configuration.HostPort;
            }

            internal void Unbind() => Configuration.PropertyChanged -= Configuration_PropertyChanged;
        }

        private void HintedTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (sender is UIElement element)
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
            if (dir != null)
            {
                ((SettingsModel)DataContext).ModelSystemDirectory = dir;
                _configuration.Save();
                MessageBoxResult result = MessageBox.Show(MainWindow.Us, "Do you wish to reload the XMTF interface with updated settings?", "Updated settings", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    if (MainWindow.Us.IsNonDefaultConfig)
                    {
                        MainWindow.Us.ReloadWithConfiguration(MainWindow.Us.ConfigurationFilePath);
                    }
                    else
                    {
                        MainWindow.Us.ReloadWithDefaultConfiguration();
                    }
                }
            }
        }

        private void Select_ProjectDirectory(object sender, RoutedEventArgs e)
        {
            var dir = MainWindow.OpenDirectory();
            if (dir != null)
            {
                ((SettingsModel)DataContext).ProjectDirectory = dir;
                _configuration.Save();
                MessageBoxResult result = MessageBox.Show(MainWindow.Us, "Do you wish to reload the XMTF interface with updated settings?", "Updated settings", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    if (MainWindow.Us.IsNonDefaultConfig)
                    {
                        MainWindow.Us.ReloadWithConfiguration(MainWindow.Us.ConfigurationFilePath);
                    }
                    else
                    {
                        MainWindow.Us.ReloadWithDefaultConfiguration();
                    }
                }
            }
        }

        private void CreateLocalConfigButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void CreateLocalConfigButton_Click(object sender, RoutedEventArgs e)
        {
            /* Reload the entire UI overriding the the configuration file to be loaded */
            if (MessageBox.Show(MainWindow.Us,
                "XTMF will reload after creating a local configuration. Do you wish to continue?", "Switch to new configuration", 
                MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
            {
                MainWindow.Us.IsLocalConfig = true;
                MainWindow.Us.ReloadWithConfiguration(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml"));
            }
        }

        private void DeleteConfigLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show(MainWindow.Us,
                "Are you sure you wish to delete the local configuration?", 
                "Confirm Deletion",
                MessageBoxButton.YesNo, 
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                File.Delete(MainWindow.Us.ConfigurationFilePath);
                MainWindow.Us.ReloadWithDefaultConfiguration();
            }
        }

       

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                if (_configuration is Configuration configuration)
                {
                    configuration.Theme = ((ThemeController.Theme)((ComboBoxItem)ThemeComboBox.SelectedItem).Tag).Name;
                    MainWindow.Us.ApplyTheme(((ThemeController.Theme)((ComboBoxItem)ThemeComboBox.SelectedItem).Tag));
                    _configuration.Save();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            new PaletteHelper().SetLightDark((bool) ThemeBaseToggleButton.IsChecked);
            if (_configuration is Configuration configuration)
            {
                configuration.IsDarkTheme = (bool) ThemeBaseToggleButton.IsChecked;
                _configuration.Save();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThemeBaseToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            new PaletteHelper().SetLightDark((bool)ThemeBaseToggleButton.IsChecked);
            if (_configuration is Configuration configuration)
            {
                configuration.IsDarkTheme = (bool)ThemeBaseToggleButton.IsChecked;
                _configuration.Save();
            }
        }
    }
}
