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
using System.IO;
using System.Threading;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description =
        @"This module is designed to create a temporary copy of the emme database an execute on that.")]
    public class SpawnEmmeCopyControllerDataSource : IDataSource<ModellerController>, IDisposable
    {
        [SubModelInformation(Required = true, Description = "The location of the Emme project file.")]
        public FileLocation ProjectFile;

        [RunParameter("Emme Databank", "", "The name of the emme databank to work with.  Leave this as empty to select the default.")]
        public string EmmeDatabank;

        [RunParameter("EmmePath", "", "Optional: The path to an EMME installation directory to use.  This will default to the one in the system's EMMEPath")]
        public string EmmePath;

        [SubModelInformation(Required = true, Description = "The location to base the temporary copy.")]
        public FileLocation TempBaseDirectory;

        [RunParameter("Delete On Exit", true, "Set this to false to keep the EMME project after the model system terminates.")]
        public bool DeleteOnExit;

        private ModellerController Controller;

        public ModellerController GiveData()
        {
            return Controller;
        }

        private string TempDirectory;

        public bool Loaded
        {
            get { return Controller != null; }
        }

        public void LoadData()
        {
            if (Controller == null)
            {
                lock (this)
                {
                    if (Controller == null)
                    {
                        GC.ReRegisterForFinalize(this);
                        var projectFile = ProjectFile.GetFilePath();
                        var originalDir = Path.GetDirectoryName(projectFile);
                        projectFile = Path.GetFileName(projectFile);
                        if (projectFile == null)
                        {
                            throw new XTMFRuntimeException(this, $"In {Name} we were unable to get the file name from {ProjectFile}!");
                        }
                        var dir = TempDirectory = TempBaseDirectory.GetFilePath();
                        DirectoryCopy(originalDir, dir);
                        var actuallyRunning = Path.GetFullPath(Path.Combine(dir, projectFile));
                        Console.WriteLine("Opening EMME at " + actuallyRunning);
                        Controller = new ModellerController(this, actuallyRunning, EmmeDatabank, String.IsNullOrWhiteSpace(EmmePath) ? null : EmmePath);
                    }
                }
            }
        }

        private void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirectory);
            DirectoryInfo[] dirs = dir.GetDirectories();
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirectory);
            }

            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destinationDirectory, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destinationDirectory, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        public void UnloadData()
        {
            //we don't dispose on unload
            //this.Dispose();
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        ~SpawnEmmeCopyControllerDataSource()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            Controller?.Dispose();
            Controller = null;
            // try to close for up to 2 seconds
            if (DeleteOnExit && TempDirectory != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Directory.Delete(TempDirectory, true);
                        return;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Thread.Sleep(200);
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(200);
                    }
                }
            }
        }
    }
}
