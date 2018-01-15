/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for RunWindow.xaml
    /// </summary>
    public partial class RunWindow : UserControl, INotifyPropertyChanged
    {


        private XTMFRun _run;
        private string _runDirectory;
        private DateTime _startTime;
        private DispatcherTimer _timer;
        private bool _windows7OrAbove;
        private volatile bool _isActive;
        private volatile bool _isFinished;
        private volatile bool _wasCanceled;
        private static readonly Tuple<byte, byte, byte> ErrorColour;


        public Action<string> UpdateRunStatus { get; set; }
        public Action<float> UpdateRunProgress { get; set; }
        public Action<string> UpdateElapsedTime { get; set; }


        public XTMFRun Run
        {
            get =>  _run;
        }

        private readonly BindingListWithRemoving<SubProgress> _subProgressBars =
            new BindingListWithRemoving<SubProgress>();

        private BindingListWithRemoving<IProgressReport> _progressReports;

        private struct SubProgress
        {
            internal Label Name;
            internal TMGProgressBar ProgressBar;
        }

        /// <summary>
        /// Requires Windows 7
        /// </summary>
        private TaskbarItemInfo _taskbarInformation;

        public static readonly DependencyProperty IsRunCancellableDependencyProperty =
            DependencyProperty.Register("IsRunCancellable", typeof(bool), typeof(RunWindow), new PropertyMetadata(false));


        public static readonly DependencyProperty IsRunClearableDependencyProperty =
            DependencyProperty.Register("IsRunClearable", typeof(bool), typeof(RunWindow), new PropertyMetadata(false));

        public bool IsRunClearable
        {
            get => !_isActive && (bool)GetValue(IsRunClearableDependencyProperty);
            set => SetValue(IsRunClearableDependencyProperty, value);
        }

        public bool IsRunCancellable
        {
            get => (bool)GetValue(IsRunCancellableDependencyProperty);
            set => SetValue(IsRunCancellableDependencyProperty, _run != null);
        }

        static RunWindow()
        {
            var findResource = Application.Current.FindResource("WarningRed");
            if (findResource != null)
            {
                var errorColour = (Color)findResource;
                ErrorColour = new Tuple<byte, byte, byte>(errorColour.R, errorColour.G, errorColour.B);
            }
        }

        private void MainWindowClosing(object sender, CancelEventArgs e)
        {
            if (_isActive)
            {
                MessageBoxResult result = MessageBox.Show("A run is currently active. Are you sure you wish to close XTMF?", "Run Currently Active", MessageBoxButton.YesNoCancel);
                e.Cancel = result != MessageBoxResult.Yes;
            }
        }

        public class ConsoleOutputController : INotifyPropertyChanged, IDisposable
        {

            public ConsoleOutputController(RunWindow runWindow, XTMFRun run)
            {
                run.RunMessage += Run_RunMessage;
            }

            private void Run_RunMessage(string message)
            {
                ConsoleOutput = ConsoleOutput + message + "\r\n";
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConsoleOutput)));
            }

            public string ConsoleOutput { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            
            public void Dispose()
            {
                ConsoleOutput = String.Empty;
            }
        }

        private int _consoleLength;

        private int _oldCaret;

        public event PropertyChangedEventHandler PropertyChanged;

        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newTextLength = ConsoleOutput.Text.Length;
            if (_oldCaret >= _consoleLength)
            {
                ConsoleScrollViewer.ScrollToEnd();
                ConsoleOutput.CaretIndex = newTextLength;
            }
            else
            {
                ConsoleOutput.CaretIndex = _oldCaret;
            }
            _consoleLength = newTextLength;
        }

        private static Window GetWindow(DependencyObject current)
        {
            while (current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        public Action<List<ErrorWithPath>> ValidationError;

        public Action<List<ErrorWithPath>> RuntimeValidationError;

        public Action<ErrorWithPath> RuntimeError;

        public RunWindow(ModelSystemEditingSession session, XTMFRun run, string runName)
        {
            InitializeComponent();
            Session = session;
            session.SessionClosed += Session_SessionClosed;
            _run = run;
            MainWindow.Us.Closing += MainWindowClosing;
            OpenDirectoryButton.IsEnabled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {

                RunNameLabel.Text = runName;
                IsRunClearable = false;
            }));
            _progressReports = _run.Configuration.ProgressReports;
            _progressReports.ListChanged += ProgressReports_ListChanged;
            _progressReports.BeforeRemove += ProgressReports_BeforeRemove;
            _subProgressBars.ListChanged += SubProgressBars_ListChanged;
            _subProgressBars.BeforeRemove += SubProgressBars_BeforeRemove;
            _run.RunCompleted += Run_RunComplete;
            _run.RunStarted += Run_RunStarted;
            _run.RuntimeError += Run_RuntimeError;
            _run.RuntimeValidationError += Run_RuntimeValidationError;
            _run.ValidationStarting += Run_ValidationStarting;
            _run.ValidationError += Run_ValidationError;
            _runDirectory = _run.RunDirectory;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(value: 33) };
            _isFinished = false;
            _wasCanceled = false;
            _timer.Tick += Timer_Tick;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var major = Environment.OSVersion.Version.Major;
                if (major > 6 || major >= 6 && Environment.OSVersion.Version.Minor >= 1)
                {
                    _windows7OrAbove = true;
                    MainWindow.Us.TaskbarItemInfo = _taskbarInformation = new TaskbarItemInfo();
                    _taskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                    _taskbarInformation.ProgressValue = 0;
                }
            }
            ConsoleOutput.DataContext = new ConsoleOutputController(this, _run);
            ConsoleBorder.DataContext = ConsoleOutput.DataContext;
            StartRunAsync();
            _timer.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                lock (this)
                {
                    if (!_isActive)
                    {
                        return;
                    }
                    if (_run != null)
                    {
                        if (_isFinished)
                        {
                            return;
                        }
                        float progress = 1;
                        var colour = ErrorColour;
                        try
                        {
                            var status = _run.PollStatusMessage();
                            ;
                            if (status != null)
                            {
                                StatusLabel.Text = status;
                                UpdateRunStatus?.Invoke(status);
                            }
                            progress = _run.PollProgress();
                            colour = _run.PollColour();
                        }
                        catch
                        {
                            // ignored
                        }
                        progress = Math.Max(Math.Min(progress * 10000, 10000), 0);
                        if (colour != null)
                        {
                            ProgressBar.SetForgroundColor(Color.FromRgb(colour.Item1, colour.Item2, colour.Item3));
                        }
                        ProgressBar.Value = progress;
                        if (_windows7OrAbove)
                        {
                            _taskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                            _taskbarInformation.ProgressValue = progress / 10000;
                        }

                        if (_subProgressBars.Count > 0)
                        {
                            /* Resize the column */
                            BaseGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
                        }
                        else
                        {
                            BaseGrid.ColumnDefinitions[0].Width = new GridLength(0);
                        }
                        for (var i = 0; i < _subProgressBars.Count; i++)
                        {
                            try
                            {
                                progress = _progressReports[i].GetProgress();
                                progress = progress * 10000;
                                if (progress > 10000)
                                {
                                    progress = 10000;
                                }
                                if (progress < 0)
                                {
                                    progress = 0;
                                }
                                _subProgressBars[i].ProgressBar.Value = progress;
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        var elapsedTime = DateTime.Now - _startTime;
                        var days = elapsedTime.Days;
                        elapsedTime = new TimeSpan(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                        ElapsedTimeLabel.Content = days < 1 ?
                            $"Elapsed Time: {elapsedTime:g}" : $"Elapsed Time: {elapsedTime} Day(s), {days:g}";
                        UpdateElapsedTime?.Invoke(days < 1 ?
                            $"{elapsedTime:g}" : $"{elapsedTime} Day(s), {days:g}");
                    }
                    else
                    {
                        ProgressBar.Value = _isFinished ? 10000 : 0;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private void Run_ValidationError(List<ErrorWithPath> errors)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Validation Error", errors[0]);
                ValidationError?.Invoke(errors);
            });
        }

        private void ShowErrorMessage(string title, ErrorWithPath error)
        {
            new ErrorWindow
            {
                Owner = GetWindow(this),
                Title = String.IsNullOrEmpty(title) ? "Error" : title,
                ErrorMessage = error.Message,
                ErrorStackTrace = error.StackTrace
            }.ShowDialog();
        }

        private static void Run_ValidationStarting()
        {
        }

        private void Run_RuntimeValidationError(List<ErrorWithPath> errors)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage(string.Empty, errors[0]);
                RuntimeValidationError?.Invoke(errors);
            });
        }

        private void Run_RuntimeError(ErrorWithPath error)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Runtime Error", error);
                RuntimeError?.Invoke(error);
            });
        }

        private void SetRunFinished()
        {
            if (_taskbarInformation != null)
            {
                _taskbarInformation.ProgressState = _wasCanceled
                    ? TaskbarItemProgressState.Error
                    : TaskbarItemProgressState.Indeterminate;
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await Dispatcher.BeginInvoke(
                        new Action(() => { _taskbarInformation.ProgressState = TaskbarItemProgressState.None; }));
                });
            }
            _isFinished = true;
            MainWindow.Us.Closing -= MainWindowClosing;
            Dispatcher.BeginInvoke((Action)(() =>
           {
               IsRunClearable = true;
               ProgressBar.Finished = true;
               ContinueButton.IsEnabled = true;
               ProgressBar.Value = ProgressBar.Maximum;
               CancelButton.IsEnabled = false;
               ButtonProgressAssist.SetIsIndeterminate(CancelButton, false);
               ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, false);
               StatusLabel.Text = _wasCanceled ? "Run Canceled" : "Run Complete";

 
               UpdateRunStatus?.Invoke(_wasCanceled ? "Run Canceled" : "Run Complete");
               ProgressBar.Finished = true;
               MainWindow.Us.UpdateStatusDisplay("Ready");
               MainWindow.Us.HideStatusLink();

           }));
        }

        private void Run_RunStarted()
        {
            _isActive = true;

            Dispatcher.BeginInvoke((Action)(() =>
            {
                CancelButton.IsEnabled = true;
                ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, true);
                ButtonProgressAssist.SetIsIndeterminate(CancelButton,true);
                //ButtonProgressAssist.
                IsRunClearable = false;
            }));
        }

        private void Run_RunComplete()
        {
            try
            {
                Dispatcher.Invoke(SetRunFinished);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Starts the run asynchronously
        /// </summary>
        private void StartRunAsync()
        {
            _startTime = DateTime.Now;
            StartTimeLabel.Content = $"Start Time: {_startTime:g}";
        }

        private void Session_SessionClosed(object sender, EventArgs e)
        {
            MainWindow.Us.CloseWindow(this);
        }

        public ModelSystemEditingSession Session { get; private set; }

        private void OpenDirectoryButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_runDirectory))
            {
                Process.Start(_runDirectory);
            }
            else
            {
                MessageBox.Show(_runDirectory + " does not exist!");
            }
        }

        private void CancelButton_Clicked(object sender, RoutedEventArgs e)
        {
            //Are you sure?
            if (MessageBox.Show(MainWindow.Us, "Are you sure you want to cancel this run?", "Cancel run?",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                if (_run != null)
                {
                    _run.DeepExitRequest();
                    _wasCanceled = _run.ExitRequest();
                    _wasCanceled = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, false);
                        ButtonProgressAssist.SetIsIndeterminate(CancelButton, false);
                        CancelButton.IsEnabled = false;
                        MainWindow.Us.UpdateStatusDisplay("Ready");
                        MainWindow.Us.HideStatusLink();
                    }));
                }
            }
        }

        private void ProgressReports_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            _subProgressBars.RemoveAt(e.NewIndex);
        }

        private void ProgressReports_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = _progressReports[e.NewIndex];
                Dispatcher.Invoke(delegate
                {
                    AdditionDetailsPanelBorder.Visibility = Visibility.Visible;
                    AdditionDetailsPanelBorder.Height = double.NaN;
                    var progressBar = new TMGProgressBar
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0x22, 0x22)),
                        Maximum = 10000,
                        Minimum = 0,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Height = 15
                    };
                    if (toAdd.Colour != null)
                    {
                        progressBar.SetForgroundColor(Color.FromRgb(toAdd.Colour.Item1, toAdd.Colour.Item2,
                            toAdd.Colour.Item3));
                    }
                    _subProgressBars.Add(new SubProgress
                    {

                        Name = new Label { Content = toAdd.Name, Foreground = Brushes.White },
                        ProgressBar = progressBar
                    });

                    BaseGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
                    ;
                });
            }
            else if (e.ListChangedType == ListChangedType.ItemDeleted)
            {
                if (_progressReports.Count == 0)
                {
                    Dispatcher.Invoke(delegate
                    {
                        AdditionDetailsPanelBorder.Visibility = Visibility.Collapsed;
                        AdditionDetailsPanelBorder.Height = 0;
                    });
                }
            }
        }

        private void SubProgressBars_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            lock (this)
            {
                Dispatcher.Invoke(delegate
                {
                    var toRemove = _subProgressBars[e.NewIndex];
                    AdditionDetailsPanel.Remove(toRemove.Name);
                    AdditionDetailsPanel.Remove(toRemove.ProgressBar);
                });
            }
        }

        private void SubProgressBars_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = _subProgressBars[e.NewIndex];
                AdditionDetailsPanel.Add(toAdd.Name);
                AdditionDetailsPanel.Add(toAdd.ProgressBar);
            }
        }

        internal bool CloseRequested()
        {
            if (_isFinished)
            {
                return true;
            }
            Dispatcher.Invoke(() => { MainWindow.ShowPageContaining(this); });
            //Are you sure?
            var window = GetWindow(this);
            var message = "Are you sure you want to cancel the run '" + _run.RunName + "'?";
            if (window == null
                ? MessageBox.Show(message, "Cancel run?",
                      MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes
                : MessageBox.Show(window, message, "Cancel run?",
                      MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                lock (this)
                {
                    _wasCanceled = true;
                    _isActive = false;
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ButtonProgressAssist.SetIsIndicatorVisible(CancelButton,false);
                        ButtonProgressAssist.SetIsIndeterminate(CancelButton, false);
                        CancelButton.IsEnabled = false;
                    }));
                    _timer.Stop();
                    _run.TerminateRun();
                    return true;
                }
            }
            return false;
        }

        private void ClearRunButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusLabel.Text = string.Empty;
                ProgressBar.Finished = false;
                ProgressBar.Value = ProgressBar.Minimum;
                IsRunClearable = false;
                IsRunCancellable = false;
                ElapsedTimeLabel.Content = string.Empty;
                StartTimeLabel.Content = string.Empty;
                _runDirectory = string.Empty;
                OpenDirectoryButton.IsEnabled = false;
                ConsoleOutput.Clear();
            }));
        }

        /// <summary>
        /// Removes the RunWindow / control from the scheduler window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.SchedulerWindow.CloseRun(this);
        }
    }

    public class RunButtonTemplateSelecctor : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
           

            return null;
        }
    }
}
