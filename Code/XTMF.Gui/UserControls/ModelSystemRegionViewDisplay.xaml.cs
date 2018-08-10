using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using MaterialDesignThemes.Wpf;
using XTMF.Editing;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;
using XTMF.Interfaces;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModelSystemRegionViewDisplay.xaml
    /// </summary>
    public partial class ModelSystemRegionViewDisplay : UserControl, IModelSystemView
    {

        private readonly ModelSystemDisplay _modelSystemDisplay;

        private ModelSystemEditingSession _modelSystemEditingSession;

        private RegionDisplaysModel _regionDisplaysModel;

        private ObservableCollection<IRegionDisplay> _regionDisplays;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelSystemDisplay"></param>
        public ModelSystemRegionViewDisplay(ModelSystemDisplay modelSystemDisplay)
        {
            InitializeComponent();

            this._modelSystemDisplay = modelSystemDisplay;
            this._modelSystemDisplay.ModelSystemEditingSessionChanged += ModelSystemDisplay_ModelSystemEditingSessionChanged;
            
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionDisplaysOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this._modelSystemDisplay.StatusSnackBar.MessageQueue.Enqueue($"New region display '{((IRegionDisplay)e.NewItems[0]).Name}' Added");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_ModelSystemEditingSessionChanged(object sender, ModelSystemEditingSessionChangedEventArgs e)
        {
            this._modelSystemEditingSession = e.Session;

            this._regionDisplaysModel = this._modelSystemEditingSession.ModelSystemModel.RegionDisplaysModel;

            this._regionDisplaysModel.RegionDisplays.CollectionChanged += RegionDisplaysOnCollectionChanged;

            this.UpdateRegionDisplayList();


            

        }

        /// <summary>
        /// Updates the RegionDisplayList with new information
        /// </summary>
        private void UpdateRegionDisplayList()
        {
            Dispatcher.Invoke(() =>
            {
                this.RegionsComboBox.DataContext = this._regionDisplaysModel;
                if (this._regionDisplaysModel.RegionDisplays.Count > 0)
                {
                    this.RegionsComboBox.SelectedIndex = 0;
                }
            });
            
        }

        public ModelSystemStructureDisplayModel SelectedModule => throw new NotImplementedException();

        public ItemsControl ViewItemsControl => Placeholder;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddRegionDisplayButton_OnClick(object sender, RoutedEventArgs e)
        {
            await this.ShowCreateNewRegionDisplayDialog();
        }

        /// <summary>
        /// Shows the input dialog for creating a new region display
        /// </summary>
        private async Task ShowCreateNewRegionDisplayDialog()
        {
            var dialog = new StringRequestDialog("Enter a name for the region display.", s => s.Trim().Length > 0);
            try
            {
                var result = await dialog.ShowAsync(false);
                string error = "";
                this._regionDisplaysModel.CreateNewRegionDisplay(dialog.UserInput, ref error);


            }
            catch (Exception e)
            {
                
            }
        }
    }
}
