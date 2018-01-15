using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
using XTMF.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SchedulerWindow.xaml
    /// </summary>
    public partial class SchedulerWindow : UserControl, INotifyPropertyChanged
    {

        private List<RunWindow> _runWindows;

        private RunWindow _activeContent;

        public RunWindow ActiveContent
        {
            get => _activeContent;
            set
            {
                _activeContent = value;
                OnPropertyChanged(nameof(ActiveContent));
            }
        }


        /// <summary>
        /// Removes a RunWindow from the SchedulerWindow
        /// </summary>
        /// <param name="run"></param>
        public void CloseRun(RunWindow run)
        {
            
            ScheduledRuns.Items.Remove(run);

            if (ActiveRunContent.DataContext == run)
            {
                ActiveRunContent.DataContext = Resources["DefaultDisplay"];
            }
        }

        public SchedulerWindow()
        {
            InitializeComponent();

            _runWindows = new List<RunWindow>();
    
            ActiveRunContent.DataContext = Resources["DefaultDisplay"];

        }

        public void AddRun(RunWindow run)
        {
            //ActiveRunContent.Content = run;
            ActiveContent = run;
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
  
            
            ActiveRunContent.DataContext = runWindow;
            ActiveContent = runWindow;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SchedulerRunItem : INotifyPropertyChanged
    {

        private string _statusText = String.Empty;
        private string _elapsedTime = String.Empty;
        public RunWindow RunWindow { get; set; }

        public string ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                _elapsedTime = value;
                OnPropertyChanged(nameof(ElapsedTime));
            }
        }

        public string Name { get; set; }

        /// <summary>
        /// StatusText property
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public float ProgressValue { get; set; }

        /// <summary>
        /// Constructor of the ScheduleRunItem, takes in the RunWindow (run control) in the constructor.
        /// </summary>
        /// <param name="runWindow"></param>
        public SchedulerRunItem(RunWindow runWindow)
        {
            Name = runWindow.Run.RunName;
            RunWindow = runWindow;

            runWindow.UpdateRunStatus = (val) => { StatusText = val; };
            runWindow.UpdateElapsedTime = (val) => { ElapsedTime = val; };

        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    
}
