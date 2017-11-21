using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
        /// Dependency propery for the ActiveDisplayModule
        /// </summary>
        public static readonly DependencyProperty DisplayModelDependencyProperty =
          DependencyProperty.Register("ActiveDisplayModule", typeof(ModelSystemStructureDisplayModel), typeof(ModuleContextControl), new PropertyMetadata(null));

        /// <summary>
        /// Event Handler for ModuleContextChanged
        /// </summary>
        public event EventHandler<ModuleContextChangedEventArgs> ModuleContextChanged;

        public ModuleContextControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The active display module. This changes when the selected module changes from ModelSystemDisplay
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
        /// Mouse double-click handler for each individual list in the module context path. This will call the associated event listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
          
            Label sourceLabel = sender as Label;

            ModuleContextChanged?.Invoke(sender, new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel)sourceLabel.Tag));
        }

        

        /// <summary>
        /// Called before the context menu opens (specific module context menu)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrameworkElement_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {


            ListView listView = (ListView) sender;

            var selectedModule = listView.SelectedItem as ModelSystemStructureDisplayModel;

            if (selectedModule == null)
            {
                e.Handled = true;
                return;
            }

            listView.ContextMenu.PlacementTarget = listView;
            listView.ContextMenu.Placement = PlacementMode.Bottom;
            listView.ContextMenu.Items.Clear();

            if (selectedModule.Parent != null)
            {
                selectedModule.Parent.Children.ToList().ForEach(item =>
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = item.Name;
                    menuItem.Tag = item;
                    listView.ContextMenu.Items.Add(menuItem);
                    menuItem.Click += ModuleContextMenuItem_Click;
                });

            }

            //suppress menu when there are no siblings
            if (listView.ContextMenu.Items.Count == 0)
            {
                e.Handled = true;
            }

        }

        /// <summary>
        /// Event listener for click of the module context listview's menu. This will trigger the ModuleContextChanged event (if set).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem senderMenuItem = sender as MenuItem;

            ModuleContextChanged?.Invoke(sender, new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel)senderMenuItem.Tag));
        }

      
    }


    /// <summary>
    /// Custom Event for triggering selected Module context change inside of the ModelSystemDisplay control
    /// </summary>
    public class ModuleContextChangedEventArgs : EventArgs
    {
        public ModuleContextChangedEventArgs(ModelSystemStructureDisplayModel module)
        {
            Module = module;
        }
        public ModelSystemStructureDisplayModel Module { get; }
    }
}
