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
    /// Interaction logic for ModuleContextControl.xaml
    /// 
    /// The ModuleContextControl is a horizontal / slim control that displays a context path to the currently
    /// displayed module. Interaction is allowed to select different modules along the path and provide
    /// feedback for the user to see which set of modules the active module exists under.
    /// </summary>
    public partial class ModuleContextControl : UserControl
    {

        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty DisplayModelDependencyProperty =
          DependencyProperty.Register("DisplayModule", typeof(ModelSystemStructureDisplayModel), typeof(ModuleContextControl), new PropertyMetadata(null));


        public ModuleContextControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        public ModelSystemStructureDisplayModel DisplayModel
        {
            get
            {
                return (ModelSystemStructureDisplayModel)GetValue(DisplayModelDependencyProperty);
            }
            set
            {
                SetValue(DisplayModelDependencyProperty, value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);


            Console.WriteLine(DisplayModel);

           
            
        }


    }
}
