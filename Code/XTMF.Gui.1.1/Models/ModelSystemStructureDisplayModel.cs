/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Media;
using System.Collections.Specialized;
using System.Windows.Forms;
using System.Windows;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Models
{
    public  class ModelSystemStructureDisplayModel : INotifyPropertyChanged
    {
        internal ModelSystemStructureModel BaseModel;
        private ObservableCollection<ModelSystemStructureModel> BaseChildren;

        public event PropertyChangedEventHandler PropertyChanged;

        private ModelSystemStructureDisplayModel _modelSystemStructureDisplayModel;

        private static Color AddingYellow;
        private static Color OptionalGreen;
        private static Color WarningRed;
        private static Color MetaModule;


        public ModelSystemStructureDisplayModel BackingDisplayModel
        {
            get { return this; }
        }

        static ModelSystemStructureDisplayModel()
        {
            AddingYellow = (Color)App.Current.FindResource("AddingYellow");
            WarningRed = (Color)App.Current.FindResource("WarningRed");
            OptionalGreen = Color.FromRgb(50, 140, 50);
            MetaModule = Color.FromArgb(255, 60, 20, 90);
        }

        public ModelSystemStructureDisplayModel(ModelSystemStructureModel baseModel)
        {
            BaseModel = baseModel;
            BaseChildren = baseModel.Children;
            UpdateChildren(baseModel);
            BaseModel.PropertyChanged += BaseModel_PropertyChanged;
            if (BaseChildren != null)
            {
                BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;
            }
        }

   

        private void UpdateChildren(ModelSystemStructureModel baseModel)
        {
            Children = baseModel.IsMetaModule || BaseChildren == null ? new ObservableCollection<ModelSystemStructureDisplayModel>()
                : new ObservableCollection<ModelSystemStructureDisplayModel>(
                from child in baseModel.Children
                select new ModelSystemStructureDisplayModel(child));
            ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
        }

        private void BaseChildren_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        var insertAt = e.NewStartingIndex;
                        foreach (var item in e.NewItems)
                        {
                            Children.Insert(insertAt, new ModelSystemStructureDisplayModel(item as ModelSystemStructureModel));
                            insertAt++;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    {
                        Children.Move(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        if (Children.Count > 0)
                        {
                            Children.RemoveAt(e.OldStartingIndex);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        Children[e.OldStartingIndex] = new ModelSystemStructureDisplayModel(e.NewItems[0] as ModelSystemStructureModel);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    {
                        Children.Clear();
                    }
                    break;
                default:
                    {
                        throw new NotImplementedException("An unknown action was performed!");
                    }
            }
            ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
        }

        private void BaseModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var ev = PropertyChanged;
            if (ev != null)
            {
                switch (e.PropertyName)
                {
                    case "Children":
                        if (BaseChildren != null)
                        {
                            BaseChildren.CollectionChanged -= BaseChildren_CollectionChanged;
                        }
                        BaseChildren = BaseModel.Children;
                        if (BaseChildren != null)
                        {
                            BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;
                        }
                        break;
                    case "IsMetaModule":
                        UpdateChildren(BaseModel);
                        ModelHelper.PropertyChanged(ev, this, "BackgroundColour");
                        ModelHelper.PropertyChanged(ev, this, "HighlightColour");
                        break;
                    case "IsDisabled":
                    case "Type":
                        ModelHelper.PropertyChanged(ev, this, "BackgroundColour");
                        ModelHelper.PropertyChanged(ev, this, "HighlightColour");
                        break;
                }            
                ModelHelper.PropertyChanged(ev, this, e.PropertyName);
            }
        }

        public string Name => BaseModel.Name;

        public string Description => BaseModel.Description;

        public Color BackgroundColour
        {
            get
            {
                if (BaseModel.IsMetaModule)
                {
                    return MetaModule;
                }
                if (BaseModel.IsCollection)
                {
                    return AddingYellow;
                }
                if (BaseModel.Type == null)
                {
                    return (BaseModel.IsOptional) ? OptionalGreen : WarningRed;
                }
                return Color.FromRgb(0x30, 0x30, 0x30);
            }
        }

        public Color HighlightColour
        {
            get
            {
                if (BaseModel.IsMetaModule)
                {
                    return MetaModule;
                }
                if (BaseModel.IsCollection)
                {
                    return AddingYellow;
                }
                if (BaseModel.Type == null)
                {
                    return (BaseModel.IsOptional) ? OptionalGreen : WarningRed;
                }
                return Color.FromRgb(0x30, 0x30, 0x30);
            }
        }

        public ObservableCollection<ModelSystemStructureDisplayModel> Children { get; private set; }
        public Type Type
        {
            get
            {
                return BaseModel.Type;
            }
            set
            {
                BaseModel.Type = value;
            }
        }

        public bool IsCollection { get { return BaseModel.IsCollection; } }

        private bool _IsExpanded = false;
        public bool IsExpanded
        {
            get
            {
                return _IsExpanded;
            }
            set
            {
                if (_IsExpanded != value)
                {
                    _IsExpanded = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, "IsExpanded");
                }
            }
        }

        private Visibility _ModuleVisibility;
        public Visibility ModuleVisibility
        {
            get
            {
                return _ModuleVisibility;
            }
            set
            {
                if (_ModuleVisibility != value)
                {
                    _ModuleVisibility = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, "ModuleVisibility");
                }
            }
        }

        public ParametersModel ParametersModel => BaseModel.Parameters;

        internal ObservableCollection<ParameterModel> GetParameters()
        {
            return !BaseModel.IsMetaModule ? BaseModel.Parameters.GetParameters() : GetMetaModuleParamters();
        }

        private ObservableCollection<ParameterModel> GetMetaModuleParamters()
        {
            var ret = new ObservableCollection<ParameterModel>();
            var toGet = new Stack<ModelSystemStructureModel>();
            toGet.Push(BaseModel);
            while (toGet.Count > 0)
            {
                var current = toGet.Pop();
                foreach (var p in current.Parameters.GetParameters())
                {
                    ret.Add(p);
                }
                if (current.Children != null)
                {
                    foreach (var c in current.Children)
                    {
                        toGet.Push(c);
                    }
                }
            }
            return ret;
        }

        internal void CopyModule()
        {
            System.Windows.Clipboard.SetDataObject(BaseModel.CopyModule());
        }

        internal static void CopyModules(List<ModelSystemStructureDisplayModel> toCopy)
        {
            System.Windows.Clipboard.SetDataObject(ModelSystemStructureModel.CopyModule(toCopy.Select(m => m.BaseModel).ToList()));
        }

        internal bool Paste(ModelSystemEditingSession session, string toPaste, ref string error)
        {
            return BaseModel.Paste(session, toPaste, ref error);
        }

        internal List<ModelSystemStructureDisplayModel> BuildChainTo(ModelSystemStructureDisplayModel selected)
        {
            return BuildChainTo(selected, this);
        }

        private static List<ModelSystemStructureDisplayModel> BuildChainTo(ModelSystemStructureDisplayModel selected, ModelSystemStructureDisplayModel current)
        {
            if (selected == current)
            {
                return new List<ModelSystemStructureDisplayModel>() { current };
            }
            var children = current.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var ret = BuildChainTo(selected, child);
                    if (ret != null)
                    {
                        ret.Insert(0, current);
                        return ret;
                    }
                }
            }
            return null;
        }

        public bool IsDisabled => BaseModel.IsDisabled;

        internal bool SetDisabled(bool disabled, ref string error)
        {
            return BaseModel.SetDisabled(disabled, ref error);
        }

        internal bool SetMetaModule(bool set, ref string error)
        {
            return BaseModel.SetMetaModule(set, ref error);
        }
    }
}
