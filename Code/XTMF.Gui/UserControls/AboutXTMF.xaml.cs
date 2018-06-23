/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XTMF.Gui.Controllers;
using IOPath = System.IO.Path;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for AboutXTMF.xaml
    /// </summary>
    public partial class AboutXTMF : Window
    {
        public AboutXTMF()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            VersionBlock.Text = Assembly.GetEntryAssembly().GetName().Version.ToString();
            BuildBlock.Text = EditorController.Runtime.Configuration.BuildDate;
            NumberOfModules.Text = EditorController.Runtime.Configuration.ModelRepository.Modules.Count.ToString();
        }

        private string GetVersionText()
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var licenseFile = IOPath.Combine(IOPath.GetDirectoryName(assemblyLocation), "license.txt");
            var versionFile = IOPath.Combine(IOPath.GetDirectoryName(assemblyLocation), "version.txt");
            try
            {
                using (StreamReader reader = new StreamReader(versionFile))
                {
                    return reader.ReadLine();
                }
            }
            catch
            {
                return "Unknown Version";
            }
        }

        private void TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Process.Start(textBlock.Text);
            }
        }
    }
}
