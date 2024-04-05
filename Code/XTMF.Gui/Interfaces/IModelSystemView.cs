using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using XTMF.Gui.Models;

namespace XTMF.Gui.Interfaces;

public interface IModelSystemView
{
    ModelSystemStructureDisplayModel SelectedModule { get;  }

    ItemsControl ViewItemsControl { get; }
}
