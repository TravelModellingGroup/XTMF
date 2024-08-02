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
using System.Linq;
using XTMF;

namespace TMG.Estimation.Calibration;

[RedirectModule("ProbabilityTarget, TMG.Estimation, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null")]
[ModuleInformation(Description = "Used when you want to hit a given target probability.")]
public sealed class ProbabilityTarget : CalibrationTarget
{
    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

    private float _targetProbability;

    private float _baseRunProbability;

    [SubModelInformation(Required = true, Description = "The target probability")]
    public IDataSource<float> TargetProbability;

    [SubModelInformation(Required = true, Description = "The current probability")]
    public IDataSource<float> ResultProbability;

    public ProbabilityTarget(IConfiguration configuration)
        : base(configuration) { }

    public override float UpdateParameter(float currentValue)
    {
        var numerator = _targetProbability * _baseRunProbability - _targetProbability;
        var denominator = _baseRunProbability * _targetProbability - _baseRunProbability;

        var delta = MathF.Log(numerator / denominator);
        if (!float.IsFinite(delta))
        {
            Console.WriteLine($"We found an invalid step size for {Name}!");
            return currentValue;
        }
        var next = ClampValue(currentValue + delta, MinimumValue, MaximumValue);
        return next;
    }

    protected override void LoadTarget()
    {
        TargetProbability.LoadData();
        _targetProbability = TargetProbability.GiveData();
        TargetProbability.UnloadData();
    }

    internal override void StoreRun(int runIndex)
    {
        if(runIndex != -1)
        {
            throw new XTMFRuntimeException(this, "Somehow the run index was not equal to -1 even through we did not request any additional runs!");
        }
        ResultProbability.LoadData();
        _baseRunProbability = ResultProbability.GiveData();
        ResultProbability.UnloadData();
    }

    internal override float ReportTargetDistance()
    {
        return _baseRunProbability - _targetProbability;
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
        // We do not require any additional runs
        return null;
    }
}