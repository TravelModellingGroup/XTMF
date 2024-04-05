using System.Collections.ObjectModel;

namespace XTMF.Interfaces;

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
