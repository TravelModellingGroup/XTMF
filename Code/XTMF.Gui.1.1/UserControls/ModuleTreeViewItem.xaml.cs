using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTreeViewItem.xaml
    /// </summary>
    public partial class ModuleTreeViewItem : UserControl
    {

        public static readonly DependencyProperty ModuleTypeDependencyProperty =
  DependencyProperty.Register("ModuleType",
      typeof(ModuleType), typeof(ModuleTreeViewItem),
          new PropertyMetadata(null));


        
        public ModuleTreeViewItem()
        {
            InitializeComponent();
        }


        public ModuleType ModuleType
        {

            get { return (ModuleType)GetValue(ModuleTypeDependencyProperty); }

            set { SetValue(ModuleTypeDependencyProperty,value);}
        }
    }

    public enum ModuleType
    {
        Optional, Meta, Required
    }
}
