using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : UserControl
    {

        private List<Label> recentProjectLabels = new List<Label>();
        public StartWindow()
        {
            InitializeComponent();


          
        }

        private void NewProject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.NewProject();
        }

        private void OpenProject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.OpenProject();
        }

        private void NewModelSystem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.NewModelSystem();
        }

        private void OpenModelSystem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.OpenModelSystem();
        }

      
    }
}
