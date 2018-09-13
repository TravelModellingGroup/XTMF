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

        private List<IModelSystemStructure2> _modules;

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
        public List<IModelSystemStructure2> Modules {
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
            _modules = new List<IModelSystemStructure2>();
        }
    }
}
