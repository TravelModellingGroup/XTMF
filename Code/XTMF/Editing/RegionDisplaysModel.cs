using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using XTMF.Annotations;
using XTMF.Interfaces;

namespace XTMF.Editing
{
    public class RegionDisplaysModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<IRegionDisplay> _regionDisplays;

        private ModelSystemEditingSession _session;

        private ModelSystemModel _modelSystemModel;

        public event EventHandler<RegionViewGroupsUpdateEventArgs> RegionViewGroupsUpdated;

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
            RegionDisplay regionDisplay = new RegionDisplay()
            {
                Name = name
            };

            return _session.RunCommand(XTMFCommand.CreateCommand("New Region Display",
                // on do
                (ref string e) =>
                {

                    this.RegionDisplays.Add(regionDisplay);
                   
                    return true;
                },
                // on undo
                (ref string e) =>
                {
                    this.RegionDisplays.Remove(regionDisplay);
                    return true;
                },

                // on redo
                (ref string e) =>
                {
                    this.RegionDisplays.Add(regionDisplay);
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
            RegionGroup regionGroup = new RegionGroup()
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
                    return true;
                },
                // on undo
                (ref string e) =>
                {
                    group.Modules.Remove(module);
                    return true;
                },

                // on redo
                (ref string e) =>
                {
                    group.Modules.Add(module);
                    return true;
                }), ref error);
        }

        /// <summary>
            /// 
            /// </summary>
            /// <param name="session"></param>
            /// <param name="modelSystemModel"></param>
            /// <param name="regionDisplays"></param>
            public RegionDisplaysModel(ModelSystemEditingSession session, ModelSystemModel modelSystemModel, List<IRegionDisplay> regionDisplays)
        {
            this._regionDisplays = new ObservableCollection<IRegionDisplay>(regionDisplays);
            this._session = session;
            this._modelSystemModel = modelSystemModel;


        }

        [NotifyPropertyChangedInvocator]
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
}
