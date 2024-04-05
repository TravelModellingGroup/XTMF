﻿/*
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

namespace XTMF.Testing.Modules.Editing
{
    public class TestSpecificGenericModuleMST : IModelSystemTemplate
    {
        [RunParameter("Input Directory", "../../Input", "The input directory.")]
        public string InputBaseDirectory { get; set; } = string.Empty;

        [RunParameter("SecondaryString", "", "Another string parameter")]
        public string SecondaryString = string.Empty;

        public IGenericInterface<float,float, float, float>? MyChild;

        public string Name { get; set; } = string.Empty;

        public string OutputBaseDirectory { get; set; } = string.Empty;

        public float Progress { get; } = 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string? error)
        {
            return true;
        }

        public void Start()
        {
            
        }
    }
}
