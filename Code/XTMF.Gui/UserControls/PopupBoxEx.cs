using MaterialDesignThemes.Wpf;
using System.Windows.Input;

namespace XTMF.Gui.UserControls;

class PopupBoxEx : PopupBox
{
    /// <summary>
    /// Overridden mouse leave event to delay the close action.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        return;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        return;
    }
}
