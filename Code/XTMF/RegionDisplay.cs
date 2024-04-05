using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XTMF.Interfaces;

namespace XTMF;

/// <summary>
/// 
/// </summary>
public class RegionDisplay : IRegionDisplay, INotifyPropertyChanged
{

    /// <summary>
    /// 
    /// </summary>
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

    /// <summary>
    /// 
    /// </summary>
    public string Description
    {
        get;set;
    }

    private ObservableCollection<IRegionGroup> _regionGroups;
    private string _name;

    public event PropertyChangedEventHandler PropertyChanged;

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
    /// <param name="name"></param>
    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 
    /// </summary>
    public RegionDisplay()
    {
        _regionGroups = [];
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
        if (displays is not null)
        {
            foreach (var region in displays)
            {
                var r = new RegionDisplay()
                {
                    Name = region.Name
                };

                foreach (var group in region.RegionGroups)
                {
                    var g = new RegionGroup(group, clone, r);


                    r.RegionGroups.Add(g);
                }

                list.Add(r);
            }
        }
        return (List<IRegionDisplay>)list;
    }
}
