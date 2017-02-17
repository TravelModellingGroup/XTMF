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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTMF.Annotations;
using XTMF.Editing;
using XTMF.Networking;

namespace XTMF
{
    /// <summary>
    /// Represents a project currently installed in
    /// the XTMF installation
    /// </summary>
    public class Project : IProject
    {
        protected List<List<ILinkedParameter>> _LinkedParameters;

        /// <summary>
        ///
        /// </summary>
        protected List<IModelSystemStructure> _ModelSystemStructure;

        protected List<string> _Descriptions;

        protected bool RemoteProject;

        /// <summary>
        /// The configuration object used for XTMF
        /// </summary>
        private IConfiguration Configuration;

        private string DirectoryLocation;

        /// <summary>
        /// This will be set to true once everything is ready for this project
        /// </summary>
        private volatile bool IsLoaded;
        private Project ClonedFrom;

        /// <summary>
        /// Create a new project
        /// For internal use only
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="configuration">A link to the configuration that XTMF is using</param>
        /// <param name="remoteProject">Are we a remote project?</param>
        public Project(string name, IConfiguration configuration, bool remoteProject = false)
        {
            Name = name;
            Configuration = configuration;
            LoadDescription();
            RemoteProject = remoteProject;
        }

        internal bool AddModelSystem(ModelSystem modelSystem, string newName, ref string error)
        {
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }
            List<ILinkedParameter> linkedParameters;
            var clone = CloneModelSystemStructure(modelSystem, out linkedParameters);
            if (clone == null)
            {
                error = "Unable to clone the model system.";
                return false;
            }
            clone.Name = newName;
            ModelSystemStructure.Add(clone);
            ModelSystemDescriptions.Add(modelSystem.Description);
            LinkedParameters.Add(linkedParameters);
            return Save(ref error);
        }

        internal bool AddModelSystem(string modelSystemName, ref string error)
        {
            if (String.IsNullOrWhiteSpace(modelSystemName))
            {
                error = "A model system's name must not be blank or only contain white space!";
                return false;
            }
            ModelSystemStructure.Add(new ModelSystemStructure(Configuration)
            {
                Name = modelSystemName,
                Required = true,
                Description = "The root of the model system",
                ParentFieldType = typeof(IModelSystemTemplate)
            });
            ModelSystemDescriptions.Add(String.Empty);
            LinkedParameters.Add(new List<ILinkedParameter>());
            return Save(ref error);
        }

        internal bool AddModelSystem(int index, string newName, ref string error)
        {
            List<ILinkedParameter> linkedParameters;
            var clone = CloneModelSystemStructure(out linkedParameters, index);
            if (clone == null)
            {
                error = "Unable to clone the model system.";
                return false;
            }
            clone.Name = newName;
            ModelSystemStructure.Add(clone);
            ModelSystemDescriptions.Add(ModelSystemDescriptions[index]);
            LinkedParameters.Add(linkedParameters);
            return Save(ref error);
        }

        /// <summary>
        /// This constructor is will clone a project.
        /// </summary>
        private Project(Project toClone)
        {
            IsLoaded = true;
            Name = toClone.Name;
            var numberOfModelSystems = toClone.ModelSystemStructure.Count;
            ModelSystemStructure = new List<IModelSystemStructure>(numberOfModelSystems);
            LinkedParameters = new List<List<ILinkedParameter>>(numberOfModelSystems);
            DirectoryLocation = toClone.DirectoryLocation;
            Configuration = toClone.Configuration;
            ClonedFrom = toClone;
            for (int i = 0; i < numberOfModelSystems; i++)
            {
                List<ILinkedParameter> lp;
                var mss = toClone.CloneModelSystemStructure(out lp, i);
                ModelSystemStructure.Add(mss);
                LinkedParameters.Add(lp);
            }
            ModelSystemDescriptions = toClone.ModelSystemDescriptions.ToList();
        }

        internal Project CreateCloneProject(bool attachToParent = true)
        {
            var project = new Project(this);
            if (!attachToParent)
            {
                project.ClonedFrom = null;
            }
            return project;
        }

        internal bool RemoveModelSystem(int index, ref string error)
        {
            ModelSystemDescriptions.RemoveAt(index);
            ModelSystemStructure.RemoveAt(index);
            LinkedParameters.RemoveAt(index);
            return Save(ref error);
        }

        internal bool MoveModelSystems(int currentIndex, int newIndex, ref string error)
        {
            var desc = ModelSystemDescriptions[currentIndex];
            var mss = ModelSystemStructure[currentIndex];
            var lp = LinkedParameters[currentIndex];
            ModelSystemDescriptions.RemoveAt(currentIndex);
            ModelSystemStructure.RemoveAt(currentIndex);
            LinkedParameters.RemoveAt(currentIndex);
            ModelSystemDescriptions.Insert(newIndex, desc);
            ModelSystemStructure.Insert(newIndex, mss);
            LinkedParameters.Insert(newIndex, lp);
            return Save(ref error);
        }

        public string Description
        {
            get;
            set;
        }

        /// <summary>
        /// Create a close of the model system structure for a particular model system inside of the project.
        /// </summary>
        /// <param name="linkedParameters">The copied linked parameters targeted for the new model system</param>
        /// <param name="modelSystemIndex">The index of the model system inside of the project to work with.</param>
        /// <returns>The cloned model system structure</returns>
        internal ModelSystemStructure CloneModelSystemStructure(out List<ILinkedParameter> linkedParameters, int modelSystemIndex)
        {
            var ourClone = ModelSystemStructure[modelSystemIndex].Clone();
            linkedParameters = LinkedParameters[modelSystemIndex].Count > 0 ?
                LinkedParameter.MapLinkedParameters(LinkedParameters[modelSystemIndex], ourClone, ModelSystemStructure[modelSystemIndex])
                : new List<ILinkedParameter>();
            return ourClone as ModelSystemStructure;
        }

        private ModelSystemStructure CloneModelSystemStructure(ModelSystem modelSystem, out List<ILinkedParameter> list)
        {
            return modelSystem.CreateEditingClone(out list);
        }

        public bool HasChanged { get; set; }

        /// <summary>
        ///
        /// </summary>
        public List<List<ILinkedParameter>> LinkedParameters
        {
            get
            {
                SetActive();
                return _LinkedParameters;
            }

            private set
            {
                _LinkedParameters = value;
            }
        }

        public List<IModelSystemStructure> ModelSystemStructure
        {
            get
            {
                SetActive();
                return _ModelSystemStructure;
            }

            private set
            {
                _ModelSystemStructure = value;
            }
        }

        public List<string> ModelSystemDescriptions
        {
            get
            {
                SetActive();
                if (_Descriptions == null)
                {
                    _Descriptions = new List<string>();
                }
                return _Descriptions;
            }
            private set
            {
                _Descriptions = value;
            }
        }

        /// <summary>
        /// The name of the Project
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Get all of the default properties from the model
        /// </summary>
        /// <param name="modelType">The model that we want all of the properties from</param>
        /// <returns>A set of parameters with their default values</returns>
        public static IModuleParameters GetParameters(Type modelType)
        {
            if (modelType == null) return null;
            ModuleParameters parameters = new ModuleParameters();
            foreach (var property in modelType.GetProperties())
            {
                AddProperties(parameters, property.GetCustomAttributes(true), property.Name, false, property.PropertyType);
            }
            foreach (var field in modelType.GetFields())
            {
                AddProperties(parameters, field.GetCustomAttributes(true), field.Name, true, field.FieldType);
            }
            return parameters;
        }

        /// <summary>
        /// Provides a way to check if a project's name is valid for adding
        /// </summary>
        /// <param name="name">The name of the project that you want to add</param>
        /// <returns>If the name is valid, true, or not, false.</returns>
        public static bool ValidateProjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (name.Contains(invalidChar)) return false;
            }
            return true;
        }

        public IModelSystemTemplate CreateModelSystem(ref string error, int modelSystemIndex)
        {
            // Pre-Validate the structure
            if (modelSystemIndex < 0 | modelSystemIndex >= ModelSystemStructure.Count)
            {
                throw new XTMFRuntimeException("The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                ModelSystemStructure.Count);
            }
            return CreateModelSystem(ref error, Configuration, ModelSystemStructure[modelSystemIndex]);
        }

        public IModelSystemTemplate CreateModelSystem(ref string error, IConfiguration configuration, int modelSystemIndex)
        {
            // Pre-Validate the structure
            if (modelSystemIndex < 0 | modelSystemIndex >= ModelSystemStructure.Count)
            {
                throw new XTMFRuntimeException("The model system requested does not exist!\r\nModel System Number:" + modelSystemIndex + " of " +
                ModelSystemStructure.Count);
            }
            return CreateModelSystem(ref error, configuration, ModelSystemStructure[modelSystemIndex]);
        }

        public IModelSystemTemplate CreateModelSystem(ref string error, IConfiguration configuration, IModelSystemStructure modelSystemStructure)
        {
            if (!modelSystemStructure.Validate(ref error))
            {
                return null;
            }

            IModelSystemTemplate modelSystem = null;
            if (CreateModule(configuration, modelSystemStructure, modelSystemStructure, ref error))
            {
                modelSystem = modelSystemStructure.Module as IModelSystemTemplate;
            }
            return modelSystem;
        }

        public void Reload()
        {
            ModelSystemStructure = null;
            if (!Load())
            {
                throw new Exception("Unable to reload!");
            }
        }


        public bool Save(ref string error)
        {
            string dirName = Path.Combine(Configuration.ProjectDirectory, Name);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            var ret = Save(Path.Combine(dirName, "Project.xml"), ref error);
            if (ClonedFrom != null)
            {
                var e = ClonedFrom.ExternallySaved;
                // swap the project that this was a clone of and replace the real project.
                ((ProjectRepository)Configuration.ProjectRepository).ReplaceProjectFromClone(ClonedFrom, this);
                e?.Invoke(ClonedFrom, new ProjectExternallySavedEventArgs(ClonedFrom, this));
            }
            return ret;
        }

        /// <summary>
        /// This event is invoked when a cloned project gets saved, overwriting this project.
        /// When a running model system saves itself, this will trigger.
        /// </summary>
        public event EventHandler<ProjectExternallySavedEventArgs> ExternallySaved;

        public bool Save(string path, ref string error)
        {
            var dirName = Path.GetDirectoryName(path);
            if (dirName == null)
            {
                error = $"The path '{path}' is invalid!";
                return false;
            }
            var tempFileName = Path.GetTempFileName();
            if (!Directory.Exists(dirName))
            {
                bool directoryExists = false;
                while (!directoryExists)
                {
                    Directory.CreateDirectory(dirName);
                    for (int i = 0; i < 10; i++)
                    {
                        if (Directory.Exists(dirName))
                        {
                            directoryExists = true;
                            break;
                        }
                        Thread.Sleep(18);
                    }
                }
            }
            var mss = ModelSystemStructure;
            var lpll = LinkedParameters;
            try
            {
                using (XmlTextWriter writer = new XmlTextWriter(tempFileName, Encoding.Unicode))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Root");

                    if (Description != null)
                    {
                        writer.WriteAttributeString("Description", Description);
                    }
                    for (int i = 0; i < mss.Count; i++)
                    {
                        var ms = mss[i];
                        var lpl = lpll[i];
                        if (ms.Type != null)
                        {
                            writer.WriteStartElement("AdvancedModelSystem");
                            writer.WriteAttributeString("Description", _Descriptions[i]);
                            writer.WriteStartElement("ModelSystem");
                            ms.Save(writer);
                            writer.WriteEndElement();
                            writer.WriteStartElement("LinkedParameters");
                            WriteLinkedParameters(writer, lpl, ms);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            if (File.Exists(path))
            {
                File.Copy(path, Path.Combine(dirName, "Project.bak.xml"), true);
            }
            File.Copy(tempFileName, path, true);
            File.Delete(tempFileName);
            HasChanged = false;
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public void SetActive()
        {
            if (!IsLoaded)
            {
                lock (this)
                {
                    Thread.MemoryBarrier();
                    if (!IsLoaded)
                    {
                        // Load off of the disk in parallel to provide faster UI reaction
                        if (!Load())
                        {
                            throw new Exception("Unable to load!");
                        }
                    }
                }
            }
        }

        public bool ValidateModelName(string possibleNewName)
        {
            if (string.IsNullOrEmpty(possibleNewName)) return false;
            // It can not be the Project because that is reserved for the project
            if (possibleNewName == "Project") return false;
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (possibleNewName.Contains(invalidChar)) return false;
            }
            return true;
        }

        internal static IModuleParameters LoadDefaultParams(Type type)
        {
            return GetParameters(type);
        }

        /// <summary>
        /// Build up the model parameters
        /// </summary>
        /// <param name="parameters">The parameter structure we are building</param>
        /// <param name="attributes">The attributes that we have found</param>
        /// <param name="fieldName">The field name of the property to add</param>
        /// <param name="field">True if it is a field, false if it is a property</param>
        /// <param name="t">The type of the property</param>
        private static void AddProperties(ModuleParameters parameters, object[] attributes, string fieldName, bool field, Type t)
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

        private bool AddCollection(IConfiguration config, IModule root, IModelSystemStructure rootMS, IModelSystemStructure child,
            FieldInfo infoField, [NotNull] PropertyInfo infoProperty, Type listOfInner, Type inner, ref string error)
        {
            object collectionValue;
            Type collectionType;

            if (infoField == null && !infoProperty.CanRead)
            {
                error =
                    $"Since the {root.GetType().FullName}.{infoProperty.Name} property has no public getter we can not initialize its values. Please add one so that XTMF can load the model.";
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
                    error =
                        $"Since the {root.GetType().FullName}.{infoProperty.Name} property has no public setter we can not create a collection.  Please either add a public setter or initialize this property in your constructor.";
                    return false;
                }
                // Lets attempt to create it IF it doesn't already exist
                bool created = false;
                if (collectionType.IsClass && !collectionType.IsAbstract)
                {
                    if (collectionType.IsArray)
                    {
                        var collectionObject = Array.CreateInstance(collectionType.GetElementType(), child.Children == null ? 0 : child.Children.Count(
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
                            infoField.SetValue(root, listOfInner.GetConstructor(new Type[] { })?.Invoke(new object[] { }));
                        }
                        else
                        {
                            infoProperty.SetValue(root, listOfInner.GetConstructor(new Type[] { })?.Invoke(new object[] { }), null);
                        }
                        created = true;
                    }
                }

                if (!created)
                {
                    if (infoField != null)
                    {
                        error =
                            $"We were unable to create any Collection object for {root.GetType().FullName}.{infoField.Name}.  Please initialize this field in your constructor!";
                    }
                    else
                    {
                        error =
                            $"We were unable to create any Collection object for {root.GetType().FullName}.{infoProperty.Name}.  Please initialize this field in your constructor!";
                    }
                    return false;
                }
            }
            // check to see if the collection is disabled, if it is we are done as we don't want to add any children.
            var mod = child as ModelSystemStructure;
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
                    error = string.Format("For module '{2}' we were unable to load back the previously created Collection object for {0}.{1}. Please make sure that its getter and setter are both working.", root.GetType().FullName,
                        infoProperty.Name, root.Name);
                    return false;
                }
                var collectionTrueType = collectionObject.GetType();
                var grandChildren = child.Children;
                if (collectionType.IsArray)
                {
                    var setValue = collectionTrueType.GetMethod("SetValue", new[] { typeof(object), typeof(int) });
                    int pos = 0;
                    for (int i = 0; i < grandChildren.Count; i++)
                    {
                        mod = grandChildren[i] as ModelSystemStructure;
                        if (mod != null && mod.IsDisabled)
                        {
                            continue;
                        }
                        if (!CreateModule(config, rootMS, child.Children[i], ref error)) return false;
                        setValue.Invoke(collectionObject, new object[] { child.Children[i].Module, pos++ });
                    }
                }
                else
                {
                    var addMethod = collectionTrueType.GetMethod("Add", new[] { inner });
                    if (addMethod == null)
                    {
                        error = string.Format("For module '{2}' we were unable to find an Add method for type {0} in Type {1}", inner.FullName, collectionType.FullName, root.Name);
                        return false;
                    }
                    foreach (var member in grandChildren)
                    {
                        mod = member as ModelSystemStructure;
                        if (mod != null && mod.IsDisabled)
                        {
                            continue;
                        }
                        if (!CreateModule(config, rootMS, member, ref error)) return false;
                        addMethod.Invoke(collectionObject, new object[] { member.Module });
                    }
                }
            }
            return true;
        }

        private bool AttachParent(IModule parent, IModelSystemStructure child, ref string error)
        {
            foreach (var field in child.Type.GetFields())
            {
                if (field.IsPublic)
                {
                    var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                    if (attributes.Length == 0) continue;
                    Type parentType = parent.GetType();

                    if (!field.FieldType.IsAssignableFrom(parentType))
                    {
                        error =
                            $"The parent type of {field.FieldType.FullName} is not assignable from the true parent type of {parentType.FullName}!";
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
                    Type parentType = parent.GetType();

                    if (!field.PropertyType.IsAssignableFrom(parentType))
                    {
                        error =
                            $"The parent type of {field.PropertyType.FullName} is not assignable from the true parent type of {parentType.FullName}!";
                        return false;
                    }
                    field.SetValue(child.Module, parent, null);
                }
            }
            return true;
        }

        private bool AttachRootModelSystem(IModelSystemStructure iModelSystem, IModule root, ref string error)
        {
            foreach (var field in root.GetType().GetFields())
            {
                if (field.IsPublic)
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if (attributes.Length == 0) continue;
                    // make sure the root model system structure actually exists
                    if (iModelSystem == null)
                    {
                        error =
                            $"The type {field.FieldType.FullName} used for the root in {root.Name} has no module to use as an ancestor.  Please contact your model system provider!";
                        return false;
                    }
                    Type rootType = iModelSystem.Module.GetType();

                    if (!field.FieldType.IsAssignableFrom(rootType))
                    {
                        error =
                            $"The parent type of {field.FieldType.FullName} is not assignable from the true root type of {rootType.FullName}!";
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
                    if (attributes.Length == 0) continue;
                    Type rootType = iModelSystem.Module.GetType();

                    if (!field.PropertyType.IsAssignableFrom(rootType))
                    {
                        error =
                            $"The parent type of {field.PropertyType.FullName} is not assignable from the true root type of {rootType.FullName}!";
                        return false;
                    }
                    field.SetValue(root, iModelSystem.Module, null);
                }
            }
            return true;
        }

        private bool CreateModule(IConfiguration config, IModelSystemStructure rootMS, IModelSystemStructure ps, ref string error)
        {
            IModule root;
            if (ps.Type == null)
            {
                error = string.Concat("Attempted to create the ", ps.Name, " module however it's type does not exist!  Please make sure you have all of the required modules installed for your model system!");
                return false;
            }
            var configConstructor = ps.Type.GetConstructor(new[] { typeof(IConfiguration) });
            if (configConstructor != null)
            {
                try
                {
                    root = configConstructor.Invoke(new object[] { config }) as IModule;
                }
                catch
                {
                    error =
                        $"There was an error while trying to initialize {ps.Type.FullName}.\nPlease make sure that no parameters are being used in the constructor!";
                    return false;
                }
                ps.Module = root;
            }
            else
            {
                var baseConstructor = ps.Type.GetConstructor(new Type[] { });
                if (baseConstructor == null)
                {
                    error =
                        $"Type {ps.Type.FullName} has no public constructor that takes an IConfiguration nor a default constructor.  Unable to create this type!";
                    return false;
                }
                try
                {
                    root = baseConstructor.Invoke(new object[] { }) as IModule;
                }
                catch (XTMFRuntimeException e)
                {
                    error = string.Concat("Construction Error in ", ps.Name, ":\r\n", e.Message);
                    return false;
                }
                catch
                {
                    return false;
                }
                ps.Module = root;
            }
            if (root != null)
            {
                try
                {
                    root.Name = ps.Name;
                }
                catch
                {
                    error = string.Concat("Unable to assign the name of ", ps.Name, " to type ", ps.Type.FullName, "!");
                    return false;
                }
                // Allow any module access to the host/client
                InstallNetworkingModules(root, ref error);
                // Install all of the parameters for this model
                if (!InstallParameters(root, ps, ref error))
                {
                    return false;
                }
                if (!AttachRootModelSystem(XTMF.ModelSystemStructure.CheckForRootModule(rootMS, ps, ps.Type), root, ref error))
                {
                    if (error == null)
                    {
                        error = "We were unable to attach the proper root for " + ps.Name + "!";
                    }
                    return false;
                }
                if (ps.Children != null)
                {
                    foreach (var child in ps.Children)
                    {
                        var mod = child as ModelSystemStructure;
                        // check to see if we should just skip loading the child
                        if (child.IsCollection)
                        {
                            bool array = child.ParentFieldType.IsArray;
                            var inner = array ? child.ParentFieldType.GetElementType() :
                                child.ParentFieldType.GetGenericArguments()[0];
                            // if it is an array make it, otherwise
                            // if the parent type is abstract just make a list
                            // otherwise create something of the proper type
                            var listOfInner = array ? inner.MakeArrayType()
                                : (child.ParentFieldType.IsInterface | child.ParentFieldType.IsAbstract ?
                              typeof(List<>).MakeGenericType(new[] { inner })
                            : child.ParentFieldType);
                            var infoField = ps.Type.GetField(child.ParentFieldName);
                            var infoProperty = ps.Type.GetProperty(child.ParentFieldName);
                            if (infoField == null && infoProperty == null)
                            {
                                error = string.Format("While building the module '{2}' we were unable to find a field or property called {0} in type {1}", child.ParentFieldName, ps.Type.FullName, ps.Name);
                                return false;
                            }
                            if (!AddCollection(config, root, rootMS, child, infoField, infoProperty, listOfInner, inner, ref error))
                            {
                                return false;
                            }
                            if (child.Children != null)
                            {
                                // now that we have created the children try to attach the parent to them
                                foreach (var cc in child.Children)
                                {
                                    // Now that the child has been created attach this parent object to any fields requesting it
                                    if (!AttachParent(root, cc, ref error))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        else if (child.Type != null)
                        {
                            // if this module is disabled, do not create it!
                            if (mod != null && mod.IsDisabled)
                            {
                                continue;
                            }
                            if (!CreateModule(config, rootMS, child, ref error))
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
                                    error = string.Format("While building the module '{2}' we were unable to write property called {0} in type {1}", child.ParentFieldName, ps.Type.FullName, ps.Name);
                                    return false;
                                }
                                infoProperty.SetValue(root, child.Module, null);
                            }
                            else
                            {
                                error = string.Format("While building the module '{2}' we were unable to find a field or property called {0} in type {1}", child.ParentFieldName, ps.Type.FullName, ps.Name);
                                return false;
                            }

                            // Now that the child has been created attach this parent object to any fields requesting it
                            if (!AttachParent(root, child, ref error))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            ps.Module = root;
            return true;
        }

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
                foreach (var p in parameters)
                {
                    if (p.Name == variableLink[index])
                    {
                        return p;
                    }
                }
            }
            else
            {
                IList<IModelSystemStructure> descList = current.Children;
                if (descList == null)
                {
                    return null;
                }
                if (current.IsCollection)
                {
                    int collectionIndex;
                    if (int.TryParse(variableLink[index], out collectionIndex))
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

        private void InstallNetworkingModules(IModule module, ref string error)
        {
            var moduleType = module.GetType();
            var clientType = typeof(IClient);
            var hostType = typeof(IHost);
            foreach (var field in moduleType.GetFields())
            {
                if (field.IsPublic)
                {
                    if (field.FieldType == clientType)
                    {
                        IClient networkingClient = Configuration.RetriveCurrentNetworkingClient();
                        if (networkingClient != null)
                        {
                            field.SetValue(module, networkingClient);
                        }
                    }
                    else if (field.FieldType == hostType)
                    {
                        IHost networkingHost;
                        if (!Configuration.StartupNetworkingHost(out networkingHost, ref error))
                        {
                            return;
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
                        IClient networkingClient;
                        if (Configuration.StartupNetworkingClient(out networkingClient, ref error))
                        {
                            field.SetValue(module, networkingClient, null);
                        }
                    }
                    else if (field.PropertyType == hostType)
                    {
                        IHost networkingHost;
                        if (Configuration.StartupNetworkingHost(out networkingHost, ref error))
                        {
                            field.SetValue(module, networkingHost, null);
                        }
                    }
                }
            }
        }

        private bool InstallParameters(IModule root, IModelSystemStructure ps, ref string error)
        {
            if (ps.Parameters == null) return true;
            foreach (var param in ps.Parameters)
            {
                if (param.OnField)
                {
                    var info = ps.Type.GetField(param.VariableName);
                    if (info == null)
                    {
                        error = string.Format(System.Globalization.CultureInfo.CurrentCulture, "Unable to find a field called {0} on type {1}!", param.VariableName, ps.Type.FullName);
                        return false;
                    }
                    try
                    {
                        info.SetValue(root, param.Value);
                    }
                    catch (ArgumentException)
                    {
                        error = string.Format("In module {3} we were unable to assign parameter {0} of type {1} with type {2}, please rebuild your model system.",
                            param.Name, info.FieldType.FullName, param.Value.GetType().FullName, ps.Name);
                        return false;
                    }
                }
                else
                {
                    var info = ps.Type.GetProperty(param.VariableName);
                    if (info == null)
                    {
                        error = string.Format(System.Globalization.CultureInfo.CurrentCulture, "Unable to find a property called {0} on type {1}!", param.VariableName, ps.Type.FullName);
                        return false;
                    }
                    try
                    {
                        info.SetValue(root, param.Value, null);
                    }
                    catch (ArgumentException)
                    {
                        error = string.Format("In module {3} we were unable to assign parameter {0} of type {1} with type {2}, please rebuild your model system.",
                            param.Name, info.PropertyType.FullName, param.Value.GetType().FullName, ps.Name);
                        return false;
                    }
                    catch (Exception e)
                    {
                        error = "An unexpected error occurred while trying to set the parameter '" + param.VariableName + "' in '" + ps.Name + "'\r\n" + e.Message + "\r\n" + e.StackTrace;
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Async, load all of the data for this project.
        /// If it doesn't exist then we will create all of the default data.
        /// </summary>
        private bool Load()
        {
            if (Path.IsPathRooted(Name))
            {
                DirectoryLocation = Path.GetDirectoryName(Name);
            }
            else
            {
                DirectoryLocation = Path.Combine(Configuration.ProjectDirectory, Name);
            }
            if (DirectoryLocation == null)
            {
                return false;
            }
            IsLoaded = false;
            string fileLocation = Path.Combine(DirectoryLocation, "Project.xml");
            _ModelSystemStructure = new List<IModelSystemStructure>();
            _LinkedParameters = new List<List<ILinkedParameter>>();
            if (RemoteProject)
            {
                IsLoaded = true;
                return true;
            }
            if (!Directory.Exists(DirectoryLocation) || !File.Exists(fileLocation))
            {
                IsLoaded = true;
                return true;
            }
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(fileLocation);
                XmlNode rootNode = doc["Root"];
                if (rootNode != null)
                {
                    var description = rootNode.Attributes?["Description"];
                    if (description != null)
                    {
                        Description = description.InnerText;
                    }
                    var rootChildren = rootNode.ChildNodes;
                    var toLoad = new ProjectModelSystem[rootChildren.Count];
                    _Descriptions = new List<string>(rootChildren.Count);
                    for (int i = 0; i < rootChildren.Count; i++)
                    {
                        _Descriptions.Add(String.Empty);
                    }
                    Parallel.For(0, rootChildren.Count, i =>
                    {
                        XmlNode child = rootChildren[i];
                        // check for the 3.0 file name
                        if (child.Name == "AdvancedModelSystem")
                        {
                            IModelSystemStructure mss;
                            List<ILinkedParameter> lp;
                            if (LoadAdvancedModelSystem(child, i, out mss, out lp))
                            {
                                toLoad[i] = new ProjectModelSystem() { LinkedParameters = lp, Root = mss };
                            }
                        }
                    });
                    for (int i = 0; i < toLoad.Length; i++)
                    {
                        if (toLoad[i] != null)
                        {
                            _ModelSystemStructure.Add(toLoad[i].Root);
                            if (toLoad[i].LinkedParameters != null)
                            {
                                _LinkedParameters.Add(toLoad[i].LinkedParameters);
                            }
                            else
                            {
                                _LinkedParameters.Add(new List<ILinkedParameter>());
                            }
                        }
                    }
                    IsLoaded = true;
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
            }
            return false;
        }

        private class ProjectModelSystem
        {
            internal IModelSystemStructure Root;
            internal List<ILinkedParameter> LinkedParameters;
        }

        private bool LoadAdvancedModelSystem(XmlNode child, int index, out IModelSystemStructure mss, out List<ILinkedParameter> lpl)
        {
            mss = null;
            lpl = null;
            bool hasDescription = false;
            var attributes = child.Attributes;
            if (attributes != null)
            {
                foreach (XmlAttribute attribute in attributes)
                {
                    if (attribute.Name == "Description")
                    {
                        hasDescription = true;
                        _Descriptions[index] = attribute.InnerText;
                        break;
                    }
                }
            }
            if (!hasDescription)
            {
                _Descriptions[index] = "No Description";
            }
            if (child.HasChildNodes)
            {
                for (int i = 0; i < child.ChildNodes.Count; i++)
                {
                    switch (child.ChildNodes[i].Name)
                    {
                        case "ModelSystem":
                            {
                                if (mss == null)
                                {
                                    if (child.ChildNodes[i].FirstChild != null)
                                    {
                                        ModelSystemStructure ms = XTMF.ModelSystemStructure.Load(child.ChildNodes[i], Configuration);
                                        if (ms != null)
                                        {
                                            mss = ms;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
                if (mss == null)
                {
                    return false;
                }
                // now do a second pass for Linked parameters, since we need the current model system to actually link things
                for (int i = 0; i < child.ChildNodes.Count; i++)
                {
                    switch (child.ChildNodes[i].Name)
                    {
                        case "LinkedParameters":
                            {
                                lpl = LoadLinkedParameters(child.ChildNodes[i], mss);
                            }
                            break;
                    }
                }
            }
            return true;
        }

        private void LoadDescription()
        {
            try
            {
                var fileName = Path.Combine(Configuration.ProjectDirectory, Name, "Project.xml");
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    while (!reader.EOF && reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        switch (reader.LocalName)
                        {
                            case "Root":
                                {
                                    Description = reader.GetAttribute("Description");
                                    // we can just exit at this point since using will clean up for us
                                    return;
                                }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private List<ILinkedParameter> LoadLinkedParameters(XmlNode xmlNode, IModelSystemStructure mss)
        {
            List<ILinkedParameter> lpl = new List<ILinkedParameter>();
            // if there is nothing to load just return back a blank list
            if (!xmlNode.HasChildNodes)
            {
                return lpl;
            }

            foreach (XmlNode lpNode in xmlNode.ChildNodes)
            {
                if (lpNode.Name == "LinkedParameter")
                {
                    var name = "unnamed";
                    var value = string.Empty;
                    var attributes = lpNode.Attributes;
                    if (attributes != null)
                    {
                        foreach (XmlAttribute attribute in attributes)
                        {
                            switch (attribute.Name)
                            {
                                case "Name":
                                {
                                    name = attribute.InnerText;
                                }
                                    break;

                                case "Value":
                                {
                                    value = attribute.InnerText;
                                }
                                    break;
                            }
                        }
                    }
                    LinkedParameter lp = new LinkedParameter(name);
                    string error = null;
                    lp.SetValue(value, ref error);
                    lpl.Add(lp);
                    // if there are no references just continue
                    if (!lpNode.HasChildNodes)
                    {
                        continue;
                    }
                    foreach (XmlNode lpCNode in lpNode)
                    {
                        if (lpCNode.Name == "Reference")
                        {
                            if (lpCNode.Attributes != null)
                            {
                                foreach (XmlAttribute attribute in lpCNode.Attributes)
                                {
                                    if (attribute.Name == "Name")
                                    {
                                        var param = GetParameterFromLink(attribute.InnerText, mss);
                                        if (param != null)
                                        {
                                            lp.Add(param, ref error);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return lpl;
        }

        private string LookupName(IModuleParameter reference, IModelSystemStructure current)
        {
            var param = current.Parameters;
            if (param != null)
            {
                int index = param.Parameters.IndexOf(reference);
                if (index >= 0)
                {
                    return current.Parameters.Parameters[index].Name;
                }
            }
            var childrenList = current.Children;
            if (childrenList != null)
            {
                for (int i = 0; i < childrenList.Count; i++)
                {
                    var res = LookupName(reference, childrenList[i]);
                    if (res != null)
                    {
                        // make sure to use an escape character before the . to avoid making the mistake of reading it as another index
                        return string.Concat(current.IsCollection ? i.ToString()
                            : childrenList[i].ParentFieldName.Replace(".", "\\."), '.', res);
                    }
                }
            }
            return null;
        }

        private string[] ParseLinkedParameterName(string variableLink)
        {
            List<string> ret = new List<string>();
            bool escape = false;
            var length = variableLink.Length;
            StringBuilder builder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                var c = variableLink[i];
                // check to see if we need to add in the escape
                if (escape & c != '.')
                {
                    builder.Append('\\');
                }
                // check to see if we need to move onto the next part
                if (escape == false & c == '.')
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

        private void WriteLinkedParameters(XmlTextWriter writer, List<ILinkedParameter> lpl, IModelSystemStructure mss)
        {
            foreach (var lp in lpl)
            {
                writer.WriteStartElement("LinkedParameter");
                writer.WriteAttributeString("Name", lp.Name);
                if (lp.Value != null)
                {
                    writer.WriteAttributeString("Value", lp.Value.ToString());
                }
                foreach (var reference in lp.Parameters)
                {
                    writer.WriteStartElement("Reference");
                    writer.WriteAttributeString("Name", LookupName(reference, mss));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
    }
}