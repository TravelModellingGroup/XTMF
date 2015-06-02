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
    /// Interaction logic for LinkedParameterDisplay.xaml
    /// </summary>
    public partial class LinkedParameterDisplay : Window
    {
        public LinkedParameterDisplay(LinkedParametersModel linkedParameters)
        {
            InitializeComponent();
            Display.ItemsSource = LinkedParameterDisplayModel.CreateDisplayModel(linkedParameters.GetLinkedParameters());
            LinkedParameterFilterBox.Display = Display;
            LinkedParameterFilterBox.Filter = (o, text) =>
            {
                var model = o as LinkedParameterDisplayModel;
                return model.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };
        }

        private void BorderIconButton_Clicked(object obj)
        {

        }
    }
}
