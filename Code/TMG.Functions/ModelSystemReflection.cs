/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using XTMF;
namespace TMG.Functions
{
    public static class ModelSystemReflection
    {
        public static IModuleParameter FindParameter(IConfiguration config, IModule callingModule, string parameterPath)
        {
            var chain = BuildModelStructureChain(config, callingModule);
            if (chain != null)
            {
                var path = SplitNameToParts(parameterPath);
                return FindParameter(chain[0], 0, path, parameterPath);
            }
            return null;
        }

        /// <summary>
        /// Find the closest ancestor of the given module that implements
        /// the given type.
        /// </summary>
        /// <param name="config">The XTMF configuration</param>
        /// <param name="type">The type to search for</param>
        /// <param name="currentModule">The module to find the ancestor of.</param>
        /// <param name="result">The model system structure that implements this.</param>
        /// <returns>True if a module satisfied the query, False otherwise with a null for result.</returns>
        public static bool GetRootOfType(IConfiguration config, Type type, IModule currentModule, out IModelSystemStructure result)
        {
            var chain = BuildModelStructureChain(config, currentModule);
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if(chain[i].Type == type)
                {
                    result = chain[i];
                    return true;
                }
            }
            result = null;
            return false;
        }

        private static IModuleParameter FindParameter(IModelSystemStructure currentStructure, int currentIndex, string[] parts, string fullPath)
        {
            if (currentIndex == parts.Length - 1)
            {
                if (currentStructure.IsMetaModule)
                {
                    // if we are a meta-module any of our child modules might be being referenced.
                    var toGet = new Stack<IModelSystemStructure>();
                    toGet.Push(currentStructure);
                    while (toGet.Count > 0)
                    {
                        var current = toGet.Pop();
                        var p = current.Parameters;
                        if (p != null)
                        {
                            var parameters = p.Parameters;
                            if (parameters != null)
                            {
                                for (int i = 0; i < parameters.Count; i++)
                                {
                                    if (parameters[i].Name == parts[currentIndex])
                                    {
                                        return parameters[i];
                                    }
                                }
                                return null;
                            }
                        }
                        if (current.Children != null)
                        {
                            foreach (var c in current.Children)
                            {
                                toGet.Push(c);
                            }
                        }
                    }
                    return null;
                }
                else
                {
                    var p = currentStructure.Parameters;
                    if (p == null)
                    {
                        throw new XTMFRuntimeException("The structure '" + currentStructure.Name + "' has no parameters!");
                    }
                    var parameters = p.Parameters;
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            if (parameters[i].Name == parts[currentIndex])
                            {
                                return parameters[i];
                            }
                        }
                        return null;
                    }
                }
            }
            if (currentStructure.Children != null)
            {
                for (int i = 0; i < currentStructure.Children.Count; i++)
                {
                    if (currentStructure.Children[i].Name == parts[currentIndex])
                    {
                        return FindParameter(currentStructure.Children[i], currentIndex + 1, parts, fullPath);
                    }
                }
            }
            throw new XTMFRuntimeException("Unable to find a child module in '" + parts[currentIndex] + "' named '" + parts[currentIndex + 1]
                + "' in order to assign parameters!\r\nFull Path:'" + fullPath + "'");
        }

        public static void AssignValue(IModelSystemStructure root, string parameterName, string value)
        {
            string[] parts = SplitNameToParts(parameterName);
            var parameter = FindParameter(root, 0, parts, parameterName);
            if(parameter != null)
            {
                AssignValue(parameter, value);
            }
        }

        public static void AssignValue(IModuleParameter parameter, string value)
        {
            string error = null;
            object trueValue;
            if (parameter == null)
            {
                throw new XTMFRuntimeException("The parameter was null!");
            }
            if ((trueValue = ArbitraryParameterParser.ArbitraryParameterParse(parameter.Type, value, ref error)) != null)
            {
                AssignValueNoTypeCheck(parameter, trueValue);
            }
            else
            {
                throw new XTMFRuntimeException("We were unable to assign the value of '" + value + "' to the parameter " + parameter.Name);
            }
        }

        public static void AssignValue<T>(IModuleParameter parameter, T t)
        {
            if (parameter.Type != typeof(T))
            {
                throw new XTMFRuntimeException("The parameter " + parameter.Name + " was not of type " + typeof(T).FullName + "!");
            }
            AssignValueNoTypeCheck(parameter, t);
        }

        private static void AssignValueNoTypeCheck<T>(IModuleParameter parameter, T t)
        {
            var currentStructure = parameter.BelongsTo;
            if (currentStructure == null)
            {
                throw new XTMFRuntimeException("The parameter doesn't belong to any module!");
            }
            if (currentStructure.Module == null)
            {
                throw new XTMFRuntimeException("The currentstructure.Module was null!");
            }
            parameter.Value = t;
            var type = currentStructure.Module.GetType();
            if (parameter.OnField)
            {
                var field = type.GetField(parameter.VariableName);
                field.SetValue(currentStructure.Module, t);
            }
            else
            {
                var field = type.GetProperty(parameter.VariableName);
                field.SetValue(currentStructure.Module, t, null);
            }
        }

        public static void AssignValueRunOnly<T>(IModuleParameter parameter, T t)
        {
            if (parameter.Type != typeof(T))
            {
                throw new XTMFRuntimeException("The parameter " + parameter.Name + " was not of type " + typeof(T).FullName + "!");
            }
            var currentStructure = parameter.BelongsTo;
            if (currentStructure == null)
            {
                throw new XTMFRuntimeException("The parameter doesn't belong to any module!");
            }
            if (currentStructure.Module == null)
            {
                throw new XTMFRuntimeException("The currentstructure.Module was null!");
            }
            // Don't execute 'parameter.Value = t;'
            var type = currentStructure.Module.GetType();
            if (parameter.OnField)
            {
                var field = type.GetField(parameter.VariableName);
                field.SetValue(currentStructure.Module, t);
            }
            else
            {
                var field = type.GetProperty(parameter.VariableName);
                field.SetValue(currentStructure.Module, t, null);
            }
        }

        private static string[] SplitNameToParts(string parameterName)
        {
            // Allow the path to take the null parameter name so we can reuse this for
            // find modules given a path
            if (String.IsNullOrWhiteSpace(parameterName))
            {
                return new string[0];
            }
            List<string> parts = new List<string>();
            var stringLength = parameterName.Length;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < stringLength; i++)
            {
                switch (parameterName[i])
                {
                    case '.':
                        parts.Add(builder.ToString());
                        builder.Clear();
                        break;
                    case '\\':
                        if (i + 1 < stringLength)
                        {
                            if (parameterName[i + 1] == '.')
                            {
                                builder.Append('.');
                                i += 2;
                            }
                            else if (parameterName[i + 1] == '\\')
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
        /// Retrieve the model system structure from the active project
        /// </summary>
        /// <param name="config">The XTMF runtime configuration</param>
        /// <param name="toFind">The module to find the model system structure of</param>
        /// <param name="modelSystemStructure">The model system structure of the given module</param>
        /// <returns>True if the module was found, false otherwise</returns>
        public static bool FindModuleStructure(IConfiguration config, IModule toFind, ref IModelSystemStructure modelSystemStructure)
        {
            return FindModuleStructure(config.ProjectRepository.ActiveProject, toFind, ref modelSystemStructure);
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
            foreach (var ms in project.ModelSystemStructure)
            {
                if (FindModuleStructure(ms, toFind, ref modelSystemStructure))
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
            if (root.Module == toFind)
            {
                modelSystemStructure = root;
                return true;
            }
            if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    if (FindModuleStructure(child, toFind, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        /// <summary>
        /// Gets the ancestry of a module from the active project
        /// </summary>
        /// <param name="config">The XTMF runtime configuration</param>
        /// <param name="toFind">The module to find</param>
        /// <returns>A list in order from root to the module to find of their model system structures.</returns>
        public static List<IModelSystemStructure> BuildModelStructureChain(IConfiguration config, IModule toFind)
        {
            return BuildModelStructureChain(config.ProjectRepository.ActiveProject, toFind);
        }

        /// <summary>
        /// Gets the ancestry of a module given a project
        /// </summary>
        /// <param name="project">The project to analyze</param>
        /// <param name="toFind">The module to find</param>
        /// <returns>A list in order from root to the module to find of their model system structures.</returns>
        public static List<IModelSystemStructure> BuildModelStructureChain(IProject project, IModule toFind)
        {
            List<IModelSystemStructure> chain = new List<IModelSystemStructure>();
            foreach (var ms in project.ModelSystemStructure)
            {
                if (BuildModelStructureChain(ms, toFind, chain))
                {
                    break;
                }
            }
            return chain;
        }

        /// <summary>
        /// Gets the ancestry of a module from the given root.
        /// </summary>
        /// <param name="root">The first node to look at.</param>
        /// <param name="toFind">The module to find</param>
        /// <param name="chain">The chain to store the results into.</param>
        /// <returns>True if we found the module, false otherwise</returns>
        public static bool BuildModelStructureChain(IModelSystemStructure root, IModule toFind, List<IModelSystemStructure> chain)
        {
            if (root.Module == toFind)
            {
                chain.Add(root);
                return true;
            }
            if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    if (BuildModelStructureChain(child, toFind, chain))
                    {
                        chain.Insert(0, root);
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        /// <summary>
        /// Get a model system structure from a path string relative to the root
        /// </summary>
        /// <param name="root">The point to start the path relative to.</param>
        /// <param name="path">The path to explore</param>
        /// <param name="structure">The structure at the end of the path</param>
        /// <returns>True if a model system structure with that path was found, false otherwise</returns>
        public static bool GetModelSystemStructureFromPath(IModelSystemStructure root, string path, ref IModelSystemStructure structure)
        {
            var parts = SplitNameToParts(path);
            IModelSystemStructure current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var children = current.Children;
                if (children == null)
                {
                    return false;
                }
                bool found = false;
                for (int j = 0; j < children.Count; j++)
                {
                    if (children[j].Name == parts[i])
                    {
                        current = children[j];
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            structure = current;
            return true;
        }
    }
}
