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
namespace TMG.Functions
{
    public static class ModelSystemReflection
    {
        public static void AssignValue(IModelSystemStructure root, string parameterName, string value)
        {
            string[] parts = SplitNameToParts(parameterName);
            AssignValue(parts, 0, root, value);
        }

        private static void AssignValue(string[] parts, int currentIndex, IModelSystemStructure currentStructure, string value)
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

        private static void AssignValue(string variableName, IModelSystemStructure currentStructure, string value)
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
                        string error = null;
                        object trueValue;
                        if((trueValue = ArbitraryParameterParser.ArbitraryParameterParse(parameters[i].Type, value, ref error)) != null)
                        {
                            parameters[i].Value = trueValue;
                            var type = currentStructure.Module.GetType();
                            if(parameters[i].OnField)
                            {
                                var field = type.GetField(parameters[i].VariableName);
                                field.SetValue(currentStructure.Module, trueValue);
                                any = true;
                            }
                            else
                            {
                                var field = type.GetProperty(parameters[i].VariableName);
                                field.SetValue(currentStructure.Module, trueValue, null);
                                any = true;
                            }
                        }
                        else
                        {
                            throw new XTMFRuntimeException("We were unable to assign the value of '" + value + "' to the parameter " + parameters[i].Name);
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

        private static string[] SplitNameToParts(string parameterName)
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

        /// <summary>
        /// Retrieve the model system structure from the given project
        /// </summary>
        /// <param name="project">The project to analyze</param>
        /// <param name="toFind">The module to find the structure of</param>
        /// <param name="modelSystemStructure">The model system structure of the module to find.</param>
        /// <returns>True if the module was found, false otherwise</returns>
        public static bool FindModuleStructure(IProject project, IModule toFind, ref IModelSystemStructure modelSystemStructure)
        {
            foreach(var ms in project.ModelSystemStructure)
            {
                if(FindModuleStructure(ms, toFind, ref modelSystemStructure))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieve the model system structure for the module the given root.
        /// </summary>
        /// <param name="root">The base module to look at</param>
        /// <param name="toFind">An instance of the module we are trying to find</param>
        /// <param name="modelSystemStructure">The resulting model system structure</param>
        /// <returns>True if the module was found, false otherwise.</returns>
        public static bool FindModuleStructure(IModelSystemStructure root, IModule toFind, ref IModelSystemStructure modelSystemStructure)
        {
            if(root.Module == toFind)
            {
                modelSystemStructure = root;
                return true;
            }
            if(root.Children != null)
            {
                foreach(var child in root.Children)
                {
                    if(FindModuleStructure(child, toFind, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }
    }
}
