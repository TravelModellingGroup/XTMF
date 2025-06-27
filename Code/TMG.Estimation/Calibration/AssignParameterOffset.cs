/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Functions;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "Allows the assignment of a specific offset to an array of different floating point parameters.")]
public sealed class AssignParameterOffset : ISelfContainedModule
{
    [RunParameter("Offset Value", 0.0f, "The offset for the parameter.")]
    public float OffsetValue = 0.0f;

    [ModuleInformation(Description = "Provides a link to a parameter that will be updated.")]
    public sealed class Parameter : IModule
    {
        [RootModule]
        public CalibrationHost Root;

        [RunParameter("Base Value", 0.0f, "The base value for the parameter.")]
        public float BaseValue = 0.0f;

        [RunParameter("Parameter Path", "", "The path to the parameter to assigned the combined value to.")]
        public string ParameterPath;

        /// <summary>
        /// A link to the XTMF configuration for reflection.
        /// </summary>
        private readonly IConfiguration _configuration;

        public string Name { get; set; } = string.Empty;

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        private IModuleParameter _parameter;

        public Parameter(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (string.IsNullOrWhiteSpace(ParameterPath))
            {
                error = "The parameter path must be set!";
                return false;
            }

            _parameter = ModelSystemReflection.FindParameter(_configuration, this, Root.Client, ParameterPath);
            if (_parameter is null)
            {
                error = $"Unable to find a parameter with the path {ParameterPath}!";
                return false;
            }
            return true;
        }

        internal void Update(float offsetValue)
        {
            ModelSystemReflection.AssignValue(_configuration, _parameter, BaseValue + offsetValue, true);
        }
    }

    /// <summary>
    /// The parameters that this target is going to use for calibration.
    /// </summary>
    [SubModelInformation(Required = false, Description = "The parameters that this target is going to use for calibration.")]
    public Parameter[] Parameters;

    public void Start()
    {
        foreach (var parameter in Parameters)
        {
            parameter.Update(OffsetValue);
        }
    }

    public string Name { get; set; } = String.Empty;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
