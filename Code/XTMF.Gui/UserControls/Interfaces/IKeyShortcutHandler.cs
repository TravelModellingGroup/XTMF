using System.Windows.Input;

namespace XTMF.Gui.UserControls.Interfaces;

public interface IKeyShortcutHandler
{

    void HandleKeyPreviewDown(object sender, KeyEventArgs e);
}
