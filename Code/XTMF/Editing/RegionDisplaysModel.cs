using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using XTMF.Interfaces;

namespace XTMF.Editing;

public class RegionDisplaysModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private ObservableCollection<IRegionDisplay> _regionDisplays;

    private ModelSystemEditingSession _session;

    private ModelSystemModel _modelSystemModel;

    public ModelSystemModel Model
    {
        get => _modelSystemModel;
    }

    public event EventHandler<RegionViewGroupsUpdateEventArgs> RegionViewGroupsUpdated;

    public event EventHandler<RegionViewsUpdateEventArgs> RegionViewsUpdated;

    public ObservableCollection<IRegionDisplay> RegionDisplays
    {
        get
        {
            return this._regionDisplays;
        }
        private set
        {
            this._regionDisplays = value;
        }
    }

    /// <summary>
    /// Creates a new Region Display with the specified name and adds it to the model.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="error"></param>
    public bool CreateNewRegionDisplay(string name, ref string error)
    {
        RegionDisplay regionDisplay = new()
        {
            Name = name
        };


        return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
            // on do
            (ref string e) =>
            {

                this.RegionDisplays.Add(regionDisplay);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(regionDisplay));
                return true;
            },
            // on undo
            (ref string e) =>
            {
                this.RegionDisplays.Remove(regionDisplay);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(regionDisplay));
                return true;
            },

            // on redo
            (ref string e) =>
            {
                this.RegionDisplays.Add(regionDisplay);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(regionDisplay));
                return true;
            }), ref error);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool RemoveRegionDisplay(RegionDisplay display, ref string error)
    {

        return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
            // on do
            (ref string e) =>
            {

                this.RegionDisplays.Remove(display);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(display));
                return true;
            },
            // on undo
            (ref string e) =>
            {
                this.RegionDisplays.Add(display);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(display));
                return true;
            },

            // on redo
            (ref string e) =>
            {
                this.RegionDisplays.Remove(display);
                RegionViewsUpdated?.Invoke(this, new RegionViewsUpdateEventArgs(display));
                return true;
            }), ref error);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="group"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool RemoveRegionGroup(RegionGroup group, ref string error)
    {
        return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
            // on do
            (ref string e) =>
            {

                group.ParentDisplay.RegionGroups.Remove(group);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(group.ParentDisplay));
                return true;
            },
            // on undo
            (ref string e) =>
            {
                group.ParentDisplay.RegionGroups.Add(group);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(group.ParentDisplay));
                return true;
            },

            // on redo
            (ref string e) =>
            {
                group.ParentDisplay.RegionGroups.Remove(group);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(group.ParentDisplay));
                return true;
            }), ref error);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="region"></param>
    /// <param name="name"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool CreateNewGroupDisplay(RegionDisplay region, string name, ref string error)
    {
        RegionGroup regionGroup = new(region)
        {
            Name = name
        };
        return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
            // on do
            (ref string e) =>
            {

                region.RegionGroups.Add(regionGroup);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(region));
                return true;
            },
            // on undo
            (ref string e) =>
            {
                region.RegionGroups.Remove(regionGroup);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(region));
                return true;
            },

            // on redo
            (ref string e) =>
            {
                region.RegionGroups.Add(regionGroup);
                RegionViewGroupsUpdated?.Invoke(this, new RegionViewGroupsUpdateEventArgs(region));
                return true;
            }), ref error);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="group"></param>
    /// <param name="name"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool AddModuleToGroup(IRegionGroup group, IModelSystemStructure2 module, ref string error)
    {
        return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
            // on do
            (ref string e) =>
            {
                
                group.Modules.Add(module);
                ((RegionGroup)group).UpdateModules(group);
                return true;
            },
            // on undo
            (ref string e) =>
            {
                group.Modules.Remove(module);
                ((RegionGroup)group).UpdateModules(group);
                return true;
            },

            // on redo
            (ref string e) =>
            {
                group.Modules.Add(module);
                ((RegionGroup)group).UpdateModules(group);
                return true;
            }), ref error);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="session"></param>
    /// <param name="modelSystemModel"></param>
    /// <param name="regionDisplays"></param>
    /// <param name="rootModel"></param>
    public RegionDisplaysModel(ModelSystemEditingSession session, ModelSystemModel modelSystemModel,
        List<IRegionDisplay> regionDisplays, ModelSystemStructureModel rootModel = null)
    {
        this._regionDisplays = new ObservableCollection<IRegionDisplay>(regionDisplays);
        this._session = session;
        this._modelSystemModel = modelSystemModel;
        RootModelSystemStructureModel = rootModel;


    }


    public ModelSystemStructureModel RootModelSystemStructureModel { get; set; }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));




}

/// <summary>
/// 
/// </summary>
public class RegionViewGroupsUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="regionDisplay"></param>
    public RegionViewGroupsUpdateEventArgs(RegionDisplay regionDisplay)
    {
        RegionDisplay = regionDisplay;
    }

    public RegionDisplay RegionDisplay { get; }
}

/// <summary>
/// 
/// </summary>
public class RegionViewsUpdateEventArgs : EventArgs
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="regionDisplay"></param>
    public RegionViewsUpdateEventArgs(RegionDisplay regionDisplay)
    {
        RegionDisplay = regionDisplay;
    }

    public RegionDisplay RegionDisplay { get; }
}
