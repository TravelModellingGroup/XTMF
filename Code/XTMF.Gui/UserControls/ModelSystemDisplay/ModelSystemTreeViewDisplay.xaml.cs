using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Reflection;
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
using XTMF.Gui.Controllers;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MahApps.Metro.Controls;
using XTMF.Annotations;
using TreeView = System.Windows.Controls.TreeView;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModelSystemTreeViewDisplay.xaml
    /// </summary>
    public partial class ModelSystemTreeViewDisplay : UserControl, IModelSystemView, INotifyPropertyChanged
    {

        private ModelSystemDisplay _display;

        private ModelSystemEditingSession Session
        {
            get { return this._display.Session; }
        }

        public ModelSystemStructureDisplayModel SelectedModule =>
            ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;

        public ItemsControl ViewItemsControl
        {
            get { return ModuleDisplay; }
        }

        private bool _disableMultipleSelectOnce;

        private ModelSystemEditingSession _modelSystemEditingSession;

        private static readonly PropertyInfo IsSelectionChangeActiveProperty = typeof(TreeView).GetProperty(
            "IsSelectionChangeActive", BindingFlags.NonPublic | BindingFlags.Instance);

        public event PropertyChangedEventHandler PropertyChanged;

        internal List<ModelSystemStructureDisplayModel> CurrentlySelected
        {
            get { return _display.CurrentlySelected; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="display"></param>
        public ModelSystemTreeViewDisplay(ModelSystemDisplay display)
        {
            InitializeComponent();
            this._display = display;
            this.AllowMultiSelection(ModuleDisplay);

            this.Loaded += this.ModelSystemDisplay_Loaded;

            this.ModuleDisplay.SelectedItemChanged += ModuleDisplay_SelectedItemChanged;

            this.ModuleContextControl.ModuleContextChanged += this.ModuleContextControlOnModuleContextChanged;

            this._display.ModelSystemEditingSessionChanged += DisplayOnModelSystemEditingSessionChanged;




        }

        /// <summary>
        /// 
        /// </summary>
        public Brush GridBackgroundBrush
        {
            get
            {
                if (EditorController.Runtime.Configuration.IsDarkTheme)
                {
                    return (Brush)FindResource("GridTextureBrushDark");
                }
                else
                {
                    return (Brush)FindResource("GridTextureBrushLight");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisplayOnModelSystemEditingSessionChanged(object sender,
            ModelSystemEditingSessionChangedEventArgs e)
        {
            this._modelSystemEditingSession = e.Session;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDisplay_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ModelSystemStructureDisplayModel module)
            {
                this._display.RefreshParameters();
                this._display.UpdateModuleCount();
                if (!this._display.ModuleParameterDisplay.IsEnabled)
                {
                    this._display.ToggleModuleParameterDisplay();
                }

                //update the module context control
                this.ModuleContextControl.ActiveDisplayModule = (ModelSystemStructureDisplayModel)e.NewValue;

                Dispatcher.Invoke(() =>
                {
                    this._display.StatusBarModuleNameTextBlock.Text = $"{module.BaseModel.Type}";
                });

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock

            this._display.UpdateQuickParameters();


        }



        /// <summary>
        ///     Callback for when the Module Context control changes the active "selected module
        /// </summary>
        /// <param name="sender1"></param>
        /// <param name="eventArgs"></param>
        private void ModuleContextControlOnModuleContextChanged(object sender1, ModuleContextChangedEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                if (eventArgs.Module != null)
                {
                    ExpandToRoot(eventArgs.Module);
                    eventArgs.Module.IsSelected = true;
                    ModuleDisplay.Focus();
                    Keyboard.Focus(ModuleDisplay);
                }
            });
        }

        /// <summary>
        /// Expands a module, tracing backwards until the root module is reached
        /// </summary>
        /// <param name="module"></param>
        public void ExpandToRoot(ModelSystemStructureDisplayModel module)
        {
            // don't expand the bottom node
            module = module?.Parent;
            while (module != null)
            {
                module.IsExpanded = true;
                module = module.Parent;
            }
        }

        /// <summary>
        /// Expand all menu item click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExpandAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ModuleDisplay.SelectedItem != null)
            {
                if (ModuleDisplay.Items.Count > 0)
                {
                    ExpandModule((ModelSystemStructureDisplayModel)ModuleDisplay.SelectedItem);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleTreeViewItem_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ModuleTreeViewItem treeViewItem)
            {
                var menu = treeViewItem.ContextMenu;
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (menuItem.Name == "DisableModuleMenuItem")
                        {
                            if (treeViewItem.BackingModel.BaseModel.CanDisable)
                            {
                                menuItem.Header = treeViewItem.BackingModel.BaseModel.IsDisabled
                                    ? "Enable Module (Ctrl + D)"
                                    : "Disable Module (Ctrl + D)";
                                menuItem.IsEnabled = true;
                            }
                            else
                            {
                                menuItem.IsEnabled = false;
                            }
                        }
                        else if (menuItem.Name == "ModuleMenuItem")
                        {
                            menuItem.Header = treeViewItem.BackingModel.BaseModel.IsCollection
                                ? "Add Module (Ctrl + M)"
                                : "Set Module (Ctrl + M)";
                        }
                    }
                }

                //clear and get an up to date list of region groups available to the model system
                var addRegionGroupMenuItem =
                    menu.Items.Cast<MenuItem>().FirstOrDefault(m => m.Name == "AddToRegionGroupMenuItem");

                this.UpdateAddRegionGroupMenu(addRegionGroupMenuItem);
            }
        }

        /// <summary>
        /// Updates the Add Region Group Sub Menu as part of the context menu
        /// </summary>
        /// <param name="addRegionGroupMenuItem"></param>
        private void UpdateAddRegionGroupMenu(MenuItem addRegionGroupMenuItem)
        {
            addRegionGroupMenuItem.Items.Clear();
            var regionDisplays = this._modelSystemEditingSession.ModelSystemModel.RegionDisplaysModel.RegionDisplays;
            foreach (var regionDisplay in regionDisplays)
            {
                var regionDisplayMenuItem = new MenuItem()
                {
                    Header = regionDisplay.Name
                };
                addRegionGroupMenuItem.Items.Add(regionDisplayMenuItem);

                foreach (var regionGroup in regionDisplay.RegionGroups)
                {
                    regionDisplayMenuItem.Items.Add(new MenuItem()
                    {
                        Header = regionGroup.Name
                    });
                }
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDisplay_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CurrentlySelected.Count == 1)
            {
                var onlySelected = CurrentlySelected[0];
                if (!onlySelected.IsCollection && onlySelected.Type == null)
                {
                    this._display.SelectReplacement();
                    e.Handled = true;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDisplay_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi)
            {
                tvi.BringIntoView();
            }
        }

        /// <summary>
        /// Collapse all menu item click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollapseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ModuleDisplay.SelectedItem != null)
            {
                if (ModuleDisplay.Items.Count > 0)
                {
                    ExpandModule((ModelSystemStructureDisplayModel)ModuleDisplay.SelectedItem, false);
                }
            }
        }

        /// <summary>
        /// Expands or collapses a module and its children.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="collapse"></param>
        private void ExpandModule(ModelSystemStructureDisplayModel module, bool collapse = true)
        {
            if (module != null)
            {
                var toProcess = new Queue<ModelSystemStructureDisplayModel>();
                toProcess.Enqueue(module);
                while (toProcess.Count > 0)
                {
                    module = toProcess.Dequeue();
                    module.IsExpanded = collapse;
                    foreach (var child in module.Children)
                    {
                        toProcess.Enqueue(child);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="up"></param>
        private void MoveFocusNextModule(bool up)
        {
            Keyboard.Focus(ModuleDisplay);
            _display.MoveFocusNext(up);
        }

        /// <summary>
        /// Disables or re-enables the selected module
        /// </summary>
        private void ToggleDisableModule()
        {
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel)?.BaseModel;
            var selectedModuleControl = _display.GetCurrentlySelectedControl();
            if (selectedModuleControl != null && selected != null)
            {
                string error = null;
                Session.ExecuteCombinedCommands(selected.IsDisabled ? "Enable Module" : "Disable Module", () =>
                {
                    foreach (var sel in CurrentlySelected)
                    {
                        if (!sel.SetDisabled(!sel.IsDisabled, ref error))
                        {
                            return;
                        }

                        if (sel.IsDisabled)
                        {
                            if (!_display.DisabledModules.Contains(sel))
                            {
                                _display.DisabledModules.Add(sel);
                            }
                        }

                        if (!sel.IsDisabled && _display.DisabledModules.Contains(sel))
                        {
                            _display.DisabledModules.Remove(sel);
                        }
                    }
                });
                if (error != null)
                {
                    MessageBox.Show(MainWindow.Us, error,
                        selected.IsDisabled ? "Unable to Enable" : "Unable to Disable", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }


            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var item = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            e.Handled = false;
            switch (e.Key)
            {
                case Key.F2:
                    this._display.RenameSelectedModule();
                    break;
                case Key.Up:
                    ModuleDisplayNavigateUp(item);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ModuleDisplayNavigateDown(item);
                    e.Handled = true;
                    break;
                case Key.D:
                    if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
                    {
                        this.ToggleDisableModule();
                    }

                    break;

            }



        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Help_Clicked(object sender, RoutedEventArgs e)
        {
            this._display.ShowDocumentation();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private ModelSystemStructureDisplayModel FindMostExpandedItem(ModelSystemStructureDisplayModel item)
        {
            return !item.IsExpanded || item.Children == null || item.Children.Count == 0
                ? item
                : FindMostExpandedItem(item.Children[item.Children.Count - 1]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        private void ModuleDisplayNavigateUp(ModelSystemStructureDisplayModel item)
        {
            // make sure we are not the root module
            if (item.Parent != null)
            {
                // if parent item has a single child
                if (item.Index == 0 || item.Parent.Children.Count == 1)
                {
                    item.Parent.IsSelected = true;
                }
                // if parent item has multiple children
                else if (item.Parent.Children.Count > 1)
                {
                    // find the most expanded "deepest" subchild of sibling element
                    var toSelect = FindMostExpandedItem(item.Parent.Children[item.Index - 1]);
                    toSelect.IsSelected = true;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        private void ModuleDisplayNavigateDown(ModelSystemStructureDisplayModel item)
        {
            if (item.IsExpanded && item.Children != null && item.Children.Count > 0)
            {
                item.Children[0].IsSelected = true;
            }
            else
            {
                var toSelect = FindNextAncestor(item);
                if (item.Parent == toSelect.Parent && item.Index < item.Parent.Children.Count - 1
                    || item.Parent != toSelect.Parent)
                {
                    toSelect.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private ModelSystemStructureDisplayModel FindNextAncestor(ModelSystemStructureDisplayModel item)
        {
            if (item.Parent == null)
            {
                return item.Children != null && item.Children.Count > 0 ? item.Children[0] : item;
            }

            if (item.Index < item.Parent.Children.Count - 1)
            {
                return item.Parent.Children[item.Index + 1];
            }

            return FindNextAncestor(item.Parent);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveCurrentModule(-1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveCurrentModule(1);
        }

        /// <summary>
        /// Moves the currently selected module by position the specified delta (negative is up, positive down)
        /// </summary>
        /// <param name="deltaPosition"></param>
        public void MoveCurrentModule(int deltaPosition)
        {
            if (CurrentlySelected.Count > 0)
            {
                var parent = this._display.Session.GetParent(CurrentlySelected[0].BaseModel);
                // make sure they all have the same parent
                if (CurrentlySelected.Any(m => this._display.Session.GetParent(m.BaseModel) != parent))
                {
                    // if not ding and exit
                    SystemSounds.Asterisk.Play();
                    return;
                }

                var mul = deltaPosition < 0 ? 1 : -1;
                var moveOrder = CurrentlySelected
                    .Select((c, i) => new { Index = i, ParentIndex = parent.Children.IndexOf(c.BaseModel) })
                    .OrderBy(i => mul * i.ParentIndex);
                var first = moveOrder.First();
                this._display.Session.ExecuteCombinedCommands(
                    "Move Selected Modules",
                    () =>
                    {
                        foreach (var el in moveOrder)
                        {
                            var selected = CurrentlySelected[el.Index];
                            string error = null;
                            if (!selected.BaseModel.MoveModeInParent(deltaPosition, ref error))
                            {
                                SystemSounds.Asterisk.Play();
                                break;
                            }
                        }
                    });
                this._display.BringSelectedIntoView(CurrentlySelected[first.Index]);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="set"></param>
        private void SetMetaModuleStateForSelected(bool set)
        {
            this._display.Session.ExecuteCombinedCommands(
                set ? "Compose to Meta-Modules" : "Decompose Meta-Modules",
                () =>
                {
                    foreach (var selected in CurrentlySelected)
                    {
                        string error = null;
                        if (!selected.SetMetaModule(set, ref error))
                        {
                            MessageBox.Show(this._display.GetWindow(), error, "Failed to convert meta module.",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                });
            this._display.RefreshParameters();
        }





        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertToMetaModule_Click(object sender, RoutedEventArgs e)
        {
            SetMetaModuleStateForSelected(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertFromMetaModule_Click(object sender, RoutedEventArgs e)
        {
            SetMetaModuleStateForSelected(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Module_Clicked(object sender, RoutedEventArgs e)
        {
            this._display.SelectReplacement();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        if (EditorController.IsShiftDown() && EditorController.IsControlDown())
                        {
                            MoveCurrentModule(1);
                            e.Handled = true;
                        }

                        break;
                    case Key.Up:
                        if (EditorController.IsShiftDown() && EditorController.IsControlDown())
                        {
                            MoveCurrentModule(-1);
                            e.Handled = true;
                        }

                        break;
                    case Key.M:
                        if (EditorController.IsControlDown() && EditorController.IsShiftDown() &&
                            !EditorController.IsAltDown())
                        {
                            this.SetMetaModuleStateForSelected(true);
                            e.Handled = true;
                        }
                        else if (EditorController.IsControlDown() && EditorController.IsShiftDown() &&
                                 EditorController.IsAltDown())
                        {
                            this.SetMetaModuleStateForSelected(false);
                            e.Handled = true;
                        }

                        break;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GridCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ModuleDisplay.Focus();
        }

        /// <summary>
        /// </summary>
        /// <param name="treeView"></param>
        /// <see cref="http://stackoverflow.com/questions/1163801/wpf-treeview-with-multiple-selection" />
        public void AllowMultiSelection(TreeView treeView)
        {
            if (IsSelectionChangeActiveProperty == null)
            {
                return;
            }

            var selectedItems = new List<TreeViewItem>();
            treeView.SelectedItemChanged += (a, b) =>
            {
                var module = this._display.GetCurrentlySelectedControl();
                if (module == null)
                {
                    // disable the event to avoid recursion
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    selectedItems.ForEach(item => item.IsSelected = true);
                    // enable the event to avoid recursion
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                    return;
                }

                var treeViewItem = VisualUpwardSearch(module);
                if (treeViewItem == null)
                {
                    return;
                }

                var disableMultiple = _disableMultipleSelectOnce;
                _disableMultipleSelectOnce = false;
                var currentItem = treeView.SelectedItem as ModelSystemStructureDisplayModel;
                // allow multiple selection
                // when control key is pressed
                if (!disableMultiple && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    // suppress selection change notification
                    // select all selected items
                    // then restore selection change notifications
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    selectedItems.ForEach(item => item.IsSelected =
                        item != treeViewItem || !selectedItems.Contains(treeViewItem));
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                }
                else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
                         CurrentlySelected.Count > 0)
                {
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    // select the range
                    var lastSelected = CurrentlySelected.Last();
                    var lastTreeItem = selectedItems.Last();
                    var currentParent = VisualUpwardSearch(VisualTreeHelper.GetParent(treeViewItem));
                    var lastParent = VisualUpwardSearch(VisualTreeHelper.GetParent(lastTreeItem));
                    if (currentParent != null && currentParent == lastParent)
                    {
                        var itemGenerator = currentParent.ItemContainerGenerator;
                        var lastSelectedIndex = itemGenerator.IndexFromContainer(lastTreeItem);
                        var currentSelectedIndex = itemGenerator.IndexFromContainer(treeViewItem);
                        var minIndex = Math.Min(lastSelectedIndex, currentSelectedIndex);
                        var maxIndex = Math.Max(lastSelectedIndex, currentSelectedIndex);
                        for (var i = minIndex; i <= maxIndex; i++)
                        {
                            var innerTreeViewItem = itemGenerator.ContainerFromIndex(i) as TreeViewItem;
                            var innerModule = itemGenerator.Items[i] as ModelSystemStructureDisplayModel;
                            if (CurrentlySelected.Contains(innerModule))
                            {
                                CurrentlySelected.Remove(innerModule);
                            }

                            CurrentlySelected.Add(innerModule);
                            selectedItems.Add(innerTreeViewItem);
                        }
                    }

                    // select all of the modules that should be selected
                    selectedItems.ForEach(item => item.IsSelected = true);
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                    return;
                }
                else
                {
                    // deselect all selected items (current one will be re-added)
                    CurrentlySelected.Clear();
                    selectedItems.ForEach(item => item.IsSelected = item == treeViewItem);
                    selectedItems.Clear();
                }

                if (!selectedItems.Contains(treeViewItem))
                {
                    selectedItems.Add(treeViewItem);
                    CurrentlySelected.Add(currentItem);
                }
                else
                {
                    // deselect if already selected
                    CurrentlySelected.Remove(currentItem);
                    treeViewItem.IsSelected = false;
                    selectedItems.Remove(treeViewItem);
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as TreeViewItem;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisableModuleMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this.ToggleDisableModule();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.RemoveSelectedModules();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkedParametersMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.ShowLinkedParameterDialog();
        }



        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDisplay_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (CurrentlySelected.Count == 1)
            {
                MetaModuleConvertFrom.IsEnabled = CurrentlySelected[0].IsMetaModule;
                MetaModuleConvertTo.IsEnabled = !CurrentlySelected[0].IsMetaModule;

            }
            else
            {
                MetaModuleConvertFrom.IsEnabled = true;
                MetaModuleConvertTo.IsEnabled = true;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.RenameSelectedModule();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditDescriptionMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.RenameDescription();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PastMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.PasteCurrentModule();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            this._display.CopyCurrentModule();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);


            ModuleTreeViewItem module = (ModuleTreeViewItem)e.Data.GetData(typeof(ModuleTreeViewItem));

            var siblings = module.GetSiblingModuleTreeViewItems();

            // Loop over each sibling and determine the Y which this was dropped
            var position = e.GetPosition(siblings[0]);

            var newPosition = -1;
            var oldPosition = 0;
            int idx = 0;

            
            foreach (var sibling in siblings)
            {
                var transform = sibling.TransformToVisual(siblings[0]);
                var point = transform.Transform(new Point(0, 0));

                if (position.Y < point.Y && newPosition < 0)
                {
                    newPosition = idx;
                }

                if (sibling == module)
                {
                    oldPosition = idx;
                }

                idx++;

            }

            //check end
             var transformEnd = siblings.Last().TransformToVisual(siblings[0]);
             var pointEnd = transformEnd.Transform(new Point(0, 0));

            if (position.Y > pointEnd.Y + 25)
            {
                newPosition = siblings.Count-1;
            }

            if (newPosition >= 0 && newPosition != oldPosition)
            {
                this.MoveCurrentModule(newPosition - oldPosition);
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemTreeViewDisplay_OnDragOver(object sender, DragEventArgs e)
        {
            var position = e.GetPosition(this);


            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemTreeViewDisplay_OnDragEnter(object sender, DragEventArgs e)
        {
            return;
        }
    }
}
