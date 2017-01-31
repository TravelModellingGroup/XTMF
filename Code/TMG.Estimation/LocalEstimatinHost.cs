using System;
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMG.Input;
using XTMF;

namespace TMG.Estimation
{
    public class LocalEstimatinHost : IEstimationHost
    {

        [SubModelInformation(Required = true, Description = "The AI to explore the parameter space.")]
        // ReSharper disable once InconsistentNaming
        public IEstimationAI AI;

        [SubModelInformation(Required = true, Description = "The client model system to execute.")]
        public IEstimationClientModelSystem ClientModelSystem;

        public bool Exit = false;

        [RunParameter("Hold Onto Result File", true, "Should we maintain the lock on the estimation file?")]
        public bool HoldOnToResultFile;

        [SubModelInformation(Required = false, Description = "The host model system to execute.")]
        public IModelSystemTemplate HostModelSystem;

        [SubModelInformation(Required = true, Description = "The logic to load in parameters.")]
        public IDataSource<List<ParameterSetting>> ParameterLoader;

        [SubModelInformation(Required = true, Description = "The location to save the estimation results.")]
        public FileLocation ResultFile;

        [RunParameter("SkipReportingResults", false, "Skip Reporting Results.  Only turn this on for increased performance during the estimation of AI's.")]
        public bool SkipReportingResults;

        public int CurrentIteration { get; set; }

        [RunParameter("Generations", "100", typeof(int), "The total number of iterations we should push the AI through.")]
        public int TotalIterations { get; set; }

        public List<Job> CurrentJobs { get; set; }

        [RunParameter("Input Directory", "../../Input", "The directory containing the model's input.")]
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        public List<ParameterSetting> Parameters { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>(50, 150, 50);
            }
        }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            InitializeHost();
            LoadParameters();
            RunIterations();
            Status = () => "Run Complete";
        }

        private int CurrentJobIndex;


        public event Action<Job, int, float> FitnessFunctionEvaluated;

        private void RunIterations()
        {
            Progress = 0.0f;
            Status = () => "Running iteration " + (CurrentIteration + 1) + " of " + TotalIterations;
            for (CurrentIteration = 0; !Exit & CurrentIteration < TotalIterations; CurrentIteration++)
            {
                Progress = (float)CurrentIteration / TotalIterations;
                CurrentJobIndex = 0;
                CurrentJobs = AI.CreateJobsForIteration();
                ClientModelSystem.Start();
                if (!SkipReportingResults)
                {
                    SaveResultsToDisk();
                }
                AI.IterationComplete();
            }
            Progress = 1.0f;
        }

        private void SaveResultsToDisk()
        {
            while (true)
            {
                try
                {
                    using (var writer = new StreamWriter(ResultFile.GetFilePath(), true))
                    {
                        if (CurrentIteration == 0)
                        {
                            // write header here
                            StringBuilder header = new StringBuilder();
                            header.Append("Generation,Value");
                            for (int i = 0; i < Parameters.Count; i++)
                            {
                                for (int j = 0; j < Parameters[i].Names.Length; j++)
                                {
                                    header.Append(',');
                                    header.Append('"');
                                    header.Append(Parameters[i].Names[j]);
                                    header.Append('"');
                                }
                            }
                            writer.WriteLine(header.ToString());
                        }
                        for (int i = 0; i < CurrentJobs.Count; i++)
                        {
                            var currentJob = CurrentJobs[i];
                            writer.Write(CurrentIteration);
                            writer.Write(',');
                            writer.Write(currentJob.Value);
                            for (int j = 0; j < currentJob.Parameters.Length; j++)
                            {
                                for (int k = 0; k < Parameters[j].Names.Length; k++)
                                {
                                    writer.Write(',');
                                    // this uses the i th value since they are all the same
                                    writer.Write(currentJob.Parameters[j].Current);
                                }
                            }
                            writer.WriteLine();
                        }
                        break;
                    }
                }
                catch
                {
                    Status = () => "Unable to write to results file.";
                    // let them close the file
                    System.Threading.Thread.Sleep(10);
                    if (Exit) break;
                }
            }
        }

        public Job GiveJob()
        {
            if (!Exit & CurrentJobIndex < CurrentJobs.Count)
            {
                return CurrentJobs[CurrentJobIndex];
            }
            return null;
        }

        public void SaveResult(float result)
        {
            if (CurrentJobIndex < CurrentJobs.Count)
            {
                CurrentJobs[CurrentJobIndex].Value = result;
                var e = FitnessFunctionEvaluated;
                if (e != null)
                {
                    e(CurrentJobs[CurrentJobIndex], CurrentIteration, result);
                }
                CurrentJobIndex++;
            }
            Progress = ((float)CurrentIteration / TotalIterations) + ((float)CurrentJobIndex) / (CurrentJobs.Count * TotalIterations);
        }

        private void InitializeHost()
        {
            if (HostModelSystem != null)
            {
                Status = () => "Running host model system";
                HostModelSystem.Start();
            }
        }

        private void LoadParameters()
        {
            Status = () => "Loading Parameters";
            ParameterLoader.LoadData();
            Parameters = ParameterLoader.GiveData();
            ParameterLoader.UnloadData();
        }

        private Func<string> Status = () => "Initializing";

        public override string ToString()
        {
            return Status();
        }
    }
}
