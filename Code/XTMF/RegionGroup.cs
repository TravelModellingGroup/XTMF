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
    class RegionGroup : IRegionGroup
    {
        private string _name;

        private List<IModelSystemStructure> _modules;

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
        public RegionGroup()
        {
            _modules = new List<IModelSystemStructure>();
        }
    }
}
