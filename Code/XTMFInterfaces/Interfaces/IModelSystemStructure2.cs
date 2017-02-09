using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace XTMF
{
    public interface IModelSystemStructure2 : IModelSystemStructure
    {
        /// <summary>
        /// 
        /// </summary>
        List<IModuleMetaProperty> ModuleMetaProperties {get;}

        /// <summary>
        /// Should this module get included during runs?
        /// </summary>
        bool IsDisabled { get; set; }
    }
}
