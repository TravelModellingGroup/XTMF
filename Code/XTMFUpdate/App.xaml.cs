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
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace XTMFUpdate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string RunAppAfterComplete = null;

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if ( e.ApplicationExitCode == 0 )
            {
                if ( this.RunAppAfterComplete != null && File.Exists( this.RunAppAfterComplete ) )
                {
                    Process.Start( this.RunAppAfterComplete );
                }
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var args = e.Args;
            if ( args != null && args.Length != 0 )
            {
                this.RunAppAfterComplete = args[0];
            }
            else
            {
                this.RunAppAfterComplete = "XTMF.GUI.exe";
            }
        }
    }
}