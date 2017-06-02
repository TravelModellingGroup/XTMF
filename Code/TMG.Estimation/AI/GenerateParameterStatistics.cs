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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datastructure;
using TMG.Input;
using XTMF;
namespace TMG.Estimation.AI
{
    [ModuleInformation(Description = "Produces a report to show the significance of the parameters in the model given a set of betas from a previous estimation run.")]
    public class GenerateParameterStatistics : IEstimationAI
    {
        [RootModule]
        public IEstimationHost Root;

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
                    ret.Add(CreateWithout(baseParameters, i));
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

        private Job CreateWithout(ParameterSetting[] baseParameters, int index)
        {
            var parameters = Clone(baseParameters);
            parameters[index].Current = 0.0f;
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
            var baseRho = GetRho(baseValue, zeroValue);
            using(var writer = new StreamWriter(ReportFile))
            {
                writer.WriteLine("ParameterName,Coefficient,Fitness,Rho^2,DeltaRho^2");
                for(int i = 2; i < jobs.Count; i++)
                {
                    var withoutParameterValue = jobs[i].Value;
                    var rho = GetRho(withoutParameterValue, zeroValue);
                    writer.Write(parameters[i - 2].Names[0]);
                    writer.Write(',');
                    writer.Write(jobs[1].Parameters[i - 2].Current);
                    writer.Write(',');
                    writer.Write(withoutParameterValue);
                    writer.Write(',');
                    writer.Write(rho);
                    writer.Write(',');
                    writer.WriteLine(rho - baseRho);
                }
            }
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
