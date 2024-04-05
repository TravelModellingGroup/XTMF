using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Interfaces;

namespace XTMF
{
    /// <summary>
    /// 
    /// </summary>
    public class RegionGroup : IRegionGroup, INotifyPropertyChanged
    {
        private string _name;

        private List<IModelSystemStructure> _modules;

        public event EventHandler ModulesUpdated;
        public event PropertyChangedEventHandler PropertyChanged;

        public RegionDisplay ParentDisplay { get; set; }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));

            }
        }
        public List<IModelSystemStructure> Modules {
            get
            {
                return _modules;
            }
            set
            {

            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="group"></param>
        public void UpdateModules(IRegionGroup group)
        {
            ModulesUpdated?.Invoke(group,new EventArgs());
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        private void OnPropertyChanged(string name)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        public RegionGroup(RegionDisplay parent)
        {
            _modules = [];
            ParentDisplay = parent;
        }

       /// <summary>
       /// 
       /// </summary>
       /// <param name="clone"></param>
       /// <param name="cloneStructure"></param>
       /// <param name="parent"></param>
        public RegionGroup(IRegionGroup clone, IModelSystemStructure cloneStructure, IRegionDisplay parent)
        {
            _modules = [];
            Name = clone.Name;
            ParentDisplay = (RegionDisplay)parent;

            foreach (var module in clone.Modules)
            {
                _modules.Add(GetSiblingModule(module,cloneStructure));


            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="original"></param>
        /// <param name="cloneRoot"></param>
        /// <returns></returns>
        private IModelSystemStructure GetSiblingModule(IModelSystemStructure original, IModelSystemStructure cloneRoot)
        {
            var path = ModelSystemStructure.GetModuleReferencePath(original, []);

            return ModelSystemStructure.GetModuleFromReference(path, cloneRoot);
        }
    }
}
