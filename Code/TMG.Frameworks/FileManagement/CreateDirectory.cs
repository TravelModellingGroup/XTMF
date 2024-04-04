/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.FileManagement;

[ModuleInformation(Description = "Creates a directory at the specified location if it doesn't already exist.")]
public sealed class CreateDirectory : ISelfContainedModule
{
    [SubModelInformation(Required = true, Description = "The path to the directory we will create.")]
    public FileLocation DirectoryToCreate;

    public void Start()
    {
        string path = "";
        try
        {
            path = DirectoryToCreate.GetFilePath();
            Directory.CreateDirectory(path);
        }
        catch (IOException e)
        {
            throw new XTMFRuntimeException(this, e, $"Unable to create directory at '{path}'!\r\n{e.Message}");
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
