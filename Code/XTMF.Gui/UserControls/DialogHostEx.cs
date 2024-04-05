using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace XTMF.Gui.UserControls;

public class DialogHostEx : DialogHost
{
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var child = GetTemplateChild("PART_ContentCoverGrid") as Grid;
       
        child.Background =  Brushes.Transparent;

        ;
    }
}
