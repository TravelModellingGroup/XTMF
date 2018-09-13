using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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





        private RegionDisplaysModel _regionDisplaysModel;

        /// <summary>
        /// </summary>
        /// <param name="modelSystemDisplay"></param>
        public ModelSystemRegionViewDisplay(ModelSystemDisplay modelSystemDisplay)
        {
            InitializeComponent();

            _modelSystemDisplay = modelSystemDisplay;
            _modelSystemDisplay.ModelSystemEditingSessionChanged += ModelSystemDisplay_ModelSystemEditingSessionChanged;
        }

        public ModelSystemStructureDisplayModel SelectedModule => throw new NotImplementedException();

        public ItemsControl ViewItemsControl => GroupDisplayList;

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionDisplaysOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _modelSystemDisplay.StatusSnackBar.MessageQueue.Enqueue(
                $"New region display '{((IRegionDisplay)e.NewItems[0]).Name}' Added");
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_ModelSystemEditingSessionChanged(object sender,
            ModelSystemEditingSessionChangedEventArgs e)
        {
            _modelSystemEditingSession = e.Session;

            _regionDisplaysModel = _modelSystemEditingSession.ModelSystemModel.RegionDisplaysModel;

            _regionDisplaysModel.RegionViewGroupsUpdated += RegionDisplaysModelOnRegionViewGroupsUpdated;

            UpdateRegionDisplayList();

            _regionDisplaysModel.RegionDisplays.CollectionChanged += RegionDisplaysOnCollectionChanged;
        }

        /// <summary>
        /// 
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


                if (_regionDisplaysModel.RegionDisplays.Count > 0)
                {
                    RegionsComboBox.SelectedIndex = 0;
                    GroupDisplayList.ItemsSource = _regionDisplaysModel.RegionDisplays[0].RegionGroups;


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
                _regionDisplaysModel.CreateNewRegionDisplay(dialog.UserInput, ref error);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task ShowCreateNewGroupDisplayDialog()
        {
            var dialog = new StringRequestDialog("Enter a name for the group.", s => s.Trim().Length > 0);
            try
            {
                var result = await dialog.ShowAsync(false);
                var error = "";
                _regionDisplaysModel.CreateNewGroupDisplay(((RegionDisplay)RegionsComboBox.SelectionBoxItem), dialog.UserInput, ref error);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void NewGroupButton_OnClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateNewGroupDisplayDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Console.WriteLine(RegionsComboBox.SelectionBoxItem);
            if (RegionsComboBox.SelectionBoxItem is RegionDisplay region)
            {
                
                GroupDisplayList.ItemsSource = region.RegionGroups;

            }

            
        }


    }

    
}