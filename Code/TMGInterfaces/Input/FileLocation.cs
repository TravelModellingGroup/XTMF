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
using System.IO;
using System.Reflection;
using XTMF;

namespace TMG.Input
{
    public abstract class FileLocation : IModule
    {
        public string Name { get; set; }

        public float Progress { get { return 0f; } }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        public abstract string GetFilePath();

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public static implicit operator String(FileLocation fileLocation)
        {
            return fileLocation.GetFilePath();
        }
    }

    [ModuleInformation(
        Description = "This module provides the ability to specify a file path, broken into two parts.  The directory is relative to the input directory unless a full path is given."
        )]
    public class DirectorySeperatedPathFromInputDirectory : FileLocation
    {
        [RunParameter("Directory Relative To Input Directory", "", typeof(FileFromInputDirectory), "A directory path to represent relative to the input directory.")]
        public FileFromInputDirectory DirectoryName;

        [RunParameter("File Name", "File.Type", "The file relative to the given directory path.")]
        public string FileName;

        [RootModule]
        public IModelSystemTemplate Root;

        public override string GetFilePath()
        {
            var directoryName = this.DirectoryName.GetFileName(this.Root.InputBaseDirectory);
            if(String.IsNullOrEmpty(directoryName))
            {
                return FileName;
            }
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return Path.Combine(directoryName, FileName);
        }
    }

    [ModuleInformation(
    Description = "This module provides the ability to specify a file path relative to the input directory unless a full path is given."
    )]
    public class FilePathFromInputDirectory : FileLocation
    {
        [RunParameter("File From Input Directory", "Filename.type", typeof(FileFromInputDirectory), "A file path to represent relative to the input directory.")]
        public FileFromInputDirectory FileName;

        [RootModule]
        public IModelSystemTemplate Root;

        public override string GetFilePath()
        {
            return this.FileName.GetFileName(this.Root.InputBaseDirectory);
        }
    }

    [ModuleInformation(
    Description = "This module provides the ability to specify a file path, broken into two parts.  The directory is relative to the output directory unless a full path is given."
    )]
    public class DirectorySeperatedPathFromOutputDirectory : FileLocation
    {
        [RunParameter("Directory Relative To Run Directory", "", typeof(FileFromInputDirectory), "A directory path to represent relative to the run directory.")]
        public FileFromOutputDirectory DirectoryName;

        [RunParameter("File Name", "File.Type", "The file relative to the given directory path.")]
        public string FileName;

        [RootModule]
        public IModelSystemTemplate Root;

        public override string GetFilePath()
        {
            var name = DirectoryName.GetFileName();
            if(!String.IsNullOrEmpty(name))
            {
                if(!Directory.Exists(name))
                {
                    Directory.CreateDirectory(name);
                }
                return Path.Combine(name, FileName);
            }
            return FileName;
        }
    }

    [ModuleInformation(
Description = "This module provides the ability to specify a file path relative to the output directory unless a full path is given."
)]
    public class FilePathFromOuputDirectory : FileLocation
    {
        [RunParameter("File From Output Directory", "Filename.type", typeof(FileFromOutputDirectory), "A file path to represent relative to the run's directory.")]
        public FileFromOutputDirectory FileName;

        public override string GetFilePath()
        {
            return this.FileName.GetFileName();
        }
    }

    [ModuleInformation(
Description = "This module provides the ability to specify a file path relative to the directory that contains XTMF unless a full path is given."
)]
    public class FilePathFromXTMFDirectory : FileLocation
    {
        [RunParameter("File From XTMF Installation", "Filename.type", typeof(FileFromOutputDirectory), "A path relative to the installation directory of XTMF.")]
        public FileFromOutputDirectory PathFromXTMFInstall;

        public override string GetFilePath()
        {
            var relativePath = PathFromXTMFInstall.GetFileName();
            if(Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }
            return Path.Combine(GetXTMFDirectory(), relativePath);
        }

        private string GetXTMFDirectory()
        {
            return Path.GetFullPath(Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase.Replace("file:///", String.Empty)));
        }
    }
}