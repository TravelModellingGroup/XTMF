/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Forms;
using System.Diagnostics;

namespace XTMF.Update
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length >= 2 && int.TryParse(args[0], out int processID))
            {
                try
                {
                    var p = Process.GetProcessById(processID);
                    Application.Run(new XTMFUpdateForm() { ParentProcess = p, LaunchPoint = args[1] });
                }
                catch
                {
                    Application.Run(new XTMFUpdateForm());
                }
            }
            else
            {
                Application.Run(new XTMFUpdateForm());
            }
        }


    }
}
