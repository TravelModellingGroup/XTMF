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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using XTMF.Editing;
namespace XTMF
{
    public class ModelSystemStructureModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ModelSystemEditingSession Session;
        internal ModelSystemStructure RealModelSystemStructure;

        public ModelSystemStructureModel(ModelSystemEditingSession session, ModelSystemStructure realModelSystemStructure)
        {
            Session = session;
            RealModelSystemStructure = realModelSystemStructure;
            Parameters = new ParametersModel(this, session);
            Children = CreateChildren(Session, RealModelSystemStructure);
        }

        public string Name
        {
            get { return RealModelSystemStructure.Name; }
        }

        public string Description
        {
            get { return RealModelSystemStructure.Description; }
        }

        public string ParentFieldName
        {
            get { return RealModelSystemStructure.ParentFieldName; }
        }

        public Type Type
        {
            get
            {
                return RealModelSystemStructure.Type;
            }
            set
            {
                var oldType = RealModelSystemStructure.Type;
                if(oldType != value)
                {
                    var oldChildren = Children;
                    var oldParameters = Parameters;
                    var oldDirty = IsDirty;
                    XTMFCommand.XTMFCommandMethod apply = (ref string e) =>
                    {
                        // right now we are using a clone
                        Dirty = true;
                        RealModelSystemStructure.Type = value;
                        UpdateChildren();
                        Parameters = new ParametersModel(this, Session);
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Type");
                        if(oldDirty == false)
                        {
                            ModelHelper.PropertyChanged(PropertyChanged, this, "IsDirty");
                        }
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Parameters");
                        return true;
                    };
                    string error = null;
                    // run the command to change the type so we can undo it later
                    Session.RunCommand(XTMFCommand.CreateCommand(
                     apply,
                     (ref string e) =>
                    {
                        // undo
                        RealModelSystemStructure.Type = oldType;
                        Children = oldChildren;
                        if(Children != null)
                        {
                            // move the old children back into place
                            for(int i = 0; i < Children.Count; i++)
                            {
                                RealModelSystemStructure.Children[i] = Children[i].RealModelSystemStructure;
                            }
                        }
                        Parameters = oldParameters;
                        Dirty = oldDirty;
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Type");
                        if(oldDirty ^ IsDirty)
                        {
                            ModelHelper.PropertyChanged(PropertyChanged, this, "IsDirty");
                        }
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Parameters");
                        return true;
                    }, apply), ref error);

                }
            }
        }

        public bool IsCollection
        {
            get
            {
                return RealModelSystemStructure.IsCollection;
            }
        }

        /// <summary>
        /// Add a new collection member to a collection using the given type
        /// </summary>
        /// <param name="type">The type to add</param>
        /// <param name="name">The name to use, pass a null to automatically name the module</param>
        public bool AddCollectionMember(Type type, ref string error, string name = null)
        {
            if(type == null)
            {
                throw new ArgumentNullException("type");
            }
            if(!IsCollection)
            {
                throw new InvalidOperationException("You can not add collection members to a module that is not a collection!");
            }

            CollectionChangeData data = new CollectionChangeData();
            return Session.RunCommand(XTMFCommand.CreateCommand(
                (ref string e) =>
                {
                    if(!ValidateType(type, ref e))
                    {
                        return false;
                    }
                    if(RealModelSystemStructure.IsCollection)
                    {
                        RealModelSystemStructure.Add(RealModelSystemStructure.CreateCollectionMember(name == null ? CreateNameFromType(type) : name, type));
                    }
                    else
                    {
                        RealModelSystemStructure.Add(name == null ? CreateNameFromType(type) : name, type);
                    }
                    data.Index = RealModelSystemStructure.Children.Count - 1;
                    data.StructureInQuestion = RealModelSystemStructure.Children[data.Index] as ModelSystemStructure;
                    if(Children == null)
                    {
                        Children = new ObservableCollection<ModelSystemStructureModel>();
                    }
                    Children.Add(data.ModelInQuestion = new ModelSystemStructureModel(Session, data.StructureInQuestion));
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

        public bool Paste(string buffer, ref string error)
        {
            ModelSystemStructure copiedStructure;
            List<TempLinkedParameter> linkedParameters;
            // Get the data
            using (MemoryStream backing = new MemoryStream())
            {
                StreamWriter writer = new StreamWriter(backing);
                writer.Write(buffer);
                writer.Flush();
                backing.Position = 0;

                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(backing);
                    copiedStructure = GetModelSystemStructureFromXML(doc["CopiedModule"]["CopiedModules"]);
                    linkedParameters = GetLinkedParametersFromXML(doc["CopiedModule"]["LinkedParameters"]);
                }
                catch
                {
                    error = "Unable to decode the copy buffer.";
                    return false;
                }
            }
            // validate the modules contained
            if(!IsAssignable(Session.ModelSystemModel.Root.RealModelSystemStructure,
                IsCollection ? RealModelSystemStructure : Session.GetParent(this).RealModelSystemStructure, copiedStructure))
            {
                error = "The copied model system is not pasteable at this location.";
                return false;
            }
            List<LinkedParameterModel> newLinkedParameters = new List<LinkedParameterModel>();
            var additions = new List<Tuple<ParameterModel, LinkedParameterModel>>();
            var oldReal = RealModelSystemStructure;
            return Session.RunCommand(XTMFCommand.CreateCommand(
                (ref string e) =>
            {
                ModelSystemStructureModel beingAdded;
                if(IsCollection)
                {
                    RealModelSystemStructure.Add(copiedStructure);
                    UpdateChildren();
                    beingAdded = Children[Children.Count - 1];
                }
                else
                {
                    RealModelSystemStructure = copiedStructure;
                    UpdateAll();
                    beingAdded = this;
                }
                var linkedParameterModel = Session.ModelSystemModel.LinkedParameters;
                var realLinkedParameters = linkedParameterModel.GetLinkedParameters();
                var missing = from lp in linkedParameters
                              where !realLinkedParameters.Any(rlp => rlp.Name == lp.Name)
                              select lp;
                var matching = linkedParameters.Join(realLinkedParameters, (p) => p.Name, (p) => p.Name, (t, r) => new { Real = r, Temp = t });
                // add links for the ones we've matched
                foreach(var lp in matching)
                {
                    foreach(var containedParameters in GetParametersFromTemp(lp.Temp, beingAdded))
                    {
                        lp.Real.AddParameterWithoutCommand(containedParameters);
                        containedParameters.SignalIsLinkedChanged();
                        additions.Add(new Tuple<ParameterModel, LinkedParameterModel>(containedParameters, lp.Real));
                    }
                }
                // add links for the ones that didn't match
                foreach(var missingLp in missing)
                {
                    var newLP = linkedParameterModel.AddWithoutCommand(missingLp.Name, missingLp.Value);
                    newLinkedParameters.Add(newLP);
                    foreach(var containedParameters in GetParametersFromTemp(missingLp, beingAdded))
                    {
                        newLP.AddParameterWithoutCommand(containedParameters);
                        containedParameters.SignalIsLinkedChanged();
                    }
                }
                return true;
            },
                  (ref string e) =>
            {
                if(IsCollection)
                {
                    RealModelSystemStructure.Children.Remove(copiedStructure);
                    UpdateChildren();
                }
                else
                {
                    RealModelSystemStructure = oldReal;
                    UpdateAll();
                }
                var linkedParameterModel = Session.ModelSystemModel.LinkedParameters;
                foreach(var newLP in newLinkedParameters)
                {
                    linkedParameterModel.RemoveWithoutCommand(newLP);
                }
                foreach(var addition in additions)
                {
                    addition.Item2.RemoveParameterWithoutCommand(addition.Item1);
                }
                return true;
            },
                    (ref string e) =>
            {
                if(IsCollection)
                {
                    RealModelSystemStructure.Children.Add(copiedStructure);
                    UpdateChildren();
                }
                else
                {
                    RealModelSystemStructure = copiedStructure;
                    UpdateAll();
                }
                var linkedParameterModel = Session.ModelSystemModel.LinkedParameters;
                foreach(var newLP in newLinkedParameters)
                {
                    linkedParameterModel.AddWithoutCommand(newLP);
                }
                foreach(var addition in additions)
                {
                    addition.Item2.AddParameterWithoutCommand(addition.Item1);
                }
                return true;
            }), ref error);
        }

        private List<ParameterModel> GetParametersFromTemp(TempLinkedParameter temp, ModelSystemStructureModel root)
        {
            return (from path in temp.Paths
                   select GetParametersFromTemp(path, root)).ToList();
        }

        private ParameterModel GetParametersFromTemp(string path, ModelSystemStructureModel root)
        {
            return GetParameterFromLink(ParseLinkedParameterName(path), 0, root);

        }

        private ParameterModel GetParameterFromLink(string[] variableLink, int index, ModelSystemStructureModel current)
        {
            if(index == variableLink.Length - 1)
            {
                // search the parameters
                var parameters = current.Parameters;
                foreach(var p in parameters.Parameters)
                {
                    if(p.Name == variableLink[index])
                    {
                        return p;
                    }
                }
            }
            else
            {
                var descList = current.Children;
                if(descList == null)
                {
                    return null;
                }
                if(current.IsCollection)
                {
                    int collectionIndex;
                    if(int.TryParse(variableLink[index], out collectionIndex))
                    {
                        if(collectionIndex >= 0 && collectionIndex < descList.Count)
                        {
                            return GetParameterFromLink(variableLink, index + 1, descList[collectionIndex]);
                        }
                        return null;
                    }
                }
                else
                {
                    foreach(var sub in descList)
                    {
                        if(sub.ParentFieldName == variableLink[index])
                        {
                            return GetParameterFromLink(variableLink, index + 1, sub);
                        }
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
            for(int i = 0; i < length; i++)
            {
                var c = variableLink[i];
                // check to see if we need to add in the escape
                if(escape & c != '.')
                {
                    builder.Append('\\');
                }
                // check to see if we need to move onto the next part
                if(escape == false & c == '.')
                {
                    ret.Add(builder.ToString());
                    builder.Clear();
                    escape = false;
                }
                else if(c != '\\')
                {
                    builder.Append(c);
                    escape = false;
                }
                else
                {
                    escape = true;
                }
            }
            if(escape)
            {
                builder.Append('\\');
            }
            ret.Add(builder.ToString());
            return ret.ToArray();
        }


        private void UpdateAll()
        {
            UpdateChildren();
            ModelHelper.PropertyChanged(PropertyChanged, this, "Type");
            ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
            ModelHelper.PropertyChanged(PropertyChanged, this, "Description");
        }

        private bool IsAssignable(ModelSystemStructure rootStructure, ModelSystemStructure parentStructure, ModelSystemStructure copyBuffer)
        {
            // This will update what module we are using for the root as per the Re-rootable extension for XTMF
            try
            {
                var parent = parentStructure == null ? typeof(IModelSystemTemplate) : parentStructure.Type;
                if(copyBuffer.IsCollection)
                {
                    // Make sure that we are doing collection to collection and that they are of the right types
                    if(!this.IsCollection || !this.RealModelSystemStructure.ParentFieldType.IsAssignableFrom(copyBuffer.ParentFieldType))
                    {
                        return false;
                    }
                    // now make sure that every new element is alright with the parent and root
                    var parentType = this.RealModelSystemStructure.ParentFieldType;
                    var arguements = parentType.IsArray ? parentType.GetElementType() : parentType.GetGenericArguments()[0];
                    foreach(var member in copyBuffer.Children)
                    {
                        var t = member.Type;
                        if(arguements.IsAssignableFrom(t) && (parent == null || ModelSystemStructure.CheckForParent(parent, t)) && ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) != null)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    var t = copyBuffer.Type;
                    rootStructure = ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) as ModelSystemStructure;
                    if(this.IsCollection)
                    {
                        var parentType = this.RealModelSystemStructure.ParentFieldType;

                        var arguements = parentType.IsArray ? parentType.GetElementType() : parentType.GetGenericArguments()[0];
                        if(arguements.IsAssignableFrom(t) && (ModelSystemStructure.CheckForParent(parent, t)) && ModelSystemStructure.CheckForRootModule(rootStructure, this.RealModelSystemStructure, t) != null)
                        {
                            return true;
                        }

                    }
                    else
                    {
                        if(this.RealModelSystemStructure.ParentFieldType.IsAssignableFrom(t) &&
                            (parent == null || ModelSystemStructure.CheckForParent(parent, t))
                            && ModelSystemStructure.CheckForRootModule(rootStructure, RealModelSystemStructure, t) != null)
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
            List<TempLinkedParameter> ret = new List<TempLinkedParameter>();
            foreach(XmlNode child in linkedParameterNode.ChildNodes)
            {
                if(child.Name == "LinkedParameter")
                {
                    var nextLp = new TempLinkedParameter()
                    {
                        Name = child.Attributes["Name"].InnerText,
                        Value = child.Attributes["Value"].InnerText
                    };
                    foreach(XmlNode link in child.ChildNodes)
                    {
                        nextLp.Paths.Add(link.Attributes["Path"].InnerText);
                    }
                    ret.Add(nextLp);
                }
            }
            return ret;
        }

        private ModelSystemStructure GetModelSystemStructureFromXML(XmlNode rootMSChild)
        {
            return ModelSystemStructure.Load(rootMSChild, Session.Configuration);
        }



        /// <summary>
        /// Return a representation of this module
        /// </summary>
        public string CopyModule()
        {
            var children = GetAllChildren();
            using (MemoryStream backing = new MemoryStream())
            {
                using (XmlTextWriter writer = new XmlTextWriter(backing, Encoding.Unicode))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.WriteStartElement("CopiedModule");
                    writer.WriteStartElement("CopiedModules");
                    RealModelSystemStructure.Save(writer);
                    writer.WriteEndElement();
                    writer.WriteStartElement("LinkedParameters");
                    foreach(var linkedParameter in GetLinkedParameters(children))
                    {
                        writer.WriteStartElement("LinkedParameter");
                        writer.WriteAttributeString("Name", linkedParameter.Name);
                        writer.WriteAttributeString("Value", linkedParameter.GetValue());
                        foreach(var link in linkedParameter.GetParameters())
                        {
                            var match = children.FirstOrDefault(m => m.RealModelSystemStructure == link.RealParameter.BelongsTo);
                            if(match != null)
                            {
                                writer.WriteStartElement("Parameter");
                                writer.WriteAttributeString("Path", LookupName(link, this));
                                writer.WriteEndElement();
                            }
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
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

        private string LookupName(ParameterModel reference, ModelSystemStructureModel current)
        {
            var param = current.Parameters;
            if(param != null && param.Parameters != null)
            {
                int index = param.Parameters.IndexOf(reference);
                if(index >= 0)
                {
                    return current.Parameters.Parameters[index].Name;
                }
            }
            var childrenList = current.Children;
            if(childrenList != null)
            {
                for(int i = 0; i < childrenList.Count; i++)
                {
                    var res = LookupName(reference, childrenList[i]);
                    if(res != null)
                    {
                        // make sure to use an escape character before the . to avoid making the mistake of reading it as another index
                        return string.Concat(current.IsCollection ? i.ToString()
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
            var linkedParameters = Session.ModelSystemModel.LinkedParameters;
            return (from lp in linkedParameters.LinkedParameters
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
            if(children != null)
            {
                foreach(var child in children)
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
            if(index < 0)
            {
                throw new InvalidOperationException("Indexes must be greater than or equal to zero!");
            }

            if(!IsCollection)
            {
                throw new InvalidOperationException("You can not add collection members to a module that is not a collection!");
            }
            CollectionChangeData data = new CollectionChangeData();
            return Session.RunCommand(XTMFCommand.CreateCommand(
                (ref string e) =>
                {
                    var children = RealModelSystemStructure.Children;
                    if(children.Count <= index)
                    {
                        e = "There is no collection member at index " + index + "!";
                        return false;
                    }
                    data.Index = index;
                    data.StructureInQuestion = RealModelSystemStructure.Children[data.Index] as ModelSystemStructure;
                    data.ModelInQuestion = Children[data.Index];
                    RealModelSystemStructure.Children.RemoveAt(data.Index);
                    Children.RemoveAt(data.Index);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
                    return true;
                },
                (ref string e) =>
                {
                    RealModelSystemStructure.Children.Insert(data.Index, data.StructureInQuestion);
                    Children.Insert(data.Index, data.ModelInQuestion);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
                    return true;
                },
                (ref string e) =>
                {
                    RealModelSystemStructure.Children.RemoveAt(data.Index);
                    Children.RemoveAt(data.Index);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
                    return true;
                }),
                ref error);
        }

        public bool RemoveAllCollectionMembers(ref string error)
        {
            if(!IsCollection)
            {
                throw new InvalidOperationException("You can not add collection members to a module that is not a collection!");
            }
            IList<ModelSystemStructureModel> oldChildren = null;
            IList<IModelSystemStructure> oldRealChildren = null;
            return Session.RunCommand(XTMFCommand.CreateCommand(
                (ref string e) =>
                {
                    oldRealChildren = RealModelSystemStructure.Children.ToList();
                    oldChildren = Children.ToList();
                    RealModelSystemStructure.Children.Clear();
                    Children.Clear();
                    return true;
                },
                (ref string e) =>
                {
                    foreach(var child in oldChildren)
                    {
                        Children.Add(child);
                    }
                    var realChildList = RealModelSystemStructure.Children;
                    foreach(var child in oldRealChildren)
                    {
                        realChildList.Add(child);
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
            var topLevelModule = Session.ModelSystemModel.Root.RealModelSystemStructure;
            return RealModelSystemStructure.CheckPossibleModule(type, topLevelModule, ref error);
        }

        /// <summary>
        /// Generate a name given the type
        /// </summary>
        /// <param name="type">The type to generate a name for</param>
        /// <returns>A string to use for a name</returns>
        private static string CreateNameFromType(Type type)
        {
            bool LastCapital = true;
            StringBuilder name = new StringBuilder(type.Name);
            for(int i = 0; i < name.Length; i++)
            {
                if(char.IsUpper(name[i]) & !LastCapital)
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

        private ObservableCollection<ModelSystemStructureModel> CreateChildren(ModelSystemEditingSession session, ModelSystemStructure realModelSystemStructure)
        {
            if(realModelSystemStructure.Children == null) return null;

            ObservableCollection<ModelSystemStructureModel> ret;
            if(Children == null)
            {
                ret = new ObservableCollection<ModelSystemStructureModel>();
                for(int i = 0; i < realModelSystemStructure.Children.Count; i++)
                {
                    ret.Add(new ModelSystemStructureModel(session, realModelSystemStructure.Children[i] as ModelSystemStructure));
                }
            }
            else
            {
                ret = Children;
                if(realModelSystemStructure.Children == null)
                {
                    ret.Clear();
                }
                else
                {
                    // remove children
                    var removedChildren = (from child in Children
                                           where !realModelSystemStructure.Children.Any(r => r == child.RealModelSystemStructure)
                                           select child).ToArray();
                    // new children go to the end
                    var newChildren = (from child in realModelSystemStructure.Children
                                       where !Children.Any(c => c.RealModelSystemStructure == child)
                                       select child).ToArray();

                    foreach(var child in removedChildren)
                    {
                        ret.Remove(child);
                    }
                    foreach(var child in newChildren)
                    {
                        ret.Add(new ModelSystemStructureModel(session, child as ModelSystemStructure));
                    }
                    bool repeat = false;
                    do
                    {
                        // now search for children that have moved indexes after adds and deleted have been performed
                        var indexes = (from child in Children
                                       select realModelSystemStructure.Children.IndexOf(child.RealModelSystemStructure)).ToArray();
                        for(int i = 0; i < indexes.Length; i++)
                        {
                            // if a child has moved
                            if(indexes[i] != i)
                            {
                                Children.Move(i, indexes[i]);
                                repeat = true;
                                break;
                            }
                        }
                    } while(repeat);
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
            var model = Children[start];
            RealModelSystemStructure.Children.RemoveAt(start);
            RealModelSystemStructure.Children.Insert(dest, real);
            Children.Move(start, dest);
        }

        public bool MoveModeInParent(int deltaPosition, ref string error)
        {
            ModelSystemStructureModel parent = Session.GetParent(this);
            if(!parent.IsCollection)
            {
                error = "You can only move the children of a collection!";
                return false;
            }
            var ourIndex = parent.Children.IndexOf(this);
            return parent.MoveChild(ourIndex, ourIndex + deltaPosition, ref error);
        }

        public bool MoveChild(int originalPosition, int newPosition, ref string error)
        {
            if(!IsCollection)
            {
                error = "You can only move the children of a collection!";
                return false;
            }
            MoveChildData move = new MoveChildData();
            return Session.RunCommand(
                XTMFCommand.CreateCommand(
                    // do
                    (ref string e) =>
                    {
                        move.OriginalPosition = originalPosition;
                        move.NewPosition = newPosition;
                        if(originalPosition < 0 | originalPosition >= Children.Count)
                        {
                            e = "The original position was invalid!";
                            return false;
                        }
                        if(newPosition < 0 | newPosition >= Children.Count)
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

        public bool SetName(string newName, ref string error)
        {
            var oldName = "";
            return Session.RunCommand(XTMFCommand.CreateCommand((ref string e) =>
            {
                oldName = this.RealModelSystemStructure.Name;
                this.RealModelSystemStructure.Name = newName;
                ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                return true;
            }, (ref string e) =>
            {
                this.RealModelSystemStructure.Name = oldName;
                ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                return true;
            },
            (ref string e) =>
            {
                this.RealModelSystemStructure.Name = newName;
                ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                return true;
            }), ref error);
        }

        private void UpdateChildren()
        {
            Children = CreateChildren(Session, RealModelSystemStructure);
            ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
        }

        private bool Dirty = false;

        /// <summary>
        /// Does the model system have changes that are not saved.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                return Dirty || (Children != null && Children.Any((child) => child.IsDirty));
            }
        }

        public ParametersModel Parameters { get; private set; }

        /// <summary>
        /// Is this an optional module.
        /// </summary>
        /// <returns>True if the module is optional.</returns>
        public bool IsOptional { get { return !RealModelSystemStructure.Required; } }


        /// <summary>
        /// Commit the changes into the underlying data
        /// </summary>
        /// <param name="error">If there is a problem</param>
        /// <returns></returns>
        public bool Save(ref string error)
        {
            if(Children != null)
            {
                foreach(var child in Children)
                {
                    if(!child.Save(ref error))
                    {
                        return false;
                    }
                }
            }
            if(Dirty)
            {
                Dirty = false;
                ModelHelper.PropertyChanged(PropertyChanged, this, "IsDirty");
            }
            return true;
        }
    }
}