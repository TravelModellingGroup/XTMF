using System.Collections.Generic;

namespace XTMF.Interfaces;

public interface IRegionGroup
{
    string Name { get; set; }

    List<IModelSystemStructure> Modules { get; set; }

    
}
