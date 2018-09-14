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

        /// <summary>
        /// 
        /// </summary>
        public RegionDisplay()
        {
            _regionGroups = new ObservableCollection<IRegionGroup>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="displays"></param>
        /// <param name="clone"></param>
        /// <returns></returns>
        public static List<IRegionDisplay> MapRegionDisplays(List<IRegionDisplay> displays, IModelSystemStructure clone)
        {
            var list = new List<IRegionDisplay>();

            foreach (var region in displays)
            {
                var r = new RegionDisplay()
                {
                    Name = region.Name
                };

                foreach (var group in region.RegionGroups)
                {
                    var g = new RegionGroup(group,clone);

                    
                    r.RegionGroups.Add(g);
                }

                list.Add(r);
            }

            return (List<IRegionDisplay>)list;
        }



    



    }
}
