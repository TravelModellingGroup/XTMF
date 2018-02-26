using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using System.Xml;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Util;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SchedulerWindow.xaml
    /// </summary>
    public partial class SchedulerWindow : UserControl, INotifyPropertyChanged
    {

        private List<RunWindow> _runWindows;

        private FrameworkElement _activeContent;

        public FrameworkElement ActiveContent
        {
            get => _activeContent;
            set
            {
                _activeContent = value;
                OnPropertyChanged(nameof(ActiveContent));
            }
        }

        public ObservableCollection<RunWindow> RunCollection { get; } = new ObservableCollection<RunWindow>();


        /// <summary>
        /// Removes a RunWindow from the SchedulerWindow
        /// </summary>
        /// <param name="run"></param>
        public void CloseRun(RunWindow run)
        {
            SchedulerRunItem toRemove = null;
            foreach (SchedulerRunItem item in FinishedRuns.Items)
            {
                if (item.RunWindow == run)
                {
                    toRemove = item;
                    break;
                }
            }

            if (toRemove != null)
            {
                FinishedRuns.Items.Remove(toRemove);
            }

            var defaultd = Resources["DefaultDisplay"];
            Dispatcher.Invoke(() => { ActiveRunContent.DataContext = Resources["DefaultDisplay"]; });


            FinishedRuns.Items.Refresh();

        }

        public SchedulerWindow()
        {
            InitializeComponent();

            _runWindows = new List<RunWindow>();

            ActiveRunContent.DataContext = Resources["DefaultDisplay"];

        }

        /// <summary>
        /// Adds a new run to the scheduler window
        /// </summary>
        /// <param name="run"></param>
        public void AddRun(RunWindow run)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ActiveContent = run;
                ScheduledRuns.Items.Add(new SchedulerRunItem(run, this));
                ActiveRunContent.DataContext = run;
            }));


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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            SchedulerRunItem item = (sender as Button).Tag as SchedulerRunItem;
            Dispatcher.Invoke((new Action(() =>
            {


                FinishedRuns.Items.Remove(item);

            })));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="runItem"></param>
        public void RemoveFromActiveRuns(SchedulerRunItem runItem)
        {
            Dispatcher.Invoke((new Action(() =>
            {
                ScheduledRuns.Items.Remove(runItem);
                FinishedRuns.Items.Insert(0, runItem);

                var defaultd = Resources["DefaultDisplay"];
                Dispatcher.Invoke(() => { ActiveRunContent.DataContext = Resources["DefaultDisplay"]; });
            })));
        }

        /// <summary>
        /// 
        /// </summary>
        public class SchedulerRunItem : INotifyPropertyChanged
        {

            private string _statusText = String.Empty;
            private string _elapsedTime = String.Empty;
            private string _startTime = String.Empty;
            public RunWindow RunWindow { get; set; }
            private SchedulerWindow _schedulerWindow;

            private float _progress;

            public string StartTime
            {
                get => _startTime;
                set
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                }
            }

            public float Progress
            {
                get => _progress;
                set
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }

            public string ElapsedTime
            {
                get => _elapsedTime;
                set
                {
                    _elapsedTime = value;
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }

            public Action RunFinished;

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


            /// <summary>
            /// Constructor of the ScheduleRunItem, takes in the RunWindow (run control) in the constructor.
            /// </summary>
            /// <param name="runWindow"></param>
            public SchedulerRunItem(RunWindow runWindow, SchedulerWindow schedulerWindow)
            {
                Name = runWindow.Run.RunName;
                RunWindow = runWindow;
                _schedulerWindow = schedulerWindow;

                runWindow.UpdateRunStatus = (val) => { StatusText = val; };
                runWindow.UpdateElapsedTime = (val) => { ElapsedTime = val; };
                runWindow.UpdateRunProgress = (val) => { Progress = val; };
                runWindow.UpdateStartTime = UpdateStartTime;

                runWindow.OnRunFinished = OnRunFinished;
                runWindow.OnRuntimeError = OnRuntimeError;
                runWindow.OnValidationError = OnValidationError;
                runWindow.RuntimeError = RuntimeError;
                runWindow.OnRunStarted = OnRunStarted;

                StatusText = "Queud";

                //StartTime = (string) $"{RunWindow.StartTime:g}";
                Progress = 0;

            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="s"></param>
            private void UpdateStartTime(string s)
            {
                StartTime = (string)$"{s:g}";
            }

            private void OnRunStarted()
            {

            }

            /// <summary>
            /// 
            /// </summary>
            private void OnRunFinished()
            {
                _schedulerWindow.RemoveFromActiveRuns(this);
                MainWindow.Us.GlobalStatusSnackBar.MessageQueue.Enqueue("Model system run finished (" + Name + ")", "SCHEDULER",
                    () => MainWindow.Us.ShowSchedulerWindow());


                XtmfNotificationIcon.ShowNotificationBalloon(Name + " has finished executing.",
                    () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model System Run Finished");

            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="errorWithPath"></param>
            private void RuntimeError(ErrorWithPath errorWithPath)
            {

                StatusText = errorWithPath.Message;
                //Console.WriteLine(errorWithPath);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="errorWithPaths"></param>
            private void OnValidationError(List<ErrorWithPath> errorWithPaths)
            {
                StatusText = "Validation error occured";
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="errorWithPaths"></param>
            private void OnRuntimeError(List<ErrorWithPath> errorWithPaths)
            {
                StatusText = "Runtime error occured";
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenOutput_OnClick(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            SchedulerRunItem item = b.Tag as SchedulerRunItem;
            ;
            if (Directory.Exists(item?.RunWindow.Run.RunDirectory))
            {
                Process.Start(item.RunWindow.Run.RunDirectory);
            }
            else
            {
                MessageBox.Show(item.RunWindow.Run.RunDirectory + " does not exist!");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAllRunsButton_OnClick(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke((new Action(() =>
            {
                FinishedRuns.Items.Clear();
                ;
            })));
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_SizeChanged_1(object sender, SizeChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            GridView gridView = listView.View as GridView;
            var actualWidth = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth;
            for (var i = 1; i < gridView.Columns.Count; i++)
            {
                gridView.Columns[i].Width = actualWidth / gridView.Columns.Count;
                //gridView.ColumnHeaderContainerStyle.

            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ListView listView = sender as ListView;
            ContextMenu menu = listView?.ContextMenu;
            if (listView.SelectedItem != null)
            {
                MenuItem menuItem = new MenuItem();
                menuItem.Header = "Remove run from list";

                Dispatcher.Invoke(() =>
                {
                    menu.Items.Clear();
                });

                menuItem.Click += (o, args) =>
                {
                    Dispatcher.Invoke((new Action(() =>
                    {

                        FinishedRuns.Items.RemoveAt(FinishedRuns.SelectedIndex);
                        //FinishedRuns.Items.Remove(item);

                    })));
                };
                menu?.Items.Add(menuItem);
            }



            return;
        }

        /// <summary>
        /// Finished runs ListView selection changed event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var runWindow = (FinishedRuns.SelectedItem as SchedulerRunItem)?.RunWindow;
            ActiveRunContent.DataContext = runWindow;
            ActiveContent = runWindow;
        }
    }


}
