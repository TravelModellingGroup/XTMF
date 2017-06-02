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
namespace TMG.Estimation.AI
{
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
            using(var reader = new CsvReader(ResultFile.GetFilePath()))
            {
                int[] columnToParameterMap = CreateParameterMap(reader);
                var baseParameters = LoadBaseParameters(parameters, reader, columnToParameterMap);
                ret.Add(CreateZero(baseParameters));
                ret.Add(CreateJob(baseParameters));
                for(int i = 0; i < baseParameters.Length; i++)
                {
                    // we will need 4 points to approx the second derivative for now
                    ret.Add(CreateWithOffset(baseParameters, i, -Delta));
                    ret.Add(CreateWithOffset(baseParameters, i, Delta));
                }
            }
            return ret;
        }

        private Job CreateZero(ParameterSetting[] baseParameters)
        {
            var parameters = Clone(baseParameters);
            for(int i = 0; i < parameters.Length; i++)
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

        private ParameterSetting[] Clone(ParameterSetting[] parameters)
        {
            ParameterSetting[] ret = new ParameterSetting[parameters.Length];
            for(int i = 0; i < parameters.Length; i++)
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
            int columns;
            if(reader.LoadLine(out columns))
            {
                for(int i = 0; i < parameters.Length; i++)
                {
                    baseParameters[i] = new ParameterSetting()
                    {
                        Names = parameters[i].Names,
                        Minimum = parameters[i].Minimum,
                        Maximum = parameters[i].Maximum
                    };
                }

                for(int i = 0; i < columnMap.Length; i++)
                {
                    reader.Get(out baseParameters[columnMap[i]].Current, i + 2);
                }
            }
            return baseParameters;
        }

        private int[] CreateParameterMap(CsvReader reader)
        {
            var parameters = Root.Parameters.ToArray();
            int columns;
            reader.LoadLine( out columns );
            var ret = new int[columns - 2];
            for ( int i = 2; i < columns; i++ )
            {
                string name;
                reader.Get( out name, i );
                var selectedParameter = ( from p in parameters
                                          where p.Names.Contains( name )
                                          select p ).FirstOrDefault();
                if ( selectedParameter == null )
                {
                    throw new XTMFRuntimeException( "In '" + Name + " the parameter '" + name + "' could not be resolved." );
                }
                ret[i - 2] = IndexOf( parameters, selectedParameter );
            }
            return ret;
        }

        private int IndexOf(ParameterSetting[] parameters, ParameterSetting selectedParameter)
        {
            for(int i = 0; i < parameters.Length; i++)
            {
                if(parameters[i] == selectedParameter) return i;
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
            using(var writer = new StreamWriter(ReportFile))
            {
                writer.WriteLine("Fitness,ZeroFitness,Rho^2");
                writer.Write(baseValue);
                writer.Write(',');
                writer.Write(zeroValue);
                writer.Write(',');
                writer.WriteLine(GetRho(baseValue, zeroValue));
                writer.WriteLine("ParameterName,Coefficient,LeftCoefficient,RightCoefficient,LeftFitness,RightFitness,SecondDerivative,t-statistic");
                for(int i = 0; i < parameters.Count; i++)
                {
                    var secondDerivative = SecondDerivative(i);
                    var current = jobs[1].Parameters[i].Current;
                    int offset = i * 2 + 2;
                    writer.Write('"');
                    writer.Write(parameters[i].Names[0]);
                    writer.Write('"');
                    writer.Write(',');
                    writer.Write(current);
                    writer.Write(',');
                    writer.Write(jobs[offset].Parameters[i].Current);
                    writer.Write(',');
                    writer.Write(jobs[offset + 1].Parameters[i].Current);
                    writer.Write(',');
                    writer.Write(jobs[offset].Value);
                    writer.Write(',');
                    writer.Write(jobs[offset + 1].Value);
                    writer.Write(',');
                    writer.Write(secondDerivative);
                    writer.Write(',');
                    writer.WriteLine(ComputeTStatistic(current, secondDerivative));
                }
            }
        }

        private double ComputeTStatistic(float current, double secondDerivative)
        {
            var variance = (Maximize ? -1.0f : 1.0f) / secondDerivative;
            var std = Math.Sqrt(variance);
            return current / std;
        }

        private double SecondDerivative(int parameterIndex)
        {
            var jobs = Root.CurrentJobs;
            var parameters = Root.Parameters;
            var parameterDelta = Delta * (parameters[parameterIndex].Maximum - parameters[parameterIndex].Minimum);
            // 4 parameters per job, 2 jobs to get value and zero value
            var parameterOffset = parameterIndex * 2 + 2;
            return (jobs[parameterOffset + 1].Value - (2 * jobs[1].Value) + jobs[parameterOffset].Value)
                / (parameterDelta * parameterDelta);
        }

        private float GetRho(float current, float zeroParams)
        {
            return 1.0f - (current / zeroParams);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
