using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XTMF.Gui.UserControls.Interfaces;

public interface IKeyShortcutHandler
{

    void HandleKeyPreviewDown(object sender, KeyEventArgs e);
}
