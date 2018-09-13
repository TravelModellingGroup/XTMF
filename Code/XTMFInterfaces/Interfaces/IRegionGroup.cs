using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Interfaces
{
    public interface IRegionGroup
    {
        string Name { get; set; }

        List<IModelSystemStructure2> Modules { get; set; }

        
    }
}
