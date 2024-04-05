/*
    Copyright 2015-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMG.Frameworks.MultiRun;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Extensibility
{
    [ModuleInformation(
        Description =
            "This module is designed to execute an external program.  It can optionally wait for the process to complete." +
            " When using the multi-run framework, assign to the ShutdownProgram parameter in order to force the process to exit."
    )]
    public class LaunchProgram : ISelfContainedModule
    {
        [RunParameter("Arguments", "", "Optional: The arguments to send to the program at launch.")]
        public string Arguments;

        [SubModelInformation(Required = false, Description = "File paths to pass in as arguments in order.  These arguments will be added to the end of the 'Arguments' parameter.")]
        public FileLocation[] FileArguments;

        private readonly IConfiguration Config;

        [SubModelInformation(Required = true, Description = "The program to execute.")]
        public FileLocation Program;

        private Process RunningProcess;

        [RunParameter("Wait For Exit", false, "Should we wait for the program to exit before continuing?")]
        public bool WaitForExit;

        [RunParameter("Throw if bad exit code", false, "Should the module throw if the error code is not zero.  Requires WaitForExist to be true.")]
        public bool ThrowIfBadExitCode;

        [RunParameter("Working Directory", ".", "The directory for the program to use as the starting point for relative paths.")]
        public string WorkingDirectory;

        [RunParameter("Route Console Outputs", true, "Should we pass along the console outputs to XTMF Console?")]
        public bool RouteConsoleOutputs;

        public LaunchProgram(IConfiguration config)
        {
            Config = config;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        public void Start()
        {
            try
            {
                var arguments = CreateArguments();
                Console.WriteLine($"Running Program \"{Program}\" {arguments}");
                var process = RunningProcess = Process.Start(new ProcessStartInfo(Program)
                {
                    Arguments = arguments,
                    WorkingDirectory = WorkingDirectory,
                    CreateNoWindow = !RouteConsoleOutputs,
                });

                if (process != null && WaitForExit)
                {
                    process.WaitForExit();
                    if (ThrowIfBadExitCode && process.ExitCode != 0)
                    {
                        throw new XTMFRuntimeException(this, $"The external program ended with an error code {process.ExitCode}!");
                    }
                }
            }
            catch (Win32Exception)
            {
                throw new XTMFRuntimeException(this,
                    "In '" + Name + "' we were unable to execute the program '" + Program.GetFilePath() + "'!");
            }
            catch (FileNotFoundException)
            {
                throw new XTMFRuntimeException(this,
                    "In '" + Name + "' we were to find the program '" + Program.GetFilePath() + "'!");
            }
        }

        private string CreateArguments()
        {
            return string.IsNullOrWhiteSpace(Arguments) ?
                  string.Join(" ", FileArguments.Select(x => AddQuotes(Path.GetFullPath(x.GetFilePath()))))
                : Arguments + " " + string.Join(" ", FileArguments.Select(x => AddQuotes(Path.GetFullPath(x.GetFilePath()))));
        }

        private static string AddQuotes(string filePath)
        {
            // Escape the quotes on the inside and add quotes to the outside
            return string.Concat("\"", filePath.Replace("\"", "\\\""), "\"");
        }

        public bool RuntimeValidation(ref string error)
        {
            IModelSystemStructure us = null;
            if (!ModelSystemReflection.FindModuleStructure(Config.ProjectRepository.ActiveProject, this, ref us))
            {
                error = "In '" + Name + "' we were unable to find ourselves!";
                return false;
            }
            if (!WaitForExit && ThrowIfBadExitCode)
            {
                error = "If you want to throw if there is a bad exit code, you must also have WaitForExit set to True!";
                return false;
            }
            AddMultiRunCommand();
            return true;
        }

        public void ShutdownProgram()
        {
            var process = RunningProcess;
            if (process != null)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
        }

        #region AddMultiRunCommand

        private void AddMultiRunCommand()
        {
            var listToUs = ModelSystemReflection.BuildModelStructureChain(Config, this);
            for (var i = listToUs.Count - 1; i >= 0; i--)
            {
                if (listToUs[i].Module is MultiRunModelSystem multiRunFramework)
                {
                    multiRunFramework.TryAddBatchCommand("LaunchProgram.ShutdownExternalProgram", node =>
                    {
                        var path = multiRunFramework.GetAttributeOrError(node, "Path",
                            "In 'LaunchProgram.ShutdownExternalProgram' we were unable to find an xml attribute called 'Path'!\r\nPlease add this to your batch script.");
                        IModelSystemStructure selectedModule = null;
                        IModelSystemStructure multiRunFrameworkChild = null;
                        if (!ModelSystemReflection.FindModuleStructure(Config, multiRunFramework.Child,
                            ref multiRunFrameworkChild))
                        {
                            throw new XTMFRuntimeException(this,
                                "We were unable to find the multi-run frameworks child module's model system structure!");
                        }

                        if (!ModelSystemReflection.GetModelSystemStructureFromPath(multiRunFrameworkChild, path,
                            ref selectedModule))
                        {
                            throw new XTMFRuntimeException(this,
                                "We were unable to find a module with the path '" + path + "'!");
                        }

                        var toShutdown = selectedModule.Module as LaunchProgram;
                        if (toShutdown == null)
                        {
                            throw new XTMFRuntimeException(this,
                                "The module with the path '" + path +
                                "' was not of type 'TMG.Frameworks.Extensibility.LaunchProgram'!");
                        }

                        toShutdown.ShutdownProgram();
                    }, true);
                    break;
                }
            }
        }

        #endregion
    }
}