using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Interfaces;

namespace XTMF
{
    /// <summary>
    /// 
    /// </summary>
    public class RegionDisplay : IRegionDisplay
    {
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

        private ObservableCollection<IRegionGroup> _regionGroups;
        private string _name;
        public ObservableCollection<IRegionGroup> RegionGroups
        {
            get
            {
                return _regionGroups;
            }
            set
            {

            }
        }

        public RegionDisplay()
        {
            _regionGroups = new ObservableCollection<IRegionGroup>();
        }
    }
}
