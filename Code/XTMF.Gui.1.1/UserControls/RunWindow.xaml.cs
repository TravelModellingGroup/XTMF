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
using System.Linq;
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
    public partial class RunWindow : UserControl
    {
        private XTMFRun Run;
        private string RunDirectory;
        private DateTime _startTime;
        private DispatcherTimer Timer;
        private bool Windows7OrAbove;
        private volatile bool IsActive;
        private volatile bool IsFinished;
        private volatile bool WasCanceled;
        private static readonly Tuple<byte, byte, byte> _errorColour;

        private readonly BindingListWithRemoving<SubProgress> SubProgressBars =
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
        private TaskbarItemInfo TaskbarInformation;

        static RunWindow()
        {
            var errorColour = (Color) Application.Current.FindResource("WarningRed");
            _errorColour = new Tuple<byte, byte, byte>(errorColour.R, errorColour.G, errorColour.B);
        }

        public sealed class ConsoleOutputController : INotifyPropertyChanged, IDisposable
        {
            public string ConsoleOutput { get; set; }

            private MemoryStream memoryStream = new MemoryStream();
            private StreamWriter Writer;
            internal volatile bool Done = false;

            public ConsoleOutputController(RunWindow page)
            {
                var previousConsole = Console.Out;
                Writer = new StreamWriter(memoryStream, Encoding.Unicode);
                page.OldCaret = 0;
                Console.SetOut(Writer);
                new Task(() =>
                {
                    try
                    {
                        var lastPosition = 0L;
                        var reader = new StreamReader(memoryStream, Encoding.Unicode);
                        while (true)
                        {
                            Thread.Sleep(60);
                            Writer.Flush();
                            var currentPosition = Writer.BaseStream.Position;
                            if (currentPosition > lastPosition)
                            {
                                var buff = new char[(currentPosition - lastPosition) / sizeof(char)];
                                memoryStream.Position = lastPosition;
                                var length = reader.ReadBlock(buff, 0, buff.Length);
                                lastPosition = currentPosition;
                                if (length > 0)
                                {
                                    page.Dispatcher.Invoke(() => { page.OldCaret = page.ConsoleOutput.CaretIndex; });
                                    ConsoleOutput = ConsoleOutput + new string(buff, 0, length);
                                    var e = PropertyChanged;
                                    if (e != null)
                                    {
                                        e(this, new PropertyChangedEventArgs("ConsoleOutput"));
                                    }
                                }
                            }
                            if (Done)
                            {
                                Writer.Dispose();
                                Writer = null;
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
                if (Writer != null)
                {
                    Writer.Dispose();
                    Writer = null;
                }
                if (memoryStream != null)
                {
                    memoryStream.Dispose();
                    memoryStream = null;
                }
                ConsoleOutput = null;
            }
        }

        private int ConsoleLength;
        private int OldCaret;

        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newTextLength = ConsoleOutput.Text.Length;
            if (OldCaret >= ConsoleLength)
            {
                ConsoleScrollViewer.ScrollToEnd();
                ConsoleOutput.CaretIndex = newTextLength;
            }
            else
            {
                ConsoleOutput.CaretIndex = OldCaret;
            }
            ConsoleLength = newTextLength;
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

        private bool ValidateName(string arg)
        {
            return !string.IsNullOrEmpty(arg) &&
                   !Path.GetInvalidFileNameChars().Any(c => arg.Contains(c));
        }

        private void StartRun(XTMFRun run, string runName)
        {
            Run = run;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainWindow.Us.SetWindowName(this, "Run - " + runName);
                RunNameLabel.Text = runName;
            }));
            _progressReports = Run.Configuration.ProgressReports;
            _progressReports.ListChanged += ProgressReports_ListChanged;
            _progressReports.BeforeRemove += ProgressReports_BeforeRemove;
            SubProgressBars.ListChanged += SubProgressBars_ListChanged;
            SubProgressBars.BeforeRemove += SubProgressBars_BeforeRemove;
            Run.RunComplete += Run_RunComplete;
            Run.RunStarted += Run_RunStarted;
            Run.RuntimeError += Run_RuntimeError;
            Run.RuntimeValidationError += Run_RuntimeValidationError;
            Run.ValidationStarting += Run_ValidationStarting;
            Run.ValidationError += Run_ValidationError;
            RunDirectory = Run.RunDirectory;
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            Timer.Tick += Timer_Tick;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var major = Environment.OSVersion.Version.Major;
                if (major > 6 || major >= 6 && Environment.OSVersion.Version.Minor >= 1)
                {
                    Windows7OrAbove = true;
                    MainWindow.Us.TaskbarItemInfo = TaskbarInformation = new TaskbarItemInfo();
                    TaskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                    TaskbarInformation.ProgressValue = 0;
                }
            }
            ConsoleOutput.DataContext = new ConsoleOutputController(this);
            ConsoleBorder.DataContext = ConsoleOutput.DataContext;
            StartRunAsync();
            Timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                lock (this)
                {
                    if (IsActive)
                    {
                        if (Run != null)
                        {
                            if (!IsFinished)
                            {
                                float progress = 1;
                                var colour = _errorColour;
                                try
                                {
                                    var status = Run.PollStatusMessage();
                                    ;
                                    if (status != null)
                                    {
                                        StatusLabel.Text = status;
                                    }
                                    progress = Run.PollProgress();
                                    colour = Run.PollColour();
                                }
                                catch
                                {
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
                                if (Windows7OrAbove)
                                {
                                    TaskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                                    TaskbarInformation.ProgressValue = progress / 10000;
                                }
                                for (var i = 0; i < SubProgressBars.Count; i++)
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
                                        SubProgressBars[i].ProgressBar.Value = progress;
                                    }
                                    catch
                                    {
                                    }
                                }
                                var elapsedTime = DateTime.Now - _startTime;
                                var days = elapsedTime.Days;
                                elapsedTime = new TimeSpan(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                                if (days < 1)
                                {
                                    ElapsedTimeLabel.Content = string.Format("Elapsed Time: {0:g}", elapsedTime);
                                }
                                else
                                {
                                    ElapsedTimeLabel.Content = string.Format("Elapsed Time: {1} Day(s), {0:g}",
                                        elapsedTime, days);
                                }
                            }
                        }
                        else
                        {
                            ProgressBar.Value = IsFinished ? 10000 : 0;
                        }
                    }
                }
            }
            catch
            {
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

        private void Run_ValidationStarting()
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
            TaskbarInformation.ProgressState = WasCanceled
                ? TaskbarItemProgressState.Error
                : TaskbarItemProgressState.Indeterminate;
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Dispatcher.BeginInvoke(
                    new Action(() => { TaskbarInformation.ProgressState = TaskbarItemProgressState.None; }));
            });
            IsFinished = true;
            ContinueButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            if (WasCanceled)
            {
                StatusLabel.Text = "Run Canceled";
            }
            else
            {
                StatusLabel.Text = "Run Complete";
            }
            ProgressBar.Finished = true;
            ContinueButton.FlashAnimation(5);
            OpenDirectoryButton.FlashAnimation(5);
        }

        private void Run_RunStarted()
        {
            IsActive = true;
        }

        private void Run_RunComplete()
        {
            try
            {
                Dispatcher.Invoke(() => { SetRunFinished(); });
            }
            catch
            {
            }
        }


        /// <summary>
        ///     Starts the run asynchronously
        /// </summary>
        private void StartRunAsync()
        {
            _startTime = DateTime.Now;
            StartTimeLabel.Content = string.Format("Start Time: {0:g}", _startTime);
            Run.Start();
        }

        private void Session_SessionClosed(object sender, EventArgs e)
        {
            MainWindow.Us.CloseWindow(this);
        }

        public ModelSystemEditingSession Session { get; private set; }

        private void OpenDirectoryButton_Clicked(object obj)
        {
            if (Directory.Exists(RunDirectory))
            {
                Process.Start(RunDirectory);
            }
            else
            {
                MessageBox.Show(RunDirectory + " does not exist!");
            }
        }

        private void CancelButton_Clicked(object obj)
        {
            //Are you sure?
            if (MessageBox.Show(GetWindow(this), "Are you sure you want to cancel this run?", "Cancel run?",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                WasCanceled = Run.ExitRequest();
            }
        }

        private void ContinueButton_Clicked(object obj)
        {
            MainWindow.Us.CloseWindow(this);
        }

        private void ProgressReports_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            SubProgressBars.RemoveAt(e.NewIndex);
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
                    SubProgressBars.Add(new SubProgress
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

        private void SubProgressBars_AddingNew(object sender, AddingNewEventArgs e)
        {
        }

        private void SubProgressBars_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            lock (this)
            {
                Dispatcher.Invoke(delegate
                {
                    var toRemove = SubProgressBars[e.NewIndex];
                    AdditionDetailsPanel.Remove(toRemove.Name);
                    AdditionDetailsPanel.Remove(toRemove.ProgressBar);
                });
            }
        }

        private void SubProgressBars_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = SubProgressBars[e.NewIndex];
                AdditionDetailsPanel.Add(toAdd.Name);
                AdditionDetailsPanel.Add(toAdd.ProgressBar);
            }
        }

        internal bool CloseRequested()
        {
            if (IsFinished)
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
                    WasCanceled = true;
                    IsActive = false;
                    Timer.Stop();
                    Run.TerminateRun();
                    return true;
                }
            }
            return false;
        }
    }
}
