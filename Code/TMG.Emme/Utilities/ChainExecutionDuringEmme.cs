﻿/*
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
namespace TMG.Emme.Utilities;


public class ChainExecutionDuringEmme : IEmmeTool
{

    public string Name { get; set; }

    public float Progress
    {
        get { return _Progress(); }
    }

    private Func<float> _Progress = () => 0f;

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [SubModelInformation(Description = "The modules to execute while emme is open.  This is useful for processing data.")]
    public ISelfContainedModule[] Children;

    public bool Execute(Controller controller)
    {
        for(int i = 0; i < Children.Length; i++)
        {
            var localI = i;
            _Progress = () => Children[localI].Progress;
            Children[i].Start();
        }
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
