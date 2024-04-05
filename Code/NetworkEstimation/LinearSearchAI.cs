/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation;

public class LinearSearchAi : INetworkEstimationAI
{
    [SubModelInformation(Description = "The module to tally the errors", Required = true)]
    public IErrorTally ErrorTally;

    [RunParameter("Interval", 0.1f, "The (0 to 1) interval to increase by per exploration, where 0.1 would be 10% of the parameter space.")]
    public float Interval;

    [RunParameter("MABS Weight", 1f, "The weight applied to the mean absolute error in the evaluation function")]
    public float MabsWeight;

    [RunParameter("RMSE Weight", 1f, "The weight applied to the root mean square error in the evaluation function")]
    public float RmseWeight;

    [RunParameter("TotalError Weight", 1f, "The weight applied to the total error in the evaluation function")]
    public float ErrorWeight;

    private Tuple<byte, byte, byte> _Colour = new(50, 150, 50);

    private bool Exit;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return _Colour; }
    }

    public bool UseComplexErrorFunction
    {
        get { return true; }
    }

    public void CancelExploration()
    {
        Exit = true;
    }

    public float ComplexErrorFunction(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted, float[] aggToTruth)
    {
        return ErrorTally.ComputeError(parameters, transitLine, predicted);
    }

    public float ErrorCombinationFunction(double rmse, double mabs, double terror)
    {
        return (float)((RmseWeight * rmse
                        + MabsWeight * mabs
                        + ErrorWeight * terror));
    }

    public void Explore(ParameterSetting[] parameters, Action updateProgress, Func<ParameterSetting[], float> evaluationfunction)
    {
        Exit = false;
        Progress = 0;
        Explore(parameters, updateProgress, evaluationfunction, 0);
        Progress = 1;
        updateProgress();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private void Explore(ParameterSetting[] parameters, Action updateProgress, Func<ParameterSetting[], float> evaluationfunction, int parameterIndex)
    {
        float point = parameters[parameterIndex].Start;
        for (; point < parameters[parameterIndex].Stop; point += (parameters[parameterIndex].Stop - parameters[parameterIndex].Start) * Interval)
        {
            parameters[parameterIndex].Current = point;
            if (parameterIndex >= parameters.Length - 1)
            {
                evaluationfunction(parameters);
                Progress = Progress + (1f / (float)Math.Pow(1f / Interval, parameters.Length));
                updateProgress();
            }
            else
            {
                Explore(parameters, updateProgress, evaluationfunction, parameterIndex + 1);
            }
            if (Exit)
            {
                break;
            }
        }
    }
}