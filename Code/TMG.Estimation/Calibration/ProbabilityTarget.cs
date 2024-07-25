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
using TMG.Estimation.Calibration;
using XTMF;

[ModuleInformation(Description = "Used when you want to hit a given target probability.")]
public sealed class ProbabilityTarget : CalibrationTarget
{
    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

    private float _targetProbability;

    private float _baseRunProbability;
    private float _stepProbability;

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

    internal override void StoreRun(bool baseRun)
    {
        ResultProbability.LoadData();
        if (baseRun)
        {
            _baseRunProbability = ResultProbability.GiveData();
        }
        else
        {
            _stepProbability = ResultProbability.GiveData();
        }
        ResultProbability.UnloadData();
    }

    internal override float ReportTargetDistance()
    {
        return _baseRunProbability - _targetProbability;
    }
}