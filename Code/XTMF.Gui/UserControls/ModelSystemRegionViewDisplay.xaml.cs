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
using XTMF.Editing;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModelSystemRegionViewDisplay.xaml
    /// </summary>
    public partial class ModelSystemRegionViewDisplay : UserControl, IModelSystemView
    {

        private ModelSystemDisplay _modelSystemDisplay;

        private ModelSystemEditingSession _modelSystemEditingSession;

        private RegionDisplaysModel _regionDisplaysModel;

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
        private void ModelSystemDisplay_ModelSystemEditingSessionChanged(object sender, ModelSystemEditingSessionChangedEventArgs e)
        {
            this._modelSystemEditingSession = e.Session;

            this._regionDisplaysModel = this._modelSystemEditingSession.ModelSystemModel.RegionDisplaysModel;

            this.UpdateRegionDisplayList();


        }

        /// <summary>
        /// Updates the RegionDisplayList with new information
        /// </summary>
        private void UpdateRegionDisplayList()
        {
            Dispatcher.Invoke(() => { this.RegionsComboBox.DataContext = this._regionDisplaysModel; });
            
        }

        public ModelSystemStructureDisplayModel SelectedModule => throw new NotImplementedException();

        public ItemsControl ViewItemsControl => Placeholder;
    }
}
