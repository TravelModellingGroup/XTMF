using System.Windows.Controls;
using XTMF.Gui.Models;

namespace XTMF.Gui.Interfaces;

public interface IModelSystemView
{
    ModelSystemStructureDisplayModel SelectedModule { get;  }

    ItemsControl ViewItemsControl { get; }
}
