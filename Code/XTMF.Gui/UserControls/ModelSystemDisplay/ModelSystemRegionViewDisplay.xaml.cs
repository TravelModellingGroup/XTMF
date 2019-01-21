using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using XTMF.Editing;
using XTMF.Gui.Annotations;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;
using XTMF.Interfaces;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ModelSystemRegionViewDisplay.xaml
    /// </summary>
    public partial class ModelSystemRegionViewDisplay : UserControl, IModelSystemView, INotifyPropertyChanged
    {
        private readonly ModelSystemDisplay _modelSystemDisplay;

        private ModelSystemEditingSession _modelSystemEditingSession;

        public event PropertyChangedEventHandler PropertyChanged;

        private RegionDisplaysDisplayModel _regionDisplaysModel;

        /// <summary>
        /// </summary>
        /// <param name="modelSystemDisplay"></param>
        public ModelSystemRegionViewDisplay(ModelSystemDisplay modelSystemDisplay)
        {
            InitializeComponent();

            _modelSystemDisplay = modelSystemDisplay;
            _modelSystemDisplay.ModelSystemEditingSessionChanged += ModelSystemDisplay_ModelSystemEditingSessionChanged;
        }

        /// <summary>
        /// </summary>
        private ModelSystemStructureDisplayModel ActiveModule { get; set; }

        public ModelSystemStructureDisplayModel SelectedModule => ActiveModule;

        private ObservableCollection<ModelSystemStructureDisplayModel> ActiveGroupModules { get; set; }

        public ItemsControl ViewItemsControl => GroupDisplayList;

        /// <summary>
        /// 
        /// </summary>
        public bool IsNewGroupButtonEnabled
        {
            get

            {
                if (RegionsComboBox.Items.Count <= 0)
                {
                    return false;
                }
                else
                {
                    return this.RegionsComboBox.SelectedIndex >= 0 && this.RegionsComboBox.Items.Count >= 0;
                }
            }
            set => OnPropertyChanged(nameof(IsNewGroupButtonEnabled));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            NewGroupButton.DataContext = this;
            RegionsComboBox.DataContext = this;

            IsNewGroupButtonEnabled = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionDisplaysOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_ModelSystemEditingSessionChanged(object sender,
            ModelSystemEditingSessionChangedEventArgs e)
        {
            _modelSystemEditingSession = e.Session;
            IsNewGroupButtonEnabled = false;
            _regionDisplaysModel = new RegionDisplaysDisplayModel(_modelSystemEditingSession.ModelSystemModel.RegionDisplaysModel);

            _regionDisplaysModel.Model.RegionViewGroupsUpdated += RegionDisplaysModelOnRegionViewGroupsUpdated;

            UpdateRegionDisplayList();

            _regionDisplaysModel.Regions.CollectionChanged += RegionDisplaysOnCollectionChanged;


        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionDisplaysModelOnRegionViewGroupsUpdated(object sender, RegionViewGroupsUpdateEventArgs e)
        {
            GroupDisplayList.UpdateLayout();
        }

        /// <summary>
        ///     Updates the RegionDisplayList with new information
        /// </summary>
        private void UpdateRegionDisplayList()
        {
            Dispatcher.Invoke(() =>
            {
                RegionsComboBox.DataContext = _regionDisplaysModel;


                if (_regionDisplaysModel.Regions.Count > 0)
                {

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RegionsComboBox.SelectedIndex = 0;
                        GroupDisplayList.ItemsSource = _regionDisplaysModel.Regions[0].Groups;
                    }));

                }
                else
                {
                    IsNewGroupButtonEnabled = false;
                }
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddRegionDisplayButton_OnClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateNewRegionDisplayDialog();
        }

        /// <summary>
        ///     Shows the input dialog for creating a new region display
        /// </summary>
        private async Task ShowCreateNewRegionDisplayDialog()
        {
            var dialog = new StringRequestDialog("Enter a name for the region display.", s => s.Trim().Length > 0);
            try
            {
                var result = await dialog.ShowAsync(false);
                var error = "";
                _regionDisplaysModel.Model.CreateNewRegionDisplay(dialog.UserInput, ref error);
                UpdateRegionDisplayList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private async Task ShowCreateNewGroupDisplayDialog()
        {
            var dialog = new StringRequestDialog("Enter a name for the group.", s => s.Trim().Length > 0);
            try
            {
                var result = await dialog.ShowAsync(false);
                var error = "";
                var item = (RegionDisplayModel)RegionsComboBox.SelectionBoxItem;
                _regionDisplaysModel.Model.CreateNewGroupDisplay((RegionDisplay)item.Model,
                    dialog.UserInput, ref error);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void NewGroupButton_OnClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateNewGroupDisplayDialog();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (RegionsComboBox.SelectedIndex >= 0)
            {
                GroupDisplayList.ItemsSource = _regionDisplaysModel.Regions[RegionsComboBox.SelectedIndex].Groups;
                RemoveRegionDisplayButton.IsEnabled = true;
                EditRegionDisplayButton.IsEnabled = true;
                IsNewGroupButtonEnabled = true;
            }
            else
            {
                RemoveRegionDisplayButton.IsEnabled = false;
                EditRegionDisplayButton.IsEnabled = false;
                GroupDisplayList.ItemsSource = null;
                IsNewGroupButtonEnabled = false;
            }

        }




        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GroupDisplayList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupModuleListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ModelSystemStructure m = (ModelSystemStructure)((ListBox)sender).SelectedItem;
                if (m == null)
                {
                    return;
                }
                ActiveModule = this._modelSystemDisplay.ModelSystemDisplayModelMap[m];
                _modelSystemDisplay.RefreshParameters();
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleItem_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ListBox view = (ListBox)((TextBlock)sender as TextBlock).Tag;
                ;
                ModelSystemStructure m = (ModelSystemStructure)((ListBox)view).SelectedItem;
                ActiveModule = this._modelSystemDisplay.ModelSystemDisplayModelMap[m];

                Dispatcher.Invoke(() =>
                {
                    ActiveModule.IsSelected = true;
                    // this._modelSystemDisplay.StatusBarModuleNameTextBlock.Text = $"{ActiveModule.BaseModel.Type} [{ActiveModule.Name}]";
                });
            }));

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFromGroupMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            MenuItem element = (MenuItem)e.Source;
            ModelSystemStructure model = element.Tag as ModelSystemStructure;

            var target = (FrameworkElement)((FrameworkElement)((ContextMenu)element.Parent).PlacementTarget).Tag;

            var target2 = target.Tag as RegionGroupDisplayModel;
            target2.Model.Modules.Remove(model);
            ((RegionGroup)target2.Model).UpdateModules(target2.Model);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupHeader_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var headerTextInput = element.FindChild<TextBox>("HeaderTextInput");
            var headerText = element.FindChild<TextBlock>("HeaderText");

            if (headerTextInput.Visibility != Visibility.Visible)
            {
                ToggleRegionGroupRename(sender as FrameworkElement);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHeaderElement"></param>
        private void ToggleRegionGroupRename(FrameworkElement regionHeaderElement, bool save = true)
        {
            var element = regionHeaderElement as FrameworkElement;
            var headerTextInput = element.FindChild<TextBox>("HeaderTextInput");
            var headerText = element.FindChild<TextBlock>("HeaderText");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                headerText.Visibility = headerText.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
                headerTextInput.Visibility = headerTextInput.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

                if (headerTextInput.Visibility == Visibility.Visible)
                {
                    headerTextInput.CaretIndex = headerTextInput.SelectionLength;
                    headerTextInput.SelectionLength = headerTextInput.Text.Length;
                    headerTextInput.SelectionStart = 0;
                    headerTextInput.Focus();
                    Keyboard.Focus(headerTextInput);
                }
                else
                {
                    if (save)
                    {
                        (headerTextInput.Tag as RegionGroupDisplayModel).Model.Name = headerTextInput.Text;

                    }
                    else
                    {
                        headerTextInput.Text = (headerTextInput.Tag as RegionGroupDisplayModel).Model.Name;
                        // (headerTextInput.Tag as RegionGroupDisplayModel).Model.Name = (headerTextInput.Tag as RegionGroupDisplayModel).Model.Name;
                    }
                }
            }));
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupHeader_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (e.Key == Key.Enter)
            {
                ToggleRegionGroupRename(element, true);

            }
            else if (e.Key == Key.Escape)
            {
                ToggleRegionGroupRename(element, false);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeaderTextInput_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var element = ((FrameworkElement)sender).Parent;
            var headerTextInput = element.FindChild<TextBox>("HeaderTextInput");
            var headerText = element.FindChild<TextBlock>("HeaderText");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                headerText.Visibility = Visibility.Visible;
                headerTextInput.Visibility = Visibility.Collapsed;
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GroupCloseIcon_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ((sender as FrameworkElement)?.Tag as RegionGroupDisplayModel)?.Model;
            string error = "";
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show($"Are sure want to delete the region group \"{item.Name}\"?",
                "Confirm Deletion", System.Windows.MessageBoxButton.OKCancel);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                _regionDisplaysModel.Model.RemoveRegionGroup((RegionGroup)item, ref error);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveRegionDisplayButton_OnClick(object sender, RoutedEventArgs e)
        {
            var display = (RegionsComboBox.SelectedItem as RegionDisplayModel)?.Model;

            string error = "";
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show($"Are sure want to delete the region display \"{display.Name}\"?",
                "Confirm Deletion", System.Windows.MessageBoxButton.YesNoCancel);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                if (display != null)
                {
                    _regionDisplaysModel.Model.RemoveRegionDisplay((RegionDisplay)display, ref error);
                }
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionGroupGroupBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            (sender as FrameworkElement)?.Focus();
            Keyboard.Focus(sender as FrameworkElement);
            FocusManager.SetFocusedElement(GroupDisplay, sender as FrameworkElement);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                var element = FocusManager.GetFocusedElement(GroupDisplay);
                if (element is GroupBox groupBox)
                {
                    ToggleRegionGroupRename(groupBox.Header as FrameworkElement);
                }
                e.Handled = true;
            }
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToModuleInTreeView_Click(object sender, RoutedEventArgs e)
        {
            _modelSystemDisplay.ShowTreeViewDisplay();
            ModelSystemStructureDisplayModel module = this._modelSystemDisplay.ModelSystemDisplayModelMap[(sender as MenuItem)?.Tag as ModelSystemStructure];
            Dispatcher.BeginInvoke(new Action(() =>
            {
                module.IsSelected = true;
                _modelSystemDisplay.TreeViewDisplay.ExpandToRoot(module);
            }));

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void EditRegionDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            var display = (RegionsComboBox.SelectedItem as RegionDisplayModel)?.Model;
            var dialog = new StringRequestDialog("Enter a new name for the region display.", s => s.Trim().Length > 0);
            try
            {
                dialog.UserInput = display.Name;
                var result = await dialog.ShowAsync(false);
                display.Name = dialog.UserInput;
                UpdateRegionDisplayList();
            }
            catch (Exception e2)
            {
                Console.WriteLine(e2.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameGroupHeaderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = (sender as MenuItem);
            var p = menuItem.TemplatedParent;
            ToggleRegionGroupRename(p as FrameworkElement);
            return;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
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
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if((bool)e.NewValue == true)
            {

                this.ViewItemsControl.Items.Refresh();
            }

            return;
        }

        /// <summary>
        /// Click action for editing the region view display description (located bottom of the description field).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditDescriptionButon_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}