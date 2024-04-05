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
using XTMF;

namespace TMG.NetworkEstimation;

[ModuleInformation( Description =
    @"This module is designed to foce something else to only be able to be executed once." )]
public class ExecuteOnce : ISelfContainedModule
{
    [SubModelInformation( Required = true, Description = "The thing to only run once." )]
    public ISelfContainedModule ToRun;

    private bool Ran;

    public void Start()
    {
        if ( !Ran )
        {
            ToRun.Start();
            Ran = true;
        }
    }

    public string Name
    {
        get;
        set;
    }

    public float Progress => ToRun.Progress;

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return ToRun.ProgressColour; }
    }

    public bool RuntimeValidation(ref string error)
    {
        Ran = false;
        return true;
    }
}
