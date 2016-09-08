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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;
namespace TMG.Frameworks.FileManagement
{
    [ModuleInformation(Description = "This module will recursively delete all files from a given directory and its children of a given type.")]
    public class DeleteFilesOfType : XTMF.ISelfContainedModule
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Required = true, Description = "The directory to delete from")]
        public FileLocation DirectoryToDeleteFrom;

        [RunParameter("Extension", "txt", "The extension name to delete.")]
        public string Extension;

        private void DeleteFrom(DirectoryInfo dir)
        {
            if (dir.Exists)
            {
                foreach(var file in dir.EnumerateFiles("*." + Extension).ToList())
                {
                    file.Delete();
                }
                foreach(var sub in dir.GetDirectories())
                {
                    DeleteFrom(sub);
                }
            }
        }

        public void Start()
        {
            var path = DirectoryToDeleteFrom.GetFilePath();
            DeleteFrom(new DirectoryInfo(path));
        }
    }

}
