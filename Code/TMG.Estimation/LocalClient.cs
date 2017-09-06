/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Globalization;
using System.Linq;
using XTMF;

namespace TMG.Estimation
{
    public class LocalClient : IEstimationClientModelSystem
    {

        [RootModule]
        public LocalEstimatinHost Root;

        private IModelSystemStructure ClientStructure;

        private IConfiguration XtmfConfig;
        public LocalClient(IConfiguration config)
        {
            XtmfConfig = config;
        }

        private volatile bool Exit;

        public ClientTask CurrentTask
        {
            get;
            set;
        }

        public string InputBaseDirectory { get; set; }

        public IModelSystemTemplate MainClient { get; set; }

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        public ParameterSetting[] Parameters { get; set; }

        [RunParameter("Save Parameters", false, "Should we save the parameters into the model system?")]
        public bool SaveParameters;


        public float Progress => MainClient.Progress;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>(50, 150, 50);
            }
        }

        public Func<float> RetrieveValue { get; set; }

        private void InitializeParameters(ClientTask task)
        {
            string error = null;
            for (int i = 0; i < task.ParameterValues.Length && i < Parameters.Length; i++)
            {
                for(int j = 0; j < Parameters[i].Names.Length; j++)
                {
                    
                    if (
                        !Functions.ModelSystemReflection.AssignValue(XtmfConfig, ClientStructure, Parameters[i].Names[j],
                            task.ParameterValues[i].ToString(CultureInfo.InvariantCulture), ref error))
                    {
                        throw new XTMFRuntimeException(this, $"In '{Name}' we encountered an error when trying to assign parameters.\r\n{error}");
                    }
                }
            }
            SaveParametersIfNeeded();
        }

        private void SaveParametersIfNeeded()
        {
            if(SaveParameters)
            {
                string error = null;
                if(!XtmfConfig.ProjectRepository.ActiveProject.Save(ref error))
                {
                    throw new XTMFRuntimeException(this, "We were unable to save the project! " + error);
                }
            }
        }

        public bool ExitRequest()
        {
            Exit = true;
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!Functions.ModelSystemReflection.FindModuleStructure(XtmfConfig, MainClient, ref ClientStructure))
            {
                error = "In '" + Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            Exit = false;
            Parameters = Root.Parameters.ToArray();
            Job job;
            while(Exit != true && (job = Root.GiveJob()) != null)
            {
                CreateClientTask(job);
                InitializeParameters(CurrentTask);
                MainClient.Start();
                ReportResult();
            }
            Exit = true;
        }

        private void CreateClientTask(Job job)
        {
            CurrentTask = new ClientTask()
            {
                Generation = -1,
                Index = -1,
                ParameterValues = (from param in job.Parameters
                                   select param.Current).ToArray(),
                Result = float.NaN
            };
        }

        private void ReportResult()
        {
            var result = RetrieveValue == null ? float.NaN : RetrieveValue();
            Root.SaveResult(result);
        }
    }
}
