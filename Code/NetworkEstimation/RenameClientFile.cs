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
using TMG.Estimation;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation;

[ModuleInformation(Description= "Renames a file by appending ([Generation] - [Index]) to the filename. This allows users to " +
                                "use a client model system to produce multiples of one file type for each Task being assigned.")]
public class RenameClientFile : ISelfContainedModule
{

    [RootModule]
    public IEstimationClientModelSystem Root;

    [SubModelInformation(Description= "File to Rename", Required= true)]
    public FileLocation FileToRename;

    private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return _ProgressColour; }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Start()
    {
        var task = Root.CurrentTask;
        string id = task.Generation + "-" + task.Index;

        var filepath = FileToRename.GetFilePath();
        var newFilepath = Path.GetFileNameWithoutExtension(filepath) + "(" + id + ")" + Path.GetExtension(filepath);

        File.Move(filepath, newFilepath);
    }
}
