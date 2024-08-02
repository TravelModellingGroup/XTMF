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
using System.Linq;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation.Calibration;

[ModuleInformation(Description = "Specify which activities will be recorded.")]
public sealed class SelectedModes : IModule
{
    [RunParameter("Mode Name", "Auto", "The name of the mode to select.")]
    public string ModeName;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}

internal static class SelectedModesExtensions
{
    /// <summary>
    /// Retrieves the selected modes based on the provided parameters.
    /// </summary>
    /// <param name="callingModule">The calling module.</param>
    /// <param name="modes">The array of selected modes.</param>
    /// <param name="runtime">The ITashaRuntime instance.</param>
    /// <returns>An array of ITashaMode representing the selected modes.</returns>
    internal static ITashaMode[] GetModes(this SelectedModes[] modes, IModule callingModule, ITashaRuntime runtime)
    {
        var ret = new ITashaMode[modes.Length];
        var allModes = runtime.AllModes;
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var selectedMode = allModes.FirstOrDefault(m => m.Name == mode.ModeName)
                ?? throw new XTMFRuntimeException(callingModule, $"Mode {mode.ModeName} not found.");
            ret[i] = selectedMode;
        }
        return ret;
    }

}
