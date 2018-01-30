/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : UserControl
    {
        private List<Label> _RecentProjectLabels = new List<Label>();

        public StartWindow()
        {
            InitializeComponent();
            MainWindow.Us.RecentProjectsUpdated += Us_RecentProjectsUpdated;
            if (MainWindow.Us.RuntimeAvailable)
            {
                Us_RecentProjectsUpdated(null, null);
            }
        }

        private void Us_RecentProjectsUpdated(object sender, EventArgs e)
        {
            var k = Application.Current.FindResource("HoverLabel");
            if (RecentProjectsStackPanel.Children.Count != MainWindow.Us.RecentProjects.Count)
            {
                RecentProjectsStackPanel.Children.Clear();
                foreach (var recentProject in MainWindow.Us.RecentProjects)
                {

                    Button b = new Button();
                    b.Content = recentProject;
                    b.HorizontalContentAlignment = HorizontalAlignment.Left;
                    b.Style = (Style)FindResource("MaterialDesignFlatButton");
                    b.Click += (senderx, EventArgs) => MainWindow.Us.LoadProjectByName(recentProject);
                    RecentProjectsStackPanel.Children.Add(b);
                }
            }
        }

        private void NewProject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            MainWindow.Us.NewProject();
        }

        private void OpenProject_MouseUp(object sender, MouseButtonEventArgs e) => MainWindow.Us.OpenProject();

        private void NewModelSystem_MouseUp(object sender, MouseButtonEventArgs e) => MainWindow.Us.NewModelSystem();

        private void OpenModelSystem_MouseUp(object sender, MouseButtonEventArgs e) => MainWindow.Us.OpenModelSystem();

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
          
            MainWindow.Us.NewProject();
        }

        private void OpenProjectButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.OpenProject();
        }
    }
}
