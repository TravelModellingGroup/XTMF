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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Reflection;
using IOPath = System.IO.Path;
namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : IXTMFPage
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        bool FirstLoad = true;

        public AboutPage(SingleWindowGUI XTMF)
            : this()
        {

        }

        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.AboutPage };

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            if ( this.FirstLoad )
            {
                LoadLicense();
                this.FirstLoad = false;
            }
        }

        private void LoadLicense()
        {
            var assemblyLocation = Assembly.GetEntryAssembly().CodeBase.Replace( "file:///", "" );
            var licenseFile = IOPath.Combine( IOPath.GetDirectoryName( assemblyLocation ), "license.txt" );
            if ( File.Exists( licenseFile ) )
            {
                this.LicenseBlock.Text = File.ReadAllText( licenseFile );
            }
            else
            {
                this.LicenseBlock.Text = "Unable to open the license file!";
            }
        }
    }
}
