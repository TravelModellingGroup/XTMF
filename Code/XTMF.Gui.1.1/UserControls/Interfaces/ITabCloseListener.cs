using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.UserControls.Interfaces
{
    interface ITabCloseListener
    {
        /// <summary>
        /// Function to intercept a closing tab, return false to cancel closing.
        /// </summary>
        /// <returns></returns>
        bool HandleTabClose();
    }
}
