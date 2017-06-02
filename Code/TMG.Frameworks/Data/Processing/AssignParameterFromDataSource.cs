/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Frameworks.Data.Processing
{

    public class AssignParameterFromDataSource<T> : ISelfContainedModule
    {
        [RunParameter("Parameter Path", "", "The path should be expressed in the 'Parent.Module.Parameter' format.")]
        public string ParameterPath;

        [RunParameter("Runtime Only", true, "If this is true the model system will not be altered.  If this is false then if the model system gets saved that parameter will be saved as well.")]
        public bool RuntimeOnly;

        [SubModelInformation(Required = true, Description = "The data source that will be loaded in order to assign tot he given parameter.")]
        public IDataSource<T> AssignFrom;

        private IConfiguration Config;

        private IModuleParameter Parameter;
        public AssignParameterFromDataSource(IConfiguration config)
        {
            Config = config;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            Parameter = Functions.ModelSystemReflection.FindParameter(Config, this, ParameterPath);
            if (Parameter == null)
            {
                error = "In '" + Name + "' we were unable to find a parameter with the path '" + ParameterPath + "'!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            if (!AssignFrom.Loaded)
            {
                AssignFrom.LoadData();
            }
            if (RuntimeOnly)
            {
                Functions.ModelSystemReflection.AssignValueRunOnly(Config, Parameter, AssignFrom.GiveData());
            }
            else
            {
                Functions.ModelSystemReflection.AssignValue(Config, Parameter, AssignFrom.GiveData());
            }
        }
    }

}
