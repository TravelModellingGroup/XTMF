/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ModuleContextControl.xaml
    ///     The ModuleContextControl is a horizontal / slim control that displays a context path to the currently
    ///     displayed module. Interaction is allowed to select different modules along the path and provide
    ///     feedback for the user to see which set of modules the active module exists under.
    /// </summary>
    public partial class ModuleContextControl : UserControl
    {
        /// <summary>
        ///     Dependency propery for the ActiveDisplayModule
        /// </summary>
        public static readonly DependencyProperty DisplayModelDependencyProperty =
            DependencyProperty.Register("ActiveDisplayModule", typeof(ModelSystemStructureDisplayModel),
                typeof(ModuleContextControl), new PropertyMetadata(null));

        /// <summary>
        /// Constructor
        /// </summary>
        public ModuleContextControl()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     The active display module. This changes when the selected module changes from ModelSystemDisplay
        /// </summary>
        public ModelSystemStructureDisplayModel ActiveDisplayModule
        {
            get => (ModelSystemStructureDisplayModel) GetValue(DisplayModelDependencyProperty);
            set
            {
                SetValue(DisplayModelDependencyProperty, value);
                UpdateModulePathToRoot(value);
                ModulePathList.SelectedItem = value;
            }
        }

        /// <summary>
        ///     Event Handler for ModuleContextChanged
        /// </summary>
        public event EventHandler<ModuleContextChangedEventArgs> ModuleContextChanged;


        /// <summary>
        ///     Generates a list, in order from root to passed active module
        /// </summary>
        /// <param name="moduleDisplayModel"></param>
        /// <returns></returns>
        private List<ModelSystemStructureDisplayModel> GenerateUpdateModulePathToRoot(
            ModelSystemStructureDisplayModel moduleDisplayModel)
        {
            var pathList = new List<ModelSystemStructureDisplayModel>();
            pathList.Add(moduleDisplayModel);
            while (moduleDisplayModel.Parent != null)
            {
                pathList.Insert(0, moduleDisplayModel.Parent);
                moduleDisplayModel = moduleDisplayModel.Parent;
            }

            return pathList;
        }

        /// <summary>
        ///     Updates the item source of ModulePathList to contain the path to the root module
        /// </summary>
        /// <param name="module"></param>
        private void UpdateModulePathToRoot(ModelSystemStructureDisplayModel module)
        {
            ModulePathList.ItemsSource = GenerateUpdateModulePathToRoot(module);
        }


        /// <summary>
        ///     Mouse double-click handler for each individual list in the module context path. This will call the associated event
        ///     listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var sourceLabel = sender as Label;

            ModuleContextChanged?.Invoke(sender,
                new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel) sourceLabel.Tag));
        }


        /// <summary>
        /// Prepares the contetx menu with appropriate items before rendering
        /// </summary>
        /// <param name="selectedModule"></param>
        /// <param name="menu"></param>
        private void PrepareMenu(ModelSystemStructureDisplayModel selectedModule, ContextMenu menu)
        {
            menu.Items.Clear();

            selectedModule.Children?.ToList().ForEach(item =>
            {
                var menuItem = new MenuItem
                {
                    Header = item.Name,
                    Tag = item
                };
                menu.Items.Add(menuItem);
                menuItem.Click += ModuleContextMenuItem_Click;
                menu.Closed += ContextMenu_Closed;

                menuItem.Icon = GenerateIconForModule(item);
            });
        }


        /// <summary>
        ///     Event listener for click of the module context listview's menu. This will trigger the ModuleContextChanged event
        ///     (if set).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var senderMenuItem = sender as MenuItem;

            var moduleDisplayModel = senderMenuItem.Tag as ModelSystemStructureDisplayModel;

            //determine type of module, non-collections get immediately called the the listener
            if (moduleDisplayModel != null && moduleDisplayModel.Children != null && !moduleDisplayModel.IsCollection &&
                moduleDisplayModel.Children.Count == 0)
                ModuleContextChanged?.Invoke(sender,
                    new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel) senderMenuItem.Tag));
            else
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.DataBind,
                    new Action(() =>
                    {
                        ModulePathList.ItemsSource = GenerateUpdateModulePathToRoot(moduleDisplayModel);

                        var item =
                            ModulePathList.ItemContainerGenerator.ContainerFromItem(moduleDisplayModel) as
                                ListViewItem;
                        if (item != null)
                        {
                            item.ContextMenu = new ContextMenu();
                            DisplayModulePathContextMenu(moduleDisplayModel, item, item.ContextMenu);
                        }
                    }));
        }


        /// <summary>
        ///     Generates a path to use as an icon for the module context menu (displays same icon as in the model system tree
        ///     view)
        /// </summary>
        /// <param name="module"></param>
        private Path GenerateIconForModule(ModelSystemStructureDisplayModel module)
        {
            var path = new Path();
            if (module.IsCollection)
                path.Data = (PathGeometry) Application.Current.Resources["CollectionIconPath"];
            else if (module.IsMetaModule)
                path.Data = (PathGeometry) Application.Current.Resources["MetaModuleIconPath"];
            else if (!module.IsMetaModule && !module.IsCollection)
                path.Data = (PathGeometry) Application.Current.Resources["ModuleIcon2Path"];

            //scale and set colour of the icon path
            path.Fill = (Brush) Application.Current.Resources["MaterialDesignBody"];
            path.Stretch = Stretch.UniformToFill;
            path.MaxWidth = 12;
            path.MaxHeight = 12;

            return path;
        }


        /// <summary>
        ///     Invoked when the selected module in the path list is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModulePathList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ModuleContextChanged?.Invoke(sender,
                new ModuleContextChangedEventArgs((ModelSystemStructureDisplayModel) ModulePathList.SelectedItem));
        }


        /// <summary>
        ///     Displays the selected module's context menu, placing it at the listViewItem target.
        /// </summary>
        /// <param name="selectedModule"></param>
        /// <param name="listViewItem"></param>
        /// <param name="menu"></param>
        private void DisplayModulePathContextMenu(ModelSystemStructureDisplayModel selectedModule,
            ListViewItem listViewItem, ContextMenu menu)
        {
            PrepareMenu(selectedModule, menu);

            if (menu != null && selectedModule.Children != null && selectedModule.Children.Count > 0)
            {
                menu.PlacementTarget = listViewItem;
                menu.Placement = PlacementMode.Bottom;
                menu.HorizontalOffset = -20;
                menu.VerticalOffset = -8;
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// Event for when the context menu closes, will select the active module if navigation terminated on a list item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            //if the menu closed, make sure the proper item is selected
            var listViewItem =
                ModulePathList.ItemContainerGenerator.ContainerFromIndex(
                    ModulePathList.Items.Count - 1) as ListViewItem;
            var module = listViewItem.DataContext as ModelSystemStructureDisplayModel;

            if (ActiveDisplayModule != module)
                ModuleContextChanged?.Invoke(sender, new ModuleContextChangedEventArgs(module));
        }


        /// <summary>
        /// Right mouse button event listener for each item in the context list, will display the module's context menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModulePathList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listView = (StackPanel) sender;

            var selectedModule = listView.Tag as ModelSystemStructureDisplayModel;

            if (selectedModule == null)
            {
                e.Handled = true;
                return;
            }
            var listViewItem = (ListViewItem) ModulePathList.ItemContainerGenerator.ContainerFromItem(selectedModule);
            listViewItem.ContextMenu = new ContextMenu();


            DisplayModulePathContextMenu(selectedModule, listViewItem, listViewItem.ContextMenu);


            e.Handled = true;
        }
    }


    /// <summary>
    ///     Custom Event for triggering selected Module context change inside of the ModelSystemDisplay control
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