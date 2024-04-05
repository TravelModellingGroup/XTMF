using MahApps.Metro.Controls;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Interaction logic for TabHostWindow.xaml
/// </summary>
public partial class TabHostWindow : MetroWindow
{
    public TabHostWindow()
    {
        InitializeComponent();

        TabablzControl.InterTabController.InterTabClient = new InterTabClient();
        
    }
}
