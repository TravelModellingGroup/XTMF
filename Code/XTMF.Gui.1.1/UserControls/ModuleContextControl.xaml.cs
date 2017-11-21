using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XTMF.Gui.Models;
using Brushes = System.Windows.Media.Brushes;

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
                ModulePathList.SelectedItem = value;
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


           

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectedModule"></param>
        /// <param name="menu"></param>
        private void PrepareMenu(ModelSystemStructureDisplayModel selectedModule, ContextMenu menu)
        {
            //menu.PlacementTarget = listView;
            menu.Placement = PlacementMode.Bottom;
            menu.Items.Clear();

            if (selectedModule.Parent != null)
            {
                selectedModule.Parent.Children.ToList().ForEach(item =>
                {
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = item.Name;
                    menuItem.Tag = item;
                    menu.Items.Add(menuItem);
                    menuItem.Click += ModuleContextMenuItem_Click;

                    menuItem.Icon = GenerateIconForModule(selectedModule);
                });

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

       

        /// <summary>
        /// Generates a path to use as an icon for the module context menu (displays same icon as in the model system tree view)
        /// </summary>
        /// <param name="module"></param>
        private Path GenerateIconForModule(ModelSystemStructureDisplayModel module)
        {
            Path path = new Path();
            if (module.IsCollection)
            {
               
                path.Data = (PathGeometry)Application.Current.Resources["CollectionIconPath"];
               
            }
            else if (module.IsMetaModule)
            {
                path.Data = (PathGeometry) Application.Current.Resources["MetaModuleIconPath"];
            }
            else if (!module.IsMetaModule && !module.IsCollection)
            {
                path.Data = (PathGeometry) Application.Current.Resources["ModuleIcon2Path"];
            }

            //scale and set colour of the icon path
            path.Fill = (System.Windows.Media.Brush)Application.Current.Resources["ThemeTextColorBrush"];
            path.Stretch = Stretch.UniformToFill;
            path.MaxWidth = 12;
            path.MaxHeight = 12;

            return path;
        }

       
        /// <summary>
        /// Invoked when the selected module in the path list is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModulePathList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ModuleContextChanged?.Invoke(sender, new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel)ModulePathList.SelectedItem));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModulePathList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanel listView = (StackPanel) sender;

            var selectedModule = listView.Tag as ModelSystemStructureDisplayModel;

            if (selectedModule == null)
            {
                e.Handled = true;
                return;
            }

            this.PrepareMenu(selectedModule, ModulePathList.ContextMenu);

            ListViewItem listViewItem = (ListViewItem)ModulePathList.ItemContainerGenerator.ContainerFromItem(selectedModule);

            ModulePathList.ContextMenu.PlacementTarget = listViewItem;
            ModulePathList.ContextMenu.Placement = PlacementMode.Bottom;
            ModulePathList.ContextMenu.HorizontalOffset = -20;
            ModulePathList.ContextMenu.VerticalOffset = -8;
            ModulePathList.ContextMenu.IsOpen = true;

            e.Handled = true;
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
