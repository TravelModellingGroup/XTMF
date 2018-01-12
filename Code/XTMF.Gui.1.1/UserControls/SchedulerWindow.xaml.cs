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
         
            ScheduledRuns.Items.Add(new SchedulerRunItem(run));

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var runWindow = (ScheduledRuns.SelectedItem as SchedulerRunItem)?.RunWindow;
            Console.WriteLine(runWindow?.Run.RunName);
            ActiveRunContent.Content = (ScheduledRuns.SelectedItem as SchedulerRunItem)?.RunWindow;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SchedulerRunItem
    {
 
        public RunWindow RunWindow { get; set; }

        public string Name { get; set; }

        public string StatusText { get; set; }

        public float ProgressValue { get; set; }

        /// <summary>
        /// Constructor of the ScheduleRunItem, takes in the RunWindow (run control) in the constructor.
        /// </summary>
        /// <param name="runWindow"></param>
        public SchedulerRunItem(RunWindow runWindow)
        {
            Name = runWindow.Run.RunName;
            RunWindow = runWindow;
        }
    }

    
}
