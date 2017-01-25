/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for RunWindow.xaml
    /// </summary>
    public partial class RunWindow
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

        private readonly BindingListWithRemoving<SubProgress> _subProgressBars =
            new BindingListWithRemoving<SubProgress>();

        private BindingListWithRemoving<IProgressReport> _progressReports;

        private struct SubProgress
        {
            internal Label Name;
            internal TMGProgressBar ProgressBar;
        }

        /// <summary>
        ///     Requires Windows 7
        /// </summary>
        private TaskbarItemInfo _taskbarInformation;

        static RunWindow()
        {
            var findResource = Application.Current.FindResource("WarningRed");
            if (findResource != null)
            {
                var errorColour = (Color) findResource;
                ErrorColour = new Tuple<byte, byte, byte>(errorColour.R, errorColour.G, errorColour.B);
            }
        }

        public sealed class ConsoleOutputController : INotifyPropertyChanged, IDisposable
        {
            public string ConsoleOutput { get; set; }

            private MemoryStream _memoryStream = new MemoryStream();
            private StreamWriter _writer;
            internal volatile bool Done = false;

            public ConsoleOutputController(RunWindow page)
            {
                var previousConsole = Console.Out;
                _writer = new StreamWriter(_memoryStream, Encoding.Unicode);
                page._oldCaret = 0;
                Console.SetOut(_writer);
                new Task(() =>
                {
                    try
                    {
                        var lastPosition = 0L;
                        var reader = new StreamReader(_memoryStream, Encoding.Unicode);
                        while (true)
                        {
                            Thread.Sleep(60);
                            _writer.Flush();
                            var currentPosition = _writer.BaseStream.Position;
                            if (currentPosition > lastPosition)
                            {
                                var buff = new char[(currentPosition - lastPosition) / sizeof(char)];
                                _memoryStream.Position = lastPosition;
                                var length = reader.ReadBlock(buff, 0, buff.Length);
                                lastPosition = currentPosition;
                                if (length > 0)
                                {
                                    page.Dispatcher.Invoke(() => { page._oldCaret = page.ConsoleOutput.CaretIndex; });
                                    ConsoleOutput = ConsoleOutput + new string(buff, 0, length);
                                    var e = PropertyChanged;
                                    e?.Invoke(this, new PropertyChangedEventArgs("ConsoleOutput"));
                                }
                            }
                            if (Done)
                            {
                                _writer.Dispose();
                                _writer = null;
                                return;
                            }
                        }
                    }
                    catch
                    {
                        Console.SetOut(previousConsole);
                    }
                }, TaskCreationOptions.LongRunning).Start();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void Dispose()
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }
                if (_memoryStream != null)
                {
                    _memoryStream.Dispose();
                    _memoryStream = null;
                }
                ConsoleOutput = null;
            }
        }

        private int _consoleLength;
        private int _oldCaret;

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

        public RunWindow(ModelSystemEditingSession session, XTMFRun run, string runName)
        {
            InitializeComponent();
            Session = session;
            session.SessionClosed += Session_SessionClosed;
            StartRun(run, runName);
        }

        private void StartRun(XTMFRun run, string runName)
        {
            _run = run;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainWindow.Us.SetWindowName(this, "Run - " + runName);
                RunNameLabel.Text = runName;
            }));
            _progressReports = _run.Configuration.ProgressReports;
            _progressReports.ListChanged += ProgressReports_ListChanged;
            _progressReports.BeforeRemove += ProgressReports_BeforeRemove;
            _subProgressBars.ListChanged += SubProgressBars_ListChanged;
            _subProgressBars.BeforeRemove += SubProgressBars_BeforeRemove;
            _run.RunComplete += Run_RunComplete;
            _run.RunStarted += Run_RunStarted;
            _run.RuntimeError += Run_RuntimeError;
            _run.RuntimeValidationError += Run_RuntimeValidationError;
            _run.ValidationStarting += Run_ValidationStarting;
            _run.ValidationError += Run_ValidationError;
            _runDirectory = _run.RunDirectory;
            _timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(value: 33)};
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
            ConsoleOutput.DataContext = new ConsoleOutputController(this);
            ConsoleBorder.DataContext = ConsoleOutput.DataContext;
            StartRunAsync();
            _timer.Start();
        }

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
                            }
                            progress = _run.PollProgress();
                            colour = _run.PollColour();
                        }
                        catch
                        {
                            // ignored
                        }
                        progress = progress * 10000;

                        if (progress > 10000)
                        {
                            progress = 10000;
                        }
                        if (progress < 0)
                        {
                            progress = 0;
                        }
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
                        if (days < 1)
                        {
                                    
                            ElapsedTimeLabel.Content = $"Elapsed Time: {elapsedTime:g}";
                        }
                        else
                        {
                            ElapsedTimeLabel.Content = string.Format("Elapsed Time: {1} Day(s), {0:g}",
                                elapsedTime, days);
                        }
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

        private void Run_ValidationError(string obj)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Validation Error", obj);
            });
        }

        private void ShowErrorMessage(string header, string message)
        {
            new ErrorWindow
            {
                Owner = GetWindow(this),
                ErrorMessage = string.IsNullOrWhiteSpace(header) ? message : header + "\r\n" + message
            }.ShowDialog();
        }

        private void ShowErrorMessage(string v, string message, string stackTrace)
        {
            new ErrorWindow {Owner = GetWindow(this), ErrorMessage = message, ErrorStackTrace = stackTrace}.ShowDialog();
        }

        private static void Run_ValidationStarting()
        {
        }

        private void Run_RuntimeValidationError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage(string.Empty, errorMessage);
            });
        }

        private void Run_RuntimeError(string message, string stackTrace)
        {
            Dispatcher.Invoke(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Runtime Error", message, stackTrace);
            });
        }

        private void SetRunFinished()
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
            _isFinished = true;
            ContinueButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            StatusLabel.Text = _wasCanceled ? "Run Canceled" : "Run Complete";
            ProgressBar.Finished = true;
            ContinueButton.FlashAnimation(5);
            OpenDirectoryButton.FlashAnimation(5);
        }

        private void Run_RunStarted()
        {
            _isActive = true;
        }

        private void Run_RunComplete()
        {
            try
            {
                Dispatcher.Invoke(SetRunFinished);
            }
            catch
            {
                // ignored
            }
        }


        /// <summary>
        ///     Starts the run asynchronously
        /// </summary>
        private void StartRunAsync()
        {
            _startTime = DateTime.Now;
            StartTimeLabel.Content = $"Start Time: {_startTime:g}";
            _run.Start();
        }

        private void Session_SessionClosed(object sender, EventArgs e)
        {
            MainWindow.Us.CloseWindow(this);
        }

        public ModelSystemEditingSession Session { get; private set; }

        private void OpenDirectoryButton_Clicked(object obj)
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

        private void CancelButton_Clicked(object obj)
        {
            //Are you sure?
            if (MessageBox.Show(GetWindow(this), "Are you sure you want to cancel this run?", "Cancel run?",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                _wasCanceled = _run.ExitRequest();
            }
        }

        private void ContinueButton_Clicked(object obj)
        {
            MainWindow.Us.CloseWindow(this);
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
                        Name = new Label {Content = toAdd.Name, Foreground = Brushes.White},
                        ProgressBar = progressBar
                    });
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
            if (window == null
                ? MessageBox.Show("Are you sure you want to cancel this run?", "Cancel run?",
                      MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes
                : MessageBox.Show(window, "Are you sure you want to cancel this run?", "Cancel run?",
                      MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                lock (this)
                {
                    _wasCanceled = true;
                    _isActive = false;
                    _timer.Stop();
                    _run.TerminateRun();
                    return true;
                }
            }
            return false;
        }
    }
}
