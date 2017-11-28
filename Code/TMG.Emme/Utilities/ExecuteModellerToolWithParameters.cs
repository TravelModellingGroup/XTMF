/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using XTMF;

namespace TMG.Emme.Utilities
{
    [ModuleInformation(Description = "Execute a given EMME modeller tool with the given name and parameters.")]
    public sealed class ExecuteModellerToolWithParameters : IEmmeTool
    {
        [ModuleInformation(Description = "A representation of an EMMETool parameter and the value to assign to it.")]
        public class Parameter : IModule
        {
            [RunParameter("Parameter Name", "", "The name of the parameter to assign the given value.")]
            public string ParameterName;

            [RunParameter("Parameter Value", "", "The value of the parameter to assign the given parameter name.")]
            public string ParameterValue;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => throw new NotImplementedException();

            public bool RuntimeValidation(ref string error)
            {
                if(String.IsNullOrWhiteSpace(ParameterName))
                {
                    error = $"In {Name} the parameter name was not selected for!";
                    return false;
                }
                return true;
            }
        }

        [RunParameter("ModuleName", "", "The name of the EMME modeller tool to execute.")]
        public string ModuleName;

        [SubModelInformation(Required = false, Description = "The parameters to assign to execute the given module.")]
        public Parameter[] Parameters;

        public string Name { get; set; }

        public float Progress { get; private set; }

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public bool Execute(Controller controller)
        {
            if (controller is ModellerController mc)
            {
                string ret = null;
                mc.Run(this, ModuleName, GetArguments(), (p) => Progress = p, ref ret);
                return true;
            }
            return false;
        }

        private ModellerControllerParameter[] GetArguments()
        {
            return (from p in Parameters
                    select new ModellerControllerParameter(p.ParameterName, p.ParameterValue))
                    .ToArray();
        }

        public bool RuntimeValidation(ref string error)
        {
            if(String.IsNullOrWhiteSpace(ModuleName))
            {
                error = $"In {Name} the Module Name was not specified!";
                return false;
            }
            return true;
        }
    }
}
