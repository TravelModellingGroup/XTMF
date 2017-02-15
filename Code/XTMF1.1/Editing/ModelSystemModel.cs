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
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.ObjectModel;

namespace XTMF
{
    public class ModelSystemModel : INotifyPropertyChanged
    {
        /// <summary>
        /// The starting node of the model system
        /// </summary>
        public ModelSystemStructureModel Root { get; private set; }
        /// <summary>
        /// The model that contains the linked parameters
        /// </summary>
        public LinkedParametersModel LinkedParameters { get; private set; }

        public event EventHandler ModelSystemStructureChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        internal ModelSystem ModelSystem { get; private set; }

        internal ModelSystemStructure ClonedModelSystemRoot { get { return Root.RealModelSystemStructure; } }

        private Project Project;

        private int ModelSystemIndex;

        public ModelSystemModel(ModelSystemEditingSession session, ModelSystem modelSystem)
        {
            ModelSystem = modelSystem;
            Name = modelSystem.Name;
            _Description = modelSystem.Description;
            List<ILinkedParameter> editingLinkedParameters;
            Root = new ModelSystemStructureModel(session, modelSystem.CreateEditingClone(out editingLinkedParameters) as ModelSystemStructure);
            LinkedParameters = new LinkedParametersModel(session, this, editingLinkedParameters);
        }

        internal ParameterModel GetParameterModel(IModuleParameter moduleParameter)
        {
            var owner = GetModelFor(moduleParameter.BelongsTo as ModelSystemStructure);
            if(owner != null)
            {
                return GetParameterModel(owner, moduleParameter);
            }
            return null;
        }

        private ParameterModel GetParameterModel(ModelSystemStructureModel owner, IModuleParameter moduleParameter)
        {
            var parameters = owner.Parameters.Parameters;
            if(parameters != null)
            {
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].RealParameter == moduleParameter)
                    {
                        return parameters[i];
                    }
                }
            }
            return null;
        }

        public ModelSystemStructureModel GetModelFor(ModelSystemStructure realStructure)
        {
            return GetModelFor(realStructure, Root);
        }

        private ModelSystemStructureModel GetModelFor(ModelSystemStructure realStructure, ModelSystemStructureModel current)
        {
            if(current.RealModelSystemStructure == realStructure)
            {
                return current;
            }
            var children = current.Children;
            if(children != null)
            {
                for(int i = 0; i < children.Count; i++)
                {
                    ModelSystemStructureModel ret;
                    if((ret = GetModelFor(realStructure, children[i])) != null)
                    {
                        return ret;
                    }
                }
            }
            return null;
        }

        public ModelSystemModel(ModelSystemEditingSession session, Project project, int modelSystemIndex)
        {
            Project = project;
            ModelSystemIndex = modelSystemIndex;
            Name = project.ModelSystemStructure[modelSystemIndex].Name;
            _Description = project.ModelSystemDescriptions[modelSystemIndex];
            List<ILinkedParameter> editingLinkedParameters;
            Root = new ModelSystemStructureModel(session, (project.CloneModelSystemStructure(out editingLinkedParameters, modelSystemIndex) as ModelSystemStructure));
            _Description = Project.ModelSystemDescriptions[modelSystemIndex];
            LinkedParameters = new LinkedParametersModel(session, this, editingLinkedParameters);
        }

        /// <summary>
        /// Create a model system model for a previous run.
        /// </summary>
        /// <param name="modelSystemEditingSession">The session to use</param>
        /// <param name="project">The project the previous run is in.</param>
        /// <param name="runFile">The path to the run file.</param>
        public ModelSystemModel(XTMFRuntime runtime, ModelSystemEditingSession modelSystemEditingSession, Project project, string runFile)
        {
            Project = project;
            ModelSystemIndex = -1;
            Name = Path.GetFileName(runFile);
            _Description = "Previous run";
            Root = new ModelSystemStructureModel(modelSystemEditingSession, runtime.ModelSystemController.LoadFromRunFile(runFile));
            LinkedParameters = new LinkedParametersModel(modelSystemEditingSession, this, new List<ILinkedParameter>());
        }

        private bool _Dirty = false;

        /// <summary>
        /// Does the model system have changes that are not saved.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                return _Dirty || Root.IsDirty;
            }
        }

        private string _Description;

        /// <summary>
        /// Describes the Model System
        /// </summary>
        public string Description
        {
            get { return _Description; }
            set
            {
                var dirtyChanged = false;
                if(IsDirty != true)
                {
                    dirtyChanged = true;
                }
                _Dirty = true;
                _Description = value;
                ModelHelper.PropertyChanged(PropertyChanged, this, "Description");
                if(dirtyChanged)
                {
                    ModelHelper.PropertyChanged(PropertyChanged, this, "IsDirty");
                }
            }
        }

        private string _Name;
        /// <summary>
        /// The name of the model system
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if(_Name != value)
                {
                    _Name = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                }
            }
        }

        /// <summary>
        /// Save the changes into the real model system structure.
        /// This should only be called by ModelSystemEditingSession.
        /// </summary>
        /// <param name="error">The error if there was one</param>
        internal bool Save(ref string error)
        {
            if(!Root.Save(ref error))
            {
                return false;
            }
            if(ModelSystem != null)
            {
                ModelSystem.ModelSystemStructure = ClonedModelSystemRoot;
                ModelSystem.Description = Description;
                ModelSystem.LinkedParameters = LinkedParameters.LinkedParameters.Select(lp => (ILinkedParameter)lp.RealLinkedParameter).ToList();
                return ModelSystem.Save(ref error);
            }
            else if(ModelSystemIndex >= 0)
            {
                Project.ModelSystemStructure[ModelSystemIndex] = ClonedModelSystemRoot;
                Project.ModelSystemDescriptions[ModelSystemIndex] = Description;
                Project.LinkedParameters[ModelSystemIndex] = LinkedParameters.LinkedParameters.Select(lp => (ILinkedParameter)lp.RealLinkedParameter).ToList();
                // changing the name should go last because it will bubble up to the GUI and if the models are not in the right place the old name still be read in
                Name = ClonedModelSystemRoot.Name;
                return Project.Save(ref error);
            }
            else
            {
                error = "You can not save over previous runs!";
                return false;
            }
        }

        public ObservableCollection<ParameterModel> GetQuickParameters()
        {
            ObservableCollection<ParameterModel> quickParameters = new ObservableCollection<ParameterModel>();
            AddQuickParameters(quickParameters, Root);
            return quickParameters;
        }

        private void AddQuickParameters(ObservableCollection<ParameterModel> quickParameters, ModelSystemStructureModel current)
        {
            var parameters = current.Parameters.Parameters;
            if(parameters != null)
            {
                foreach(var p in parameters)
                {
                    if(p.QuickParameter)
                    {
                        quickParameters.Add(p);
                    }
                }
            }
            var children = current.Children;
            if(children != null)
            {
                foreach(var child in children)
                {
                    AddQuickParameters(quickParameters, child);
                }
            }
        }

        /// <summary>
        /// Change the name of the model system
        /// </summary>
        /// <param name="error">The reason why changing the name failed.</param>
        public bool ChangeModelSystemName(string newName, ref string error)
        {
            return Root.SetName(newName, ref error);
        }

        public bool Remove(ModelSystemStructureModel selected, ref string error)
        {
            if(selected.IsCollection)
            {
                return selected.RemoveAllCollectionMembers(ref error);
            }
            return Remove(Root, null, selected, ref error);
        }

        private bool Remove(ModelSystemStructureModel current, ModelSystemStructureModel previous, ModelSystemStructureModel selected, ref string error)
        {
            if(current == selected)
            {
                if(previous == null)
                {
                    Root.Type = null;
                    return true;
                }
                else
                {
                    if(previous.IsCollection)
                    {
                        return previous.RemoveCollectionMember(previous.Children.IndexOf(selected), ref error);
                    }
                    else
                    {
                        selected.Type = null;
                        return true;
                    }
                }
            }
            var children = current.Children;
            if(children != null)
            {
                foreach(var child in current.Children)
                {
                    var success = Remove(child, current, selected, ref error);
                    if(success)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Create a clone of this model system model as a model system.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public ModelSystem CloneAsModelSystem(IConfiguration config)
        {
            if (ModelSystem != null)
            {
                return ModelSystem.Clone();
            }
            var ms = new ModelSystem(config, _Name)
            {
                LinkedParameters = LinkedParameters.GetRealLinkedParameters(),
                ModelSystemStructure = Root.RealModelSystemStructure
            };
            return ms.Clone();
        }
    }
}