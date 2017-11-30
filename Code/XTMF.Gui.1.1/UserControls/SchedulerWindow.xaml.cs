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

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SchedulerWindow.xaml
    /// </summary>
    public partial class SchedulerWindow : UserControl
    {

        private List<RunWindow> _runWindows;

        public SchedulerWindow()
        {
            InitializeComponent();

            _runWindows = new List<RunWindow>();
        }

        public void AddRun(RunWindow run)
        {
            ActiveRunContent.Content = run;

        }
    }

    
}
