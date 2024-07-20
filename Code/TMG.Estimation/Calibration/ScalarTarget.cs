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
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "A scalar target to try to calibrate to.")]
public sealed class ScalarTarget : CalibrationTarget
{

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

    private float _value1;

    private float _value2;

    public override float UpdateParameter(float currentValue)
    {
        var derivative = ((_value2 - _value1) / ExploreSize);

        // We need to detect if the derivative is flat

        // TODO: We might want to add some momentum to avoid getting stuck
        if (MathF.Abs(derivative) < MinimumAbsoluteDerivative)
        {
            Console.WriteLine($"{Name} encountered a derivative that was under the minimum allowed {MinimumAbsoluteDerivative}, no parameter change has been applied.");
            return currentValue;
        }

        // Step * derivative + value1 = target
        // <=> Step = (target - value1) / derivative
        var step = (_targetValue - _value1) / derivative;
        var delta = (step * (step < ExploreSize ? 1.0f : LearningRate));

        currentValue += ClampValue(delta, -MaximumChange, MaximumChange);

        return ClampValue(currentValue, MinimumValue, MaximumValue);
    }

    internal override void StoreRun(bool baseRun)
    {
        ComputeCurrentValue.LoadData();
        if (baseRun)
        {
            _value1 = ComputeCurrentValue.GiveData();
        }
        else
        {
            _value2 = ComputeCurrentValue.GiveData();
        }
        ComputeCurrentValue.UnloadData();
    }

    internal override float ReportTargetDistance()
    {
        return (_value1 - _targetValue);
    }

    protected override void LoadTarget()
    {
        Target.LoadData();
        _targetValue = Target.GiveData();
        Target.UnloadData();
    }

}
