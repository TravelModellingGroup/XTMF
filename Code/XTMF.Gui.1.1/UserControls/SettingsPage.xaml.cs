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
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Controllers;
using System.Collections.Generic;
using System.Linq;
using MaterialDesignColors;

namespace XTMF.Gui.UserControls
{
    using Dragablz.Themes;

    using MaterialDesignThemes.Wpf.Transitions;

    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : UserControl
    {
        private Configuration Configuration => EditorController.Runtime.Configuration;

        public SettingsPage()
        {
            DataContext = new SettingsModel();
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e) => Keyboard.Focus(ProjectDirectoryBox);

        public void Close() => ((SettingsModel)DataContext).Unbind();

        public class SettingsModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private string _projectDirectory;

            public Swatch PrimarySwatch { get; set; }

            public Swatch AccentSwatch { get; set; }

            public IEnumerable<Swatch> Swatches { get; }

            public IEnumerable<Swatch> AccentSwatches { get; }

            public bool IsDarkTheme { get; set; }

            public string LocalHostButtonName => Configuration.IsLocalConfiguration ?
                "Delete Local XTMF Configuration" :
                "Create Local XTMF Configuration";

            public void UpdateButtons()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalHostButtonName)));
            }

            private void UpdateAll()
            {
                _projectDirectory = Configuration.ProjectDirectory;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalHostButtonName)));
                _modelSystemDirectory = Configuration.ModelSystemDirectory;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_modelSystemDirectory)));
                _hostPort = Configuration.HostPort;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_hostPort)));
                if (Configuration.PrimaryColour != null)
                {
                    PrimarySwatch = Swatches.First((s) => s.Name == Configuration.PrimaryColour);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimarySwatch)));
                }
                if (Configuration.AccentColour != null)
                {
                    AccentSwatch = AccentSwatches.First((s) => s.Name == Configuration.AccentColour);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccentSwatch)));
                }
                IsDarkTheme = Configuration.IsDarkTheme;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDarkTheme)));
            }

            public void Save(string configurationFileName = null)
            {
                if (configurationFileName == null)
                {
                    Configuration.Save();
                }
                else
                {
                    Configuration.Save(configurationFileName, true);
                }
                UpdateButtons();
            }

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
                            Configuration.ReloadProjects();
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ProjectDirectory");
                            Save();
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
                            Configuration.ReloadModelSystems();
                            ModelHelper.PropertyChanged(PropertyChanged, this, "ModelSystemDirectory");
                            Save();
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
                        Save();
                    }
                }
            }

            private Configuration Configuration => EditorController.Runtime.Configuration;

            public SettingsModel()
            {
                Swatches = new SwatchesProvider().Swatches;
                AccentSwatches = new SwatchesProvider().Swatches.Where((swatch) => swatch.IsAccented);
                _projectDirectory = Configuration.ProjectDirectory;
                _modelSystemDirectory = Configuration.ModelSystemDirectory;
                _hostPort = Configuration.HostPort;
                Configuration.PropertyChanged += Configuration_PropertyChanged;
                if (Configuration.PrimaryColour != null)
                {
                    PrimarySwatch = Swatches.First((s) => s.Name == Configuration.PrimaryColour);
                }
                if (Configuration.AccentColour != null)
                {
                    AccentSwatch = AccentSwatches.First((s) => s.Name == Configuration.AccentColour);
                }
                IsDarkTheme = Configuration.IsDarkTheme;
            }

            private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                ProjectDirectory = Configuration.ProjectDirectory;
                ModelSystemDirectory = Configuration.ModelSystemDirectory;
                HostPort = Configuration.HostPort;
            }

            internal void Unbind() => Configuration.PropertyChanged -= Configuration_PropertyChanged;

            internal void DeleteLocalConfiguration()
            {
                var result = MessageBox.Show(MainWindow.Us,
                    "Are you sure you wish to delete the local configuration?",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Configuration.DeleteConfiguration();
                }
                UpdateAll();
            }

            internal void CreateLocalConfiguration()
            {
                /* Reload the entire UI overriding the configuration file to be loaded */
                if (MessageBox.Show(MainWindow.Us,
                    "XTMF will reload after creating a local configuration. Do you wish to continue?", "Switch to new configuration",
                    MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocalXTMFConfiguration.xml");
                    Configuration.Save(path, true);
                }
                UpdateButtons();
            }
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
                Configuration.Save();
                MessageBoxResult result = MessageBox.Show(MainWindow.Us, "Do you wish to reload the XMTF interface with updated settings?", "Updated settings", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    MainWindow.Us.Reload();
                }
            }
        }

        private void Select_ProjectDirectory(object sender, RoutedEventArgs e)
        {
            var dir = MainWindow.OpenDirectory();
            if (dir != null)
            {
                ((SettingsModel)DataContext).ProjectDirectory = dir;
                Configuration.Save();
                MessageBoxResult result = MessageBox.Show(MainWindow.Us, "Do you wish to reload the XMTF interface with updated settings?", "Updated settings", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    MainWindow.Us.Reload();
                }
            }
        }

        private void CreateLocalConfigButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void SwitchLocalConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var context = (SettingsModel)DataContext;
            if (Configuration.IsLocalConfiguration)
            {
                context.DeleteLocalConfiguration();
            }
            else
            {
                context.CreateLocalConfiguration();

            }
            context.UpdateButtons();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                new PaletteHelper().SetLightDark((bool)ThemeBaseToggleButton.IsChecked);
                if (Configuration is Configuration configuration)
                {
                    configuration.IsDarkTheme = (bool)ThemeBaseToggleButton.IsChecked;
                    Configuration.Save();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThemeBaseToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                new PaletteHelper().SetLightDark((bool)ThemeBaseToggleButton.IsChecked);
                if (Configuration is Configuration configuration)
                {
                    configuration.IsDarkTheme = (bool)ThemeBaseToggleButton.IsChecked;
                    Configuration.Save();
                }
            }
        }

        /// <summary>
        /// Activates and replaces primary display colour.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrimaryColourComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                if (Configuration is Configuration configuration)
                {
                    new PaletteHelper().ReplacePrimaryColor((Swatch)PrimaryColourComboBox.SelectedItem);
                    configuration.PrimaryColour = ((Swatch)PrimaryColourComboBox.SelectedItem).Name;
                    Configuration.Save();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AccentColourComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                if (Configuration is Configuration configuration)
                {
                    new PaletteHelper().ReplaceAccentColor((Swatch)AccentColourComboBox.SelectedItem);
                    configuration.AccentColour = ((Swatch)AccentColourComboBox.SelectedItem).Name;
                    Configuration.Save();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisableTransitionsToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                //MainWindow.Us.SetValue(MaterialDesignAssist.)
                TransitionAssist.SetDisableTransitions(MainWindow.Us, true);

                if (Configuration is Configuration configuration)
                {
                    configuration.IsDisableTransitionAnimations = (bool)DisableTransitionsToggleButton.IsChecked;
                    Configuration.Save();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisableTransitionsToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {

                TransitionAssist.SetDisableTransitions(MainWindow.Us, false);
                if (Configuration is Configuration configuration)
                {
                    configuration.IsDisableTransitionAnimations = (bool)DisableTransitionsToggleButton.IsChecked;
                    Configuration.Save();
                }
            }
        }
    }
}
