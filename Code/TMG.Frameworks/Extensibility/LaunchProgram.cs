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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using TMG.Input;
using XTMF;
using System.ComponentModel;
using System.IO;

namespace TMG.Frameworks.Extensibility
{
    [ModuleInformation(
        Description = "This module is designed to execute an external program.  It can optionally wait for the process to complete."+
        " When using the multi-run framework, assign to the ShutdownProgram parameter in order to force the process to exit."
        )]
    public class LaunchProgram : ISelfContainedModule
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Required = true, Description = "The program to execute.")]
        public FileLocation Program;

        [RunParameter("Wait For Exit", false, "Should we wait for the program to exit before continuing?")]
        public bool WaitForExit;

        [RunParameter("Arguments", "", "Optional: The arguments to send to the program at launch.")]
        public string Arguments;

        [Parameter("ShutdownProgram", false, "When this variable is assigned to, the running process will be killed.")]
        public bool ShutdownProgram
        {
            get
            {
                return false;
            }
            set
            {
                var process = RunningProcess;
                if(process != null)
                {
                    try
                    {
                        if(!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                    }
                    catch
                    {

                    }
                }
            }
        }

        private Process RunningProcess;

        public void Start()
        {
            try
            {
                var process = RunningProcess = Process.Start(Program, Arguments);
                if(WaitForExit)
                {
                    process.WaitForExit();
                }
            }
            catch (Win32Exception)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were unable to execute the program '" + Program.GetFilePath() + "'!");
            }
            catch(FileNotFoundException)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were to find the program '" + Program.GetFilePath() + "'!");
            }
        }
    }

}
