
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using XTMF.Editing;
using XTMF.Gui.Models;
using XTMF.Interfaces;

namespace XTMF.Gui.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class RegionDisplayModel
    {
        private IRegionDisplay _model;

        public ObservableCollection<RegionGroupDisplayModel> Groups { get; set; }

        public IRegionDisplay Model
        {
            get => _model;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        public RegionDisplayModel(IRegionDisplay region)
        {
            Groups = new ObservableCollection<RegionGroupDisplayModel>();
            this._model = region;

            foreach (var group in region.RegionGroups)
            {
                Groups.Add(new RegionGroupDisplayModel(group));
            }

            ((RegionDisplay)region).RegionGroups.CollectionChanged += RegionGroupsOnCollectionChanged;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Groups.Clear();
            foreach (var g in Model.RegionGroups)
            {
                Groups.Add(new RegionGroupDisplayModel(g));
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class RegionGroupDisplayModel
    {
        private IRegionGroup _model;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="group"></param>
        public RegionGroupDisplayModel(IRegionGroup group)
        {
            _model = group;
            Modules = new ObservableCollection<IModelSystemStructure>();
            foreach (var module in group.Modules)
            {
                Modules.Add(module);
            }

            ((RegionGroup)group).ModulesUpdated += OnModulesUpdated;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnModulesUpdated(object sender, EventArgs e)
        {
            var group = sender as IRegionGroup;

            Modules.Clear();
            foreach (var module in group.Modules)
            {
                Modules.Add(module);
            }
        }

        public IRegionGroup Model
        {
            get => _model;
        }

        public ObservableCollection<IModelSystemStructure> Modules { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class RegionDisplaysDisplayModel
    {
        private RegionDisplaysModel _model;

        public ObservableCollection<RegionDisplayModel> Regions { get; set; }

        public RegionDisplaysModel Model
        {
            get => _model;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        public RegionDisplaysDisplayModel(RegionDisplaysModel model)
        {
            Regions = new ObservableCollection<RegionDisplayModel>();
            this._model = model;

            
            model.RegionViewsUpdated += ModelOnRegionViewsUpdated;

            foreach (var region in model.RegionDisplays)
            {
                Regions.Add(new RegionDisplayModel(region));
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelOnRegionViewsUpdated(object sender, RegionViewsUpdateEventArgs e)
        {

            Regions.Clear();
            foreach (var region in Model.RegionDisplays)
            {

                Regions.Add(new RegionDisplayModel(region));
            }
        }


    }



}
