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

        private bool IsLoaded;

        protected void SetIsLoaded(bool value)
        {
            IsLoaded = value;
        }

        /// <summary>
        /// The configuration that this model system will use
        /// </summary>
        private IConfiguration Config;

        /// <summary>
        /// Create a new instance of a model system
        /// </summary>
        /// <param name="config">The configuration of the XTMFRuntime</param>
        /// <param name="structure">The structure to use for this model system</param>
        public ModelSystem(IConfiguration config, string name = null)
        {
            Config = config;
            Name = name;
            SetIsLoaded(false);
            LinkedParameters = new List<ILinkedParameter>();
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
        internal ModelSystemStructure CreateEditingClone(out List<ILinkedParameter> linkedParameters)
        {
            var ourClone = ModelSystemStructure.Clone();
            linkedParameters = LinkedParameters.Count > 0 ?
                LinkedParameter.MapLinkedParameters(LinkedParameters, ourClone, ModelSystemStructure)
                : new List<ILinkedParameter>();
            return ourClone as ModelSystemStructure;
        }

        /// <summary>
        ///
        /// </summary>
        public string Description
        {
            get;
            set;
        }

        /// <summary>
        /// The structure that defines this model system
        /// </summary>
        public IModelSystemStructure ModelSystemStructure
        {
            get
            {
                lock (this)
                {
                    if (!IsLoaded)
                    {
                        Load(Config, Name);
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
                    if(!IsLoaded)
                    {
                        Load(Config, Name);
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
        public string Name
        {
            get;
            set;
        }

        public bool Save(string fileName, ref string error)
        {
            string tempFileName = Path.GetTempFileName();
            try
            {
                using (XmlWriter writer = XmlWriter.Create(tempFileName, new XmlWriterSettings() { Indent = true, Encoding = Encoding.Unicode }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Root");
                    writer.Flush();
                    ModelSystemStructure.Save(writer);
                    if (Description != null)
                    {
                        writer.WriteStartElement("Description");
                        writer.WriteString(Description);
                        writer.WriteEndElement();
                    }
                    if (LinkedParameters != null)
                    {
                        foreach (var lp in LinkedParameters)
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
                                writer.WriteAttributeString("Name", LookupName(reference));
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndDocument();
                }
            }
            catch (Exception e)
            {
                Description = string.Empty;
                error = e.Message;
                return false;
            }
            File.Copy(tempFileName, fileName, true);
            File.Delete(tempFileName);
            return true;
        }

        public bool Save(ref string error)
        {
            string fileName = Path.Combine(Config.ModelSystemDirectory, Name + ".xml");
            return Save(fileName, ref error);
        }

        public override string ToString()
        {
            if (ModelSystemStructure != null && ModelSystemStructure.Type != null)
            {
                return string.Format("{0}:{1}", Name, ModelSystemStructure.Type.Name);
            }
            return Name;
        }

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

        private void Load(IConfiguration config, string name)
        {
            if (name != null)
            {
                var fileName = Path.Combine(Config.ModelSystemDirectory, name + ".xml");
                if (_LinkedParameters == null)
                {
                    _LinkedParameters = new List<ILinkedParameter>();
                }
                else
                {
                    _LinkedParameters.Clear();
                }
                try
                {
                    ModelSystemStructure = XTMF.ModelSystemStructure.Load(fileName, config);
                    using (XmlReader reader = XmlReader.Create(fileName))
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
                                        string error = null;
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
                                            else if (reader.NodeType == XmlNodeType.Element)
                                            {
                                                skipRead = true;
                                                break;
                                            }
                                        }
                                        if (textLast)
                                        {
                                            Description = reader.ReadContentAsString();
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                catch
                {
                    Description = string.Empty;
                    if (_ModelSystemStructure == null)
                    {
                        _ModelSystemStructure = new ModelSystemStructure(Config, Name, typeof(IModelSystemTemplate));
                        _ModelSystemStructure.Required = true;
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
            IsLoaded = false;
            _ModelSystemStructure = null;
            LinkedParameters = null;
        }

        private string LookupName(IModuleParameter reference)
        {
            return LookupName(reference, ModelSystemStructure);
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
            var fileName = Path.Combine(Config.ModelSystemDirectory, Name + ".xml");
            try
            {
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    bool skipRead = false;
                    while (!reader.EOF && (skipRead || reader.Read()))
                    {
                        skipRead = false;
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
                                        else if (reader.NodeType == XmlNodeType.Element)
                                        {
                                            skipRead = true;
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