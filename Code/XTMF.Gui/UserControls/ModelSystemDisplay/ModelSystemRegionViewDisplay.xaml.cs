using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using XTMF.Editing;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;
using XTMF.Interfaces;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ModelSystemRegionViewDisplay.xaml
    /// </summary>
    public partial class ModelSystemRegionViewDisplay : UserControl, IModelSystemView
    {
        private readonly ModelSystemDisplay _modelSystemDisplay;

        private ModelSystemEditingSession _modelSystemEditingSession;


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
            Console.WriteLine(RegionsComboBox.SelectionBoxItem);
            if (RegionsComboBox.SelectedIndex >= 0)
            {
                GroupDisplayList.ItemsSource = _regionDisplaysModel.Regions[RegionsComboBox.SelectedIndex].Groups;
                RemoveRegionDisplayButton.IsEnabled = true;
            }
            else
            {
                RemoveRegionDisplayButton.IsEnabled = false;
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
                ModelSystemStructure m = (ModelSystemStructure) ((ListBox) sender).SelectedItem;
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
            Dispatcher.BeginInvoke(new Action(() => {
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

            var target = (FrameworkElement)((FrameworkElement) ((ContextMenu) element.Parent).PlacementTarget).Tag;

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
            ToggleRegionGroupRename(sender as FrameworkElement);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHeaderElement"></param>
        private void ToggleRegionGroupRename(FrameworkElement regionHeaderElement)
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
            if (e.Key == Key.Enter)
            {
                var element = sender as FrameworkElement;
                var headerTextInput = element.FindChild<TextBox>("HeaderTextInput");
                var headerText = element.FindChild<TextBlock>("HeaderText");
                if (headerTextInput.Visibility == Visibility.Visible)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        headerText.Visibility = headerText.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        headerTextInput.Visibility = headerTextInput.Visibility == Visibility.Collapsed
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }));
                }
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeaderTextInput_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var element = ((FrameworkElement) sender).Parent;
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
            _regionDisplaysModel.Model.RemoveRegionGroup((RegionGroup)item, ref error);
            
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
            if (display != null)
            {
                _regionDisplaysModel.Model.RemoveRegionDisplay((RegionDisplay) display, ref error);
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
            Console.WriteLine((sender as FrameworkElement).IsFocused);
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
    }
}