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
using System.Text;
using System.Xml;
using XTMF.Interfaces;

namespace XTMF
{
    /// <summary>
    /// The XTMF version of the IModelSystem interface.
    /// All instances of IModelSystem created by XTMF will
    /// actually be of this type, however you should not
    /// assume this, nor any member's existence not defined
    /// by IModelSystem as this is subject to change
    /// </summary>
    public class ModelSystem : IModelSystem
    {
        protected IModelSystemStructure _ModelSystemStructure;

        private bool _IsLoaded;

        protected void SetIsLoaded(bool value) => _IsLoaded = value;

        /// <summary>
        /// The configuration that this model system will use
        /// </summary>
        private IConfiguration _Config;

        /// <summary>
        /// Create a new instance of a model system
        /// </summary>
        /// <param name="config">The configuration of the XTMFRuntime</param>
        /// <param name="name">The name of the model system</param>
        public ModelSystem(IConfiguration config, string name = null)
        {
            _Config = config;
            Name = name;
            SetIsLoaded(false);
            LinkedParameters = new List<ILinkedParameter>();
            RegionDisplays = new List<IRegionDisplay>();
            if (name != null)
            {
                ReadDescription();
            }
        }

        /// <summary>
        /// Create a clone of this model system
        /// </summary>
        /// <param name="linkedParameters">The linked parameters</param>
        /// <returns>A cloned model system that can be used for editing.</returns>
        internal ModelSystemStructure CreateEditingClone(out List<ILinkedParameter> linkedParameters, out List<IRegionDisplay> regionDisplays)
        {
            var ourClone = ModelSystemStructure.Clone();
            linkedParameters = LinkedParameters.Count > 0 ?
                LinkedParameter.MapLinkedParameters(LinkedParameters, ourClone, ModelSystemStructure)
                : new List<ILinkedParameter>();

            regionDisplays = RegionDisplay.MapRegionDisplays(this._regionDisplays, ourClone);
            return ourClone as ModelSystemStructure;
        }

        /// <summary>
        /// Create a clone of the model system
        /// </summary>
        /// <returns></returns>
        public ModelSystem Clone()
        {
            var structure = CreateEditingClone(out List<ILinkedParameter> linkedParameters, out List<IRegionDisplay> regionDisplays);
            return new ModelSystem(_Config, Name)
            {
                ModelSystemStructure = structure,
                LinkedParameters = linkedParameters,
                RegionDisplays = regionDisplays
                
            };
        }

        /// <summary>
        ///
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The structure that defines this model system
        /// </summary>
        public IModelSystemStructure ModelSystemStructure
        {
            get
            {
                lock (this)
                {
                    if (!_IsLoaded)
                    {
                        Load(_Config, Name);
                        SetIsLoaded(true);
                    }
                    return _ModelSystemStructure;
                }
            }
            set
            {
                lock (this)
                {
                    SetIsLoaded(true);
                    _ModelSystemStructure = value;
                }
            }
        }

        private List<ILinkedParameter> _LinkedParameters;
        /// <summary>
        ///
        /// </summary>
        public List<ILinkedParameter> LinkedParameters
        {
            get
            {
                lock (this)
                {
                    if (!_IsLoaded)
                    {
                        Load(_Config, Name);
                        SetIsLoaded(true);
                    }
                    return _LinkedParameters;
                }
            }
            internal set
            {

                _LinkedParameters = value;
            }
        }

        /// <summary>
        /// The name of the model system
        /// </summary>
        public string Name { get; set; }

        private List<IRegionDisplay> _regionDisplays;

        public List<IRegionDisplay> RegionDisplays
        {
            get
            {

                lock (this)
                {
                    if (!_IsLoaded)
                    {
                        Load(_Config, Name);
                        SetIsLoaded(true);
                    }
                    return _regionDisplays;
                }
            }
            internal set
            {

                _regionDisplays = value;
            }
        }

        public bool Save(Stream stream, ref string error)
        {
            return Save(stream, ModelSystemStructure, Description, LinkedParameters, ref error);
        }

        public bool Save(string fileName, ref string error)
        {
            return Save(fileName, ModelSystemStructure, Description, LinkedParameters, ref error);
        }

        /// <summary>
        /// Save a model system to file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="root"></param>
        /// <param name="description"></param>
        /// <param name="linkedParameters"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool Save(string fileName, IModelSystemStructure root, string description, List<ILinkedParameter> linkedParameters, ref string error)
        {
            string tempFileName = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFileName, FileMode.Create, FileAccess.Write))
                {
                    Save(stream, root, description, linkedParameters, ref error);
                }
            }
            catch (Exception e)
            {
                description = string.Empty;
                error = e.Message;
                return false;
            }
            File.Copy(tempFileName, fileName, true);
            File.Delete(tempFileName);
            return true;
        }

        public static bool Save(Stream stream, IModelSystemStructure root, string description,
            List<ILinkedParameter> linkedParameters, ref string error)
        {
            try
            {
                using (
                    XmlWriter writer = XmlWriter.Create(stream,
                        new XmlWriterSettings() { Indent = true, Encoding = Encoding.Unicode }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Root");
                    writer.Flush();
                    root.Save(writer);
                    if (description != null)
                    {
                        writer.WriteStartElement("Description");
                        writer.WriteString(description);
                        writer.WriteEndElement();
                    }
                    if (linkedParameters != null)
                    {
                        foreach (var lp in linkedParameters)
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
                                writer.WriteAttributeString("Name", LookupName(reference, root));
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndDocument();
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public bool Save(ref string error)
        {
            string fileName = Path.Combine(_Config.ModelSystemDirectory, Name + ".xml");
            return Save(fileName, ref error);
        }

        /// <summary>
        /// Save the quick parameters to the run directory.
        /// </summary>
        /// <param name="quickParamterPath">The path to the file to save the quick parameters to.</param>
        /// <param name="root">The root of the model system to save the quick parameters to.</param>
        internal static void SaveQuickParameters(string quickParamterPath, ModelSystemStructure root)
        {
            var parameters = new List<(string name, string value)>();
            void AddParameters(IModuleParameters moduleParameters)
            {
                if (moduleParameters is not null)
                {
                    foreach (var parameter in moduleParameters)
                    {
                        if (parameter.QuickParameter)
                        {
                            parameters.Add((parameter.Name, parameter.Value.ToString()));
                        }
                    }
                }

            }
            void Explore(IModelSystemStructure current)
            {
                AddParameters(current.Parameters);
                if (current.Children is not null)
                {
                    foreach (var child in current.Children)
                    {
                        Explore(child);
                    }
                }
            }
            Explore(root);
            using var stream = new FileStream(quickParamterPath, FileMode.Create, FileAccess.Write);
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, Encoding = Encoding.Unicode });
            writer.WriteStartDocument();
            writer.WriteStartElement("QuickParameters");
            foreach(var parameter in parameters)
            {
                writer.WriteStartElement("Parameter");
                writer.WriteAttributeString("Name", parameter.name);
                writer.WriteAttributeString("Value", parameter.value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        public override string ToString() => Name;

        /// <summary>
        /// Validate the correctness of this Model System
        /// </summary>
        /// <param name="error">A description of the error, if one is found</param>
        /// <returns>If an error was found</returns>
        public bool Validate(ref string error)
        {
            if (ModelSystemStructure == null)
            {
                error = "No model system structure is present in this model system!";
                return false;
            }
            else if (string.IsNullOrEmpty(Name))
            {
                error = "This model system does not have a name!";
                return false;
            }
            // Since we are going to be storing these things, we need to make sure that invalid characters are not
            // included in the model system names
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                if (Name.Contains(invalidChar))
                {
                    error = string.Format("The character {0} is not allowed in a Model System's name.", invalidChar);
                    return false;
                }
            }
            // Make sure that the structure itself is valid
            if (!ModelSystemStructure.Validate(ref error))
            {
                return false;
            }
            return true;
        }

        private IModuleParameter GetParameterFromLink(string variableLink)
        {
            // we need to search the space now
            return GetParameterFromLink(ParseLinkedParameterName(variableLink), 0, ModelSystemStructure);
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
                IList<IModelSystemStructure> descList = current.Children;
                if (descList == null)
                {
                    return null;
                }
                if (current.IsCollection)
                {
                    if (int.TryParse(variableLink[index], out int collectionIndex))
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

        /// <summary>
        /// Load a model system into memory with no
        /// references to the model system repository.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="config">The XTMF configuration to use</param>
        /// <param name="error">A description of the error if there is one.</param>
        /// <returns>The loaded model system, null if there was an error loading the model system.</returns>
        public static ModelSystem LoadDetachedModelSystem(Stream stream, IConfiguration config, ref string error)
        {
            var ms = new ModelSystem(config);
            ms.LoadFromStream(stream, config, ref error);
            return ms;
        }

        private void LoadFromStream(Stream stream, IConfiguration config, ref string error)
        {
            if (_LinkedParameters == null)
            {
                _LinkedParameters = new List<ILinkedParameter>();
            }
            else
            {
                _LinkedParameters.Clear();
            }
            ModelSystemStructure = XTMF.ModelSystemStructure.Load(stream, config);
            ModelSystemStructure.Required = true;
            // restart to get to the linked parameters
            stream.Seek(0, SeekOrigin.Begin);
            using (XmlReader reader = XmlReader.Create(stream))
            {
                bool skipRead = false;
                while (!reader.EOF && (skipRead || reader.Read()))
                {
                    skipRead = false;
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    switch (reader.LocalName)
                    {
                        case "LinkedParameter":
                            {
                                string linkedParameterName = "Unnamed";
                                string valueRepresentation = null;
                                var startingDepth = reader.Depth;
                                while (reader.MoveToNextAttribute())
                                {
                                    if (reader.NodeType == XmlNodeType.Attribute)
                                    {
                                        if (reader.LocalName == "Name")
                                        {
                                            linkedParameterName = reader.ReadContentAsString();
                                        }
                                        else if (reader.LocalName == "Value")
                                        {
                                            valueRepresentation = reader.ReadContentAsString();
                                        }
                                    }
                                }
                                LinkedParameter lp = new LinkedParameter(linkedParameterName);
                                lp.SetValue(valueRepresentation, ref error);
                                _LinkedParameters.Add(lp);
                                skipRead = true;
                                while (reader.Read())
                                {
                                    if (reader.Depth <= startingDepth && reader.NodeType != XmlNodeType.Element)
                                    {
                                        break;
                                    }
                                    if (reader.NodeType != XmlNodeType.Element)
                                    {
                                        continue;
                                    }
                                    if (reader.LocalName == "Reference")
                                    {
                                        string variableLink = null;
                                        while (reader.MoveToNextAttribute())
                                        {
                                            if (reader.Name == "Name")
                                            {
                                                variableLink = reader.ReadContentAsString();
                                            }
                                        }
                                        if (variableLink != null)
                                        {
                                            IModuleParameter param = GetParameterFromLink(variableLink);
                                            if (param != null)
                                            {
                                                // in any case if there is a type error, just throw it out
                                                lp.Add(param, ref error);
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case "Description":
                            {
                                bool textLast = false;

                                while (reader.Read())
                                {
                                    if (reader.NodeType == XmlNodeType.Text)
                                    {
                                        textLast = true;
                                        break;
                                    }
                                    if (reader.NodeType == XmlNodeType.Element)
                                    {
                                        skipRead = true;
                                        break;
                                    }
                                }
                                if (textLast)
                                {
                                    Description = reader.ReadContentAsString();
                                }
                                break;
                            }
                        case "Regions":
                            {
                                while(reader.Read())
                                {
                                    if(reader.NodeType == XmlNodeType.Element && reader.Name == "RegionDisplay")
                                    {
                                        Console.WriteLine("Here");
                                    }
                                }
                                break;
                            }
                         
                            
                    }
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        /// <param name="name"></param>
        private void Load(IConfiguration config, string name)
        {
            if (name != null)
            {
                string error = null;
                try
                {
                    var fileName = Path.Combine(_Config.ModelSystemDirectory, name + ".xml");
                    using (Stream stream = File.OpenRead(fileName))
                    {
                        LoadFromStream(stream, config, ref error);
                    }
                }
                catch
                {
                    Description = string.Empty;
                    if (_ModelSystemStructure == null)
                    {
                        _ModelSystemStructure = new ModelSystemStructure(_Config, Name, typeof(IModelSystemTemplate))
                        {
                            Required = true
                        };
                    }
                    else
                    {
                        _ModelSystemStructure.ParentFieldType = typeof(IModelSystemTemplate);
                        _ModelSystemStructure.Required = true;
                    }
                }
            }
        }

        internal void Unload()
        {
            lock (this)
            {
                _IsLoaded = false;
                _ModelSystemStructure = null;
                LinkedParameters = null;
            }
        }

        private static string LookupName(IModuleParameter reference, IModelSystemStructure current)
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
                    escape = false;
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

        private void ReadDescription()
        {
            var fileName = Path.Combine(_Config.ModelSystemDirectory, Name + ".xml");
            try
            {
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    while (!reader.EOF && reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        switch (reader.LocalName)
                        {
                            case "Description":
                                {
                                    bool textLast = false;
                                    while (reader.Read())
                                    {
                                        if (reader.NodeType == XmlNodeType.Text)
                                        {
                                            textLast = true;
                                            break;
                                        }
                                        if (reader.NodeType == XmlNodeType.Element)
                                        {
                                            break;
                                        }
                                    }
                                    if (textLast)
                                    {
                                        Description = reader.ReadContentAsString();
                                    }
                                }
                                // we can just exit at this point since using will clean up for us
                                return;
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}