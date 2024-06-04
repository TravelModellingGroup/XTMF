﻿/*
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
using System.Linq;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "A group of modes that will be analyzed together.")]
public sealed class ModeGroup : IModule
{
    [ModuleInformation(Description = "A reference to a mode to bind to.")]
    public sealed class Mode : IModule
    {

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Name", "", "The name of the mode.")]
        public string ModeName;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        internal IMode LinkedMode;

        public bool RuntimeValidation(ref string error)
        {
            if ((LinkedMode = Root.AllModes.FirstOrDefault(mode => mode.ModeName == ModeName)) is null)
            {
                error = $"Mode {ModeName} not found.";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Modules containing the names of the modes in the group.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The modes that are a part of this group.")]
    public Mode[] Modes;

    /// <summary>
    /// The names of the modes in the group.
    /// </summary>
    private string[] _modeNames;

    /// <summary>
    /// Checks to see if the given mode is in the group.
    /// </summary>
    /// <param name="mode">The mode to check for.</param>
    /// <returns>True if the mode was found, false otherwise.</returns>
    public bool Contains(IMode mode)
    {
        var targetName = mode.ModeName;
        return Array.IndexOf(_modeNames, targetName) != -1;
    }

    /// <summary>
    /// Checks to see if the given mode is in the group.
    /// </summary>
    /// <param name="mode">The mode to check for.</param>
    /// <returns>True if the mode was found, false otherwise.</returns>
    public bool Contains(string mode)
    {
        return Array.IndexOf(_modeNames, mode) != -1;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        // We can get the names of the modes from the Mode objects
        // even though they have not been linked yet.
        _modeNames = Modes
            .Select(m => m.ModeName)
            .ToArray();
        return true;
    }

}