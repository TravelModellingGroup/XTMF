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
using System.Linq;
using System.Text;
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
            Parameters = new ParametersModel(RealModelSystemStructure, session);
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
                if((var oldType = RealModelSystemStructure.Type) != value)
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
                        Parameters = new ParametersModel(RealModelSystemStructure, Session);
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Type");
                        if(oldDirty == false)
                        {
                            ModelHelper.PropertyChanged(PropertyChanged, this, "IsDirty");
                        }
                        ModelHelper.PropertyChanged(PropertyChanged, this, "Parameters");
                        return true;
                    };

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
                    }, apply), ref (string error = null));

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

                    RealModelSystemStructure.Add(name == null ? CreateNameFromType(type) : name, type);
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
                    RealModelSystemStructure.Children.RemoveAt(data.Index);
                    Children.RemoveAt(data.Index);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
                    return true;
                },
                (ref string e) =>
                {
                    Children.Insert(data.Index, data.ModelInQuestion);
                    RealModelSystemStructure.Children.Insert(data.Index, data.StructureInQuestion);
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

        private static ObservableCollection<ModelSystemStructureModel> CreateChildren(ModelSystemEditingSession session, ModelSystemStructure realModelSystemStructure)
        {
            if(realModelSystemStructure.Children == null) return null;
            var ret = new ObservableCollection<ModelSystemStructureModel>();
            for(int i = 0; i < realModelSystemStructure.Children.Count; i++)
            {
                ret.Add(new ModelSystemStructureModel(session, realModelSystemStructure.Children[i] as ModelSystemStructure));
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
            Children.RemoveAt(start);
            RealModelSystemStructure.Children.Insert(dest, real);
            Children.Insert(dest, model);
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