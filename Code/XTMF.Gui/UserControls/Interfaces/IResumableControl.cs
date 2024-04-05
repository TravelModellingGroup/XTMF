using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.UserControls.Interfaces;

interface IResumableControl
{
    void RestoreWithData(object data);
}
