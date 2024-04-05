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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Exceptions;
using XTMF;
using XTMF.Editing;

namespace XTMF
{
    public class ModelSystemStructureModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ModelSystemEditingSession _Session;

        public ModelSystemStructure RealModelSystemStructure { get; set; }

        private ModelSystemStructureModel _parent;

        public ModelSystemStructureModel(ModelSystemEditingSession session,
            ModelSystemStructure realModelSystemStructure)
        {
            _Session = session;
            RealModelSystemStructure = realModelSystemStructure;
            Parameters = new ParametersModel(this, session);
            Children = CreateChildren(_Session, RealModelSystemStructure);
        }

        public ModelSystemStructureModel(ModelSystemEditingSession session,
            ModelSystemStructure realModelSystemStructure, ModelSystemStructureModel parent)
        {
            _Session = session;
            RealModelSystemStructure = realModelSystemStructure;
            Parameters = new ParametersModel(this, session);
            Children = CreateChildren(_Session, RealModelSystemStructure);
            _parent = parent;
        }

        public ModelSystemStructureModel Parent => _parent;


        public string Name => RealModelSystemStructure.Name;

        public string Description => RealModelSystemStructure.Description;

        public string ParentFieldName => RealModelSystemStructure.ParentFieldName;

        public bool IsDisabled => RealModelSystemStructure.IsDisabled;

        public bool CanDisable => !RealModelSystemStructure.Required;

        /// <summary>
        /// Toggles disable status on this module.
        /// </summary>
        /// <param name="disabled"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool SetDisabled(bool disabled, ref string error)
        {
            var oldValue = RealModelSystemStructure.IsDisabled;
            if (oldValue != disabled)
            {
                return _Session.RunCommand(XTMFCommand.CreateCommand(
                    disabled ? "Disable Module" : "Enable Module",
                    (ref string e) =>
                    {
                        if (RealModelSystemStructure.Required && disabled)
                        {
                            e = "You can not disable a module that is required!";
                            return false;
                        }

                        RealModelSystemStructure.IsDisabled = disabled;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDisabled));
                        return true;
                    }, (ref string e) =>
                    {
                        if (RealModelSystemStructure.Required && oldValue)
                        {
                            e = "You can not disable a module that is required!";
                            return false;
                        }

                        RealModelSystemStructure.IsDisabled = oldValue;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDisabled));
                        return true;
                    }, (ref string e) =>
                    {
                        if (RealModelSystemStructure.Required && disabled)
                        {
                            e = "You can not disable a module that is required!";
                            return false;
                        }

                        RealModelSystemStructure.IsDisabled = disabled;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDisabled));
                        return true;
                    }
                ), ref error);
            }

            return true;
        }

        public bool IsMetaModule => RealModelSystemStructure.IsMetaModule;

        public Type Type
        {
            get => RealModelSystemStructure.Type;
            set
            {
                var oldType = RealModelSystemStructure.Type;

                if (oldType != value)
                {
                    var oldChildren = Children?.ToList();
                    var oldParameters = Parameters;
                    var oldDirty = IsDirty;

                    bool apply(ref string e)
                    {
                        // right now we are using a clone
                        Dirty = true;
                        RealModelSystemStructure.Type = value;
                        UpdateChildren();
                        Parameters = new ParametersModel(this, _Session);
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Type));
                        if (oldDirty == false)
                        {
                            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDirty));
                        }

                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Parameters));
                        return true;
                    }

                    string error = null;
                    // run the command to change the type so we can undo it later
                    _Session.RunCommand(XTMFCommand.CreateCommand(
                        "Set Module Type",
                        apply,
                        (ref string e) =>
                        {
                            // undo
                            RealModelSystemStructure.Type = oldType;
                            if (Children != null)
                            {
                                Children.Clear();
                                // move the old children back into place
                                for (var i = 0; i < oldChildren.Count; i++)
                                {
                                    RealModelSystemStructure.Children[i] = oldChildren[i].RealModelSystemStructure;
                                    Children.Add(oldChildren[i]);
                                }
                            }

                            //UpdateChildren();
                            Parameters = oldParameters;
                            SetRealParametersToModel();
                            Dirty = oldDirty;
                            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Type));
                            if (oldDirty ^ IsDirty)
                            {
                                ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDirty));
                            }

                            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Parameters));
                            return true;
                        }, apply), ref error);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetRealParametersToModel()
        {
            var parameters = RealModelSystemStructure.Parameters;
            if (parameters != null)
            {
                foreach (var realParmameter in parameters.Parameters)
                {
                    var name = realParmameter.Name;
                    realParmameter.Value = (from p in Parameters.Parameters
                                            where p.Name == name
                                            select p.Value).First();
                }
            }
        }

        public bool IsCollection => RealModelSystemStructure.IsCollection;

        /// <summary>
        /// Add a new collection member to a collection using the given type
        /// </summary>
        /// <param name="type">The type to add</param>
        /// <param name="name">The name to use, pass a null to automatically name the module</param>
        public bool AddCollectionMember(Type type, ref string error, string name = null)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!IsCollection)
            {
                throw new InvalidOperationException(
                    "You can not add collection members to a module that is not a collection!");
            }

            var data = new CollectionChangeData();
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                    "Add Collection Member",
                    (ref string e) =>
                    {
                        if (!ValidateType(type, ref e))
                        {
                            return false;
                        }

                        if (RealModelSystemStructure.IsCollection)
                        {
                            RealModelSystemStructure.Add(
                                RealModelSystemStructure.CreateCollectionMember(name ?? CreateNameFromType(type),
                                    type));
                        }
                        else
                        {
                            RealModelSystemStructure.Add(name ?? CreateNameFromType(type), type);
                        }

                        data.Index = RealModelSystemStructure.Children.Count - 1;
                        data.StructureInQuestion =
                            RealModelSystemStructure.Children[data.Index] as ModelSystemStructure;
                        if (Children == null)
                        {
                            Children = new ObservableCollection<ModelSystemStructureModel>();
                        }

                        Children.Add(data.ModelInQuestion =
                            new ModelSystemStructureModel(_Session, data.StructureInQuestion, this));
                        return true;
                    },
                    (ref string e) =>
                    {
                        Children.RemoveAt(data.Index);
                        RealModelSystemStructure.Children.RemoveAt(data.Index);
                        return true;
                    },
                    (ref string e) =>
                    {
                        Children.Insert(data.Index, data.ModelInQuestion);
                        RealModelSystemStructure.Children.Insert(data.Index, data.StructureInQuestion);
                        return true;
                    }),
                ref error);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="buffer"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool Paste(ModelSystemEditingSession session, string buffer, ref string error)
        {
            // Get the data
            using (var backing = new MemoryStream())
            {
                var writer = new StreamWriter(backing);
                writer.Write(buffer);
                writer.Flush();
                backing.Position = 0;
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(backing);
                    var node = doc["MultipleModules"];
                    if (node != null)
                    {
                        var ret = true;
                        string retError = null;
                        session.ExecuteCombinedCommands(
                            "Pasting Modules",
                            () =>
                            {
                                foreach (XmlNode subNode in node)
                                {
                                    if (subNode.Name == "CopiedModule")
                                    {
                                        string e = null;
                                        if (!Paste(ref e,
                                            GetModelSystemStructureFromXML(subNode["CopiedModules"]),
                                            GetLinkedParametersFromXML(subNode["LinkedParameters"])))
                                        {
                                            retError = e;
                                            ret = false;
                                        }
                                    }
                                }
                            });
                        error = retError;
                        return ret;
                    }
                    else
                    {
                        return Paste(ref error,
                            GetModelSystemStructureFromXML(doc["CopiedModule"]["CopiedModules"]),
                            GetLinkedParametersFromXML(doc["CopiedModule"]["LinkedParameters"]));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    error = "Unable to decode the copy buffer.\r\n" + e.Message;
                    return false;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <param name="copiedStructure"></param>
        /// <param name="linkedParameters"></param>
        /// <returns></returns>
        private bool Paste(ref string error, ModelSystemStructure copiedStructure,
            List<TempLinkedParameter> linkedParameters)
        {
            if (copiedStructure.IsCollection)
            {
                if (!IsCollection)
                {
                    error = "The copied model system is not pasteable at this location.";
                    return false;
                }

                foreach (var child in copiedStructure.Children)
                {
                    if (!IsAssignable(_Session.ModelSystemModel.Root.RealModelSystemStructure,
                        IsCollection ? RealModelSystemStructure : _Session.GetParent(this).RealModelSystemStructure,
                        child as ModelSystemStructure))
                    {
                        error = "The copied model system is not pasteable at this location.";
                        return false;
                    }
                }
            }
            else
            {
                // validate the modules contained
                if (!IsAssignable(_Session.ModelSystemModel.Root.RealModelSystemStructure,
                    IsCollection ? RealModelSystemStructure : _Session.GetParent(this).RealModelSystemStructure,
                    copiedStructure))
                {
                    error = "The copied model system is not pasteable at this location.";
                    return false;
                }

                // if we are not a collection update the name of the module that is going to replace us with our name and description
                if (!IsCollection)
                {
                    copiedStructure.Name = Name;
                    copiedStructure.Description = Description;
                }
            }

            var newLinkedParameters = new List<LinkedParameterModel>();
            var additions = new List<Tuple<ParameterModel, LinkedParameterModel>>();
            var oldReal = RealModelSystemStructure;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Paste Module",
                (ref string e) =>
                {
                    ModelSystemStructureModel beingAdded;
                    var indexOffset = 0;
                    if (IsCollection)
                    {
                        if (copiedStructure.IsCollection)
                        {
                            indexOffset = RealModelSystemStructure.Children != null
                                ? RealModelSystemStructure.Children.Count
                                : 0;
                            foreach (var child in copiedStructure.Children)
                            {
                                child.Required = false;
                                RealModelSystemStructure.Add(child);
                            }

                            UpdateChildren();
                            beingAdded = this;
                        }
                        else
                        {
                            copiedStructure.Required = false;
                            RealModelSystemStructure.Add(copiedStructure);
                            UpdateChildren();
                            beingAdded = Children[Children.Count - 1];
                        }
                    }
                    else
                    {
                        var modelSystemRoot = _Session.ModelSystemModel.Root.RealModelSystemStructure;
                        // if we are the root of the model system
                        if (modelSystemRoot == RealModelSystemStructure)
                        {
                            copiedStructure.Required = RealModelSystemStructure.Required;
                            copiedStructure.ParentFieldType = RealModelSystemStructure.ParentFieldType;
                            copiedStructure.ParentFieldName = RealModelSystemStructure.ParentFieldName;
                            _Session.ModelSystemModel.Root.RealModelSystemStructure = copiedStructure;
                        }
                        else
                        {
                            var parent = ModelSystemStructure.GetParent(modelSystemRoot, RealModelSystemStructure);
                            var index = parent.Children.IndexOf(RealModelSystemStructure);
                            copiedStructure.Required = RealModelSystemStructure.Required;
                            copiedStructure.ParentFieldType = RealModelSystemStructure.ParentFieldType;
                            copiedStructure.ParentFieldName = RealModelSystemStructure.ParentFieldName;
                            RealModelSystemStructure = copiedStructure;
                            parent.Children[index] = copiedStructure;
                        }

                        UpdateAll();
                        beingAdded = this;
                    }

                    var linkedParameterModel = _Session.ModelSystemModel.LinkedParameters;
                    var realLinkedParameters = linkedParameterModel.GetLinkedParameters();
                    var missing = from lp in linkedParameters
                                  where !realLinkedParameters.Any(rlp => rlp.Name == lp.Name)
                                  select lp;
                    var matching = linkedParameters.Join(realLinkedParameters, (p) => p.Name, (p) => p.Name,
                        (t, r) => new { Real = r, Temp = t });
                    // add links for the ones we've matched
                    foreach (var lp in matching)
                    {
                        foreach (var containedParameters in GetParametersFromTemp(lp.Temp, beingAdded, indexOffset))
                        {
                            if (containedParameters == null)
                            {
                                continue;
                            }

                            if (!lp.Real.AddParameterWithoutCommand(containedParameters, ref e))
                            {
                                return false;
                            }

                            containedParameters.SignalIsLinkedChanged();
                            additions.Add(
                                new Tuple<ParameterModel, LinkedParameterModel>(containedParameters, lp.Real));
                        }
                    }

                    // add links for the ones that didn't match
                    foreach (var missingLp in missing)
                    {
                        var newLP = linkedParameterModel.AddWithoutCommand(missingLp.Name, missingLp.Value);
                        newLinkedParameters.Add(newLP);
                        foreach (var containedParameters in GetParametersFromTemp(missingLp, beingAdded, indexOffset))
                        {
                            if (!newLP.AddParameterWithoutCommand(containedParameters, ref e))
                            {
                                return false;
                            }

                            containedParameters.SignalIsLinkedChanged();
                        }
                    }

                    return true;
                },
                (ref string e) =>
                {
                    if (IsCollection)
                    {
                        if (copiedStructure.IsCollection)
                        {
                            foreach (var child in copiedStructure.Children)
                            {
                                RealModelSystemStructure.Children.Remove(child);
                            }
                        }
                        else
                        {
                            RealModelSystemStructure.Children.Remove(copiedStructure);
                        }

                        UpdateChildren();
                    }
                    else
                    {
                        var modelSystemRoot = _Session.ModelSystemModel.Root.RealModelSystemStructure;
                        // if we are the root of the model system
                        if (modelSystemRoot == RealModelSystemStructure)
                        {
                            RealModelSystemStructure = oldReal;
                            _Session.ModelSystemModel.Root.RealModelSystemStructure = oldReal;
                        }
                        else
                        {
                            var parent = ModelSystemStructure.GetParent(
                                _Session.ModelSystemModel.Root.RealModelSystemStructure, RealModelSystemStructure);
                            var index = parent.Children.IndexOf(RealModelSystemStructure);
                            RealModelSystemStructure = oldReal;
                            parent.Children[index] = RealModelSystemStructure;
                        }

                        UpdateAll();
                    }

                    var linkedParameterModel = _Session.ModelSystemModel.LinkedParameters;
                    foreach (var newLP in newLinkedParameters)
                    {
                        linkedParameterModel.RemoveWithoutCommand(newLP);
                    }

                    foreach (var addition in additions)
                    {
                        addition.Item2.RemoveParameterWithoutCommand(addition.Item1);
                    }

                    return true;
                },
                (ref string e) =>
                {
                    if (IsCollection)
                    {
                        if (copiedStructure.IsCollection)
                        {
                            foreach (var child in copiedStructure.Children)
                            {
                                RealModelSystemStructure.Add(child);
                            }
                        }
                        else
                        {
                            RealModelSystemStructure.Add(copiedStructure);
                        }

                        UpdateChildren();
                    }
                    else
                    {
                        var modelSystemRoot = _Session.ModelSystemModel.Root.RealModelSystemStructure;
                        // if we are the root of the model system
                        if (modelSystemRoot == RealModelSystemStructure)
                        {
                            RealModelSystemStructure = copiedStructure;
                            _Session.ModelSystemModel.Root.RealModelSystemStructure = RealModelSystemStructure;
                        }
                        else
                        {
                            var parent = ModelSystemStructure.GetParent(
                                _Session.ModelSystemModel.Root.RealModelSystemStructure, RealModelSystemStructure);
                            var index = parent.Children.IndexOf(RealModelSystemStructure);
                            RealModelSystemStructure = copiedStructure;
                            parent.Children[index] = RealModelSystemStructure;
                        }

                        UpdateAll();
                    }

                    var linkedParameterModel = _Session.ModelSystemModel.LinkedParameters;
                    foreach (var newLP in newLinkedParameters)
                    {
                        linkedParameterModel.AddWithoutCommand(newLP);
                    }

                    foreach (var addition in additions)
                    {
                        if (!addition.Item2.AddParameterWithoutCommand(addition.Item1, ref e))
                        {
                            return false;
                        }
                    }

                    return true;
                }), ref error);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="temp"></param>
        /// <param name="root"></param>
        /// <param name="indexOffset"></param>
        /// <returns></returns>
        private List<ParameterModel> GetParametersFromTemp(TempLinkedParameter temp, ModelSystemStructureModel root,
            int indexOffset)
        {
            return (from path in temp.Paths
                    select GetParametersFromTemp(path, root, indexOffset)).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="root"></param>
        /// <param name="indexOffset"></param>
        /// <returns></returns>

        private ParameterModel GetParametersFromTemp(string path, ModelSystemStructureModel root, int indexOffset)
        {
            return GetParameterFromLink(ParseLinkedParameterName(path), 0, root, indexOffset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="variableLink"></param>
        /// <param name="index"></param>
        /// <param name="current"></param>
        /// <param name="indexOffset"></param>
        /// <returns></returns>
        private ParameterModel GetParameterFromLink(string[] variableLink, int index, ModelSystemStructureModel current,
            int indexOffset)
        {
            if (current.Type == null && !current.IsCollection)
            {
                //throw an exception - model system references a module with a missing assembly
                throw new MissingModuleTypeException(current);
            }

            if (index == variableLink.Length - 1)
            {
                // search the parameters
                var parameters = current.Parameters;

                foreach (var p in parameters.Parameters)
                {
                    if (p.Name == variableLink[index])
                    {
                        return p;
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
                        // if we are at the first index we need to look at the index offset.
                        // This is needed if we are copying a collection, thus the indexes will
                        // not be starting at the 0th position in the new model system structure.
                        if (index == 0)
                        {
                            collectionIndex += indexOffset;
                        }

                        if (collectionIndex >= 0 && collectionIndex < descList.Count)
                        {
                            return GetParameterFromLink(variableLink, index + 1, descList[collectionIndex],
                                indexOffset);
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
                            return GetParameterFromLink(variableLink, index + 1, sub, 0);
                        }
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

        private void UpdateAll()
        {
            UpdateChildren();
            if (!IsCollection)
            {
                Parameters.Update();
            }

            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Type));
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Name));
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Description));
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsMetaModule));
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDisabled));
        }

        private bool IsAssignable(ModelSystemStructure rootStructure, ModelSystemStructure parentStructure,
            ModelSystemStructure copyBuffer)
        {
            // This will update what module we are using for the root as per the Re-root-able extension for XTMF
            try
            {
                var parent = parentStructure == null ? typeof(IModelSystemTemplate) : parentStructure.Type;
                if (copyBuffer.IsCollection)
                {
                    // Make sure that we are doing collection to collection and that they are of the right types
                    if (!IsCollection ||
                        !RealModelSystemStructure.ParentFieldType.IsAssignableFrom(copyBuffer.ParentFieldType))
                    {
                        return false;
                    }

                    // now make sure that every new element is alright with the parent and root
                    var parentType = RealModelSystemStructure.ParentFieldType;
                    var arguements = parentType.IsArray
                        ? parentType.GetElementType()
                        : parentType.GetGenericArguments()[0];
                    foreach (var member in copyBuffer.Children)
                    {
                        var t = member.Type;
                        if (arguements.IsAssignableFrom(t) &&
                            (parent == null || ModelSystemStructure.CheckForParent(parent, t)) &&
                            ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) != null)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    var t = copyBuffer.Type;
                    rootStructure =
                        ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) as
                            ModelSystemStructure;
                    if (IsCollection)
                    {
                        var parentType = _Session.GetParent(this).Type;
                        var arguements = ParentFieldType.IsArray
                            ? ParentFieldType.GetElementType()
                            : ParentFieldType.GetGenericArguments()[0];
                        if (arguements.IsAssignableFrom(t) && ModelSystemStructure.CheckForParent(parentType, t) &&
                            ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) != null)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (RealModelSystemStructure.ParentFieldType.IsAssignableFrom(t) &&
                            (parent == null || ModelSystemStructure.CheckForParent(parent, t))
                            && ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) !=
                            null)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private class TempLinkedParameter
        {
            internal string Name;
            internal string Value;
            internal List<string> Paths;

            public TempLinkedParameter()
            {
                Paths = new List<string>();
            }
        }

        private List<TempLinkedParameter> GetLinkedParametersFromXML(XmlNode linkedParameterNode)
        {
            var ret = new List<TempLinkedParameter>();
            if (linkedParameterNode != null)
            {
                foreach (XmlNode child in linkedParameterNode.ChildNodes)
                {
                    if (child.Name == "LinkedParameter")
                    {
                        var nextLp = new TempLinkedParameter()
                        {
                            Name = child.Attributes["Name"].InnerText,
                            Value = child.Attributes["Value"].InnerText
                        };
                        foreach (XmlNode link in child.ChildNodes)
                        {
                            nextLp.Paths.Add(link.Attributes["Path"].InnerText);
                        }

                        ret.Add(nextLp);
                    }
                }
            }

            return ret;
        }

        private ModelSystemStructure GetModelSystemStructureFromXML(XmlNode rootMSChild)
        {
            return ModelSystemStructure.Load(rootMSChild, _Session.Configuration);
        }

        /// <summary>
        /// Return a representation of this module
        /// </summary>
        public string CopyModule()
        {
            MemoryStream backing = null;
            try
            {
                backing = new MemoryStream();
                using (var writer = new XmlTextWriter(backing, Encoding.Unicode))
                {
                    writer.Formatting = Formatting.Indented;
                    CopyModule(writer);
                    writer.Flush();
                    backing.Position = 0;
                    using (var reader = new StreamReader(backing))
                    {
                        backing = null;
                        return reader.ReadToEnd();
                    }
                }
            }
            finally
            {
                backing?.Dispose();
            }
        }

        /// <summary>
        /// Return a representation of all of the given modules
        /// </summary>
        /// <param name="modules"></param>
        /// <returns></returns>
        public static string CopyModule(List<ModelSystemStructureModel> modules)
        {
            using (var backing = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(backing, Encoding.Unicode))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.WriteStartElement("MultipleModules");
                    foreach (var module in modules)
                    {
                        module.CopyModule(writer);
                    }

                    writer.WriteEndElement();
                    writer.Flush();
                    backing.Position = 0;
                    using (var reader = new StreamReader(backing))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private void CopyModule(XmlTextWriter writer)
        {
            var children = GetAllChildren();
            writer.WriteStartElement("CopiedModule");
            writer.WriteStartElement("CopiedModules");
            RealModelSystemStructure.Save(writer);
            writer.WriteEndElement();
            writer.WriteStartElement("LinkedParameters");
            foreach (var linkedParameter in GetLinkedParameters(children))
            {
                writer.WriteStartElement("LinkedParameter");
                writer.WriteAttributeString("Name", linkedParameter.Name);
                writer.WriteAttributeString("Value", linkedParameter.GetValue());
                foreach (var link in linkedParameter.GetParameters())
                {
                    var match = children.FirstOrDefault(m =>
                        m.RealModelSystemStructure == link.RealParameter.BelongsTo);
                    if (match != null)
                    {
                        var lname = LookupName(link, this);
                        if (lname != null)
                        {
                            writer.WriteStartElement("Parameter");
                            writer.WriteAttributeString("Path", lname);
                            writer.WriteEndElement();
                        }
                    }
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private string LookupName(ParameterModel reference, ModelSystemStructureModel current)
        {
            var param = current.Parameters;
            if (param != null && param.Parameters != null)
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
                        // make sure to use an escape character before the . to avoid making the mistake of reading it as another index
                        return string.Concat(current.IsCollection
                            ? i.ToString()
                            : childrenList[i].ParentFieldName.Replace(".", "\\."), '.', res);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get all of the referenced linked parameters
        /// </summary>
        /// <returns>A list of the linked parameters referenced by any of the modules in this subtree</returns>
        private List<LinkedParameterModel> GetLinkedParameters(List<ModelSystemStructureModel> children)
        {
            return (from lp in _Session.ModelSystemModel.LinkedParameters.LinkedParameters
                    where children.Any(child => lp.HasContainedModule(child))
                    select lp).ToList();
        }


        private List<ModelSystemStructureModel> GetAllChildren()
        {
            var ret = new List<ModelSystemStructureModel>();
            GetAllChildren(ret, this);
            return ret;
        }

        private void GetAllChildren(List<ModelSystemStructureModel> list, ModelSystemStructureModel root)
        {
            list.Add(root);
            var children = root.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    GetAllChildren(list, child);
                }
            }
        }

        /// <summary>
        /// Removes a collection member at the given index
        /// </summary>
        /// <param name="index">The index to remove from.</param>
        /// <param name="error">If an error happens it will contain a text representation of the error.</param>
        /// <returns>If the index was able to be removed</returns>
        /// <exception cref="InvalidOperationException">This operation is only allowed on Collections.</exception>
        public bool RemoveCollectionMember(int index, ref string error)
        {
            if (index < 0)
            {
                throw new InvalidOperationException("Indexes must be greater than or equal to zero!");
            }

            if (!IsCollection)
            {
                throw new InvalidOperationException(
                    "You can not add collection members to a module that is not a collection!");
            }

            var data = new CollectionChangeData();
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                    "Remove Collection Member",
                    (ref string e) =>
                    {
                        var children = RealModelSystemStructure.Children;
                        if (children.Count <= index)
                        {
                            e = "There is no collection member at index " + index + "!";
                            return false;
                        }

                        data.Index = index;
                        data.StructureInQuestion =
                            RealModelSystemStructure.Children[data.Index] as ModelSystemStructure;
                        data.ModelInQuestion = Children[data.Index];
                        RealModelSystemStructure.Children.RemoveAt(data.Index);
                        Children.RemoveAt(data.Index);
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
                        return true;
                    },
                    (ref string e) =>
                    {
                        RealModelSystemStructure.Children.Insert(data.Index, data.StructureInQuestion);
                        Children.Insert(data.Index, data.ModelInQuestion);
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
                        return true;
                    },
                    (ref string e) =>
                    {
                        RealModelSystemStructure.Children.RemoveAt(data.Index);
                        Children.RemoveAt(data.Index);
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
                        return true;
                    }),
                ref error);
        }

        public bool RemoveAllCollectionMembers(ref string error)
        {
            if (!IsCollection)
            {
                throw new InvalidOperationException(
                    "You can not add collection members to a module that is not a collection!");
            }

            IList<ModelSystemStructureModel> oldChildren = null;
            IList<IModelSystemStructure> oldRealChildren = null;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                    "Remove All Collection Members",
                    (ref string e) =>
                    {
                        if (RealModelSystemStructure.Children == null || RealModelSystemStructure.Children.Count <= 0)
                        {
                            e = "There were no modules to delete in the collection!";
                            return false;
                        }

                        oldRealChildren = RealModelSystemStructure.Children.ToList();
                        RealModelSystemStructure.Children.Clear();
                        oldChildren = Children.ToList();
                        Children.Clear();
                        return true;
                    },
                    (ref string e) =>
                    {
                        foreach (var child in oldChildren)
                        {
                            Children.Add(child);
                        }

                        var realChildList = RealModelSystemStructure.Children;
                        if (oldRealChildren != null)
                        {
                            foreach (var child in oldRealChildren)
                            {
                                realChildList.Add(child);
                            }
                        }

                        return true;
                    },
                    (ref string e) =>
                    {
                        RealModelSystemStructure.Children.Clear();
                        Children.Clear();
                        return true;
                    }),
                ref error);
        }

        private class CollectionChangeData
        {
            internal ModelSystemStructureModel ModelInQuestion;
            internal ModelSystemStructure StructureInQuestion;
            internal int Index;
            internal int NextIndex = -1;
        }

        /// <summary>
        /// Validate that a type is allowed to be added to the given collection
        /// </summary>
        /// <param name="type">The type to check for</param>
        /// <param name="error">A detailed message of the error if there is one</param>
        /// <returns>True if the type is allowed</returns>
        private bool ValidateType(Type type, ref string error)
        {
            var topLevelModule = _Session.ModelSystemModel.Root.RealModelSystemStructure;
            return RealModelSystemStructure.CheckPossibleModule(type, topLevelModule, ref error);
        }

        /// <summary>
        /// Generate a name given the type
        /// </summary>
        /// <param name="type">The type to generate a name for</param>
        /// <returns>A string to use for a name</returns>
        private static string CreateNameFromType(Type type)
        {
            var LastCapital = true;
            var name = new StringBuilder(type.Name);
            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) & !LastCapital)
                {
                    name.Insert(i, ' ');
                    LastCapital = true;
                }
                else
                {
                    LastCapital = false;
                }
            }

            return name.ToString();
        }

        public ObservableCollection<ModelSystemStructureModel> Children { get; private set; }

        private ObservableCollection<ModelSystemStructureModel> CreateChildren(ModelSystemEditingSession session,
            ModelSystemStructure realModelSystemStructure)
        {
            if (realModelSystemStructure.Children == null)
            {
                if (Children != null)
                {
                    Children.Clear();
                    return Children;
                }

                var children = new ObservableCollection<ModelSystemStructureModel>();

                return children;
            }

            ObservableCollection<ModelSystemStructureModel> ret;
            if (Children == null)
            {
                ret = new ObservableCollection<ModelSystemStructureModel>();
                for (var i = 0; i < realModelSystemStructure.Children.Count; i++)
                {
                    ret.Add(new ModelSystemStructureModel(session,
                        realModelSystemStructure.Children[i] as ModelSystemStructure, this));
                }
            }
            else
            {
                ret = Children;
                if (realModelSystemStructure.Children == null)
                {
                    ret.Clear();
                }
                else
                {
                    var previousChildren = (from child in Children
                                            where !realModelSystemStructure.Children.Any(r => r == child.RealModelSystemStructure)
                                            select child).ToList();
                    foreach (var child in previousChildren)
                    {
                        ret.Remove(child);
                    }

                    foreach (var child in from child in realModelSystemStructure.Children
                                          where !Children.Any(c => c.RealModelSystemStructure == child)
                                          select child)
                    {
                        ret.Add(new ModelSystemStructureModel(session, child as ModelSystemStructure, this));
                    }

                    var repeat = false;
                    do
                    {
                        // now search for children that have moved indexes after adds and deleted have been performed
                        var indexes = (from child in Children
                                       select realModelSystemStructure.Children.IndexOf(child.RealModelSystemStructure)).ToArray();
                        for (var i = 0; i < indexes.Length; i++)
                        {
                            // if a child has moved
                            if (indexes[i] != i)
                            {
                                Children.Move(i, indexes[i]);
                                repeat = true;
                                break;
                            }
                        }
                    } while (repeat);
                }
            }

            return ret;
        }



        private sealed class MoveChildData
        {
            internal int OriginalPosition;
            internal int NewPosition;
        }

        private void Move(int start, int dest)
        {
            var real = RealModelSystemStructure.Children[start];
            RealModelSystemStructure.Children.RemoveAt(start);
            RealModelSystemStructure.Children.Insert(dest, real);
            Children.Move(start, dest);
        }

        public bool MoveModeInParent(int deltaPosition, ref string error)
        {
            var parent = _Session.GetParent(this);
            if (!parent.IsCollection)
            {
                error = "You can only move the children of a collection!";
                return false;
            }

            var ourIndex = parent.Children.IndexOf(this);
            return parent.MoveChild(ourIndex, ourIndex + deltaPosition, ref error);
        }

        public bool MoveChild(int originalPosition, int newPosition, ref string error)
        {
            if (!IsCollection)
            {
                error = "You can only move the children of a collection!";
                return false;
            }

            var move = new MoveChildData();
            return _Session.RunCommand(
                XTMFCommand.CreateCommand(
                    "Move Collection Member",
                    // do
                    (ref string e) =>
                    {
                        move.OriginalPosition = originalPosition;
                        move.NewPosition = newPosition;
                        if ((originalPosition < 0) | (originalPosition >= Children.Count))
                        {
                            e = "The original position was invalid!";
                            return false;
                        }

                        if ((newPosition < 0) | (newPosition >= Children.Count))
                        {
                            e = "The destination position was invalid!";
                            return false;
                        }

                        Move(originalPosition, newPosition);
                        return true;
                    },
                    // undo
                    (ref string e) =>
                    {
                        Move(newPosition, originalPosition);
                        return true;
                    },
                    // redo
                    (ref string e) =>
                    {
                        Move(originalPosition, newPosition);
                        return true;
                    }
                ), ref error);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="error"></param>
        /// <returns></returns>

        public bool SetName(string newName, ref string error)
        {
            var oldName = "";
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Set Module Name",
                (ref string e) =>
                {
                    oldName = RealModelSystemStructure.Name;
                    RealModelSystemStructure.Name = newName;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Name));
                    return true;
                }, (ref string e) =>
                {
                    RealModelSystemStructure.Name = oldName;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Name));
                    return true;
                },
                (ref string e) =>
                {
                    RealModelSystemStructure.Name = newName;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Name));
                    return true;
                }), ref error);
        }

        public bool SetMetaModule(bool isMetaModule, ref string error)
        {
            var oldMeta = false;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                isMetaModule ? "Compose Meta-Module" : "Decompose Meta-Module",
                (ref string e) =>
                {
                    if (isMetaModule && RealModelSystemStructure.IsCollection)
                    {
                        e = "You can not create a meta-module from a collection!";
                        return false;
                    }

                    oldMeta = RealModelSystemStructure.IsMetaModule;
                    RealModelSystemStructure.IsMetaModule = isMetaModule;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsMetaModule));
                    return true;
                }, (ref string e) =>
                {
                    RealModelSystemStructure.IsMetaModule = oldMeta;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsMetaModule));
                    return true;
                },
                (ref string e) =>
                {
                    RealModelSystemStructure.IsMetaModule = isMetaModule;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsMetaModule));
                    return true;
                }), ref error);
        }

        public bool SetDescription(string newDescription, ref string error)
        {
            var oldDescription = "";
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Set Module Description",
                (ref string e) =>
                {
                    oldDescription = RealModelSystemStructure.Description;
                    RealModelSystemStructure.Description = newDescription;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Description));
                    return true;
                }, (ref string e) =>
                {
                    RealModelSystemStructure.Description = oldDescription;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Description));
                    return true;
                },
                (ref string e) =>
                {
                    RealModelSystemStructure.Description = newDescription;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Description));
                    return true;
                }), ref error);
        }

        private void UpdateChildren()
        {
            Children = CreateChildren(_Session, RealModelSystemStructure);
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
        }

        private bool Dirty = false;

        public Type ParentFieldType => RealModelSystemStructure.ParentFieldType;

        /// <summary>
        /// Does the model system have changes that are not saved.
        /// </summary>
        public bool IsDirty => Dirty || Children != null && Children.Any((child) => child.IsDirty);

        public ParametersModel Parameters { get; private set; }

        /// <summary>
        /// Is this an optional module.
        /// </summary>
        /// <returns>True if the module is optional.</returns>
        public bool IsOptional => !RealModelSystemStructure.Required;


        /// <summary>
        /// Commit the changes into the underlying data
        /// </summary>
        /// <param name="error">If there is a problem</param>
        /// <returns></returns>
        public bool Save(ref string error)
        {
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    if (!child.Save(ref error))
                    {
                        return false;
                    }
                }
            }

            if (Dirty)
            {
                Dirty = false;
                ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsDirty));
            }
            return true;
        }

        /// <summary>
        /// Save a model system starting at this point as the root.
        /// </summary>
        /// <param name="saveTo">The stream to save the result to.</param>
        /// <returns>True if the operation was successfully completed.</returns>
        public bool Save(Stream saveTo)
        {
            // save to a temporary stream in case of a failure
            using (var tempStream = new MemoryStream())
            {
                try
                {
                    RealModelSystemStructure.Save(tempStream);
                    tempStream.Position = 0;
                    // if we have successfully saved continue by copying it to the real stream.
                    var writer = new BinaryWriter(saveTo, Encoding.Unicode, true);
                    writer.Write(tempStream.Length);
                    writer.Flush();
                    tempStream.WriteTo(saveTo);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString() + " " + Name + ", IsCollection=" + IsCollection + ", Type=" + Type +
                   ", ParentFieldName=" + ParentFieldName + ", ParentFieldType=" + ParentFieldType;
        }
    }
}

namespace Exceptions
{
    /// <summary>
    /// Exception for failed import due to a module missing
    /// </summary>
    internal class MissingModuleTypeException : Exception
    {
        public MissingModuleTypeException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelSystemStructure"></param>
        public MissingModuleTypeException(ModelSystemStructureModel modelSystemStructure) :
            base("Missing Module: " + modelSystemStructure)
        {
        }
    }
}