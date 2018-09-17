using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Interfaces;

namespace XTMF
{
    /// <summary>
    /// 
    /// </summary>
    public class RegionGroup : IRegionGroup
    {
        private string _name;

        private List<IModelSystemStructure> _modules;

        public event EventHandler ModulesUpdated;

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
        /// <param name="parent"></param>
        public RegionGroup(RegionDisplay parent)
        {
            _modules = new List<IModelSystemStructure>();
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
            _modules = new List<IModelSystemStructure>();
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
            var path = ModelSystemStructure.GetModuleReferencePath(original, new List<string>());

            return ModelSystemStructure.GetModuleFromReference(path, cloneRoot);
        }
    }
}
