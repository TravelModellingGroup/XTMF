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
using System.Collections.Generic;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "A scalar target to try to calibrate to.")]
public sealed class ScalarTarget : CalibrationTarget
{
    [RunParameter("Explore Size", 0.01f, "The different in the value of the parameter to explore to compute the derivative.")]
    public float ExploreSize;

    [RunParameter("Learning Rate", 0.75f, "The distance to move towards hitting the target.")]
    public float LearningRate;

    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

    public ScalarTarget(IConfiguration configuration)
        : base(configuration)
    {
    }

    [SubModelInformation(Required = true, Description = "The value to compare against the target.")]
    public IDataSource<float> ComputeCurrentValue;

    [SubModelInformation(Required = true, Description = "The target value to try to converge to.")]
    public IDataSource<float> Target;

    [RunParameter("Minimum Absolute Derivative", 0.00001f, "The minimum derivative a cell can have and still have an effect on the result.")]
    public float MinimumAbsoluteDerivative;

    [RunParameter("Maximum Change", float.PositiveInfinity, "The most a value is allowed to change in a single step.")]
    public float MaximumChange;

    private float _targetValue;

    private float _baseValue;

    private float _stepValue;

    public override float UpdateParameter(float currentValue)
    {
        var derivative = ((_stepValue - _baseValue) / ExploreSize);

        // We need to detect if the derivative is flat

        // TODO: We might want to add some momentum to avoid getting stuck
        if (MathF.Abs(derivative) < MinimumAbsoluteDerivative)
        {
            Console.WriteLine($"{Name} encountered a derivative that was under the minimum allowed {MinimumAbsoluteDerivative}, no parameter change has been applied.");
            return currentValue;
        }

        // Step * derivative + value1 = target
        // <=> Step = (target - value1) / derivative
        var step = (_targetValue - _baseValue) / derivative;
        var delta = (step * (step < ExploreSize ? 1.0f : LearningRate));

        currentValue += ClampValue(delta, -MaximumChange, MaximumChange);

        return ClampValue(currentValue, MinimumValue, MaximumValue);
    }

    internal override void StoreRun(int runIndex)
    {
        ComputeCurrentValue.LoadData();
        if (runIndex < 0)
        {
            _baseValue = ComputeCurrentValue.GiveData();
        }
        else
        {
            _stepValue = ComputeCurrentValue.GiveData();
        }
        ComputeCurrentValue.UnloadData();
    }

    internal override float ReportTargetDistance()
    {
        return (_baseValue - _targetValue);
    }

    protected override void LoadTarget()
    {
        Target.LoadData();
        _targetValue = Target.GiveData();
        Target.UnloadData();
    }

    public override bool RuntimeValidation(ref string error)
    {
        if (MinimumValue > MaximumValue)
        {
            error = "Minimum value is greater than maximum value.";
            return false;
        }
        return base.RuntimeValidation(ref error);
    }

    public override IEnumerable<ParameterSetting[]> CreateAdditionalRuns(ParameterSetting[] baseParameters, int iteration, int targetIndex)
    {
        // We need an additional run in order to compute the derivative.
        var copy = new ParameterSetting[baseParameters.Length];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = new ParameterSetting()
            {
                Current = baseParameters[i].Current,
                Names = baseParameters[i].Names,
                Minimum = baseParameters[i].Minimum,
                Maximum = baseParameters[i].Maximum,
                NullHypothesis = baseParameters[i].NullHypothesis,
            };
        }
        copy[targetIndex].Current += ExploreSize;
        yield return copy;
    }

}
