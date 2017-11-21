using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleContextControl.xaml
    /// 
    /// The ModuleContextControl is a horizontal / slim control that displays a context path to the currently
    /// displayed module. Interaction is allowed to select different modules along the path and provide
    /// feedback for the user to see which set of modules the active module exists under.
    /// </summary>
    public partial class ModuleContextControl : UserControl
    {

        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty DisplayModelDependencyProperty =
          DependencyProperty.Register("ActiveDisplayModule", typeof(ModelSystemStructureDisplayModel), typeof(ModuleContextControl), new PropertyMetadata(null));

        public event EventHandler<ModuleContextChangedEventArgs> ModuleContextChanged;

        public ModuleContextControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        public ModelSystemStructureDisplayModel ActiveDisplayModule
        {
            get => (ModelSystemStructureDisplayModel)GetValue(DisplayModelDependencyProperty);
            set
            {
                SetValue(DisplayModelDependencyProperty, value);
                UpdateModulePathToRoot(value);
             }

        }

        /// <summary>
        /// Updates the item source of ModulePathList to contain the path to the root module
        /// </summary>
        /// <param name="module"></param>
        private void UpdateModulePathToRoot(ModelSystemStructureDisplayModel module)
        {
            List<ModelSystemStructureDisplayModel> pathList = new List<ModelSystemStructureDisplayModel>();
            pathList.Add(module);
            while (module.Parent != null)
            {
                pathList.Insert(0,module.Parent);
                module = module.Parent;
            }
            ModulePathList.ItemsSource = pathList;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);


            Console.WriteLine(ActiveDisplayModule);

           
            
        }


        /// <summary>
        /// Mouse double-click handler for each individual list in the module context path. This will call the associated event listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            

           
            Label sourceLabel = sender as Label;
           
            if (this.ModuleContextChanged != null)
            {
                this.ModuleContextChanged(sender, new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel)sourceLabel.Tag));
            }
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public class ModuleContextChangedEventArgs : EventArgs
    {
        public ModuleContextChangedEventArgs(ModelSystemStructureDisplayModel module)
        {
            this.Module = module;
        }
        public ModelSystemStructureDisplayModel Module { get; }
    }
}
