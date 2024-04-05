﻿/*
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Models;

public class ModelSystemStructureDisplayModel : INotifyPropertyChanged
{
    private ObservableCollection<ModelSystemStructureModel> _BaseChildren;

    private bool _IsExpanded;

    private bool _isSelected;

    private Visibility _ModuleVisibility;
    internal ModelSystemStructureModel BaseModel;

    public ModelSystemStructureDisplayModel(ModelSystemStructureModel baseModel,
        ModelSystemStructureDisplayModel parent, int index)
    {
        //BaseChildren.
        Parent = parent;
        Index = index;
        BaseModel = baseModel;
        _BaseChildren = baseModel.Children;
        UpdateChildren(baseModel);
        BaseModel.PropertyChanged += BaseModel_PropertyChanged;
        if (_BaseChildren != null) _BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;
    }

    public int Index { get; set; }

    public ModelSystemStructureDisplayModel Parent { get; }

    public ModelSystemStructureDisplayModel BackingDisplayModel => this;

    public string Name => BaseModel.Name;

    public string Description => BaseModel.Description;

    public ObservableCollection<ModelSystemStructureDisplayModel> Children { get; private set; }

    public ModuleTreeViewItem ControlTreeViewItem { get; set; }

    public Type Type
    {
        get => BaseModel.Type;
        set => BaseModel.Type = value;
    }


    public bool IsCollection => BaseModel.IsCollection;

    public bool IsExpanded
    {
        get => _IsExpanded;
        set
        {
            _IsExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public Visibility ModuleVisibility
    {
        get => _ModuleVisibility;
        set
        {
            if (_ModuleVisibility != value)
            {
                _ModuleVisibility = value;
                ModelHelper.PropertyChanged(PropertyChanged, this, nameof(ModuleVisibility));
            }
        }
    }

    public ParametersModel ParametersModel => BaseModel.Parameters;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public bool IsDisabled => BaseModel.IsDisabled;

    public bool IsMetaModule => BaseModel.IsMetaModule;

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// </summary>
    /// <param name="baseModel"></param>
    private void UpdateChildren(ModelSystemStructureModel baseModel)
    {
        if (baseModel.IsMetaModule || _BaseChildren == null)
        {
            Children = [];
        }
        else
        {
            Children = [];
            var i = 0;
            foreach (var item in baseModel.Children)
            {
                var s = new ModelSystemStructureDisplayModel(item, this, i);
                Children.Add(s);
                i++;
            }
        }

        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
    }

    /// <summary>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BaseChildren_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var insertAt = e.NewStartingIndex;
                foreach (var item in e.NewItems)
                {
                    var s2 = new ModelSystemStructureDisplayModel(item as ModelSystemStructureModel, this,
                        insertAt);
                    //s2.ModelSystemStructureChanged = this.ModelSystemStructureChanged;
                    Children.Insert(insertAt, s2);
                    insertAt++;
                }
            }
                break;
            case NotifyCollectionChangedAction.Move:
                Children.Move(e.OldStartingIndex, e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (Children.Count > 0) Children.RemoveAt(e.OldStartingIndex);

                break;
            case NotifyCollectionChangedAction.Replace:
                var s = new ModelSystemStructureDisplayModel(e.NewItems[0] as ModelSystemStructureModel, this,
                    e.OldStartingIndex);
                //s.ModelSystemStructureChanged = this.ModelSystemStructureChanged;
                Children[e.OldStartingIndex] = s;
                break;
            case NotifyCollectionChangedAction.Reset:
                Children.Clear();
                break;
            default:
                throw new NotImplementedException("An unknown action was performed!");
        }

        UpdateIndices();
        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Children));
    }

    private void UpdateIndices()
    {
        for(int i = 0; i < Children.Count;i++)
        {
            Children[i].Index = i;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>

    private void BaseModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var ev = PropertyChanged;
        if (ev != null)
        {
            switch (e.PropertyName)
            {
                case "Children":
                    if (_BaseChildren != null) _BaseChildren.CollectionChanged -= BaseChildren_CollectionChanged;

                    _BaseChildren = BaseModel.Children;
                    if (_BaseChildren != null) _BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;

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

    internal ObservableCollection<ParameterModel> GetParameters()
    {
        return !BaseModel.IsMetaModule ? BaseModel.Parameters.GetParameters() : GetMetaModuleParamters();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private ObservableCollection<ParameterModel> GetMetaModuleParamters()
    {
        var ret = new ObservableCollection<ParameterModel>();
        var toGet = new Stack<ModelSystemStructureModel>();
        toGet.Push(BaseModel);
        while (toGet.Count > 0)
        {
            var current = toGet.Pop();
            foreach (var p in current.Parameters.GetParameters()) ret.Add(p);

            if (current.Children != null)
                foreach (var c in current.Children)
                    toGet.Push(c);
        }

        return ret;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public static ObservableCollection<ParameterModel> GetMetaModuleParamters(ModelSystemStructureModel model)
    {
        var ret = new ObservableCollection<ParameterModel>();
        var toGet = new Stack<ModelSystemStructureModel>();
        toGet.Push(model);
        while (toGet.Count > 0)
        {
            var current = toGet.Pop();
            foreach (var p in current.Parameters.GetParameters()) ret.Add(p);

            if (current.Children != null)
                foreach (var c in current.Children)
                    toGet.Push(c);
        }

        return ret;
    }

    internal void CopyModule()
    {
        Clipboard.SetDataObject(BaseModel.CopyModule());
    }

    internal static void CopyModules(List<ModelSystemStructureDisplayModel> toCopy)
    {
        Clipboard.SetDataObject(ModelSystemStructureModel.CopyModule(toCopy.Select(m => m.BaseModel).ToList()));
    }

    internal bool Paste(ModelSystemEditingSession session, string toPaste, ref string error)
    {
        return BaseModel.Paste(session, toPaste, ref error);
    }

    internal List<ModelSystemStructureDisplayModel> BuildChainTo(ModelSystemStructureDisplayModel selected)
    {
        return BuildChainTo(selected, this);
    }

    private static List<ModelSystemStructureDisplayModel> BuildChainTo(ModelSystemStructureDisplayModel selected,
        ModelSystemStructureDisplayModel current)
    {
        if (selected == current) return [current];

        var children = current.Children;
        if (children != null)
            foreach (var child in children)
            {
                var ret = BuildChainTo(selected, child);
                if (ret != null)
                {
                    ret.Insert(0, current);
                    return ret;
                }
            }

        return null;
    }

    internal bool SetDisabled(bool disabled, ref string error)
    {
        return BaseModel.SetDisabled(disabled, ref error);
    }

    internal bool SetMetaModule(bool set, ref string error)
    {
        return BaseModel.SetMetaModule(set, ref error);
    }
}