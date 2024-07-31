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
using System.Collections;
using System.Collections.Generic;
using TMG.Functions;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "Provides targets for calibration.")]
public abstract class CalibrationTarget : IModule
{
    [RunParameter("Parameter Path", "", "The path separated by '.'s to the parameter relative to the client model system's root, commas allows for additional parameters.")]
    public string ParameterPath;

    [RunParameter("Explore Size", 0.01f, "The different in the value of the parameter to explore to compute the derivative.")]
    public float ExploreSize;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    /// <summary>
    /// Computes the amount the parameter should move.
    /// </summary>
    /// <returns>The amount the parameter should move.</returns>
    /// <param name="currentValue">The current value of the parameter.</param>
    public abstract float UpdateParameter(float currentValue);

    /// <summary>
    /// A link to the XTMF configuration for reflection.
    /// </summary>
    private IConfiguration _configuration;

    /// <summary>
    /// The parameters that this target is going to use for calibration.
    /// </summary>
    private IModuleParameter[] _parameters;

    protected CalibrationTarget(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public virtual bool RuntimeValidation(ref string error)
    {
        if (string.IsNullOrWhiteSpace(ParameterPath))
        {
            error = "The parameter path must be set!";
            return false;
        }
        return true;
    }

    internal void Intialize(IModelSystemTemplate clientModelSystem)
    {
        var parts = ParameterPath.Split(',');
        _parameters = new IModuleParameter[parts.Length];
        for(int i = 0; i < _parameters.Length; i++)
        {
            _parameters[i] = ModelSystemReflection.FindParameter(_configuration, this, clientModelSystem, parts[i]);
            if (_parameters[i] is null)
            {
                throw new XTMFRuntimeException(this, $"Unable to find the parameter {parts[i]}!");
            }
        }
        LoadTarget();
    }

    /// <summary>
    /// Load the target value
    /// </summary>
    protected abstract void LoadTarget();

    internal float GetParameterValue()
    {
        object value = _parameters[0].Value;
        try
        {
            return (float)value;
        }
        catch (Exception e)
        {
            throw new XTMFRuntimeException(this, e, $"Unable to load the value of the parameter {ParameterPath} as a floating point number, '{value}'");
        }
    }

    /// <summary>
    /// Set the parameter to give the value at runtime only.
    /// </summary>
    /// <param name="value">The value to set.</param>
    internal void SetParameterValue(float value)
    {
        foreach (var parameter in _parameters)
        {
            ModelSystemReflection.AssignValueRunOnly(_configuration, parameter, value);
        }
    }

    /// <summary>
    /// Set the parameter's value and make the change if the model system is saved.
    /// </summary>
    /// <param name="value"></param>
    internal void SetParameterAndSave(float value)
    {
        foreach (var parameter in _parameters)
        {
            ModelSystemReflection.AssignValue(_configuration, parameter, value);
        }
    }

    /// <summary>
    /// Called after the calibration client has finished running.
    /// </summary>
    /// <param name="runIndex">The index of the run as requested by the target, -1 if it is the base run.</param>
    internal abstract void StoreRun(int runIndex);

    /// <summary>
    /// Report the distance to the target
    /// </summary>
    /// <returns></returns>
    internal abstract float ReportTargetDistance();
    
    /// Clamps the given value between the minimum and maximum values.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <returns>The clamped value.</returns>
    protected static float ClampValue(float value, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, value));
    }

    /// <summary>
    /// Requests the additional run required to compute the next step for this target.
    /// </summary>
    /// <param name="baseParameters">The current position for all of the parameters</param>
    /// <param name="iteration">The current iteration into the estimation.</param>
    /// <param name="targetIndex">The index of the parameter this target is computing.</param>
    /// <returns>A list of runs in addition to the base run to execute.</returns>
    public abstract IEnumerable<ParameterSetting[]> CreateAdditionalRuns(ParameterSetting[] baseParameters, int iteration, int targetIndex);

}
