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
using System.Linq;
using System.Text;
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


        public float Progress
        {
            get
            {
                return MainClient.Progress;
            }
        }

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
            for(int i = 0; i < task.ParameterValues.Length && i < Parameters.Length; i++)
            {
                for(int j = 0; j < Parameters[i].Names.Length; j++)
                {
                    AssignValue(Parameters[i].Names[j], task.ParameterValues[i]);
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
                    throw new XTMFRuntimeException("We were unable to save the project! " + error);
                }
            }
        }

        private void AssignValue(string parameterName, float value)
        {
            string[] parts = SplitNameToParts(parameterName);
            AssignValue(parts, 0, ClientStructure, value);
        }

        private void AssignValue(string[] parts, int currentIndex, IModelSystemStructure currentStructure, float value)
        {
            if(currentIndex == parts.Length - 1)
            {
                AssignValue(parts[currentIndex], currentStructure, value);
                return;
            }
            if(currentStructure.Children != null)
            {
                for(int i = 0; i < currentStructure.Children.Count; i++)
                {
                    if(currentStructure.Children[i].Name == parts[currentIndex])
                    {
                        AssignValue(parts, currentIndex + 1, currentStructure.Children[i], value);
                        return;
                    }
                }
            }
            throw new XTMFRuntimeException("Unable to find a child module in '" + parts[currentIndex] + "' named '" + parts[currentIndex + 1]
                + "' in order to assign parameters!");
        }

        private void AssignValue(string variableName, IModelSystemStructure currentStructure, float value)
        {
            if(currentStructure == null)
            {
                throw new XTMFRuntimeException("Unable to assign '" + variableName + "', the module is null!");
            }
            var p = currentStructure.Parameters;
            if(p == null)
            {
                throw new XTMFRuntimeException("The structure '" + currentStructure.Name + "' has no parameters!");
            }
            var parameters = p.Parameters;
            bool any = false;
            if(parameters != null)
            {
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].Name == variableName)
                    {
                        parameters[i].Value = value;
                        var type = currentStructure.Module.GetType();
                        if(parameters[i].OnField)
                        {
                            var field = type.GetField(parameters[i].VariableName);
                            field.SetValue(currentStructure.Module, value);
                            any = true;
                        }
                        else
                        {
                            var field = type.GetProperty(parameters[i].VariableName);
                            field.SetValue(currentStructure.Module, value, null);
                            any = true;
                        }
                    }
                }
            }
            if(!any)
            {
                throw new XTMFRuntimeException("Unable to find a parameter named '" + variableName
                    + "' for module '" + currentStructure.Name + "' in order to assign it a parameter!");
            }
        }

        private string[] SplitNameToParts(string parameterName)
        {
            List<string> parts = new List<string>();
            var stringLength = parameterName.Length;
            StringBuilder builder = new StringBuilder();
            for(int i = 0; i < stringLength; i++)
            {
                switch(parameterName[i])
                {
                case '.':
                    parts.Add(builder.ToString());
                    builder.Clear();
                    break;
                case '\\':
                    if(i + 1 < stringLength)
                    {
                        if(parameterName[i + 1] == '.')
                        {
                            builder.Append('.');
                            i += 2;
                        }
                        else if(parameterName[i + 1] == '\\')
                        {
                            builder.Append('\\');
                        }
                    }
                    break;
                default:
                    builder.Append(parameterName[i]);
                    break;
                }
            }
            parts.Add(builder.ToString());
            return parts.ToArray();
        }

        public bool ExitRequest()
        {
            Exit = true;
            return false;
        }

        private bool FindUs(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
        {
            if(mst.Module == this)
            {
                modelSystemStructure = mst;
                return true;
            }
            if(mst.Children != null)
            {
                foreach(var child in mst.Children)
                {
                    if(FindUs(child, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            IModelSystemStructure ourStructure = null;
            foreach(var mst in XtmfConfig.ProjectRepository.ActiveProject.ModelSystemStructure)
            {
                if(FindUs(mst, ref ourStructure))
                {
                    foreach(var child in ourStructure.Children)
                    {
                        if(child.ParentFieldName == "MainClient")
                        {
                            ClientStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if(ClientStructure == null)
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
