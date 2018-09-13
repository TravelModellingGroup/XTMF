
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.Models
{
    public class RegionDisplayModel
    {
        public ObservableCollection<RegionGroupDisplayModel> Groups { get; set; }
    }

    public class RegionGroupDisplayModel
    {
        public ObservableCollection<ModelSystemStructureDisplayModel> Modules { get; set; }
    }
    public class RegionDisplaysDisplayModel
    {
        public ObservableCollection<RegionDisplayModel> Regions { get; set; }
    }
}
