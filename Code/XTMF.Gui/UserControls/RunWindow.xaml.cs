/*
    Copyright 2015-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for RunWindow.xaml
    /// </summary>
    public partial class RunWindow : UserControl, INotifyPropertyChanged, IDisposable
    {
        private static readonly Tuple<byte, byte, byte> ErrorColour;

        public static readonly DependencyProperty IsRunCancellableDependencyProperty =
            DependencyProperty.Register("IsRunCancellable", typeof(bool), typeof(RunWindow),
                new PropertyMetadata(false));


        public static readonly DependencyProperty IsRunClearableDependencyProperty =
            DependencyProperty.Register("IsRunClearable", typeof(bool), typeof(RunWindow), new PropertyMetadata(false));

        private readonly BindingListWithRemoving<IProgressReport> _progressReports;

        private readonly BindingListWithRemoving<SubProgress> _subProgressBars =
            new BindingListWithRemoving<SubProgress>();

        /// <summary>
        ///     Requires Windows 7
        /// </summary>
        private readonly TaskbarItemInfo _taskbarInformation;

        private readonly DispatcherTimer _timer;
        private readonly bool _windows7OrAbove;

        private int _consoleLength;
        private volatile bool _isActive;
        private volatile bool _isFinished;

        public Visibility ProgressReportsVisibility => _subProgressBars.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        private ModelSystemDisplay _launchedFromModelSystemDisplay;

        private int _oldCaret;

        private string _runDirectory;

        private bool _runtimeValidationErrorOccured;
        private volatile bool _wasCanceled;

        public Action<bool> OnRunFinished;

        public Action OnRunStarted;

        //public Action<List<ErrorWithPath>> OnRuntimeError;

        public Action<List<ErrorWithPath>> OnValidationError;

        public Action<ErrorWithPath> RuntimeError;

        public Action<List<ErrorWithPath>> RuntimeValidationError;

        public SchedulerWindow SchedulerWindow { get; set; }

        //Display model reference in the scheduler window
        public SchedulerWindow.SchedulerRunItemDisplayModel SchedulerRunItemDisplayModel { get; set; }

        /// <summary>
        /// log4net logger for this run instance (file appender).
        /// </summary>
        private ILog iLog;

        private ConsoleOutputAppender _consoleAppender;

        private FileAppender _fileAppender;

        static RunWindow()
        {
            ErrorColour = new Tuple<byte, byte, byte>(200, 20, 30);
        }

        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <param name="immediateRun"></param>
        /// <param name="launchedFrom"></param>
        public RunWindow(ModelSystemEditingSession session, XTMFRun run, string runName, bool immediateRun = false,
            ModelSystemDisplay launchedFrom = null, SchedulerWindow schedulerWindow = null)
        {
            InitializeComponent();
            ErrorVisibility = Visibility.Collapsed;
            Session = session;
            Run = run;
            OpenDirectoryButton.IsEnabled = true;
            SchedulerWindow = schedulerWindow;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RunNameLabel.Text = runName;
                RunNameText.Text = runName;
                IsRunClearable = false;
            }));

            if (launchedFrom != null)
            {
                _launchedFromModelSystemDisplay = launchedFrom;
            }

            _progressReports = Run.Configuration.ProgressReports;
            _progressReports.ListChanged += ProgressReports_ListChanged;
            _progressReports.BeforeRemove += ProgressReports_BeforeRemove;
            _subProgressBars.ListChanged += SubProgressBars_ListChanged;
            _subProgressBars.BeforeRemove += SubProgressBars_BeforeRemove;
            Run.RunCompleted += Run_RunComplete;
            Run.RunStarted += Run_RunStarted;
            Run.RuntimeError += Run_RuntimeError;
            Run.RuntimeValidationError += Run_RuntimeValidationError;
            Run.ValidationStarting += RunOnValidationStarting;
            Run.ValidationError += RunOnValidationError;

            ErrorGroupBox.Visibility = Visibility.Collapsed;
            BaseGrid.RowDefinitions[1].Height = new GridLength(0);
            _runDirectory = Run.RunDirectory;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
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
            DetailsGroupBox.DataContext = this;
            ConfigureLogger();
            var conc = new ConsoleOutputController(Run, iLog);
            ConsoleOutput.DataContext = conc;
            _consoleAppender.ConsoleOutputController = conc;

            ConsoleBorder.DataContext = ConsoleOutput.DataContext;
            session.ExecuteRun(run, immediateRun);
            StartRunAsync();
            _timer.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ConfigureLogger()
        {
            var repos = LogManager.GetAllRepositories();
            ILoggerRepository repo = null;
            foreach (var r in repos)
            {
                if (r.Name == Run.RunName)
                {
                    repo = r;
                }
            }
            if (repo == null)
            {
                repo = LogManager.CreateRepository(Run.RunName);
            }

            _fileAppender = new log4net.Appender.RollingFileAppender
            {
                Name = "RollingFileAppender",
                File = Path.Combine(Run.RunDirectory, "XTMF.Console.log"),
                StaticLogFileName = true,
                AppendToFile = false,
                RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Once,
                MaxSizeRollBackups = 10,
                MaximumFileSize = "10MB",
                PreserveLogFileNameExtension = true
            };
            var layout = new log4net.Layout.PatternLayout()
            {
                ConversionPattern = "%date %-5level %logger - %message%newline"
            };
            _fileAppender.Layout = layout;
            layout.ActivateOptions();
            _consoleAppender = new ConsoleOutputAppender()
            {
                Layout = layout
            };
            //Let log4net configure itself based on the values provided
            _fileAppender.ActivateOptions();
            log4net.Config.BasicConfigurator.Configure(repo, _fileAppender, _consoleAppender);
            iLog = LogManager.GetLogger(Run.RunName, Run.RunName);
            
        }

        ~RunWindow()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool managed)
        {
            LogManager.ShutdownRepository(Run.RunName);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newQueuePosition"></param>
        public void ReorderRun(int newQueuePosition)
        {
            Session.ReorderQueuedRun(Run, newQueuePosition);
        }

        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <param name="immediateRun"></param>
        /// <param name="launchedFrom"></param>
        public RunWindow(ModelSystemEditingSession session, XTMFRun run, string runName, DateTime delayedStartTime,
            ModelSystemDisplay launchedFrom = null, SchedulerWindow schedulerWindow = null)
        {
            InitializeComponent();
            ErrorVisibility = Visibility.Collapsed;
            Session = session;
            Run = run;
            SchedulerWindow = schedulerWindow;
            OpenDirectoryButton.IsEnabled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RunNameLabel.Text = runName;
                RunNameText.Text = runName;
                IsRunClearable = false;
            }));
            if (launchedFrom != null)
            {
                _launchedFromModelSystemDisplay = launchedFrom;
            }
            _progressReports = Run.Configuration.ProgressReports;
            _progressReports.ListChanged += ProgressReports_ListChanged;
            _progressReports.BeforeRemove += ProgressReports_BeforeRemove;
            _subProgressBars.ListChanged += SubProgressBars_ListChanged;
            _subProgressBars.BeforeRemove += SubProgressBars_BeforeRemove;
            Run.RunCompleted += Run_RunComplete;
            Run.RunStarted += Run_RunStarted;
            Run.RuntimeError += Run_RuntimeError;
            Run.RuntimeValidationError += Run_RuntimeValidationError;
            Run.ValidationStarting += RunOnValidationStarting;
            Run.ValidationError += RunOnValidationError;

            ErrorGroupBox.Visibility = Visibility.Collapsed;
            BaseGrid.RowDefinitions[1].Height = new GridLength(0);
            _runDirectory = Run.RunDirectory;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
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
            ConfigureLogger();
            var conc = new ConsoleOutputController(Run, iLog);
            ConsoleOutput.DataContext = conc;
            _consoleAppender.ConsoleOutputController = conc;
            ConsoleBorder.DataContext = ConsoleOutput.DataContext;
            session.ExecuteDelayedRun(run, delayedStartTime);
            DetailsGroupBox.DataContext = this;
            StartRunAsync();
            _timer.Start();
        }

        public Action OnRuntimeValidationError { get; set; }

        public Action OnRuntimeError { get; set; }

        public Visibility ErrorVisibility
        {
            get => ErrorListView.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            set => OnPropertyChanged(nameof(ErrorVisibility));
        }

        public Action<string> UpdateRunStatus { get; set; }
        public Action<float> UpdateRunProgress { get; set; }
        public Action<string> UpdateElapsedTime { get; set; }
        public Action<string> UpdateStartTime { get; set; }

        public XTMFRun Run { get; }

        public bool IsRunClearable
        {
            get => !_isActive && (bool)GetValue(IsRunClearableDependencyProperty);
            set => SetValue(IsRunClearableDependencyProperty, value);
        }

        public bool IsRunCancellable
        {
            get => (bool)GetValue(IsRunCancellableDependencyProperty);
            set => SetValue(IsRunCancellableDependencyProperty, Run != null);
        }

        public DateTime StartTime { get; private set; }

        public ModelSystemEditingSession Session { get; }

        /// <summary>
        /// Scrolls the console output and caret to the bottom of the containing textbox.
        /// </summary>
        public void ScrollToBottomOfConsole()
        {
            if (ConsoleOutput.Text.Length > 0)
            {
                ConsoleOutput.CaretIndex = ConsoleOutput.Text.Length - 1;
                ConsoleOutput.ScrollToEnd();
                ConsoleScrollViewer.ScrollToBottom();
                ConsoleScrollViewer.ScrollToEnd();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RunOnValidationStarting()
        {
            UpdateRunStatus?.Invoke("Validation starting");
        }

        /// <summary>
        ///     Callback method invokved by XTMF to notify of a validation error in the model system.
        /// </summary>
        /// <param name="errorWithPaths"></param>
        private void RunOnValidationError(List<ErrorWithPath> errorWithPaths)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished(false);
                _consoleAppender.Close();
                ShowErrorMessages(errorWithPaths.ToArray());
                OnValidationError?.Invoke(errorWithPaths);
            });
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindowClosing(object sender, CancelEventArgs e)
        {
            if (_isActive)
            {
                var result = MessageBox.Show("A run is currently active. Are you sure you wish to close XTMF?",
                    "Run Currently Active", MessageBoxButton.YesNoCancel);
                e.Cancel = result != MessageBoxResult.Yes;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newTextLength = ConsoleOutput.Text.Length;
            if (_oldCaret >= _consoleLength)
            {
                ConsoleOutput.Select(ConsoleOutput.Text.Length - 1, 0);
            }
            else
            {
                ConsoleOutput.Select(ConsoleOutput.Text.Length - 1, 0);
            }
            _consoleLength = newTextLength;
        }

        /// <summary>
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

                    if (Run != null)
                    {
                        if (_isFinished)
                        {
                            return;
                        }

                        float progress = 1;
                        var colour = ErrorColour;
                        try
                        {
                            var status = Run.PollStatusMessage();
                            ;
                            if (status != null)
                            {
                                StatusLabel.Text = status;
                                UpdateRunStatus?.Invoke(status);
                            }

                            progress = Run.PollProgress();
                            colour = Run.PollColour();
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
                        UpdateRunProgress.Invoke(progress);
                        if (_windows7OrAbove)
                        {
                            _taskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                            _taskbarInformation.ProgressValue = progress / 10000;
                        }

                        BaseGrid.ColumnDefinitions[0].Width = _subProgressBars.Count > 0
                            ? new GridLength(2, GridUnitType.Star)
                            : new GridLength(0);

                        for (var i = 0; i < _subProgressBars.Count; i++)
                        {
                            try
                            {
                                progress = _progressReports[i].GetProgress();
                                progress *= 10000;
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

                        var elapsedTime = DateTime.Now - StartTime;
                        
                        elapsedTime = new TimeSpan(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                        var days = elapsedTime.Days;
                        ElapsedTimeLabel.Content = days < 1
                            ? $"{elapsedTime:g}"
                            : $"{days} Day(s), {elapsedTime.ToString("hh\\:mm\\:ss")}";
                        UpdateElapsedTime?.Invoke(days < 1
                            ? $"{elapsedTime:g}"
                            : $"{days} Day(s), {elapsedTime.ToString("hh\\:mm\\:ss")}");
                    }
                    else
                    {
                        ProgressBar.Value = _isFinished ? 10000 : 0;
                        UpdateRunProgress.Invoke(_isFinished ? 10000 : 0);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        ///     Appends the ErrorWithPath array to the ErrorListView that becomes visible underneath the console output.
        /// </summary>
        /// <param name="errors"></param>
        private void ShowErrorMessages(ErrorWithPath[] errors)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SchedulerRunItemDisplayModel?.ModelSystemErrors.Clear();
                foreach (var error in errors)
                {
                    var displayError = new ModelSystemErrorDisplayModel(error.Message, error.ModuleName,
                        error.StackTrace, error, this);
                    ErrorListView.Items.Add(displayError);
                    SchedulerRunItemDisplayModel?.ModelSystemErrors.Add(displayError);
                }
            }));
        }

        /// <summary>
        /// </summary>
        /// <param name="errors"></param>
        private void Run_RuntimeValidationError(List<ErrorWithPath> errors)
        {
            _runtimeValidationErrorOccured = true;
            Dispatcher.Invoke(() =>
            {
                ShowErrorMessages(errors.ToArray());
                SetRunFinished(false);
                UpdateRunStatus?.Invoke("Runtime validation error");
                RuntimeValidationError?.Invoke(errors);
                OnRuntimeValidationError?.Invoke();
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="error"></param>
        private void Run_RuntimeError(ErrorWithPath error)
        {
            Dispatcher.Invoke(() =>
            {
                ShowErrorMessages(new[] { error });
                SetRunFinished(false);
                UpdateRunStatus?.Invoke("Runtime Error");                
                RuntimeError?.Invoke(error);
                OnRuntimeError?.Invoke();
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="callback"></param>
        private void SetRunFinished(bool callback = true)
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
            _fileAppender.Close();
            _consoleAppender.Close();
            _isFinished = true;
            MainWindow.Us.Closing -= MainWindowClosing;
            Dispatcher.Invoke(() =>
            {
                IsRunClearable = true;
                ProgressBar.Finished = true;
                ProgressBar.Value = ProgressBar.Maximum;
                UpdateRunProgress(ProgressBar.Maximum);
                CancelButton.IsEnabled = false;
                ButtonProgressAssist.SetIsIndeterminate(CancelButton, false);
                ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, false);
                StatusLabel.Text = _wasCanceled ? "Run Canceled" : "Run Complete";
                ProgressBar.Finished = true;

                //call scheduler window callback
                if (callback)
                {
                    OnRunFinished(!_wasCanceled && !_runtimeValidationErrorOccured);
                }
            });
        }

        /// <summary>
        /// </summary>
        private void Run_RunStarted()
        {
            _isActive = true;
            Dispatcher.BeginInvoke((Action)(() =>
           {
               CancelButton.IsEnabled = true;

               ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, true);
               ButtonProgressAssist.SetIsIndeterminate(CancelButton, true);
               OnRunStarted?.Invoke();
               StartTime = DateTime.Now;
               StartTimeLabel.Content = $"Start Time: {StartTime:g}";
               UpdateStartTime?.Invoke($"{StartTime:g}");
               //ButtonProgressAssist.
               IsRunClearable = false;
           }));
        }

        /// <summary>
        /// </summary>
        private void Run_RunComplete()
        {
            try
            {
                Dispatcher.Invoke(() => { SetRunFinished(true); });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        ///     Starts the run asynchronously
        /// </summary>
        private void StartRunAsync()
        {
            StartTime = DateTime.Now;
            StartTimeLabel.Content = $"Start Time: {StartTime:g}";
            UpdateStartTime?.Invoke($"{StartTime:g}");
        }

        private void OpenDirectoryButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_runDirectory))
            {
                Process.Start("explorer", _runDirectory);
            }
            else
            {
                MessageBox.Show(_runDirectory + " does not exist!");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CancelRun()
        {
            if (MessageBox.Show(MainWindow.Us, "Are you sure you want to cancel this run?", "Cancel run?",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                if (Run != null)
                {
                    Session.CancelRun(Run);
                    _wasCanceled = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ButtonProgressAssist.SetIsIndicatorVisible(CancelButton, false);
                        ButtonProgressAssist.SetIsIndeterminate(CancelButton, false);
                        CancelButton.IsEnabled = false;
                        MainWindow.Us.UpdateStatusDisplay("Ready");

                    }));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelButton_Clicked(object sender, RoutedEventArgs e)
        {
            //Are you sure?
            CancelRun();
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
                    //AdditionDetailsPanelBorder.Visibility = Visibility.Visible;
                    //AdditionDetailsPanelBorder.Height = 600;
                    var progressBar = new TMGProgressBar
                    {
                        //Background = MaterialDesignColors.Swatch.
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
                        Name = new Label { Content = toAdd.Name, Foreground = (Brush)FindResource("MaterialDesignBody") },
                        ProgressBar = progressBar
                    });

                    BaseGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
                });
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubProgressBars_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = _subProgressBars[e.NewIndex];
                AdditionDetailsPanel.Add(toAdd.Name);
                AdditionDetailsPanel.Add(toAdd.ProgressBar);
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OnPropertyChanged(nameof(ProgressReportsVisibility));
                BaseGrid.RowDefinitions[1].Height = _subProgressBars.Count == 0 ? new GridLength(0) : new GridLength(250);
            }));
        }

        internal void ClearRun()
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
            _consoleAppender.Close();
            Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ClearRunButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClearRun();
            }));
        }

        public void NavigateToModelSystemDisplay(object extraData)
        {
            Dispatcher.Invoke(async () =>
            {
                var result = await MainWindow.Us.BringDisplayIntoView(this._launchedFromModelSystemDisplay, extraData);

                //if the display failed to open, relaunch and edit the model system
                if (!result)
                {
                    var display = MainWindow.Us.EditModelSystem(this._launchedFromModelSystemDisplay.Session);
                    if (display != null)
                    {
                        this._launchedFromModelSystemDisplay = display;
                        await MainWindow.Us.BringDisplayIntoView(this._launchedFromModelSystemDisplay, extraData);
                    }

                }
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemNameLink_OnClick(object sender, RoutedEventArgs e)
        {
            this.NavigateToModelSystemDisplay((sender as FrameworkContentElement)?.Tag);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StackTraceLinkOnClick(object sender, RoutedEventArgs e)
        {
            var errorDataContext = (ModelSystemErrorDisplayModel)(sender as FrameworkContentElement)?.DataContext;
            SchedulerWindow.ShowTrackTraceError(errorDataContext);
        }

        private struct SubProgress
        {
            internal Label Name;
            internal TMGProgressBar ProgressBar;
        }

        public class ConsoleOutputAppender : AppenderSkeleton
        {

            public ConsoleOutputController ConsoleOutputController { get; set; }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="loggingEvent"></param>
            protected override void Append(LoggingEvent loggingEvent)
            {
                ConsoleOutputController.ConsoleOutput += RenderLoggingEvent(loggingEvent);
            }
        }

        public sealed class ConsoleOutputController : AppenderSkeleton, INotifyPropertyChanged, IDisposable
        {
            private readonly ILog _log;
            private string _output;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="runWindow"></param>
            /// <param name="run"></param>
            /// <param name="log"></param>
            /// <param name="memoryAppender"></param>
            public ConsoleOutputController(XTMFRun run, ILog log = null)
            {
                run.RunMessage += Run_RunMessage;
                _log = log;
            }

            public string ConsoleOutput
            {
                get
                {
                    return _output;
                }
                set
                {
                    _output = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConsoleOutput)));
                }
            }

            public void Dispose()
            {
                ConsoleOutput = string.Empty;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="message"></param>
            private void Run_RunMessage(string message)
            {
                message = message.Replace("***", " ");
                if (_log != null)
                {
                    _log.Info(message);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="loggingEvent"></param>
            protected override void Append(LoggingEvent loggingEvent)
            {
                ConsoleOutput = ConsoleOutput + loggingEvent.RenderedMessage + Environment.NewLine;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConsoleOutput)));
            }
        }

        /// <summary>
        /// copy to clipboard (stack trace)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            var error = (sender as FrameworkContentElement)?.Tag as ModelSystemErrorDisplayModel;
            Clipboard.SetText(error.StackTrace == "Unavailable" ?
                error.Description :
                error.Description + "\r\n" + error.StackTrace);
        }

        private void ConsoleOutput_SelectionChanged(object sender, RoutedEventArgs e)
        {
            _oldCaret = ((TextBox)sender).CaretIndex;
        }
    }

    /// <summary>
    /// Error Display Model for the error list in the run window.
    /// </summary>
    public class ModelSystemErrorDisplayModel : INotifyPropertyChanged
    {
        private string _description;

        private string _modelSystemName;

        private string _stackTrace;

        public RunWindow RunWindow { get; set; }

        private ErrorWithPath _errorWithPath;

        public ModelSystemErrorDisplayModel()
        {
            RunWindow = null;
            Description = "";
            StackTrace = "";
        }


        /// <summary>
        /// </summary>
        /// <param name="description"></param>
        /// <param name="modelSystemName"></param>
        /// <param name="stackTrace"></param>
        public ModelSystemErrorDisplayModel(string description, string modelSystemName, string stackTrace, ErrorWithPath errorWithPath, RunWindow runWindow)
        {
            RunWindow = runWindow;
            Description = description;
            ModelSystemName = modelSystemName;
            if (stackTrace != null)
            {
                StackTrace = stackTrace;
            }
            else
            {
                StackTrace = "Unavailable";
            }
            _errorWithPath = errorWithPath;
        }

        /// <summary>
        /// </summary>
        public string StackTrace
        {
            get => _stackTrace;
            set
            {
                _stackTrace = value;
                OnPropertyChanged(nameof(StackTrace));
            }
        }

        /// <summary>
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        /// <summary>
        /// </summary>
        public string ModelSystemName
        {
            get => _modelSystemName;
            set
            {
                _modelSystemName = value;
                OnPropertyChanged(nameof(ModelSystemName));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ErrorWithPath ErrorWithPath
        {
            get => _errorWithPath;
            set
            {
                _errorWithPath = value;
                OnPropertyChanged(nameof(ErrorWithPath));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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