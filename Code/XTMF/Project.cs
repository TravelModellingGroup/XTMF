/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using XTMF.Annotations;
using XTMF.Attributes;
using XTMF.Editing;
using XTMF.Interfaces;
using XTMF.Logging;
using XTMF.Networking;

namespace XTMF
{
    /// <summary>
    ///     Represents a project currently installed in
    ///     the XTMF installation
    /// </summary>
    public sealed partial class Project : IProject
    {
        private Project _ClonedFrom;

        /// <summary>
        ///     The configuration object used for XTMF
        /// </summary>
        private readonly IConfiguration _Configuration;

        private string _DirectoryLocation;

        /// <summary>
        ///     This will be set to true once everything is ready for this project
        /// </summary>
        private volatile bool _IsLoaded;

        private List<ProjectModelSystem> _ProjectModelSystems = new List<ProjectModelSystem>();

        private readonly bool RemoteProject;

        /// <summary>
        ///     Create a new project
        ///     For internal use only
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="configuration">A link to the configuration that XTMF is using</param>
        /// <param name="remoteProject">Are we a remote project?</param>
        public Project(string name, IConfiguration configuration, bool remoteProject = false)
        {
            Name = name;
            _Configuration = configuration;
            LoadDescription();
            RemoteProject = remoteProject;
        }

        /// <summary>
        ///     This constructor is will clone a project.
        /// </summary>
        private Project(Project toClone)
        {
            _IsLoaded = true;
            Name = toClone.Name;
            var numberOfModelSystems = toClone.ModelSystemStructure.Count;
            _DirectoryLocation = toClone._DirectoryLocation;
            _Configuration = toClone._Configuration;
            _ClonedFrom = toClone;
            var loadTo = new ProjectModelSystem[numberOfModelSystems];
            Parallel.For(0, numberOfModelSystems, i =>
            {
                var mss = toClone.CloneModelSystemStructure(out var lp,
                    out var regionDisplays, i);
                loadTo[i] = new ProjectModelSystem
                {
                    Root = mss,
                    LinkedParameters = lp,
                    Description = mss?.Description ?? "No Description",
                    GUID = Guid.NewGuid().ToString(),
                    RegionDisplays = regionDisplays
                };
            });
            _ProjectModelSystems = loadTo.ToList();
        }

        /// <summary>
        /// </summary>
        public IReadOnlyList<List<IRegionDisplay>> RegionDisplays
        {
            get
            {
                SetActive();
                return _ProjectModelSystems.Select(pms => pms.RegionDisplays).ToList();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IReadOnlyList<string> ModelSystemDescriptions
        {
            get
            {
                SetActive();
                return _ProjectModelSystems.Select(pms => pms.Description).ToList();
            }
        }

        /// <summary>
        ///     Finds the index of the given model system.
        ///     Returns -1 if it is not found.
        /// </summary>
        /// <param name="realModelSystemStructure">The model system to find.</param>
        /// <returns>The index for this model system, -1 if it is not found.</returns>
        public int IndexOf(IModelSystemStructure realModelSystemStructure)
        {
            if (realModelSystemStructure == null)
            {
                throw new ArgumentNullException(nameof(realModelSystemStructure));
            }

            for (var i = 0; i < _ProjectModelSystems.Count; i++)
            {
                if (_ProjectModelSystems[i]?.Root == realModelSystemStructure)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The Project Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Dirty flag on whether changes have been made or not
        /// </summary>
        public bool HasChanged { get; set; }

        /// <summary>
        /// </summary>
        public IReadOnlyList<List<ILinkedParameter>> LinkedParameters
        {
            get
            {
                SetActive();
                return _ProjectModelSystems.Select(pms => pms.LinkedParameters).ToList();
            }
        }

        /// <summary>
        /// Model System Structures associated with this project
        /// </summary>
        public IReadOnlyList<IModelSystemStructure> ModelSystemStructure
        {
            get
            {
                SetActive();
                return _ProjectModelSystems.Select(pms => pms.Root).ToList();
            }
        }

        /// <summary>
        ///     The name of the Project
        /// </summary>
        public string Name { get; set; }

        public IModelSystemTemplate CreateModelSystem(ref string error, int modelSystemIndex)
        {
            // Pre-Validate the structure
            if ((modelSystemIndex < 0) | (modelSystemIndex >= ModelSystemStructure.Count))
            {
                throw new XTMFRuntimeException(null,
                    "The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                    ModelSystemStructure.Count);
            }

            return CreateModelSystem(ref error, _Configuration, ModelSystemStructure[modelSystemIndex]);
        }

        /// <summary>
        /// Reloads the Project
        /// </summary>
        public void Reload()
        {
            _ProjectModelSystems.Clear();
            string error = null;
            if (!Load(ref error))
            {
                throw new Exception(error);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool Save(ref string error)
        {
            var dirName = Path.Combine(_Configuration.ProjectDirectory, Name);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            bool ret;
            if (((Configuration) _Configuration).DivertSaveRequests)
            {
                ret = true;
                ExternallySaved?.Invoke(this, new ProjectExternallySavedEventArgs(this, null));
            }
            else
            {
                ret = Save(Path.Combine(dirName, "Project.xml"), ref error);
                if (_ClonedFrom != null)
                {
                    var e = _ClonedFrom.ExternallySaved;
                    // swap the project that this was a clone of and replace the real project.
                    ((ProjectRepository) _Configuration.ProjectRepository).ReplaceProjectFromClone(_ClonedFrom, this);
                    e?.Invoke(_ClonedFrom, new ProjectExternallySavedEventArgs(_ClonedFrom, this));
                }
            }

            return ret;
        }

        /// <summary>
        /// Validates a Model name
        /// </summary>
        /// <param name="possibleNewName"></param>
        /// <returns></returns>
        public bool ValidateModelName(string possibleNewName)
        {
            if (string.IsNullOrEmpty(possibleNewName))
            {
                return false;
            }

            // It can not be the Project because that is reserved for the project
            if (possibleNewName == "Project")
            {
                return false;
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (possibleNewName.Contains(invalidChar))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a Model System to this Project
        /// </summary>
        /// <param name="root"></param>
        /// <param name="lps"></param>
        /// <param name="description"></param>
        public void AddModelSystem(IModelSystemStructure root, List<ILinkedParameter> lps, string description)
        {
            _ProjectModelSystems.Add(new ProjectModelSystem
            {
                Root = root,
                LinkedParameters = lps,
                Description = description,
                GUID = Guid.NewGuid().ToString()
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <param name="newName"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool AddModelSystem(ModelSystem modelSystem, string newName, ref string error)
        {
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }

            var clone = CloneModelSystemStructure(modelSystem, out var linkedParameters,
                out var regionDisplays);
            if (clone == null)
            {
                error = "Unable to clone the model system.";
                return false;
            }

            clone.Name = newName;
            _ProjectModelSystems.Add(new ProjectModelSystem
            {
                Root = clone,
                LinkedParameters = linkedParameters,
                Description = modelSystem.Description,
                GUID = Guid.NewGuid().ToString(),
                RegionDisplays = regionDisplays
            });
            return Save(ref error);
        }

        internal bool AddExternalModelSystem(IModelSystem system, ref string error)
        {
            _ProjectModelSystems.Add(new ProjectModelSystem
            {
                Root = system.ModelSystemStructure,
                LinkedParameters = system.LinkedParameters,
                Description = system.Description,
                GUID = Guid.NewGuid().ToString()
            });
            return Save(ref error);
        }

        internal bool AddModelSystem(string modelSystemName, ref string error)
        {
            if (string.IsNullOrWhiteSpace(modelSystemName))
            {
                error = "A model system's name must not be blank or only contain white space!";
                return false;
            }

            _ProjectModelSystems.Add(new ProjectModelSystem
            {
                Root = new ModelSystemStructure(_Configuration)
                {
                    Name = modelSystemName,
                    Required = true,
                    Description = "The root of the model system",
                    ParentFieldType = typeof(IModelSystemTemplate)
                },
                LinkedParameters = new List<ILinkedParameter>(),
                Description = string.Empty,
                GUID = Guid.NewGuid().ToString()
            });
            return Save(ref error);
        }

        /// <summary>
        /// </summary>
        /// <param name="index"></param>
        /// <param name="newName"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal bool AddModelSystem(int index, string newName, ref string error)
        {
            var clone = CloneModelSystemStructure(out var linkedParameters,
                out var regionDisplays, index);
            if (clone == null)
            {
                error = "Unable to clone the model system.";
                return false;
            }

            clone.Name = newName;
            _ProjectModelSystems.Add(new ProjectModelSystem
            {
                Root = clone,
                LinkedParameters = linkedParameters,
                Description = _ProjectModelSystems[index].Description,
                GUID = Guid.NewGuid().ToString(),
                RegionDisplays = regionDisplays
            });
            return Save(ref error);
        }

        internal Project CreateCloneProject(bool attachToParent = true)
        {
            var project = new Project(this);
            if (!attachToParent)
            {
                project._ClonedFrom = null;
            }

            return project;
        }

        internal bool RemoveModelSystem(int index, ref string error)
        {
            _ProjectModelSystems.RemoveAt(index);
            return Save(ref error);
        }

        internal bool MoveModelSystems(int currentIndex, int newIndex, ref string error)
        {
            var temp = _ProjectModelSystems[currentIndex];
            _ProjectModelSystems.RemoveAt(currentIndex);
            _ProjectModelSystems.Insert(newIndex, temp);
            return Save(ref error);
        }

        /// <summary>
        ///     Create a close of the model system structure for a particular model system inside of the project.
        /// </summary>
        /// <param name="linkedParameters">The copied linked parameters targeted for the new model system</param>
        /// <param name="modelSystemIndex">The index of the model system inside of the project to work with.</param>
        /// <returns>The cloned model system structure</returns>
        internal ModelSystemStructure CloneModelSystemStructure(out List<ILinkedParameter> linkedParameters,
            out List<IRegionDisplay> regionDisplays, int modelSystemIndex)
        {
            var ourClone = ModelSystemStructure[modelSystemIndex].Clone();
            linkedParameters = LinkedParameters[modelSystemIndex].Count > 0
                ? LinkedParameter.MapLinkedParameters(LinkedParameters[modelSystemIndex], ourClone,
                    ModelSystemStructure[modelSystemIndex])
                : new List<ILinkedParameter>();

            
            //regionDisplays = RegionDisplays[modelSystemIndex];

            regionDisplays = RegionDisplay.MapRegionDisplays(RegionDisplays[modelSystemIndex], ourClone);

            return ourClone as ModelSystemStructure;
        }


        internal ModelSystem CloneModelSystem(IModelSystemStructure modelSystemStructure)
        {
            var index = 0;
            var clone = modelSystemStructure.Clone();
            var modelSystem = new ModelSystem(_Configuration, modelSystemStructure.Name)
            {
                ModelSystemStructure = clone
            };
            foreach (var f in ModelSystemStructure)
            {
                if (f.Name == modelSystemStructure.Name)
                {
                    modelSystem.LinkedParameters = LinkedParameters[index].Count > 0
                        ? LinkedParameter.MapLinkedParameters(LinkedParameters[index], clone,
                            ModelSystemStructure[index])
                        : new List<ILinkedParameter>();
                }

                index++;
            }

            return modelSystem;
        }

        /// <summary>
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        private ModelSystemStructure CloneModelSystemStructure(ModelSystem modelSystem, out List<ILinkedParameter> list,
            out List<IRegionDisplay> regionDisplays)
        {
            return modelSystem.CreateEditingClone(out list, out regionDisplays);
        }

        /// <summary>
        /// </summary>
        /// <param name="modelSystemIndex"></param>
        /// <param name="realModelSystemStructure"></param>
        /// <param name="lps"></param>
        /// <param name="regionDisplays"></param>
        /// <param name="description"></param>
        public void SetModelSystem(int modelSystemIndex, IModelSystemStructure realModelSystemStructure,
            List<ILinkedParameter> lps,
            List<IRegionDisplay> regionDisplays, string description)
        {
            _ProjectModelSystems[modelSystemIndex] = new ProjectModelSystem
            {
                Root = realModelSystemStructure,
                LinkedParameters = lps,
                Description = description,
                GUID = _ProjectModelSystems[modelSystemIndex]?.GUID ?? Guid.NewGuid().ToString(),
                RegionDisplays = regionDisplays
            };
        }

        /// <summary>
        ///     Get all of the default properties from the model
        /// </summary>
        /// <param name="modelType">The model that we want all of the properties from</param>
        /// <returns>A set of parameters with their default values</returns>
        public static IModuleParameters GetParameters(Type modelType)
        {
            if (modelType == null)
            {
                return null;
            }

            var parameters = new ModuleParameters();
            try
            {
                foreach (var property in modelType.GetProperties())
                {
                    AddProperties(parameters, property.GetCustomAttributes(true), property.Name, false,
                        property.PropertyType);
                }

                foreach (var field in modelType.GetFields())
                {
                    AddProperties(parameters, field.GetCustomAttributes(true), field.Name, true, field.FieldType);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error trying to load parameters for module type " + modelType.FullName + "\r\n" +
                                    e.Message);
            }

            return parameters;
        }

        /// <summary>
        /// </summary>
        /// <param name="modelSystemIndex"></param>
        /// <param name="newMSS"></param>
        public void UpdateModelSystemStructure(int modelSystemIndex, ModelSystemStructure newMSS)
        {
            if (modelSystemIndex < 0 || modelSystemIndex >= _ProjectModelSystems.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(modelSystemIndex));
            }

            _ProjectModelSystems[modelSystemIndex].Root = newMSS;
        }

        /// <summary>
        ///     Provides a way to check if a project's name is valid for adding
        /// </summary>
        /// <param name="name">The name of the project that you want to add</param>
        /// <returns>If the name is valid, true, or not, false.</returns>
        public static bool ValidateProjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (name.Contains(invalidChar))
                {
                    return false;
                }
            }

            return true;
        }

        public IModelSystemTemplate CreateModelSystem(ref ErrorWithPath error, int modelSystemIndex)
        {
            // Pre-Validate the structure
            if ((modelSystemIndex < 0) | (modelSystemIndex >= ModelSystemStructure.Count))
            {
                throw new XTMFRuntimeException(null,
                    "The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                    ModelSystemStructure.Count);
            }

            return CreateModelSystem(ref error, _Configuration, ModelSystemStructure[modelSystemIndex]);
        }

        public IModelSystemTemplate CreateModelSystem(ref string error, IConfiguration configuration,
            int modelSystemIndex)
        {
            // Pre-Validate the structure
            if ((modelSystemIndex < 0) | (modelSystemIndex >= ModelSystemStructure.Count))
            {
                throw new XTMFRuntimeException(null,
                    "The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                    ModelSystemStructure.Count);
            }

            return CreateModelSystem(ref error, configuration, ModelSystemStructure[modelSystemIndex]);
        }

        public IModelSystemTemplate CreateModelSystem(ref ErrorWithPath error, IConfiguration configuration,
            int modelSystemIndex)
        {
            // Pre-Validate the structure
            if ((modelSystemIndex < 0) | (modelSystemIndex >= ModelSystemStructure.Count))
            {
                throw new XTMFRuntimeException(null,
                    "The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                    ModelSystemStructure.Count);
            }

            return CreateModelSystem(ref error, configuration, ModelSystemStructure[modelSystemIndex]);
        }

        public IModelSystemTemplate CreateModelSystem(ref string error, IConfiguration configuration,
            IModelSystemStructure modelSystemStructure)
        {
            var errorWithPath = new ErrorWithPath();
            var ret = CreateModelSystem(ref errorWithPath, configuration, modelSystemStructure);
            if (ret == null)
            {
                error = errorWithPath.Message;
            }

            return ret;
        }

        public IModelSystemTemplate CreateModelSystem(ref ErrorWithPath error, IConfiguration configuration,
            IModelSystemStructure modelSystemStructure)
        {
            if (!((ModelSystemStructure) modelSystemStructure).Validate(ref error, new List<int>()))
            {
                return null;
            }

            IModelSystemTemplate modelSystem = null;
            if (CreateModule(configuration, modelSystemStructure, modelSystemStructure, new List<int>(), ref error))
            {
                modelSystem = modelSystemStructure.Module as IModelSystemTemplate;
            }

            return modelSystem;
        }

        /// <summary>
        ///     This event is invoked when a cloned project gets saved, overwriting this project.
        ///     When a running model system saves itself, this will trigger.
        /// </summary>
        public event EventHandler<ProjectExternallySavedEventArgs> ExternallySaved;

        public bool SaveModelSystem(int modelSystemIndex, ref string error)
        {
            return false;
        }


        /// <summary>
        /// </summary>
        public void SetActive()
        {
            if (!_IsLoaded)
            {
                lock (this)
                {
                    Thread.MemoryBarrier();
                    if (!_IsLoaded)
                    {
                        string error = null;
                        // Load off of the disk in parallel to provide faster UI reaction
                        if (!Load(ref error))
                        {
                            throw new Exception(error);
                        }
                    }
                }
            }
        }

        internal static IModuleParameters LoadDefaultParams(Type type)
        {
            return GetParameters(type);
        }

        /// <summary>
        ///     Build up the model parameters
        /// </summary>
        /// <param name="parameters">The parameter structure we are building</param>
        /// <param name="attributes">The attributes that we have found</param>
        /// <param name="fieldName">The field name of the property to add</param>
        /// <param name="field">True if it is a field, false if it is a property</param>
        /// <param name="t">The type of the property</param>
        private static void AddProperties(ModuleParameters parameters, object[] attributes, string fieldName,
            bool field, Type t)
        {
            foreach (var attribute in attributes)
            {
                if (attribute is ParameterAttribute)
                {
                    var temp = attribute as ParameterAttribute;
                    temp.AttachedToField = field;
                    temp.VariableName = fieldName;
                    parameters.Add(temp, t);
                }
            }
        }

        private static bool AddCollection(IConfiguration config, IModule root, IModelSystemStructure rootMS,
            IModelSystemStructure child,
            FieldInfo infoField, [NotNull] PropertyInfo infoProperty, Type listOfInner, Type inner, List<int> path,
            ref ErrorWithPath error)
        {
            var mod = child as ModelSystemStructure;
            object collectionValue;
            Type collectionType;
            if (infoField == null && !infoProperty.CanRead)
            {
                error = new ErrorWithPath(path,
                    $"Since the {root.GetType().FullName}.{infoProperty.Name} property has no public getter we can not initialize its values. Please add one so that XTMF can load the model.");
                return false;
            }

            if (infoField != null)
            {
                collectionValue = infoField.GetValue(root);
                collectionType = infoField.FieldType;
            }
            else
            {
                collectionValue = infoProperty.GetValue(root, null);
                collectionType = infoProperty.PropertyType;
            }

            // check to make sure that it exists before trying to add new values to it.
            if (collectionValue == null)
            {
                if (infoField == null && !infoProperty.CanWrite)
                {
                    error = new ErrorWithPath(path,
                        $"Since the {root.GetType().FullName}.{infoProperty.Name} property has no public setter we can not create a collection.  Please either add a public setter or initialize this property in your constructor."
                        , null, child.Name);
                    return false;
                }

                // Lets attempt to create it IF it doesn't already exist
                var created = false;
                if (collectionType.IsClass && !collectionType.IsAbstract)
                {
                    if (collectionType.IsArray)
                    {
                        var collectionObject = Array.CreateInstance(collectionType.GetElementType(),
                            child.Children == null || mod != null && mod.IsDisabled
                                ? 0
                                : child.Children.Count(
                                    gc =>
                                    {
                                        var mss = gc as ModelSystemStructure;
                                        return mss == null || !mss.IsDisabled;
                                    }));
                        if (infoField != null)
                        {
                            infoField.SetValue(root, collectionObject);
                        }
                        else
                        {
                            infoProperty.SetValue(root, collectionObject, null);
                        }

                        created = true;
                    }
                    else
                    {
                        // if we know it is concrete, lets try to just make it with a default constructor
                        var defaultConstructor = collectionType.GetConstructor(new Type[] { });
                        if (defaultConstructor != null)
                        {
                            var collectionObject = defaultConstructor.Invoke(new object[] { });
                            if (infoField != null)
                            {
                                infoField.SetValue(root, collectionObject);
                            }
                            else
                            {
                                infoProperty.SetValue(root, collectionObject, null);
                            }

                            created = true;
                        }
                    }
                }
                else
                {
                    if (collectionType.IsAssignableFrom(listOfInner))
                    {
                        if (infoField != null)
                        {
                            infoField.SetValue(root,
                                listOfInner.GetConstructor(new Type[] { })?.Invoke(new object[] { }));
                        }
                        else
                        {
                            infoProperty.SetValue(root,
                                listOfInner.GetConstructor(new Type[] { })?.Invoke(new object[] { }), null);
                        }

                        created = true;
                    }
                }

                if (!created)
                {
                    if (infoField != null)
                    {
                        error = new ErrorWithPath(path,
                            $"We were unable to create any Collection object for {root.GetType().FullName}.{infoField.Name}.  Please initialize this field in your constructor!"
                            , null, child.Name);
                    }
                    else
                    {
                        error = new ErrorWithPath(path,
                            $"We were unable to create any Collection object for {root.GetType().FullName}.{infoProperty.Name}.  Please initialize this field in your constructor!"
                            , null, child.Name);
                    }

                    return false;
                }
            }

            // check to see if the collection is disabled, if it is we are done as we don't want to add any children.            
            if (mod != null && mod.IsDisabled)
            {
                return true;
            }

            // If we get to this point, we know that there is in fact an extension of ICollection @ this field
            if (child.Children != null)
            {
                object collectionObject;
                if (infoField != null)
                {
                    collectionObject = infoField.GetValue(root);
                }
                else
                {
                    collectionObject = infoProperty.GetValue(root, null);
                }

                if (collectionObject == null)
                {
                    error = new ErrorWithPath(path, string.Format(
                        "For module '{2}' we were unable to load back the previously created Collection object for {0}.{1}. Please make sure that its getter and setter are both working.",
                        root.GetType().FullName,
                        infoProperty.Name, root.Name), null, child.Name);
                    return false;
                }

                var collectionTrueType = collectionObject.GetType();
                var grandChildren = child.Children;
                if (collectionType.IsArray)
                {
                    var setValue = collectionTrueType.GetMethod("SetValue", new[] {typeof(object), typeof(int)});
                    var pos = 0;
                    for (var i = 0; i < grandChildren.Count; i++)
                    {
                        path.Add(i);
                        mod = grandChildren[i] as ModelSystemStructure;
                        if (!(mod != null && mod.IsDisabled))
                        {
                            if (!CreateModule(config, rootMS, child.Children[i], path, ref error))
                            {
                                return false;
                            }

                            setValue.Invoke(collectionObject, new object[] {child.Children[i].Module, pos++});
                        }

                        path.RemoveAt(path.Count - 1);
                    }
                }
                else
                {
                    var addMethod = collectionTrueType.GetMethod("Add", new[] {inner});
                    if (addMethod == null)
                    {
                        error = new ErrorWithPath(path,
                            string.Format(
                                "For module '{2}' we were unable to find an Add method for type {0} in Type {1}",
                                inner.FullName, collectionType.FullName, root.Name)
                            , null, child.Name);
                        return false;
                    }

                    var i = 0;
                    foreach (var member in grandChildren)
                    {
                        path.Add(i++);
                        mod = member as ModelSystemStructure;
                        if (!(mod != null && mod.IsDisabled))
                        {
                            if (!CreateModule(config, rootMS, member, path, ref error))
                            {
                                return false;
                            }

                            addMethod.Invoke(collectionObject, new object[] {member.Module});
                        }

                        path.RemoveAt(path.Count - 1);
                    }
                }
            }

            return true;
        }

        private static bool AttachParent(IModule parent, IModelSystemStructure child, List<int> path,
            ref ErrorWithPath error)
        {
            if (child.Type == null)
            {
                error = new ErrorWithPath(path,
                    $"The type of the child {child.Name} was expected but not selected for!", null, child.Name);
                return false;
            }

            foreach (var field in child.Type.GetFields())
            {
                if (field.IsPublic)
                {
                    var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                    if (attributes.Length == 0)
                    {
                        continue;
                    }

                    var parentType = parent.GetType();
                    if (!field.FieldType.IsAssignableFrom(parentType))
                    {
                        error = new ErrorWithPath(path,
                            $"The parent type of {field.FieldType.FullName} is not assignable from the true parent type of {parentType.FullName}!"
                            , null, child.Name);
                        return false;
                    }

                    field.SetValue(child.Module, parent);
                }
            }

            foreach (var field in child.Type.GetProperties())
            {
                if (field.CanRead && field.CanWrite)
                {
                    var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                    if (attributes.Length == 0)
                    {
                        continue;
                    }

                    var parentType = parent.GetType();
                    if (!field.PropertyType.IsAssignableFrom(parentType))
                    {
                        error = new ErrorWithPath(path,
                            $"The parent type of {field.PropertyType.FullName} is not assignable from the true parent type of {parentType.FullName}!"
                            , null, child.Name);
                        return false;
                    }

                    field.SetValue(child.Module, parent, null);
                }
            }

            return true;
        }

        private static bool AttachRootModelSystem(IModelSystemStructure iModelSystem, IModule root, List<int> path,
            ref ErrorWithPath error)
        {
            foreach (var field in root.GetType().GetFields())
            {
                if (field.IsPublic)
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if (attributes.Length == 0)
                    {
                        continue;
                    }

                    // make sure the root model system structure actually exists
                    if (iModelSystem == null)
                    {
                        error = new ErrorWithPath(path,
                            $"The type {field.FieldType.FullName} used for the root in {root.Name} has no module to use as an ancestor.  Please contact your model system provider!",
                            null, root.Name);
                        return false;
                    }

                    var rootType = iModelSystem.Module.GetType();
                    if (!field.FieldType.IsAssignableFrom(rootType))
                    {
                        error = new ErrorWithPath(path,
                            $"The parent type of {field.FieldType.FullName} is not assignable from the true root type of {rootType.FullName}!"
                            , null, iModelSystem.Name);
                        return false;
                    }

                    field.SetValue(root, iModelSystem.Module);
                }
            }

            foreach (var field in root.GetType().GetProperties())
            {
                if (field.CanRead && field.CanWrite)
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if (attributes.Length == 0)
                    {
                        continue;
                    }

                    var rootType = iModelSystem.Module.GetType();
                    if (!field.PropertyType.IsAssignableFrom(rootType))
                    {
                        error = new ErrorWithPath(path,
                            $"The parent type of {field.PropertyType.FullName} is not assignable from the true root type of {rootType.FullName}!"
                            , null, iModelSystem.Name);
                        return false;
                    }

                    field.SetValue(root, iModelSystem.Module, null);
                }
            }

            return true;
        }

        /// <summary>
        ///     Loads and set logger fields on the passed module.
        /// </summary>
        /// <param name="module">The module to assign and initialize loggers to</param>
        private static void LoadLogger(IModule module)
        {
            var type = module.GetType();
            foreach (var f in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = f.GetCustomAttribute<LoggerAttribute>();
                if (attribute != null)
                {
                    try
                    {
                        var logger = new Logger(LogManager.GetLogger(attribute.LoggerName == null
                            ? type.ToString()
                            : attribute.LoggerName));


                        f.SetValue(module,logger);
                    }
                    catch(Exception e)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="config"></param>
        /// <param name="rootMS"></param>
        /// <param name="ps"></param>
        /// <param name="path"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool CreateModule(IConfiguration config, IModelSystemStructure rootMS, IModelSystemStructure ps,
            List<int> path, ref ErrorWithPath error)
        {
            IModule root;
            if (ps.Type == null)
            {
                error = new ErrorWithPath(path,
                    string.Concat("Attempted to create the ", ps.Name,
                        " module however it's type does not exist!  Please make sure you have all of the required modules installed for your model system!"));
                return false;
            }

            var constructor = (from c in ps.Type.GetConstructors()
                orderby c.GetParameters().Length
                select c).FirstOrDefault();

            var parameterList = new object[constructor.GetParameters().Length];
            var parameters = constructor.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(IConfiguration))
                {
                    parameterList[i] = config;
                }
                else if (parameters[i].ParameterType == typeof(ILogger))
                {
                    parameterList[i] = new Logger(ps.Type);
                }
                else
                {
                    parameterList[i] = null;
                }
            }

            try
            {
                root = constructor.Invoke(parameterList) as IModule;
            }
            catch (Exception e)
            {
                error = new ErrorWithPath(path,
                    $"There was an error while trying to initialize {ps.Type.FullName}.\nPlease make sure that no parameters are being used in the constructor!",
                    exception: e);
                return false;
            }

            ps.Module = root;
            if (root != null)
            {
                LoadLogger(root);
                try
                {
                    root.Name = ps.Name;
                }
                catch (Exception e)
                {
                    error = new ErrorWithPath(path,
                        string.Concat("Unable to assign the name of ", ps.Name, " to type ", ps.Type.FullName, "!"),
                        exception: e);
                    return false;
                }

                // Allow any module access to the host/client
                if (!InstallNetworkingModules(config, root, path, ref error))
                {
                    return false;
                }

                // Install all of the parameters for this model
                if (!InstallParameters(root, ps, path, ref error))
                {
                    return false;
                }

                if (!AttachRootModelSystem(XTMF.ModelSystemStructure.CheckForRootModule(rootMS, ps, ps.Type), root,
                    path, ref error))
                {
                    error = new ErrorWithPath(path, "We were unable to attach the proper root for " + ps.Name + "!");
                    return false;
                }

                if (ps.Children != null)
                {
                    for (var i = 0; i < ps.Children.Count; i++)
                    {
                        path.Add(i);
                        var child = ps.Children[i];
                        var mod = child as ModelSystemStructure;
                        // check to see if we should just skip loading the child
                        if (child.IsCollection)
                        {
                            var array = child.ParentFieldType.IsArray;
                            var inner = array
                                ? child.ParentFieldType.GetElementType()
                                : child.ParentFieldType.GetGenericArguments()[0];
                            // if it is an array make it, otherwise
                            // if the parent type is abstract just make a list
                            // otherwise create something of the proper type
                            var listOfInner = array
                                ? inner.MakeArrayType()
                                : (child.ParentFieldType.IsInterface | child.ParentFieldType.IsAbstract
                                    ? typeof(List<>).MakeGenericType(inner)
                                    : child.ParentFieldType);
                            var infoField = ps.Type.GetField(child.ParentFieldName);
                            var infoProperty = ps.Type.GetProperty(child.ParentFieldName);
                            if (infoField == null && infoProperty == null)
                            {
                                error = new ErrorWithPath(path,
                                    string.Format(
                                        "While building the module '{2}' we were unable to find a field or property called {0} in type {1}",
                                        child.ParentFieldName, ps.Type.FullName, ps.Name));
                                return false;
                            }

                            if (!AddCollection(config, root, rootMS, child, infoField, infoProperty, listOfInner, inner,
                                path, ref error))
                            {
                                return false;
                            }

                            if (child.Children != null)
                            {
                                var j = 0;
                                // now that we have created the children try to attach the parent to them
                                foreach (var cc in child.Children)
                                {
                                    path.Add(j++);
                                    // Now that the child has been created attach this parent object to any fields requesting it
                                    if (!AttachParent(root, cc, path, ref error))
                                    {
                                        return false;
                                    }

                                    path.RemoveAt(path.Count - 1);
                                }
                            }
                        }
                        else if (child.Type != null)
                        {
                            // if this module is disabled, do not create it!
                            if (mod != null && mod.IsDisabled)
                            {
                                path.RemoveAt(path.Count - 1);
                                continue;
                            }

                            if (!CreateModule(config, rootMS, child, path, ref error))
                            {
                                return false;
                            }

                            var infoField = ps.Type.GetField(child.ParentFieldName);
                            var infoProperty = ps.Type.GetProperty(child.ParentFieldName);
                            if (infoField != null)
                            {
                                infoField.SetValue(root, child.Module);
                            }
                            else if (infoProperty != null)
                            {
                                if (!infoProperty.CanWrite)
                                {
                                    error = new ErrorWithPath(path,
                                        string.Format(
                                            "While building the module '{2}' we were unable to write property called {0} in type {1}",
                                            child.ParentFieldName, ps.Type.FullName, ps.Name));
                                    return false;
                                }

                                infoProperty.SetValue(root, child.Module, null);
                            }
                            else
                            {
                                error = new ErrorWithPath(path,
                                    string.Format(
                                        "While building the module '{2}' we were unable to find a field or property called {0} in type {1}",
                                        child.ParentFieldName, ps.Type.FullName, ps.Name));
                                return false;
                            }

                            // Now that the child has been created attach this parent object to any fields requesting it
                            if (!AttachParent(root, child, path, ref error))
                            {
                                return false;
                            }
                        }

                        path.RemoveAt(path.Count - 1);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="mss"></param>
        /// <returns></returns>
        private IModelSystemStructure GetModuleFromReference(string reference, IModelSystemStructure mss)
        {
            var modules = reference.Split('.');

            if (modules.Length == 1)
            {
                return mss;
            }

         

            return GetModuleFromReference(modules.Skip(1).ToArray(), mss);
        }

        /// <summary>
        /// </summary>
        /// <param name="modules"></param>
        /// <param name="mss"></param>
        /// <returns></returns>
        private IModelSystemStructure GetModuleFromReference(string[] modules, IModelSystemStructure mss)
        {

            if (mss.IsCollection)
            {
                var childStructure = mss.Children[int.Parse(modules[0])];
                return GetModuleFromReference(modules.Skip(2).ToArray(), childStructure);

            }

            else if (modules.Length == 0)
            {
                return mss;
            }

            //find the child node of MSS that has the same name as reference [0]
            var structure = mss.Children.SingleOrDefault(m => m.Name == modules[0]);

            if (modules.Length == 1)
            {
                return structure;
            }

            return GetModuleFromReference(modules.Skip(1).ToArray(), structure);
        }

        /// <summary>
        /// </summary>
        /// <param name="variableLink"></param>
        /// <param name="mss"></param>
        /// <returns></returns>
        private IModuleParameter GetParameterFromLink(string variableLink, IModelSystemStructure mss)
        {
            // we need to search the space now
            return GetParameterFromLink(ParseLinkedParameterName(variableLink), 0, mss);
        }

        private IModuleParameter GetParameterFromLink(string[] variableLink, int index, IModelSystemStructure current)
        {
            if (index == variableLink.Length - 1)
            {
                // search the parameters
                var parameters = current.Parameters;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        if (p.Name == variableLink[index])
                        {
                            return p;
                        }
                    }
                }
            }
            else
            {
                var descList = current.Children;
                if (descList == null)
                {
                    return null;
                }

                if (current.IsCollection)
                {
                    if (int.TryParse(variableLink[index], out var collectionIndex))
                    {
                        if (collectionIndex >= 0 && collectionIndex < descList.Count)
                        {
                            return GetParameterFromLink(variableLink, index + 1, descList[collectionIndex]);
                        }

                        return null;
                    }
                }
                else
                {
                    foreach (var sub in descList)
                    {
                        if (sub.ParentFieldName == variableLink[index])
                        {
                            return GetParameterFromLink(variableLink, index + 1, sub);
                        }
                    }
                }
            }

            return null;
        }

        private static bool InstallNetworkingModules(IConfiguration configuration, IModule module, List<int> path,
            ref ErrorWithPath error)
        {
            var moduleType = module.GetType();
            var clientType = typeof(IClient);
            var hostType = typeof(IHost);
            string strError = null;
            foreach (var field in moduleType.GetFields())
            {
                if (field.IsPublic)
                {
                    if (field.FieldType == clientType)
                    {
                        var networkingClient = configuration.RetriveCurrentNetworkingClient();
                        if (networkingClient != null)
                        {
                            field.SetValue(module, networkingClient);
                        }
                    }
                    else if (field.FieldType == hostType)
                    {
                        if (!configuration.StartupNetworkingHost(out var networkingHost, ref strError))
                        {
                            error = new ErrorWithPath(path, strError);
                            return false;
                        }

                        field.SetValue(module, networkingHost);
                    }
                }
            }

            foreach (var field in moduleType.GetProperties())
            {
                if (field.CanRead && field.CanWrite)
                {
                    if (field.PropertyType == clientType)
                    {
                        if (configuration.StartupNetworkingClient(out var networkingClient, ref strError))
                        {
                            field.SetValue(module, networkingClient, null);
                        }
                        else
                        {
                            error = new ErrorWithPath(path, strError);
                            return false;
                        }
                    }
                    else if (field.PropertyType == hostType)
                    {
                        if (configuration.StartupNetworkingHost(out var networkingHost, ref strError))
                        {
                            field.SetValue(module, networkingHost, null);
                        }
                        else
                        {
                            error = new ErrorWithPath(path, strError);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool InstallParameters(IModule root, IModelSystemStructure ps, List<int> path,
            ref ErrorWithPath error)
        {
            if (ps.Parameters == null)
            {
                return true;
            }

            foreach (var param in ps.Parameters)
            {
                if (param.OnField)
                {
                    var info = ps.Type.GetField(param.VariableName);
                    if (info == null)
                    {
                        error = new ErrorWithPath(path,
                            string.Format(CultureInfo.CurrentCulture,
                                "Unable to find a field called {0} on type {1}!", param.VariableName,
                                ps.Type.FullName));
                        return false;
                    }

                    try
                    {
                        info.SetValue(root, param.Value);
                    }
                    catch (ArgumentException e)
                    {
                        error = new ErrorWithPath(path, string.Format(
                                "In module {3} we were unable to assign parameter {0} of type {1} with type {2}, please rebuild your model system.",
                                param.Name, info.FieldType.FullName, param.Value.GetType().FullName, ps.Name),
                            exception: e);
                        return false;
                    }
                }
                else
                {
                    var info = ps.Type.GetProperty(param.VariableName);
                    if (info == null)
                    {
                        error = new ErrorWithPath(path,
                            string.Format(CultureInfo.CurrentCulture,
                                "Unable to find a property called {0} on type {1}!", param.VariableName,
                                ps.Type.FullName));
                        return false;
                    }

                    try
                    {
                        info.SetValue(root, param.Value, null);
                    }
                    catch (ArgumentException e)
                    {
                        error = new ErrorWithPath(path, string.Format(
                                "In module {3} we were unable to assign parameter {0} of type {1} with type {2}, please rebuild your model system.",
                                param.Name, info.PropertyType.FullName, param.Value.GetType().FullName, ps.Name),
                            exception: e);
                        return false;
                    }
                    catch (Exception e)
                    {
                        error = new ErrorWithPath(path,
                            "An unexpected error occurred while trying to set the parameter '" + param.VariableName +
                            "' in '" + ps.Name + "'\r\n" + e.Message, e.StackTrace, exception: e);
                        return false;
                    }
                }
            }

            return true;
        }


        private string LookupName(IModuleParameter reference, IModelSystemStructure current)
        {
            var param = current.Parameters;
            if (param != null)
            {
                var index = param.Parameters.IndexOf(reference);
                if (index >= 0)
                {
                    return current.Parameters.Parameters[index].Name;
                }
            }

            var childrenList = current.Children;
            if (childrenList != null)
            {
                for (var i = 0; i < childrenList.Count; i++)
                {
                    var res = LookupName(reference, childrenList[i]);
                    if (res != null)
                    {
                        return string.Concat(current.IsCollection
                            ? i.ToString()
                            : childrenList[i].ParentFieldName.Replace(".", "\\."), '.', res);
                    }
                }
            }

            return null;
        }

        private string[] ParseLinkedParameterName(string variableLink)
        {
            var ret = new List<string>();
            var escape = false;
            var length = variableLink.Length;
            var builder = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                var c = variableLink[i];
                // check to see if we need to add in the escape
                if (escape & (c != '.'))
                {
                    builder.Append('\\');
                }

                // check to see if we need to move onto the next part
                if ((escape == false) & (c == '.'))
                {
                    ret.Add(builder.ToString());
                    builder.Clear();
                }
                else if (c != '\\')
                {
                    builder.Append(c);
                    escape = false;
                }
                else
                {
                    escape = true;
                }
            }

            if (escape)
            {
                builder.Append('\\');
            }

            ret.Add(builder.ToString());
            return ret.ToArray();
        }
    }
}