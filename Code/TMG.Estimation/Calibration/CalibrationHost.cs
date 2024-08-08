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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;

namespace TMG.Estimation.Calibration;

[ModuleInformation(Description = "The root module for an automatic calibration model system.")]
public sealed class CalibrationHost : IModelSystemTemplate, IResourceSource
{
    /// <summary>
    /// Set to true if the calibration should exit early.
    /// </summary>
    private volatile bool _exit = false;

    [RunParameter("Input Base Directory", ".", "The base directory for the input files.")]
    public string InputBaseDirectory { get; set; }

    [RunParameter("Max Iterations", 10, "The maximum number of steps to use for calibration.")]
    public int MaxIterations;

    [SubModelInformation(Required = false, Description = "Modules to run before the calibration starts.", Index = 0)]
    public ISelfContainedModule[] PreRun;

    [SubModelInformation(Required = true, Description = "The model system to calibrate.", Index = 1)]
    public IModelSystemTemplate Client;

    [SubModelInformation(Required = false, Description = "Modules to run after the calibration completes, if not terminated early.", Index = 2)]
    public ISelfContainedModule[] PostRun;

    [SubModelInformation(Required = false, Description = "The location to store the calibration report to.", Index = 3)]
    public FileLocation CalibrationReport;

    [SubModelInformation(Required = true, Description = "The targets to calibrate to.")]
    public CalibrationTarget[] Targets;

    [RunParameter("Just Compute Against Targets", false, "Instead of doing a full run instead just run the parameters as they are and compare against the targets.")]
    public bool JustComputeAgainstTargets;

    public string OutputBaseDirectory { get; set; }

    public bool ExitRequest()
    {
        try
        {
            Client.ExitRequest();
            return true;
        }
        finally
        {
            _exit = true;
        }
    }

    [RunParameter("Save Parameters", true, "Should we save the parameters at the end of the calibration?")]
    public bool SaveParameters;

    private IConfiguration _config;

    public CalibrationHost(IConfiguration config)
    {
        _config = config;
    }

    private Func<float> _progress = null;
    private Func<string> _status = null;

    private record struct JobTracker(Job Job, int TargetIndex, int RunForTarget);

    public void Start()
    {
        if (JustComputeAgainstTargets)
        {
            MaxIterations = 1;
        }
        // Get the initial position from the model system.
        LoadTargets();
        _status = () => "Initializing the calibration run.";
        ParameterSetting[] position = LoadPosition();
        for (int i = 0; i < PreRun.Length && !_exit; i++)
        {
            PreRun[i].Start();
            Thread.MemoryBarrier();
        }
        int iteration = 0;
        _status = () => $"Running calibration iteration {iteration + 1} of {MaxIterations}";
        for (; iteration < MaxIterations && !_exit; iteration++)
        {
            // Compute the jobs to run
            JobTracker[] toRun = ComputeJobsForIteration(position, iteration);
            // Execute the jobs and update the Targets
            RunJobs(toRun, iteration);
            // Evaluate the results
            ComputeNewPosition(position);
            RunSaveParameters(position);
            StoreResults(position, iteration);
            // Store the calibration results
            Thread.MemoryBarrier();
        }
        _status = () => "Running post calibration.";
        for (int i = 0; i < PostRun.Length && !_exit; i++)
        {
            PostRun[i].Start();
            Thread.MemoryBarrier();
        }
        _status = () => "Complete";
    }

    private void RunSaveParameters(ParameterSetting[] position)
    {
        if (!SaveParameters || JustComputeAgainstTargets)
        {
            return;
        }
        for (int i = 0; i < Targets.Length; i++)
        {
            Targets[i].SetParameterAndSave(position[i].Current);
        }
        string error = null;
        if (!_config.ProjectRepository.ActiveProject.Save(ref error))
        {
            throw new XTMFRuntimeException(this, error);
        }
    }

    private void StoreResults(ParameterSetting[] current, int iteration)
    {
        if (CalibrationReport is null) return;

        using var writer = new StreamWriter(CalibrationReport, true);
        if (iteration == 0)
        {
            // Write the header
            writer.Write("Iteration");
            for (int i = 0; i < Targets.Length; i++)
            {
                writer.Write($",TargetDistance-{Targets[i].Name}");
            }
            for (int i = 0; i < Targets.Length; i++)
            {
                writer.Write($",Value-{Targets[i].Name}");
            }
            writer.WriteLine();
        }
        writer.Write(iteration);
        for (int i = 0; i < Targets.Length; i++)
        {
            writer.Write($",{Targets[i].ReportTargetDistance()}");
        }
        for (int i = 0; i < Targets.Length; i++)
        {
            writer.Write($",{current[i].Current}");
        }
        writer.WriteLine();

    }

    private void LoadTargets()
    {
        Parallel.ForEach(Targets, target => target.Intialize(Client));
    }

    private ParameterSetting[] LoadPosition()
    {
        ParameterSetting[] position = new ParameterSetting[Targets.Length];
        for (int i = 0; i < Targets.Length; i++)
        {
            position[i] = new ParameterSetting()
            {
                Current = Targets[i].GetParameterValue()
            };
        }
        return position;
    }

    private JobTracker[] ComputeJobsForIteration(ParameterSetting[] currentPosition, int iteration)
    {

        if (JustComputeAgainstTargets)
        {
            var jobs = new JobTracker[1];
            jobs[0] = new JobTracker(new Job()
            {
                Parameters = currentPosition,
                Processed = false,
                ProcessedBy = null,
                Processing = false,
                Value = 0
            }, -1, -1);
            return jobs;
        }
        var jobList = new List<JobTracker>();
        for (int i = 0; i < Targets.Length; i++)
        {
            var additionalJobs = Targets[i].CreateAdditionalRuns(currentPosition, iteration, i);
            if (additionalJobs is not null)
            {
                var jobCount = 0;
                foreach (var parameterSet in additionalJobs)
                {
                    Job job = new()
                    {
                        Parameters = parameterSet,
                        Processed = false,
                        ProcessedBy = null,
                        Processing = false,
                        Value = 0
                    };
                    var jobTracker = new JobTracker(job, i, jobCount);
                    jobList.Add(jobTracker);
                    jobCount++;
                }
            }
        }
        // The last index is our base
        var baseJob = new JobTracker(new Job()
        {
            Parameters = currentPosition,
            Processed = false,
            ProcessedBy = null,
            Processing = false,
            Value = 0
        }, -1, -1);
        jobList.Add(baseJob);
        return [.. jobList];
    }

    private void ComputeNewPosition(ParameterSetting[] position)
    {
        for (int i = 0; i < Targets.Length; i++)
        {
            var updatedValue = Targets[i].UpdateParameter(position[i].Current);
            if (!JustComputeAgainstTargets)
            {
                position[i].Current = updatedValue;
            }
        }
    }

    private void RunJobs(JobTracker[] jobs, int iteration)
    {
        // Run the jobs
        int i = 0;
        _progress = () => ((float)iteration / MaxIterations) + (float)i / (MaxIterations * jobs.Length);
        for (; i < jobs.Length; i++)
        {
            var targetIndex = jobs[i].TargetIndex;
            var runForTarget = jobs[i].RunForTarget;
            var jobParameters = jobs[i].Job.Parameters;
            for (int j = 0; j < jobParameters.Length; j++)
            {
                Targets[j].SetParameterValue(jobParameters[j].Current);
            }
            Client.Start();
            if (targetIndex >= 0)
            {
                Targets[targetIndex].StoreRun(runForTarget);
            }
            else
            {
                // If this is the base job, store the results for all targets
                Parallel.ForEach(Targets, target => target.StoreRun(-1));
            }
        }
    }

    public string Name { get; set; } = string.Empty;

    public float Progress => _progress?.Invoke() ?? 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (MaxIterations < 1)
        {
            error = "The maximum number of iterations must be at least 1.";
            return false;
        }
        return true;
    }

    [SubModelInformation(Required = false, Description = "Resources to use for this model system.")]
    public List<IResource> Resources { get; set; }

    public override string ToString()
    {
        var status = _status?.Invoke();
        return status is not null ? status : base.ToString();
    }

}
