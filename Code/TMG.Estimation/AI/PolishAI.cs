/*
    Copyright 2026 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.Estimation;

[ModuleInformation(Description = "EXPERIMENTAL: This AI computes t-statistics from log likelihood using the Hessian matrix and iteratively refines parameters.")]
public sealed class PolishAI : IEstimationAI
{
    [RootModule]
    public IEstimationHost Root;

    [RunParameter("Delta", 0.0001f, "The distance used to estimate derivatives, scaled by (1 + |P|) where P is the parameter value.")]
    public float Delta;

    [RunParameter("Use Full Hessian", false, "If true, computes the full Hessian matrix accounting for parameter correlations. If false, uses diagonal approximation (faster but assumes independence).")]
    public bool UseFullHessian;

    [SubModelInformation(Required = true, Description = "The location of the result file to read in.")]
    public FileLocation ResultFile;

    [SubModelInformation(Required = true, Description = "The location to save our report to.")]
    public FileLocation ReportFile;

    public string Name { get; set; }
    public float Progress { get; set; }
    public Tuple<byte, byte, byte> ProgressColour => null;

    private int _currentIteration;
    private ParameterSetting[] _baseParameters;
    private float _zeroValue = float.NaN;


    public List<Job> CreateJobsForIteration()
    {
        _currentIteration = Root.CurrentIteration;
        if (_currentIteration == 0)
        {
            return LoadInitialParameters();
        }

        // Store the zero value from the first iteration
        if (_currentIteration == 1)
        {
            _zeroValue = Root.CurrentJobs[0].Value;
        }

        // Check if we found better parameters in the previous iteration
        var jobs = Root.CurrentJobs;
        var bestJob = FindBestJob(jobs);
        
        // Base job is at index 1 on first iteration (after zero), index 0 on subsequent
        var baseJobIndex = _currentIteration == 1 ? 1 : 0;
        
        // If no improvement found, return null to stop iterating
        if (bestJob == jobs[baseJobIndex])
        {
            return null;
        }

        // Better parameters found, update base and continue
        _baseParameters = Clone(bestJob.Parameters);
        return CreateJobsFromBase(_baseParameters);
    }

    private Job FindBestJob(List<Job> jobs)
    {
        // On first iteration: job[0]=zero, job[1]=base, job[2+]=offsets
        // On subsequent: job[0]=base, job[1+]=offsets
        var baseJobIndex = _currentIteration == 1 ? 1 : 0;
        var skipCount = _currentIteration == 1 ? 2 : 1;
        
        Job best = jobs[baseJobIndex];
        foreach (var job in jobs.Skip(skipCount))
        {
            // Maximize log likelihood
            if (job.Value > best.Value)
            {
                best = job;
            }
        }
        return best;
    }

    private List<Job> CreateJobsFromBase(ParameterSetting[] baseParameters)
    {
        var ret = new List<Job>
        {
            CreateJob(baseParameters) // Base job only, zero already computed
        };

        if (UseFullHessian)
        {
            // Full Hessian: need diagonal and off-diagonal terms
            // Diagonal: 2n jobs (±δ for each parameter)
            for (int i = 0; i < baseParameters.Length; i++)
            {
                ret.Add(CreateWithOffset(baseParameters, i, -Delta));
                ret.Add(CreateWithOffset(baseParameters, i, Delta));
            }

            // Off-diagonal: 4 jobs per parameter pair (corners of a square)
            for (int i = 0; i < baseParameters.Length; i++)
            {
                for (int j = i + 1; j < baseParameters.Length; j++)
                {
                    ret.Add(CreateWithTwoOffsets(baseParameters, i, j, -Delta, -Delta));
                    ret.Add(CreateWithTwoOffsets(baseParameters, i, j, -Delta, Delta));
                    ret.Add(CreateWithTwoOffsets(baseParameters, i, j, Delta, -Delta));
                    ret.Add(CreateWithTwoOffsets(baseParameters, i, j, Delta, Delta));
                }
            }
        }
        else
        {
            // Diagonal only: 2n jobs
            for (int i = 0; i < baseParameters.Length; i++)
            {
                ret.Add(CreateWithOffset(baseParameters, i, -Delta));
                ret.Add(CreateWithOffset(baseParameters, i, Delta));
            }
        }
        return ret;
    }

    private List<Job> LoadInitialParameters()
    {
        var parameters = Root.Parameters.ToArray();
        using (var reader = new CsvReader(ResultFile.GetFilePath()))
        {
            int[] columnToParameterMap = CreateParameterMap(reader);
            _baseParameters = LoadBaseParameters(parameters, reader, columnToParameterMap);
            
            // First iteration includes zero and base
            var ret = new List<Job>
            {
                CreateZero(_baseParameters),
                CreateJob(_baseParameters)
            };

            // Create offset jobs for Hessian computation
            if (UseFullHessian)
            {
                // Diagonal elements
                for (int i = 0; i < _baseParameters.Length; i++)
                {
                    ret.Add(CreateWithOffset(_baseParameters, i, -Delta));
                    ret.Add(CreateWithOffset(_baseParameters, i, Delta));
                }

                // Off-diagonal elements (cross-derivatives)
                for (int i = 0; i < _baseParameters.Length; i++)
                {
                    for (int j = i + 1; j < _baseParameters.Length; j++)
                    {
                        ret.Add(CreateWithTwoOffsets(_baseParameters, i, j, -Delta, -Delta));
                        ret.Add(CreateWithTwoOffsets(_baseParameters, i, j, -Delta, Delta));
                        ret.Add(CreateWithTwoOffsets(_baseParameters, i, j, Delta, -Delta));
                        ret.Add(CreateWithTwoOffsets(_baseParameters, i, j, Delta, Delta));
                    }
                }
            }
            else
            {
                // Diagonal only
                for (int i = 0; i < _baseParameters.Length; i++)
                {
                    ret.Add(CreateWithOffset(_baseParameters, i, -Delta));
                    ret.Add(CreateWithOffset(_baseParameters, i, Delta));
                }
            }
            return ret;
        }
    }

    private static ParameterSetting[] Clone(ParameterSetting[] parameters)
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

    private Job CreateZero(ParameterSetting[] baseParameters)
    {
        var parameters = Clone(baseParameters);
        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i].Current = parameters[i].NullHypothesis;
        }
        return CreateJob(parameters);
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

    private Job CreateWithOffset(ParameterSetting[] baseParameters, int index, float delta)
    {
        var parameters = Clone(baseParameters);
        // Use delta * (1 + |P|) where P is the parameter's current value
        var adjustedDelta = delta * (1.0f + Math.Abs(parameters[index].Current));
        parameters[index].Current += adjustedDelta;
        return CreateJob(parameters);
    }

    private Job CreateWithTwoOffsets(ParameterSetting[] baseParameters, int index1, int index2, float delta1, float delta2)
    {
        var parameters = Clone(baseParameters);
        // Apply offsets to both parameters
        var adjustedDelta1 = delta1 * (1.0f + Math.Abs(parameters[index1].Current));
        var adjustedDelta2 = delta2 * (1.0f + Math.Abs(parameters[index2].Current));
        parameters[index1].Current += adjustedDelta1;
        parameters[index2].Current += adjustedDelta2;
        return CreateJob(parameters);
    }

    private static Job CreateJob(ParameterSetting[] parameters)
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

    public void IterationComplete()
    {
        // Write out the polish report with t-statistics
        WriteReport();
    }

    private void WriteReport()
    {
        var jobs = Root.CurrentJobs;
        var parameters = Root.Parameters;
        
        // Get zero and base values
        var zeroValue = _currentIteration == 0 ? jobs[0].Value : _zeroValue;
        var baseJobIndex = _currentIteration == 0 ? 1 : 0;
        var baseValue = jobs[baseJobIndex].Value;
        
        double[] standardErrors;
        double[] tStatistics;
        double[,] hessian;

        if (UseFullHessian)
        {
            // Compute full Hessian matrix
            hessian = ComputeFullHessian(jobs, baseJobIndex);
            
            // Invert negative Hessian to get covariance matrix
            var covariance = InvertSymmetricMatrix(NegateMatrix(hessian));
            
            // Standard errors are sqrt of diagonal elements
            standardErrors = new double[parameters.Count];
            tStatistics = new double[parameters.Count];
            
            for (int i = 0; i < parameters.Count; i++)
            {
                if (covariance != null && covariance[i, i] > 0)
                {
                    standardErrors[i] = Math.Sqrt(covariance[i, i]);
                    tStatistics[i] = jobs[baseJobIndex].Parameters[i].Current / standardErrors[i];
                }
                else
                {
                    standardErrors[i] = double.NaN;
                    tStatistics[i] = double.NaN;
                }
            }
        }
        else
        {
            // Diagonal approximation only
            hessian = new double[parameters.Count, parameters.Count];
            standardErrors = new double[parameters.Count];
            tStatistics = new double[parameters.Count];
            
            for (int i = 0; i < parameters.Count; i++)
            {
                var current = jobs[baseJobIndex].Parameters[i].Current;
                var delta = Delta * (1.0f + Math.Abs(current));
                
                // Get offset job indices
                var offsetBase = _currentIteration == 0 ? (i * 2 + 2) : (i * 2 + 1);
                var leftValue = jobs[offsetBase].Value;
                var rightValue = jobs[offsetBase + 1].Value;
                
                // Second derivative: d²LL/dθ² ≈ (LL(θ+δ) - 2*LL(θ) + LL(θ-δ)) / δ²
                hessian[i, i] = (rightValue - 2.0 * baseValue + leftValue) / (delta * delta);
                
                // Standard error = sqrt(-1 / d²LL/dθ²)
                if (hessian[i, i] < 0)
                {
                    var variance = -1.0 / hessian[i, i];
                    standardErrors[i] = Math.Sqrt(variance);
                    tStatistics[i] = current / standardErrors[i];
                }
                else
                {
                    standardErrors[i] = double.NaN;
                    tStatistics[i] = double.NaN;
                }
            }
        }
        
        // Write report
        using var writer = new StreamWriter(ReportFile);
        Span<char> buffer = stackalloc char[32];
        
        writer.WriteLine("Fitness,ZeroFitness,Rho^2");
        Functions.Utilities.Write(writer, baseValue, buffer);
        writer.Write(',');
        Functions.Utilities.Write(writer, zeroValue, buffer);
        writer.Write(',');
        Functions.Utilities.WriteLine(writer, GetRho(baseValue, zeroValue), buffer);
        
        writer.WriteLine("ParameterName,Coefficient,StandardError,t-statistic,LeftCoefficient,RightCoefficient,LeftLogLikelihood,RightLogLikelihood,Hessian_Diagonal");
        
        for (int i = 0; i < parameters.Count; i++)
        {
            var current = jobs[baseJobIndex].Parameters[i].Current;
            
            // Get the offset job indices for this parameter
            var offsetBase = _currentIteration == 0 ? (i * 2 + 2) : (i * 2 + 1);
            var leftCoef = jobs[offsetBase].Parameters[i].Current;
            var rightCoef = jobs[offsetBase + 1].Parameters[i].Current;
            var leftLL = jobs[offsetBase].Value;
            var rightLL = jobs[offsetBase + 1].Value;
            
            writer.Write('"');
            writer.Write(parameters[i].Names[0]);
            writer.Write('"');
            writer.Write(',');
            Functions.Utilities.Write(writer, current, buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, standardErrors[i], buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, tStatistics[i], buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, leftCoef, buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, rightCoef, buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, leftLL, buffer);
            writer.Write(',');
            Functions.Utilities.Write(writer, rightLL, buffer);
            writer.Write(',');
            Functions.Utilities.WriteLine(writer, hessian[i, i], buffer);
        }

        // If full Hessian, write correlation matrix
        if (UseFullHessian)
        {
            writer.WriteLine();
            writer.WriteLine("Correlation Matrix:");
            WriteCorrelationMatrix(writer, hessian, standardErrors, buffer);
        }
    }

    private double[,] ComputeFullHessian(List<Job> jobs, int baseJobIndex)
    {
        var n = Root.Parameters.Count;
        var hessian = new double[n, n];
        var baseValue = jobs[baseJobIndex].Value;
        
        // Compute diagonal elements
        for (int i = 0; i < n; i++)
        {
            var current = jobs[baseJobIndex].Parameters[i].Current;
            var delta = Delta * (1.0f + Math.Abs(current));
            
            var offsetBase = _currentIteration == 0 ? (i * 2 + 2) : (i * 2 + 1);
            var leftValue = jobs[offsetBase].Value;
            var rightValue = jobs[offsetBase + 1].Value;
            
            hessian[i, i] = (rightValue - 2.0 * baseValue + leftValue) / (delta * delta);
        }
        
        // Compute off-diagonal elements (mixed partials)
        var crossStartIndex = _currentIteration == 0 ? (2 + 2 * n) : (1 + 2 * n);
        int crossIndex = crossStartIndex;
        
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var deltaI = Delta * (1.0f + Math.Abs(jobs[baseJobIndex].Parameters[i].Current));
                var deltaJ = Delta * (1.0f + Math.Abs(jobs[baseJobIndex].Parameters[j].Current));
                
                // Get the 4 corner values: (i-,j-), (i-,j+), (i+,j-), (i+,j+)
                var llMinusMinus = jobs[crossIndex].Value;
                var llMinusPlus = jobs[crossIndex + 1].Value;
                var llPlusMinus = jobs[crossIndex + 2].Value;
                var llPlusPlus = jobs[crossIndex + 3].Value;
                
                // Mixed partial: ∂²LL/∂θᵢ∂θⱼ ≈ (LL(+,+) - LL(+,-) - LL(-,+) + LL(-,-)) / (4·δᵢ·δⱼ)
                var mixedPartial = (llPlusPlus - llPlusMinus - llMinusPlus + llMinusMinus) 
                                 / (4.0 * deltaI * deltaJ);
                
                hessian[i, j] = mixedPartial;
                hessian[j, i] = mixedPartial; // Symmetric
                
                crossIndex += 4;
            }
        }
        
        return hessian;
    }

    private double[,] NegateMatrix(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var result = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                result[i, j] = -matrix[i, j];
            }
        }
        return result;
    }

    private double[,] InvertSymmetricMatrix(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        
        // Create augmented matrix [A | I]
        var aug = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                aug[i, j] = matrix[i, j];
            }
            aug[i, n + i] = 1.0; // Identity on the right
        }
        
        // Gaussian elimination with partial pivoting
        for (int col = 0; col < n; col++)
        {
            // Find pivot
            int pivotRow = col;
            double maxVal = Math.Abs(aug[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(aug[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(aug[row, col]);
                    pivotRow = row;
                }
            }
            
            // Check for singular matrix
            if (Math.Abs(aug[pivotRow, col]) < 1e-10)
            {
                return null; // Singular matrix, can't invert
            }
            
            // Swap rows
            if (pivotRow != col)
            {
                for (int j = 0; j < 2 * n; j++)
                {
                    (aug[col, j], aug[pivotRow, j]) = (aug[pivotRow, j], aug[col, j]);
                }
            }
            
            // Scale pivot row
            double pivot = aug[col, col];
            for (int j = 0; j < 2 * n; j++)
            {
                aug[col, j] /= pivot;
            }
            
            // Eliminate column
            for (int row = 0; row < n; row++)
            {
                if (row != col)
                {
                    double factor = aug[row, col];
                    for (int j = 0; j < 2 * n; j++)
                    {
                        aug[row, j] -= factor * aug[col, j];
                    }
                }
            }
        }
        
        // Extract inverse from right half
        var inverse = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                inverse[i, j] = aug[i, n + j];
            }
        }
        
        return inverse;
    }

    private void WriteCorrelationMatrix(StreamWriter writer, double[,] hessian, double[] standardErrors, Span<char> buffer)
    {
        int n = hessian.GetLength(0);
        var parameters = Root.Parameters;
        
        // Write header
        writer.Write("Parameter");
        for (int j = 0; j < n; j++)
        {
            writer.Write(',');
            writer.Write('"');
            writer.Write(parameters[j].Names[0]);
            writer.Write('"');
        }
        writer.WriteLine();
        
        // Compute and write correlations
        for (int i = 0; i < n; i++)
        {
            writer.Write('"');
            writer.Write(parameters[i].Names[0]);
            writer.Write('"');
            
            for (int j = 0; j < n; j++)
            {
                writer.Write(',');
                if (i == j)
                {
                    Functions.Utilities.Write(writer, 1.0, buffer);
                }
                else if (!double.IsNaN(standardErrors[i]) && !double.IsNaN(standardErrors[j]))
                {
                    // Correlation = Cov(i,j) / (SE(i) * SE(j))
                    // Where Cov comes from inverted -Hessian
                    var correlation = hessian[i, j] / (standardErrors[i] * standardErrors[j]);
                    Functions.Utilities.Write(writer, correlation, buffer);
                }
                else
                {
                    writer.Write("NaN");
                }
            }
            writer.WriteLine();
        }
    }

    private float GetRho(float logLikelihood, float zeroLogLikelihood)
    {
        // McFadden's Rho-squared: 1 - (LL(θ) / LL(0))
        return 1.0f - (logLikelihood / zeroLogLikelihood);
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
