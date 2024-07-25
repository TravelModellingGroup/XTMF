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

using Datastructure;
using System;
using System.Linq;
using TMG.Functions;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "A matrix target to try to calibrate to.")]
public sealed class MatrixTarget : CalibrationTarget
{
    [RunParameter("Learning Rate", 0.75f, "The distance to move towards hitting the target.")]
    public float LearningRate;

    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

    public MatrixTarget(IConfiguration config)
        : base(config)
    {
    }

    [SubModelInformation(Required = true, Description = "The matrix to attempt to match.")]
    public IDataSource<SparseTwinIndex<float>> Target;

    private SparseTwinIndex<float> _target;

    [SubModelInformation(Required = true, Description = "A matrix storing the result.")]
    public IDataSource<SparseTwinIndex<float>> Result;

    [RunParameter("Minimum Absolute Derivative", 0.00001f, "The minimum derivative a cell can have and still have an effect on the result.")]
    public float MinimumAbsoluteDerivative;

    private SparseTwinIndex<float> _baseResult;

    private SparseTwinIndex<float> _offsetResult;

    private float _baseError = 0f;

    [RunParameter("Maximum Change", float.PositiveInfinity, "The most a value is allowed to change in a single step.")]
    public float MaximumChange;

    public override float UpdateParameter(float currentValue)
    {
        var flatTarget = _target.GetFlatData();
        var flatBaseResult = _baseResult.GetFlatData();
        var flatOffsetResult = _offsetResult.GetFlatData();

        var sumTarget = SumMatrix(flatTarget);
        var sumBase = SumMatrix(flatBaseResult);
        var sumOffset = SumMatrix(flatOffsetResult);

        _baseError = sumBase - sumTarget;

        var derivative = (sumOffset - sumBase) / ExploreSize;

        if (MathF.Abs(derivative) < MinimumAbsoluteDerivative)
        {
            Console.WriteLine($"{Name} encountered a derivative that was under the minimum allowed {MinimumAbsoluteDerivative}, no parameter change has been applied.");
            return currentValue;
        }

        // Step * derivative + value1 = target
        // <=> Step = (target - value1) / derivative
        var step = (sumTarget - sumBase) / derivative;
        var delta = (step * (step < ExploreSize ? 1.0f : LearningRate));

        currentValue += ClampValue(delta, -MaximumChange, MaximumChange);

        return ClampValue(currentValue, MinimumValue, MaximumValue);
    }

    private static float SumMatrix(float[][] matrix)
    {
        var ret = 0.0f;
        for (int i = 0; i < matrix.Length; i++)
        {
            var local = VectorHelper.Sum(matrix[i], 0, matrix[i].Length);
            ret += local;
        }
        return ret;
    }

    protected override void LoadTarget()
    {
        Target.LoadData();
        _target = Target.GiveData();
        Target.UnloadData();
    }

    internal override void StoreRun(bool baseRun)
    {
        Result.LoadData();
        if (baseRun)
        {
            _baseResult = Result.GiveData();
        }
        else
        {
            _offsetResult = Result.GiveData();
        }
        Result.UnloadData();
    }

    internal override float ReportTargetDistance()
    {
        return _baseError;
    }

    public override bool RuntimeValidation(ref string error)
    {
        if (MaximumChange <= 0)
        {
            error = "The maximum change must be greater than 0!";
            return false;
        }
        if(MaximumValue < MinimumValue)
        {
            error = "The maximum value must be greater than the minimum value!";
            return false;
        }
        return base.RuntimeValidation(ref error);
    }
}
