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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XTMF
{
    public class ModelSystemStructure : IModelSystemStructure
    {
        private Type _Type;

        public ModelSystemStructure(IConfiguration config, string name, Type parentFieldType)
            : this(config)
        {
            this.Name = name;
            this.Children = null;
            this.Module = null;
            this.ParentFieldType = parentFieldType;
        }

        internal ModelSystemStructure(IConfiguration config)
        {
            this.Configuration = config;
        }

        public IList<IModelSystemStructure> Children
        {
            get;
            set;
        }

        public IConfiguration Configuration { get; set; }

        public string Description { get; set; }

        public bool IsCollection { get; private set; }

        public IModule Module
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public IModuleParameters Parameters
        {
            get;
            set;
        }

        public string ParentFieldName { get; set; }

        public Type ParentFieldType { get; set; }

        public bool Required { get; set; }

        public Type Type
        {
            get
            {
                return this._Type;
            }

            set
            {
                if(this.Children != null)
                {
                    this.Children.Clear();
                }
                if(value != null)
                {
                    if((this.Parameters = Project.LoadDefaultParams(value)) != null)
                    {
                        (this.Parameters as ModuleParameters).BelongsTo = this;
                        foreach(var p in this.Parameters)
                        {
                            (p as ModuleParameter).BelongsTo = this;
                        }
                    }
                    bool nullBefore = this._Type == null;
                    this._Type = value;
                    ModelSystemStructure.GenerateChildren(this.Configuration, this);
                }
                else
                {
                    this.Parameters = null;
                    this._Type = null;
                }
            }
        }

        public static bool CheckForParent(Type parent, Type t)
        {
            foreach(var field in t.GetFields())
            {
                var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                if(attributes != null && attributes.Length > 0)
                {
                    if(!field.FieldType.IsAssignableFrom(parent))
                    {
                        return false;
                    }
                }
            }
            foreach(var field in t.GetProperties())
            {
                var attributes = field.GetCustomAttributes(typeof(ParentModel), true);
                if(attributes != null && attributes.Length > 0)
                {
                    if(!field.PropertyType.IsAssignableFrom(parent))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool CheckForRootModel(Type root, Type t)
        {
            // get what module is required
            var rootRequirement = GetRootRequirement(t);
            // if there is no requirement then we are fine
            if(rootRequirement == null)
            {
                return true;
            }
            // if there is a requirement, make sure that we can assign to it properly
            return rootRequirement.IsAssignableFrom(root);
        }

        /// <summary>
        /// Check to see which module can act as the root
        /// </summary>
        /// <param name="start">The starting point to check from</param>
        /// <param name="objective">The structure that we are trying to assign to</param>
        /// <param name="t">The type that we want to insert</param>
        /// <returns>The structure that can act as the root for the given type, and objective structure</returns>
        public static IModelSystemStructure CheckForRootModule(IModelSystemStructure start, IModelSystemStructure objective, Type t)
        {
            IModelSystemStructure result = null;
            var rootRequirement = GetRootRequirement(t);
            if(rootRequirement == null)
            {
                return start;
            }
            else if(start == objective)
            {
                if(rootRequirement.IsAssignableFrom(start.Type))
                {
                    return start;
                }
                return null;
            }
            CheckForRootModule(start, objective, rootRequirement, ref result);
            return result;
        }

        public static void GenerateChildren(IConfiguration config, IModelSystemStructure element)
        {
            if(element == null) return;
            if(element.Type == null) return;

            foreach(var field in element.Type.GetFields())
            {
                IModelSystemStructure child = null;
                if((child = GenerateChildren(element, field.FieldType, field.GetCustomAttributes(true), config)) != null)
                {
                    // set the name
                    child.ParentFieldName = field.Name;
                    child.Name = CreateModuleName(field.Name);
                    if(element.IsCollection)
                    {
                        child.ParentFieldType = field.FieldType.GetGenericArguments()[0];
                    }
                    else
                    {
                        child.ParentFieldType = field.FieldType;
                    }
                    element.Add(child);
                }
            }
            foreach(var property in element.Type.GetProperties())
            {
                IModelSystemStructure child = null;
                if((child = GenerateChildren(element, property.PropertyType, property.GetCustomAttributes(true), config)) != null)
                {
                    child.ParentFieldName = property.Name;
                    child.Name = CreateModuleName(property.Name);
                    child.ParentFieldType = property.PropertyType;
                    element.Add(child);
                }
            }
            if(element.Children != null & !element.IsCollection)
            {
                SortChildren(element.Children);
            }
        }

        /// <summary>
        /// Get the parent structure of the objective
        /// </summary>
        /// <param name="topModule"></param>
        /// <param name="objective"></param>
        /// <returns>null if the top and objective are not connected, the parent otherwise</returns>
        public static IModelSystemStructure GetParent(IModelSystemStructure topModule, IModelSystemStructure objective)
        {
            // if we are the module, return ourselves
            if(topModule == objective)
            {
                return topModule;
            }
            // otherwise find which list to go down
            IList<IModelSystemStructure> childrenList = topModule.Children;
            // if there are no children then we are done
            if(childrenList == null)
            {
                return null;
            }
            // search our children to see if their are either the objective or are the ancestors of the objective
            IModelSystemStructure ret = null;
            foreach(var child in childrenList)
            {
                if(child == objective)
                {
                    return topModule;
                }
                else if((ret = GetParent(child, objective)) != null)
                {
                    break;
                }
            }
            // return what we found
            return ret;
        }

        public static Type GetRootRequirement(Type moduleType)
        {
            if(moduleType != null)
            {
                foreach(var field in moduleType.GetFields())
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if(attributes != null && attributes.Length > 0)
                    {
                        return field.FieldType;
                    }
                }
                foreach(var field in moduleType.GetProperties())
                {
                    var attributes = field.GetCustomAttributes(typeof(RootModule), true);
                    if(attributes != null && attributes.Length > 0)
                    {
                        return field.PropertyType;
                    }
                }
            }
            return null;
        }

        public static IModelSystemStructure Load(Stream stream, IConfiguration config)
        {
            ModelSystemStructure root = new ModelSystemStructure(config);
            root.Description = "The Model System Template that the project is based on";
            root.Required = true;
            root.ParentFieldType = typeof(IModelSystemTemplate);
            root.ParentFieldName = "Root";
            XmlDocument doc = new XmlDocument();
            doc.Load(stream);
            LoadRoot(config, root, doc["Root"].ChildNodes);
            return root;
        }

        public static IModelSystemStructure Load(string fileName, IConfiguration config)
        {
            ModelSystemStructure root = new ModelSystemStructure(config);
            root.Description = "The Model System Template that the project is based on";
            root.Required = true;
            root.ParentFieldType = typeof(IModelSystemTemplate);
            root.ParentFieldName = "Root";
            if(!File.Exists(fileName))
            {
                return root;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            var list = doc["Root"].ChildNodes;
            LoadRoot(config, root, list);
            return root;
        }

        public void Add(string name, Type type)
        {
            if(this.Children == null)
            {
                this.Children = new List<IModelSystemStructure>();
            }
            var newChild = new ModelSystemStructure(this.Configuration, name, ParentFieldType);
            newChild.Type = type;
            this.Children.Add(newChild);
        }

        public void Add(IModelSystemStructure p)
        {
            if(this.Children == null)
            {
                this.Children = new List<IModelSystemStructure>();
            }
            this.Children.Add(p);
        }

        public IModelSystemStructure Clone()
        {
            ModelSystemStructure cloneUs = new ModelSystemStructure(this.Configuration);
            cloneUs.Name = this.Name;
            cloneUs.Description = this.Description;
            cloneUs.Module = this.Module;
            if(this.Parameters != null)
            {
                if((cloneUs.Parameters = this.Parameters.Clone()) != null)
                {
                    (cloneUs.Parameters as ModuleParameters).BelongsTo = cloneUs;
                    foreach(var p in cloneUs.Parameters)
                    {
                        (p as ModuleParameter).BelongsTo = cloneUs;
                    }
                }
            }
            cloneUs.Required = this.Required;
            cloneUs.ParentFieldName = this.ParentFieldName;
            cloneUs.ParentFieldType = this.ParentFieldType;
            cloneUs._Type = this._Type;
            cloneUs.IsCollection = this.IsCollection;
            if(this.Children != null)
            {
                foreach(var child in this.Children)
                {
                    cloneUs.Add(child.Clone());
                }
            }
            return cloneUs;
        }

        internal ModelSystemStructure GetRoot(ModelSystemStructure modelSystemRoot)
        {
            var requiredRootType = ModelSystemStructure.GetRootRequirement(Type);
            if(requiredRootType == null)
            {
                requiredRootType = typeof(IModelSystemTemplate);
            }
            return ModelSystemStructure.CheckForRootModule(modelSystemRoot, this, requiredRootType) as ModelSystemStructure;
        }

        public IModelSystemStructure CreateCollectionMember(Type newType)
        {
            if(this.IsCollection)
            {
                return CreateCollectionMember(CreateModuleName(newType.Name), newType);
            }
            return null;
        }

        public IModelSystemStructure CreateCollectionMember(string name, Type newType)
        {
            if(this.IsCollection)
            {
                if(this.Children == null)
                {
                    this.Children = new List<IModelSystemStructure>();
                }
                ModelSystemStructure p = new ModelSystemStructure(this.Configuration);
                Type innerType = this.ParentFieldType.IsArray ? this.ParentFieldType.GetElementType()
                    : this.ParentFieldType.GetGenericArguments()[0];
                p.Type = newType;
                p.ParentFieldType = innerType;
                p.ParentFieldName = this.ParentFieldName;
                p.Name = name;
                return p;
            }
            return null;
        }

        /// <summary>
        /// Check to see if a type is valid for a module.
        /// </summary>
        /// <param name="type">The type to check for.</param>
        /// <param name="topLevelModule">The top level module</param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal bool CheckPossibleModule(Type type, ModelSystemStructure topLevelModule, ref string error)
        {
            var rootRequirement = GetRootRequirement(type);
            if(this.IsCollection)
            {
                var arguements = this.ParentFieldType.IsArray ? this.ParentFieldType.GetElementType() : this.ParentFieldType.GetGenericArguments()[0];
                if(!(arguements.IsAssignableFrom(type) && (CheckForParent(arguements, type)) && CheckForRootModule(topLevelModule, this, rootRequirement) != null))
                {
                    if(!arguements.IsAssignableFrom(type))
                    {
                        error = "The type is not valid for the collection!";
                    }
                    else if(!CheckForParent(arguements, type))
                    {
                        error = "This type requires a parent module which is unsupported for collections!";
                    }
                    else if(CheckForRootModule(topLevelModule, this, rootRequirement) == null)
                    {
                        error = "There is no root module that can support this type at this position!";
                    }
                    return false;
                }
            }
            else
            {
                var parent = GetParent(topLevelModule, this);
                if(!(this.ParentFieldType.IsAssignableFrom(type) && (parent == null || CheckForParent(parent.Type, type))
                        && CheckForRootModule(topLevelModule, this, rootRequirement) != null))
                {
                    if(!this.ParentFieldType.IsAssignableFrom(type))
                    {
                        error = "This type does not meet the requirements of the parent!";
                    }
                    else if(!(parent == null || CheckForParent(parent.Type, type)))
                    {
                        error = "The type does not support the parent as a valid option!";
                    }
                    else if(CheckForRootModule(topLevelModule, this, rootRequirement) == null)
                    {
                        error = "There is no root module that can support this type at this position!";
                    }
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get all of the possible modules given the top level structure
        /// </summary>
        /// <param name="topModule"></param>
        /// <returns></returns>
        public List<Type> GetPossibleModules(IModelSystemStructure topModule)
        {
            ConcurrentBag<Type> possibleTypes = new ConcurrentBag<Type>();
            var parent = GetParent(topModule, this);
            if(this.IsCollection)
            {
                GetPossibleModulesCollection(possibleTypes, parent.Type, topModule);
            }
            else
            {
                GetPossibleModulesChildren(topModule, possibleTypes, parent);
            }
            // return a list of the bag
            return new List<Type>(possibleTypes);
        }

        public void Save(Stream stream)
        {
            XmlTextWriter writer = new XmlTextWriter(stream, Encoding.Unicode);
            writer.Formatting = Formatting.Indented;
            writer.WriteStartDocument();
            writer.WriteStartElement("Root");
            Save(writer);
            writer.WriteEndElement();
            writer.Flush();
        }

        public void Save(XmlWriter writer)
        {
            var typesUsed = GatherAllTypes(this);
            var lookUp = CreateInverseLookupTable(typesUsed);
            SaveTypes(writer, typesUsed);
            typesUsed = null;
            this.Save(writer, this, lookUp);
            writer.Flush();
        }

        public void Save(string fileName)
        {
            var dirName = Path.GetDirectoryName(fileName);
            if(!String.IsNullOrWhiteSpace(dirName) && !Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                this.Save(fs);
            }
        }

        public override string ToString()
        {
            return this.Name != null ? this.Name : "No Name";
        }

        public bool Validate(ref string error, IModelSystemStructure parent = null)
        {
            if(this.Required)
            {
                if(this.IsCollection)
                {
                    if(this.Children == null || this.Children.Count == 0)
                    {
                        error = "The collection '" + this.Name + "' in module '" + parent.Name + "'requires at least one module for the list!\r\nPlease remove this model system from your project and edit the model system.";
                        return false;
                    }
                }
                else
                {
                    if(this.Type == null)
                    {
                        error = "In '" + this.Name + "' a type for a required field is not selected for.\r\nPlease remove this model system from your project and edit the model system.";
                        return false;
                    }
                }
            }

            if(this.ParentFieldType == null)
            {
                error = "There is an error where a parent's field type was not loaded properly!\nPlease contact the TMG to resolve this."
                    + "\r\nError for module '" + this.Name + "' of type '" + this.Type.FullName + "'";
                return false;
            }

            if(this.Type != null && !this.ParentFieldType.IsAssignableFrom(this.Type))
            {
                error = String.Format("In {2} the type {0} selected can not be assigned to its parent's field of type {1}!", this.Type, this.ParentFieldType, this.Name);
                return false;
            }

            if(this.Children != null)
            {
                foreach(var child in this.Children)
                {
                    if(!child.Validate(ref error, this))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal static ModelSystemStructure Load(XmlNode modelSystemNode, IConfiguration config)
        {
            XTMF.ModelSystemStructure structure = new ModelSystemStructure(config);
            LoadRoot(config, structure, modelSystemNode.ChildNodes);
            return structure;
        }

        private static void AddIfNotContained(Type t, List<Type> ret)
        {
            if(t != null)
            {
                if(!ret.Contains(t))
                {
                    ret.Add(t);
                }
            }
        }

        private static Type AquireTypeFromField(IModelSystemStructure parent, string fieldName)
        {
            if(parent.Type == null)
            {
                return null;
            }
            var field = parent.Type.GetField(fieldName);
            if(field != null)
            {
                return field.FieldType;
            }
            else
            {
                var property = parent.Type.GetProperty(fieldName);
                if(property != null)
                {
                    return property.PropertyType;
                }
            }
            return null;
        }

        private static void AssignTypeValue(XmlAttribute paramTIndex, XmlAttribute paramTypeAttribute, XmlAttribute paramValueAttribute, IModuleParameter selectedParam, Dictionary<int, Type> lookUp)
        {
            string error = null;
            var temp = ArbitraryParameterParser.ArbitraryParameterParse(selectedParam.Type, paramValueAttribute.InnerText, ref error);
            if(temp != null)
            {
                // don't overwrite the default if we are loading something bad
                selectedParam.Value = temp;
            }
        }

        private static void BackupTypeLoader(XmlAttribute paramTypeAttribute, XmlAttribute paramValueAttribute, IModuleParameter selectedParam)
        {
            switch(paramTypeAttribute.InnerText)
            {
                case "System.String":
                    {
                        selectedParam.Value = paramValueAttribute.InnerText;
                    }
                    break;

                case "System.Int32":
                    {
                        Int32 temp;
                        if(Int32.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.Int64":
                    {
                        Int64 temp;
                        if(Int64.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.DateTime":
                    {
                        DateTime temp;
                        if(DateTime.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.Single":
                    {
                        Single temp;
                        if(Single.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.Double":
                    {
                        Double temp;
                        if(Double.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.Boolean":
                    {
                        bool temp;
                        if(Boolean.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                case "System.Char":
                    {
                        char temp;
                        if(Char.TryParse(paramValueAttribute.InnerText, out temp))
                        {
                            selectedParam.Value = temp;
                        }
                    }
                    break;

                default:
                    {
                        //TODO: Unable to load a type we don't know about, should add this to a Log entry or something
                    }
                    break;
            }
        }

        /// <summary>
        /// Recursively find the last instance of a structure that is connected to the objective that
        /// is able to satisfy t's root requirement
        /// </summary>
        /// <param name="start"></param>
        /// <param name="objective"></param>
        /// <param name="rootRequirement"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static bool CheckForRootModule(IModelSystemStructure start, IModelSystemStructure objective, Type rootRequirement, ref IModelSystemStructure result)
        {
            if(start == objective)
            {
                return true;
            }
            IList<IModelSystemStructure> childrenList = start.Children;
            if(childrenList == null)
            {
                return false;
            }
            foreach(var child in childrenList)
            {
                if(CheckForRootModule(child, objective, rootRequirement, ref result))
                {
                    // check to see if we have a result already
                    if(result == null && start.Type != null && rootRequirement.IsAssignableFrom(start.Type))
                    {
                        // if there is no result yet check to see if we can match the root request
                        result = start;
                    }
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<Type, int> CreateInverseLookupTable(List<Type> typesUsed)
        {
            Dictionary<Type, int> ret = new Dictionary<Type, int>();
            for(int i = 0; i < typesUsed.Count; i++)
            {
                ret[typesUsed[i]] = i;
            }
            return ret;
        }

        private static string CreateModuleName(string baseName)
        {
            StringBuilder nameBuilder = new StringBuilder(50);
            var length = baseName.Length;
            bool lastUpper = true;
            if(length > 0)
            {
                nameBuilder.Append(baseName[0]);
            }
            for(int i = 1; i < length; i++)
            {
                var c = baseName[i];
                if(Char.IsUpper(c))
                {
                    if(!lastUpper)
                    {
                        nameBuilder.Append(' ');
                    }
                }
                else
                {
                    lastUpper = false;
                }
                nameBuilder.Append(c);
            }
            return nameBuilder.ToString();
        }

        private static List<Type> GatherAllTypes(ModelSystemStructure start)
        {
            List<Type> ret = new List<Type>();
            GatherAllTypes(start, ret);
            return ret;
        }

        private static void GatherAllTypes(IModelSystemStructure current, List<Type> ret)
        {
            // Gather the types
            GatherTypes(current, ret);
            // recurse for the rest of the structure
            if(current.Children != null)
            {
                foreach(var child in current.Children)
                {
                    GatherAllTypes(child, ret);
                }
            }
        }

        private static void GatherTypes(IModelSystemStructure current, List<Type> ret)
        {
            AddIfNotContained(current.ParentFieldType, ret);
            AddIfNotContained(current.Type, ret);
            var parameters = current.Parameters;
            if(parameters != null)
            {
                foreach(var v in parameters)
                {
                    AddIfNotContained(v.Type, ret);
                }
            }
        }

        private static IModelSystemStructure GenerateChildren(IModelSystemStructure element, Type type, object[] attributes, IConfiguration config)
        {
            Type iModel = typeof(IModule);
            if(type.IsArray)
            {
                var argument = type.GetElementType();
                if(iModel.IsAssignableFrom(argument))
                {
                    ModelSystemStructure child = new ModelSystemStructure(config);
                    child.IsCollection = true;
                    child.Children = new List<IModelSystemStructure>();
                    foreach(var at in attributes)
                    {
                        if(at is DoNotAutomate)
                        {
                            return null;
                        }
                        else if(at is SubModelInformation)
                        {
                            SubModelInformation info = at as SubModelInformation;
                            child.Description = info.Description;
                            child.Required = info.Required;
                        }
                        if(child.Description == null)
                        {
                            child.Description = "No description available";
                            child.Required = false;
                        }
                    }
                    return child;
                }
            }

            if(type.IsGenericType)
            {
                var arguements = type.GetGenericArguments();
                if(arguements != null && arguements.Length == 1)
                {
                    // if the type of this generic is assignable to IModel..
                    if(iModel.IsAssignableFrom(arguements[0]))
                    {
                        Type iCollection = typeof(ICollection<>).MakeGenericType(arguements[0]);
                        if(iCollection.IsAssignableFrom(type))
                        {
                            ModelSystemStructure child = new ModelSystemStructure(config);
                            child.IsCollection = true;
                            child.Children = new List<IModelSystemStructure>();
                            foreach(var at in attributes)
                            {
                                if(at is DoNotAutomate)
                                {
                                    return null;
                                }
                                else if(at is SubModelInformation)
                                {
                                    SubModelInformation info = at as SubModelInformation;
                                    child.Description = info.Description;
                                    child.Required = info.Required;
                                }
                                if(child.Description == null)
                                {
                                    child.Description = "No description available";
                                    child.Required = false;
                                }
                            }
                            return child;
                        }
                    }
                }
            }

            if(iModel.IsAssignableFrom(type))
            {
                ModelSystemStructure child = new ModelSystemStructure(config);
                foreach(var at in attributes)
                {
                    if(at is ParentModel || at is DoNotAutomate || at is RootModule)
                    {
                        return null;
                    }
                    if(at is SubModelInformation)
                    {
                        SubModelInformation info = at as SubModelInformation;
                        child.Description = info.Description;
                        child.Required = info.Required;
                    }
                }
                if(child.Description == null)
                {
                    child.Description = "No description available";
                    child.Required = false;
                }
                return child;
            }
            return null;
        }

        private static void Load(IModelSystemStructure projectStructure, IModelSystemStructure parent, XmlNode currentNode, IConfiguration config, Dictionary<int, Type> lookup)
        {
            var nameAttribute = currentNode.Attributes["Name"];
            var descriptionAttribute = currentNode.Attributes["Description"];
            var typeAttribute = currentNode.Attributes["Type"];
            var tIndexAttribute = currentNode.Attributes["TIndex"];
            var parentFieldNameAttribute = currentNode.Attributes["ParentFieldName"];
            var parentFieldTypeAttribute = currentNode.Attributes["ParentFieldType"];
            var parentTIndexAttribute = currentNode.Attributes["ParentTIndex"];
            if(nameAttribute != null)
            {
                projectStructure.Name = nameAttribute.InnerText;
            }
            // Find the type
            if(tIndexAttribute != null)
            {
                int index = -1;
                if(!int.TryParse(tIndexAttribute.InnerText, out index))
                {
                    index = -1;
                }
                if(index >= 0)
                {
                    Type t;
                    if(lookup.TryGetValue(index, out t))
                    {
                        projectStructure.Type = t;
                    }
                    else
                    {
                        projectStructure.Type = null;
                    }
                }
                else
                {
                    projectStructure.Type = null;
                }
            }
            else if(typeAttribute != null)
            {
                string typeName = typeAttribute.InnerText;
                if(typeName == "null")
                {
                    projectStructure.Type = null;
                }
                else
                {
                    projectStructure.Type = Type.GetType(typeName);
                }
            }
            if(descriptionAttribute != null)
            {
                projectStructure.Description = descriptionAttribute.InnerText;
            }
            if(parentFieldNameAttribute != null)
            {
                projectStructure.ParentFieldName = parentFieldNameAttribute.InnerText;
            }
            if(parentTIndexAttribute != null)
            {
                int index = -1;
                if(!int.TryParse(parentTIndexAttribute.InnerText, out index))
                {
                    index = -1;
                }
                if(index >= 0)
                {
                    projectStructure.ParentFieldType = lookup[index];
                    if(projectStructure.ParentFieldType == null)
                    {
                        projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                    }
                }
                else
                {
                    projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                }
            }
            else if(parentFieldTypeAttribute != null)
            {
                var typeName = parentFieldTypeAttribute.InnerText;
                if(typeName == "null")
                {
                    projectStructure.ParentFieldType = AquireTypeFromField(parent, projectStructure.ParentFieldName);
                }
                else
                {
                    projectStructure.ParentFieldType = Type.GetType(typeName);
                }
            }
            // get the default parameters before loading from
            if((projectStructure.Parameters = Project.LoadDefaultParams(projectStructure.Type)) != null)
            {
                (projectStructure.Parameters as ModuleParameters).BelongsTo = projectStructure;
                foreach(var p in projectStructure.Parameters)
                {
                    (p as ModuleParameter).BelongsTo = projectStructure;
                }
            }
            if(currentNode.HasChildNodes)
            {
                foreach(XmlNode child in currentNode.ChildNodes)
                {
                    LoadChildNode(projectStructure, child, config, lookup);
                }
                //Organize in alphabetical order
                if(!projectStructure.IsCollection & projectStructure.Children != null)
                {
                    SortChildren(projectStructure.Children);
                }
            }
        }

        /// <summary>
        /// Sort the list of model system structures based upon their name.
        /// </summary>
        /// <param name="list">The list of model system structures to sort.</param>
        private static void SortChildren(IList<IModelSystemStructure> list)
        {
            for(int i = 0; i < list.Count; i++)
            {
                bool anyChanges = false;
                for(int j = 0; j < list.Count - 1 - i; j++)
                {
                    if(list[j].Name.CompareTo(list[j + 1].Name) > 0)
                    {
                        var temp = list[j];
                        list[j] = list[j + 1];
                        list[j + 1] = temp;
                        anyChanges = true;
                    }
                }
                if(!anyChanges)
                {
                    break;
                }
            }
        }

        private static void LoadChildNode(IModelSystemStructure modelSystemStructure, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            switch(child.Name)
            {
                case "Module":
                    {
                        LoadModule(modelSystemStructure, child, config, lookUp);
                    }
                    break;

                case "Collection":
                    {
                        LoadCollection(modelSystemStructure, child, config, lookUp);
                    }
                    break;

                case "Parameters":
                    {
                        LoadParameters(modelSystemStructure, child, lookUp);
                    }
                    break;
            }
        }

        private static void LoadCollection(IModelSystemStructure parent, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            var paramNameAttribute = child.Attributes["ParentFieldName"];
            var paramTIndexAttribute = child.Attributes["ParentTIndex"];
            var paramTypeAttribute = child.Attributes["ParentFieldType"];
            var NameAttribute = child.Attributes["Name"];
            IModelSystemStructure us = null;
            if(paramNameAttribute != null && (paramTIndexAttribute != null || paramTypeAttribute != null))
            {
                if(parent.Children == null)
                {
                    return;
                }
                for(int i = 0; i < parent.Children.Count; i++)
                {
                    if(parent.Children[i].ParentFieldName == paramNameAttribute.InnerText)
                    {
                        us = parent.Children[i];
                        break;
                    }
                }
                if(us != null)
                {
                    us.ParentFieldType = AquireTypeFromField(parent, us.ParentFieldName);
                    if(NameAttribute != null)
                    {
                        us.Name = NameAttribute.InnerText;
                    }
                    us.ParentFieldName = paramNameAttribute.InnerText;
                    // now load the children
                    if(child.HasChildNodes)
                    {
                        foreach(XmlNode element in child.ChildNodes)
                        {
                            XTMF.ModelSystemStructure ps = new ModelSystemStructure(config);
                            Load(ps, us, element, config, lookUp);
                            if(ps.ParentFieldType == null || ps.ParentFieldName == null)
                            {
                                ps.ParentFieldName = us.ParentFieldName;
                                ps.ParentFieldType = us.Type;
                            }
                            us.Children.Add(ps);
                        }
                    }
                }
            }
        }

        private static void LoadDefinitions(XmlNode definitionNode, Dictionary<int, Type> lookUp)
        {
            if(definitionNode.HasChildNodes)
            {
                foreach(XmlNode child in definitionNode.ChildNodes)
                {
                    //writer.WriteStartElement( "Type" );
                    if(child.LocalName == "Type")
                    {
                        try
                        {
                            //writer.WriteElementString( "Name", typesUsed[i].AssemblyQualifiedName );
                            var type = Type.GetType(child.Attributes["Name"].InnerText);
                            int index = -1;
                            //writer.WriteElementString( "TIndex", i.ToString() );
                            if(!int.TryParse(child.Attributes["TIndex"].InnerText, out index))
                            {
                                continue;
                            }
                            lookUp[index] = type;
                        }
                        catch (TypeLoadException)
                        {
                        }
                    }
                }
            }
        }

        private static void LoadModule(IModelSystemStructure modelSystemStructure, XmlNode child, IConfiguration config, Dictionary<int, Type> lookUp)
        {
            if(modelSystemStructure.Children != null && !modelSystemStructure.IsCollection)
            {
                var parentFieldNameAttribute = child.Attributes["ParentFieldName"];
                if(parentFieldNameAttribute != null)
                {
                    for(int i = 0; i < modelSystemStructure.Children.Count; i++)
                    {
                        if(modelSystemStructure.Children[i].ParentFieldName == parentFieldNameAttribute.InnerText)
                        {
                            Load(modelSystemStructure.Children[i], modelSystemStructure, child, config, lookUp);
                        }
                    }
                }
            }
        }

        private static void LoadParameters(IModelSystemStructure modelSystemStructure, XmlNode child, Dictionary<int, Type> lookUp)
        {
            if(child.HasChildNodes)
            {
                foreach(XmlNode paramChild in child.ChildNodes)
                {
                    if(paramChild.Name == "Param")
                    {
                        var paramNameAttribute = paramChild.Attributes["Name"];
                        var paramTIndexAttribute = paramChild.Attributes["TIndex"];
                        var paramTypeAttribute = paramChild.Attributes["Type"];
                        var paramValueAttribute = paramChild.Attributes["Value"];
                        var paramQuickParameterAttribute = paramChild.Attributes["QuickParameter"];
                        if(paramNameAttribute != null || paramTypeAttribute != null || paramValueAttribute != null)
                        {
                            string name = paramNameAttribute.InnerText;
                            if(modelSystemStructure.Parameters != null)
                            {
                                IModuleParameter selectedParam = null;
                                foreach(var p in modelSystemStructure.Parameters)
                                {
                                    if(p.Name == name)
                                    {
                                        selectedParam = p;
                                        break;
                                    }
                                }

                                // we will just ignore parameters that no longer exist
                                if(selectedParam != null)
                                {
                                    if(paramQuickParameterAttribute != null)
                                    {
                                        bool quick;
                                        if(bool.TryParse(paramQuickParameterAttribute.InnerText, out quick))
                                        {
                                            selectedParam.QuickParameter = quick;
                                        }
                                    }
                                    AssignTypeValue(paramTIndexAttribute, paramTypeAttribute, paramValueAttribute, selectedParam, lookUp);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void LoadRoot(IConfiguration config, ModelSystemStructure root, XmlNodeList list)
        {
            if(list != null)
            {
                var lookUp = new Dictionary<int, Type>(20);
                for(int i = 0; i < list.Count; i++)
                {
                    var child = list[i];
                    if(child.LocalName == "TypeDefinitions")
                    {
                        LoadDefinitions(child, lookUp);
                    }
                }
                for(int i = 0; i < list.Count; i++)
                {
                    var child = list[i];
                    if(child.LocalName == "Module")
                    {
                        Load(root, null, list[i], config, lookUp);
                    }
                }
            }
        }

        private static void SaveTypes(XmlWriter writer, List<Type> typesUsed)
        {
            writer.WriteStartElement("TypeDefinitions");
            for(int i = 0; i < typesUsed.Count; i++)
            {
                writer.WriteStartElement("Type");
                writer.WriteAttributeString("Name", typesUsed[i].AssemblyQualifiedName);
                writer.WriteAttributeString("TIndex", i.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void GetPossibleModulesChildren(IModelSystemStructure topModule, ConcurrentBag<Type> possibleTypes, IModelSystemStructure parent)
        {
            var modules = this.Configuration.ModelRepository.Modules;
            if(this.ParentFieldType == null) return;
            Parallel.For(0, modules.Count, delegate (int i)
            {
                Type t = modules[i];
                if(this.ParentFieldType.IsAssignableFrom(t)
                    && (parent == null || CheckForParent(parent.Type, t))
                    && (CheckForRootModule(topModule, this, t) != null))
                {
                    possibleTypes.Add(t);
                }
            });
        }

        private void GetPossibleModulesCollection(ConcurrentBag<Type> possibleTypes, Type parent, IModelSystemStructure topModule)
        {
            if(this.ParentFieldType == null) return;
            var arguements = this.ParentFieldType.IsArray ? this.ParentFieldType.GetElementType() : this.ParentFieldType.GetGenericArguments()[0];
            var modules = this.Configuration.ModelRepository.Modules;
            Parallel.For(0, modules.Count, delegate (int i)
            {
                Type t = modules[i];
                if(arguements.IsAssignableFrom(t)
                    && (CheckForParent(parent, t))
                    && (CheckForRootModule(topModule, this, t) != null))
                {
                    possibleTypes.Add(t);
                }
            });
        }

        private void Save(XmlWriter writer, IModelSystemStructure s, Dictionary<Type, int> lookup)
        {
            if(s.IsCollection)
            {
                SaveCollection(writer, s, lookup);
            }
            else
            {
                SaveModel(writer, s, lookup);
            }
        }

        private void SaveCollection(XmlWriter writer, IModelSystemStructure s, Dictionary<Type, int> lookup)
        {
            writer.WriteStartElement("Collection");
            if(s.ParentFieldType == null)
            {
                throw new XTMFRuntimeException("The type for " + s.Name + "'s Parent was not found!");
            }
            writer.WriteAttributeString("ParentTIndex", lookup[s.ParentFieldType].ToString());
            writer.WriteAttributeString("ParentFieldName", s.ParentFieldName);
            writer.WriteAttributeString("Name", s.Name);
            if(s.Children != null)
            {
                foreach(var model in s.Children)
                {
                    this.Save(writer, model, lookup);
                }
            }
            writer.WriteEndElement();
        }

        private void SaveModel(XmlWriter writer, IModelSystemStructure s, Dictionary<Type, int> lookup)
        {
            writer.WriteStartElement("Module");
            writer.WriteAttributeString("Name", s.Name);
            writer.WriteAttributeString("Description", s.Description);
            if(s.Type == null)
            {
                writer.WriteAttributeString("TIndex", "-1");
            }
            else
            {
                writer.WriteAttributeString("TIndex", lookup[s.Type].ToString());
            }
            if(s.ParentFieldType == null)
            {
                writer.WriteAttributeString("ParentTIndex", "-1");
            }
            else
            {
                writer.WriteAttributeString("ParentTIndex", lookup[s.ParentFieldType].ToString());
            }
            writer.WriteAttributeString("ParentFieldName", s.ParentFieldName);
            this.SaveParameters(writer, s, lookup);
            if(s.Children != null)
            {
                foreach(var c in s.Children)
                {
                    Save(writer, c, lookup);
                }
            }
            writer.WriteEndElement();
        }

        private void SaveParameters(XmlWriter writer, IModelSystemStructure element, Dictionary<Type, int> lookup)
        {
            // make sure we are loaded before trying to save
            writer.WriteStartElement("Parameters");
            if(element.Parameters != null)
            {
                foreach(var param in element.Parameters)
                {
                    writer.WriteStartElement("Param");
                    writer.WriteAttributeString("Name", param.Name);
                    writer.WriteAttributeString("TIndex", lookup[param.Type == null ? param.Value.GetType() : param.Type].ToString());
                    writer.WriteAttributeString("Value", param.Value.ToString());
                    writer.WriteAttributeString("QuickParameter", param.QuickParameter ? "true" : "false");
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }
    }
}