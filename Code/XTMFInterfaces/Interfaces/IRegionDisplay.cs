using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Interfaces
{
    public interface IRegionDisplay
    {
        /// <summary>
        /// 
        /// </summary>
        string Name { get; set; }

        string Description { get; set; }

        /// <summary>
        /// 
        /// </summary>
        ObservableCollection<IRegionGroup> RegionGroups { get; set; }
    }
}
