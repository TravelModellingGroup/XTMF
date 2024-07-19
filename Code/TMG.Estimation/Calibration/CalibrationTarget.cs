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
using TMG.Functions;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "Provides targets for calibration.")]
public abstract class CalibrationTarget : IModule
{
    [RunParameter("Parameter Path", "", "The path separated by '.'s to the parameter relative to the client model system's root.")]
    public string ParameterPath;

    [RunParameter("Learning Rate", 0.75f, "The distance to move towards hitting the target.")]
    public float LearningRate;

    [RunParameter("Explore Size", 0.01f, "The different in the value of the parameter to explore to compute the derivative.")]
    public float ExploreSize;

    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

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
    /// The parameter that this target is going to use for calibration.
    /// </summary>
    private IModuleParameter _parameter;

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
        _parameter = ModelSystemReflection.FindParameter(_configuration, this, clientModelSystem, ParameterPath);
        if (_parameter is null)
        {
            throw new XTMFRuntimeException(this, $"Unable to find the parameter {ParameterPath}!");
        }
        LoadTarget();
    }

    /// <summary>
    /// Load the target value
    /// </summary>
    protected abstract void LoadTarget();

    internal float GetParameterValue()
    {
        object value = _parameter.Value;
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
        ModelSystemReflection.AssignValueRunOnly(_configuration, _parameter, value);
    }

    /// <summary>
    /// Set the parameter's value and make the change if the model system is saved.
    /// </summary>
    /// <param name="value"></param>
    internal void SetParameterAndSave(float value)
    {
        ModelSystemReflection.AssignValue(_configuration, _parameter, value);
    }

    /// <summary>
    /// Called after the calibration client has finished running.
    /// </summary>
    /// <param name="baseRun">True if this is the base position, False if the single parameter has changed.</param>
    internal abstract void StoreRun(bool baseRun);

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
}
