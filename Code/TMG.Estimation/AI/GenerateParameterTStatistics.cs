/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using Datastructure;
using TMG.Input;
using XTMF;
namespace TMG.Estimation.AI;

[ModuleInformation(Description = "Produces a report to show the t-statistics of the parameters in the model given a set of betas from a previous estimation run.")]
public class GenerateParameterTStatistics : IEstimationAI
{
    [RootModule]
    public IEstimationHost Root;

    [RunParameter("Delta", 0.0001f, "In relative parameter space the distance that will be used to estimate the derivatives.")]
    public float Delta;

    [RunParameter("Maximize", true, "Is the estimation trying to maximize or minimize the fitness function (maximize = true)?")]
    public bool Maximize;

    [SubModelInformation(Required = true, Description = "The location of the result file to read in.")]
    public FileLocation ResultFile;

    [SubModelInformation(Required = true, Description = "The location to save our report to.")]
    public FileLocation ReportFile;

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
        get
        {
            return null;
        }
    }

    public List<Job> CreateJobsForIteration()
    {
        var ret = new List<Job>();
        var parameters = Root.Parameters.ToArray();
        using (var reader = new CsvReader(ResultFile.GetFilePath()))
        {
            int[] columnToParameterMap = CreateParameterMap(reader);
            var baseParameters = LoadBaseParameters(parameters, reader, columnToParameterMap);
            ret.Add(CreateZero(baseParameters));
            ret.Add(CreateJob(baseParameters));
            // Create the jobs for computing the diagonal of the Hessian
            for (int i = 0; i < baseParameters.Length; i++)
            {
                // we will need 5 points to approx the second derivative for now
                ret.Add(CreateWithOffset(baseParameters, i, -Delta));
                ret.Add(CreateWithOffset(baseParameters, i, -Delta / 2));
                ret.Add(CreateWithOffset(baseParameters, i, Delta / 2));
                ret.Add(CreateWithOffset(baseParameters, i, Delta));
            }
            // Create the jobs for computing the off-diagonal of the Hessian
            for (int i = 0; i < baseParameters.Length; i++)
            {
                for (int j = i + 1; j < baseParameters.Length; j++)
                {
                    ret.Add(CreateWithOffset(baseParameters, i, j, Delta));
                }
            }
        }
        return ret;
    }

    private Job CreateZero(ParameterSetting[] baseParameters)
    {
        var parameters = Clone(baseParameters);
        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i].Current = parameters[i].NullHypothesis;
        }
        return CreateJob(parameters);
    }

    private Job CreateWithOffset(ParameterSetting[] baseParameters, int index, float delta)
    {
        var parameters = Clone(baseParameters);
        parameters[index].Current += delta * (parameters[index].Maximum - parameters[index].Minimum);
        return CreateJob(parameters);
    }

    private Job CreateWithOffset(ParameterSetting[] baseParameters, int index, int index2, float delta)
    {
        var parameters = Clone(baseParameters);
        parameters[index].Current += delta * (parameters[index].Maximum - parameters[index].Minimum);
        parameters[index2].Current += delta * (parameters[index2].Maximum - parameters[index2].Minimum);
        return CreateJob(parameters);
    }

    private ParameterSetting[] Clone(ParameterSetting[] parameters)
    {
        ParameterSetting[] ret = new ParameterSetting[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            ret[i] = new ParameterSetting()
            {
                Current = parameters[i].Current,
                Names = parameters[i].Names,
                Minimum = parameters[i].Minimum,
                Maximum = parameters[i].Maximum,
                NullHypothesis = parameters[i].NullHypothesis
            };
        }
        return ret;
    }

    private Job CreateJob(ParameterSetting[] parameters)
    {
        return new Job()
        {
            Parameters = parameters,
            Processed = false,
            ProcessedBy = null,
            Processing = false,
            Value = float.NaN
        };
    }

    private static ParameterSetting[] LoadBaseParameters(ParameterSetting[] parameters, CsvReader reader, int[] columnMap)
    {
        var baseParameters = new ParameterSetting[parameters.Length];
        // we only read the first line
        if (reader.LoadLine(out int _))
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                baseParameters[i] = new ParameterSetting()
                {
                    Names = parameters[i].Names,
                    Minimum = parameters[i].Minimum,
                    Maximum = parameters[i].Maximum
                };
            }

            for (int i = 0; i < columnMap.Length; i++)
            {
                reader.Get(out baseParameters[columnMap[i]].Current, i + 2);
            }
        }
        return baseParameters;
    }

    private int[] CreateParameterMap(CsvReader reader)
    {
        var parameters = Root.Parameters.ToArray();
        reader.LoadLine(out int columns);
        var ret = new int[columns - 2];
        for (int i = 2; i < columns; i++)
        {
            reader.Get(out string name, i);
            var selectedParameter = (from p in parameters
                                     where p.Names.Contains(name)
                                     select p).FirstOrDefault() ?? throw new XTMFRuntimeException(this, "In '" + Name + " the parameter '" + name + "' could not be resolved.");
            ret[i - 2] = IndexOf(parameters, selectedParameter);
        }
        return ret;
    }

    private static int IndexOf(ParameterSetting[] parameters, ParameterSetting selectedParameter)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] == selectedParameter) return i;
        }
        return -1;
    }

    public void IterationComplete()
    {
        var jobs = Root.CurrentJobs;
        var parameters = Root.Parameters;
        // job 0 is no parameters included
        // job 1 is all parameters included
        var zeroValue = jobs[0].Value;
        var baseValue = jobs[1].Value;
        using var writer = new StreamWriter(ReportFile);
        writer.WriteLine("Fitness,ZeroFitness,Rho^2");
        writer.Write(baseValue);
        writer.Write(',');
        writer.Write(zeroValue);
        writer.Write(',');
        writer.WriteLine(GetRho(baseValue, zeroValue));
        double[] tStatistics = ComputeTStatistics(jobs, parameters);
        writer.WriteLine("ParameterName,Coefficient,NullHypothesis,TwoLeftCoefficient,LeftCoefficient,RightCoefficient,TwoRight,TwoLeftFitness,LeftFitness,RightFitness,TwoRightFitness,t-statistic");
        for (int i = 0; i < parameters.Count; i++)
        {
            var secondDerivative = SecondDerivativeForDiagonal(i);
            var current = jobs[1].Parameters[i].Current;
            int offset = i * 4 + 2;
            writer.Write('"');
            writer.Write(parameters[i].Names[0]);
            writer.Write('"');
            writer.Write(',');
            writer.Write(current);
            writer.Write(',');
            writer.Write(jobs[offset + 0].Parameters[i].NullHypothesis);
            writer.Write(',');
            writer.Write(jobs[offset + 0].Parameters[i].Current);
            writer.Write(',');
            writer.Write(jobs[offset + 1].Parameters[i].Current);
            writer.Write(',');
            writer.Write(jobs[offset + 2].Parameters[i].Current);
            writer.Write(',');
            writer.Write(jobs[offset + 3].Parameters[i].Current);
            writer.Write(',');
            writer.Write(jobs[offset + 0].Value);
            writer.Write(',');
            writer.Write(jobs[offset + 1].Value);
            writer.Write(',');
            writer.Write(jobs[offset + 2].Value);
            writer.Write(',');
            writer.Write(jobs[offset + 3].Value);
            writer.Write(',');
            writer.Write(tStatistics[i]);
            writer.WriteLine();
        }
    }

    private double[] ComputeTStatistics(List<Job> jobs, List<ParameterSetting> parameters)
    {
        double[] ret = new double[parameters.Count];

        // Step 1) Compute the Fisher Information Matrix
        double[,] fisherMatrix = new double[parameters.Count, parameters.Count];
        for (int i = 0; i < parameters.Count; i++)
        {
            fisherMatrix[i, i] = -SecondDerivativeForDiagonal(i);
        }
        for (int i = 0; i < parameters.Count; i++)
        {
            for (int j = i + 1; j < parameters.Count; j++)
            {
                fisherMatrix[i, j] = -SecondDerivativeForOffDiagonal(parameters, jobs, i, j);
            }
        }
        // Copy the upper half to the lower half
        for (int i = 0; i < parameters.Count; i++)
        {
            for (int j = i + 1; j < parameters.Count; j++)
            {
                fisherMatrix[j, i] = fisherMatrix[i, j];
            }
        }

        WriteMatrix(parameters, fisherMatrix, "FisherMatrix.csv");

        // Step 2) Compute the inverse of the Fisher Information Matrix

        double[,] inverseFisherMatrix = Inverse(fisherMatrix);

        WriteMatrix(parameters, inverseFisherMatrix, "InverseFisherMatrix.csv");

        // Step 3) Compute the t-statistics

        for (int i = 0; i < parameters.Count; i++)
        {
            ret[i] = (jobs[1].Parameters[i].Current - jobs[1].Parameters[i].NullHypothesis) / Math.Sqrt(inverseFisherMatrix[i, i]);
        }

        return ret;
    }

    private void WriteMatrix(List<ParameterSetting> parameters, double[,] fisherMatrix, string fileName)
    {
        using var writer = new StreamWriter(fileName);
        for (int i = 0; i < fisherMatrix.GetLength(0); i++)
        {
            if(i != 0)
            {
                writer.Write(',');
            }
            writer.Write(parameters[i].Names[i]);
        }
        for (int i = 0; i < fisherMatrix.GetLength(0); i++)
        {
            writer.Write(parameters[i].Names[i]);
            for (int j = 0; j < fisherMatrix.GetLength(1); j++)
            {
                writer.Write(',');
                writer.Write(fisherMatrix[i, j]);
            }
            writer.WriteLine();
        }
    }

    private static double GetPlusDeltaFor(List<Job> jobs, int parameterIndex)
    {
        return jobs[parameterIndex * 4 + 2 + 4].Value;
    }

    private static double GetPlusDeltaFor(List<Job> jobs, int totalParameters, int parameterIndex, int parameterIndex2)
    {
        var additionalOffset = ComputeTopHalfIndex(parameterIndex, parameterIndex2, totalParameters);
        return jobs[totalParameters * 4 
            + additionalOffset]
            .Value;
    }

    private static int ComputeTopHalfIndex(int parameterIndex, int parameterIndex2, int totalParameters)
    {
        int n = totalParameters - 1;
        int index = parameterIndex * (n + 1) - (parameterIndex * (parameterIndex + 1)) / 2 + parameterIndex2 - parameterIndex - 1;
        return index;
    }

    private double SecondDerivativeForDiagonal(int parameterIndex)
    {
        var jobs = Root.CurrentJobs;
        var parameters = Root.Parameters;
        var parameterDelta = Delta * (parameters[parameterIndex].Maximum - parameters[parameterIndex].Minimum);
        // 5 parameters per job (4 for second derivative and the first for the null hypothesis), 2 jobs to get value and zero value
        var parameterOffset = parameterIndex * 4 + 2;

        var yn2 = jobs[parameterOffset + 0].Value;
        var yn1 = jobs[parameterOffset + 1].Value;
        // Job 0 is the zero parameters model, job 1 is the full parameters model
        var y0 = jobs[1].Value;
        var y1 = jobs[parameterOffset + 2].Value;
        var y2 = jobs[parameterOffset + 3].Value;
        // The points are spaced out by half of the delta
        return SecondDerivativeFivePoints(yn2, yn1, y0, y1, y2, parameterDelta / 2.0);
    }

    private double GetDeltaForParameter(int parameterIndex)
    {
        var parameters = Root.Parameters;
        return Delta * (parameters[parameterIndex].Maximum - parameters[parameterIndex].Minimum);
    }

    private double SecondDerivativeForOffDiagonal(List<ParameterSetting> parameters, List<Job> jobs, int i, int j)
    {
        double original = jobs[1].Value;
        double plusDeltaI = GetPlusDeltaFor(jobs, i);
        double plusDeltaJ = GetPlusDeltaFor(jobs, j);
        double plusDeltaBoth = GetPlusDeltaFor(jobs, parameters.Count, i, j);

        double secondDerivative = (plusDeltaBoth - plusDeltaI - plusDeltaJ + original) / (GetDeltaForParameter(i) * GetDeltaForParameter(j));
        return secondDerivative;
    }

    private static double SecondDerivativeFivePoints(double yn2, double yn1, double y0, double y1, double y2, double deltaX)
    {
        var numerator = -yn2 + 16 * yn1 - 30 * y0 + 16 * y1 - y2;
        var denominator = 12 * deltaX * deltaX;
        return numerator / denominator;
    }

    private float GetRho(float current, float zeroParams)
    {
        return 1.0f - (current / zeroParams);
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private static double[,] Inverse(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        double[,] result = new double[n, n];
        double[,] augmented = new double[n, 2 * n];
        int i, j, k;
        double temp;

        // Initialize the result matrix as an identity matrix
        for (i = 0; i < n; i++)
        {
            for (j = 0; j < n; j++)
            {
                if (i == j) result[i, j] = 1;
                else result[i, j] = 0;
            }
        }

        // Copy the input matrix to the augmented matrix
        for (i = 0; i < n; i++)
        {
            for (j = 0; j < n; j++)
            {
                augmented[i, j] = matrix[i, j];
            }
        }

        // Perform the Gauss-Jordan elimination
        for (i = 0; i < n; i++)
        {
            // Make the diagonal contain all 1's
            temp = augmented[i, i];
            for (j = 0; j < 2 * n; j++)
            {
                augmented[i, j] /= temp;
            }

            // Make the rest of the column contain all 0's
            for (j = 0; j < n; j++)
            {
                if (i != j)
                {
                    temp = augmented[j, i];
                    for (k = 0; k < 2 * n; k++)
                    {
                        augmented[j, k] -= augmented[i, k] * temp;
                    }
                }
            }
        }

        // Extract the inverse matrix from the augmented matrix
        for (i = 0; i < n; i++)
        {
            for (j = n; j < 2 * n; j++)
            {
                result[i, j - n] = augmented[i, j];
            }
        }
        return result;
    }

}
