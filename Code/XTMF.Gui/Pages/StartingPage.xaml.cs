/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Reflection;
namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for StartingPage.xaml
    /// </summary>
    public partial class StartingPage : UserControl, IXTMFPage
    {
        private const string UpdateProgram = "XTMF.Update2.exe";
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage };
        private string DocumentationName = "TMG XTMF Documentation.pdf";
        private SingleWindowGUI XTMFGUI;

        public StartingPage(SingleWindowGUI mainWindow)
        {
            this.XTMFGUI = mainWindow;
            InitializeComponent();
            if ( !System.IO.File.Exists( UpdateProgram ) )
            {
                this.UpdateButton.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                this.UpdateButton.Visibility = System.Windows.Visibility.Visible;
            }
            var versionFileName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:///","")),
                "version.txt");
            if ( File.Exists( versionFileName ) )
            {
                var text = File.ReadAllText( versionFileName );
                text = text.Replace("\r\n", "");
                this.VersionText.Text = "version: " + text;
            }
            else
            {
                this.VersionText.Text = "version: Unknown";
            }
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
        }

        private void GoToModelSystems_Click(object sender)
        {
            this.XTMFGUI.Navigate( XTMFPage.SelectModelSystemPage, this );
        }

        private void GoToProjects_Click(object sender)
        {
            this.XTMFGUI.Navigate( XTMFPage.ProjectSelectPage );
        }

        private void Help_Clicked(object obj)
        {
            try
            {
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                System.Diagnostics.Process.Start( System.IO.Path.Combine( System.IO.Path.GetDirectoryName( path ), this.DocumentationName ) );
            }
            catch
            {
                MessageBox.Show( "We were unable to find the documentation", "Documentation Missing!", MessageBoxButton.OK, MessageBoxImage.Error );
            }
        }

        private void Import_Clicked(object obj)
        {
            this.XTMFGUI.Navigate( XTMFPage.ImportPage );
        }

        private void Settings_Clicked(object obj)
        {
            this.XTMFGUI.Navigate( XTMFPage.SettingsPage );
        }

        private void Update_Clicked(object obj)
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            System.Diagnostics.Process.Start( System.IO.Path.Combine( System.IO.Path.GetDirectoryName( path ), UpdateProgram ), "\"" + path + "\"" );
            this.XTMFGUI.Close();
        }

        private void About_Clicked(object obj)
        {
            this.XTMFGUI.Navigate( XTMFPage.AboutPage );
        }
    }
}