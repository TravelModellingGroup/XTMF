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
using Datastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMG.Functions;
using XTMF;

namespace TMG.Estimation.Calibration;

/// <summary>
/// This module is designed to represent a probability matrix target for calibration.
/// </summary>
[ModuleInformation(Description = "This module is designed to update a constant")]
public sealed class ProbabilityMatrixTarget : CalibrationTarget
{
    /// <summary>
    /// The minimum value allowed for this parameter.
    /// </summary>
    [RunParameter("Minimum Value", float.NegativeInfinity, "The lowest value allowed for this parameter.")]
    public float MinimumValue;

    /// <summary>
    /// The maximum value allowed for this parameter.
    /// </summary>
    [RunParameter("Maximum Value", float.PositiveInfinity, "The highest value allowed for this parameter.")]
    public float MaximumValue;

    [SubModelInformation(Required = true, Description = "The total matrix from the observed.")]
    public IDataSource<SparseTwinIndex<float>> ObservedTotal;

    [SubModelInformation(Required = true, Description = "The selected matrix from the observed.")]
    public IDataSource<SparseTwinIndex<float>> ObservedSelection;

    [SubModelInformation(Required = true, Description = "The total matrix from the model.")]
    public IDataSource<SparseTwinIndex<float>> ModelTotal;

    [SubModelInformation(Required = true, Description = "The selected matrix from the model.")]
    public IDataSource<SparseTwinIndex<float>> ModelSelection;

    [SubModelInformation(Required = false, Description = "A mask matrix to apply to the matrices.")]
    public IDataSource<SparseTwinIndex<float>> Mask;

    [RunParameter("Paramter Is Ratio", false, "Set this to true if the parameter is a a ratio instead of linear.")]
    public bool ParameterIsRatio;

    [RunParameter("Only Mask Selection", false, "Set this to true if you want the ratio of masked/unmasked for both modelled and observed.")]
    public bool OnlyMaskSelection;

    private float[][] _mask = null!;
    private float _targetProbability = float.NegativeInfinity;
    private float _baseRunProbability = float.NegativeInfinity;

    /// <summary>
    /// Called by XTMF
    /// </summary>
    /// <param name="config">The configuration.</param>
    public ProbabilityMatrixTarget(IConfiguration config) : base(config)
    {
    }

    /// <summary>
    /// Updates the parameter value based on the current value.
    /// </summary>
    /// <param name="currentValue">The current value of the parameter.</param>
    /// <returns>The updated value of the parameter.</returns>
    public override float UpdateParameter(float currentValue)
    {
        var numerator = _targetProbability * _baseRunProbability - _targetProbability;
        var denominator = _baseRunProbability * _targetProbability - _baseRunProbability;
        var ratio = numerator / denominator;

        if (!float.IsFinite(ratio))
        {
            Console.WriteLine($"We found an invalid step size for {Name}, TargetProbability {_targetProbability}, Current {_baseRunProbability}!");
            return currentValue;
        }
        if (ParameterIsRatio)
        {
            var delta = ratio;
            return ClampValue(currentValue * delta, MinimumValue, MaximumValue);
        }
        else
        {
            var delta = MathF.Log(ratio);
            return ClampValue(currentValue + delta, MinimumValue, MaximumValue);
        }
    }

    /// <summary>
    /// Loads the target data.
    /// </summary>
    protected override void LoadTarget()
    {
        _mask = Mask is not null ? GetValue(Mask).GetFlatData() : null;
        _targetProbability = GetValue(ObservedTotal, ObservedSelection);
    }

    /// <summary>
    /// Stores the run data.
    /// </summary>
    /// <param name="runIndex">The index of the run.</param>
    internal override void StoreRun(int runIndex)
    {
        _baseRunProbability = GetValue(ModelTotal, ModelSelection);
    }

    private static SparseTwinIndex<float> GetValue(IDataSource<SparseTwinIndex<float>> source)
    {
        source.LoadData();
        var ret = source.GiveData();
        source.UnloadData();
        return ret;
    }

    private float GetValue(IDataSource<SparseTwinIndex<float>> totalSource, IDataSource<SparseTwinIndex<float>> selectionSource)
    {
        SparseTwinIndex<float> total = null;
        SparseTwinIndex<float> selection = null;
        float sumTotal = float.NegativeInfinity;
        float sumSelection = float.NegativeInfinity;
        // Get the matrices
        Parallel.Invoke(
            () => total = GetValue(totalSource),
            () => selection = GetValue(selectionSource)
        );
        // Mask Sum the matrices
        Parallel.Invoke(
            () => sumTotal = OnlyMaskSelection ? GetSum(total) : GetMaskedSum(total),
            () => sumSelection = GetMaskedSum(selection)
        );
        return sumSelection / sumTotal;
    }

    private float GetSum(SparseTwinIndex<float> matrix)
    {
        var flat = matrix.GetFlatData();
        var acc = 0.0f;
        for ( var i = 0; i < flat.Length; i++)
        {
            acc += VectorHelper.Sum(flat[i], 0, flat.Length);
        }
        return acc;
    }

    private float GetMaskedSum(SparseTwinIndex<float> matrix)
    {
        float acc = 0.0f;
        var data = matrix.GetFlatData();
        if (_mask is null)
        {
            for (int i = 0; i < data.Length; i++)
            {
                acc += VectorHelper.Sum(data[i], 0, data.Length);
            }
            return acc;
        }
        else
        {
            var mask = _mask;
            CheckMaskNonZero(mask);
            for (int i = 0; i < data.Length; i++)
            {
                acc += VectorHelper.MultiplyAndSumNoStore(data[i], mask[i]);
            }
            return acc;
        }
    }

    private void CheckMaskNonZero(float[][] mask)
    {
        var acc = 0.0f;
        for (int i = 0; i < mask.Length; i++)
        {
            acc += VectorHelper.Sum(mask[i], 0, mask.Length);
        }
        if (acc <= 0.0f)
        {
            throw new XTMFRuntimeException(this, "The mask matrix is all zeros!");
        }
    }

    internal override float ReportTargetDistance()
    {
        return _baseRunProbability - _targetProbability;
    }

    public override IEnumerable<ParameterSetting[]> CreateAdditionalRuns(ParameterSetting[] baseParameters, int iteration, int targetIndex)
    {
        yield break;
    }

}
