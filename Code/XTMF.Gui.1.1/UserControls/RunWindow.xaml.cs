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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Shell;
using System.Windows.Threading;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for RunWindow.xaml
    /// </summary>
    public partial class RunWindow : UserControl
    {
        private XTMFRun Run;
        private string RunDirectory;
        private DateTime StartTime;
        private DispatcherTimer Timer;
        private bool Windows7OrAbove = false;
        private volatile bool IsActive = false;
        private volatile bool IsFinished = false;
        private volatile bool WasCanceled = false;
        static Tuple<byte, byte, byte> ErrorColour;
        private BindingListWithRemoving<SubProgress> SubProgressBars = new BindingListWithRemoving<SubProgress>();
        private BindingListWithRemoving<IProgressReport> ProgressReports;

        private struct SubProgress
        {
            internal Label Name;
            internal TMGProgressBar ProgressBar;
        }

        /// <summary>
        /// Requires Windows 7
        /// </summary>
        private TaskbarItemInfo TaskbarInformation;

        static RunWindow()
        {
            var errorColour = (Color)Application.Current.FindResource("WarningRed");
            ErrorColour = new Tuple<byte, byte, byte>(errorColour.R, errorColour.G, errorColour.B);
        }

        public sealed class ConsoleOutputController : INotifyPropertyChanged, IDisposable
        {
            public string ConsoleOutput { get; set; }

            MemoryStream memoryStream = new MemoryStream();
            StreamWriter Writer;
            internal volatile bool Done = false;

            public ConsoleOutputController(RunWindow page)
            {
                var previousConsole = Console.Out;
                Writer = new StreamWriter(memoryStream, System.Text.Encoding.Unicode);
                page.OldCaret = 0;
                Console.SetOut(Writer);
                new Task(() =>
                {
                    try
                    {
                        var lastPosition = 0L;
                        StreamReader reader = new StreamReader(memoryStream, System.Text.Encoding.Unicode);
                        while (true)
                        {
                            Thread.Sleep(60);
                            Writer.Flush();
                            var currentPosition = Writer.BaseStream.Position;
                            if (currentPosition > lastPosition)
                            {
                                var buff = new char[(currentPosition - lastPosition) / sizeof(char)];
                                memoryStream.Position = lastPosition;
                                int length = reader.ReadBlock(buff, 0, buff.Length);
                                lastPosition = currentPosition;
                                if (length > 0)
                                {
                                    page.Dispatcher.Invoke(new Action(() =>
                                    {
                                        page.OldCaret = page.ConsoleOutput.CaretIndex;
                                    }));
                                    ConsoleOutput = ConsoleOutput + new string(buff, 0, length);
                                    var e = PropertyChanged;
                                    if (e != null)
                                    {
                                        e(this, new PropertyChangedEventArgs("ConsoleOutput"));
                                    }
                                }
                            }
                            if (this.Done)
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
                if (this.Writer != null)
                {
                    this.Writer.Dispose();
                    this.Writer = null;
                }
                if (this.memoryStream != null)
                {
                    this.memoryStream.Dispose();
                    this.memoryStream = null;
                }
                this.ConsoleOutput = null;
            }
        }

        int ConsoleLength = 0;
        int OldCaret = 0;
        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newTextLength = this.ConsoleOutput.Text.Length;
            if (this.OldCaret >= this.ConsoleLength)
            {
                this.ConsoleScrollViewer.ScrollToEnd();
                this.ConsoleOutput.CaretIndex = newTextLength;
            }
            else
            {
                this.ConsoleOutput.CaretIndex = this.OldCaret;
            }
            this.ConsoleLength = newTextLength;
        }

        private static Window GetWindow(DependencyObject current)
        {
            while (current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        public RunWindow(ModelSystemEditingSession session, DependencyObject callingControl)
        {
            InitializeComponent();
            Session = session;
            session.SessionClosed += Session_SessionClosed;
            var runName = "Run Name";
            StringRequest req = new StringRequest("Run Name", ValidateName);
            var trueWindow = Window.GetWindow(callingControl);
            var testWindow = GetWindow(callingControl);
            var vis = callingControl as UserControl;
            if (vis != null && testWindow != trueWindow)
            {
                var topLeft = vis.PointToScreen(new Point());
                // Since the string request dialog isn't shown yet we need to use some defaults as width and height are not available.
                req.Left = topLeft.X + ((vis.ActualWidth - StringRequest.DefaultWidth) / 2);
                req.Top = topLeft.Y + ((vis.ActualHeight - StringRequest.DefaultHeight) / 2);
            }
            else
            {
                req.Owner = trueWindow;
            }
            if (req.ShowDialog() == true)
            {
                runName = req.Answer;
                StartRun(session, runName);
            }
            else
            {
                MainWindow.Us.CloseWindow(this);
            }
        }

        private bool ValidateName(string arg)
        {
            return !String.IsNullOrEmpty(arg) &&
                !System.IO.Path.GetInvalidFileNameChars().Any(c => arg.Contains(c));
        }

        private void StartRun(ModelSystemEditingSession session, string runName)
        {
            string error = null;
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainWindow.Us.SetWindowName(this, "Run - " + runName);
                    RunNameLabel.Text = runName;
                }));
            Run = session.Run(runName, ref error);
            ProgressReports = Run.Configuration.ProgressReports;
            ProgressReports.ListChanged += new ListChangedEventHandler(ProgressReports_ListChanged);
            ProgressReports.BeforeRemove += new EventHandler<ListChangedEventArgs>(ProgressReports_BeforeRemove);
            SubProgressBars.ListChanged += new ListChangedEventHandler(SubProgressBars_ListChanged);
            SubProgressBars.BeforeRemove += new EventHandler<ListChangedEventArgs>(SubProgressBars_BeforeRemove);
            Run.RunComplete += Run_RunComplete;
            Run.RunStarted += Run_RunStarted;
            Run.RuntimeError += Run_RuntimeError;
            Run.RuntimeValidationError += Run_RuntimeValidationError;
            Run.ValidationStarting += Run_ValidationStarting;
            Run.ValidationError += Run_ValidationError;
            RunDirectory = Run.RunDirectory;
            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            Timer.Tick += new EventHandler(Timer_Tick);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var major = Environment.OSVersion.Version.Major;
                if (major > 6 || (major >= 6 && Environment.OSVersion.Version.Minor >= 1))
                {
                    Windows7OrAbove = true;
                    TaskbarInformation = new TaskbarItemInfo();
                }
            }
            this.ConsoleOutput.DataContext = new ConsoleOutputController(this);
            this.ConsoleBorder.DataContext = this.ConsoleOutput.DataContext;
            StartRunAsync();
            Timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (IsActive)
                {
                    if (Run != null)
                    {
                        if (!(IsFinished))
                        {
                            float progress = 1;
                            Tuple<byte, byte, byte> colour = ErrorColour;
                            try
                            {
                                var status = Run.PollStatusMessage(); ;
                                if (status != null)
                                {
                                    StatusLabel.Text = status;
                                }
                                progress = Run.PollProgress();
                                colour = Run.PollColour();
                            }
                            catch
                            { }
                            progress = progress * 10000;

                            if (progress > 10000) progress = 10000;
                            if (progress < 0) progress = 0;
                            if (colour != null)
                            {
                                ProgressBar.SetForgroundColor(Color.FromRgb(colour.Item1, colour.Item2, colour.Item3));
                            }
                            ProgressBar.Value = progress;
                            if (Windows7OrAbove)
                            {
                                TaskbarInformation.ProgressValue = ((progress / 10000));
                            }
                            for (int i = 0; i < SubProgressBars.Count; i++)
                            {
                                try
                                {
                                    progress = ProgressReports[i].GetProgress();
                                    progress = progress * 10000;
                                    if (progress > 10000) progress = 10000;
                                    if (progress < 0) progress = 0;
                                    SubProgressBars[i].ProgressBar.Value = progress;
                                }
                                catch
                                {
                                }
                            }
                            var elapsedTime = (DateTime.Now - StartTime);
                            int days = elapsedTime.Days;
                            elapsedTime = new TimeSpan(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                            if (days < 1)
                            {
                                ElapsedTimeLabel.Content = string.Format("Elapsed Time: {0:g}", elapsedTime);
                            }
                            else
                            {
                                ElapsedTimeLabel.Content = string.Format("Elapsed Time: {1} Day(s), {0:g}", elapsedTime, days);
                            }
                        }
                    }
                    else
                    {
                        ProgressBar.Value = IsFinished ? 10000 : 0;
                    }
                }
            }
            catch
            {
            }
        }

        private void Run_ValidationError(string obj)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Validation Error", obj);
            }));
        }

        private void ShowErrorMessage(string header, string message)
        {
            (new ErrorWindow() { Owner = GetWindow(this), ErrorMessage = header + "\r\n" + message }).ShowDialog();
        }

        private void ShowErrorMessage(string v, Exception error)
        {
            (new ErrorWindow() { Owner = GetWindow(this), Exception = error }).ShowDialog();
        }

        private void Run_ValidationStarting()
        {
        }

        private void Run_RuntimeValidationError(string errorMessage)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                SetRunFinished();
                ShowErrorMessage("Runtime Validation Error", errorMessage);
            }));
        }

        private void Run_RuntimeError(Exception error)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                SetRunFinished();
                error = GetInnermostError(error);
                ShowErrorMessage("Runtime Error", error);
            }));

        }

        private Exception GetInnermostError(Exception error)
        {
            var ret = error;
            while (ret.InnerException != null)
            {
                ret = ret.InnerException;
            }
            return ret;
        }

        private void SetRunFinished()
        {
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
            Dispatcher.Invoke(new Action(() =>
            {
                SetRunFinished();
            }));
        }


        /// <summary>
        /// Starts the run asynchronously
        /// </summary>
        private void StartRunAsync()
        {
            StartTime = DateTime.Now;
            StartTimeLabel.Content = string.Format("Start Time: {0:g}", StartTime);
            Run.Start();
        }

        private void Session_SessionClosed(object sender, EventArgs e)
        {
            MainWindow.Us.CloseWindow(this);
        }

        public ModelSystemEditingSession Session { get; private set; }

        private void OpenDirectoryButton_Clicked(object obj)
        {
            if (System.IO.Directory.Exists(RunDirectory))
            {
                System.Diagnostics.Process.Start(RunDirectory);
            }
            else
            {
                MessageBox.Show(RunDirectory + " does not exist!");
            }
        }

        private void CancelButton_Clicked(object obj)
        {
            WasCanceled = Run.ExitRequest();
        }

        private void ContinueButton_Clicked(object obj)
        {
            MainWindow.Us.CloseWindow(this);
        }

        private void ProgressReports_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            this.SubProgressBars.RemoveAt(e.NewIndex);
        }

        private void ProgressReports_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = this.ProgressReports[e.NewIndex];
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    this.AdditionDetailsPanelBorder.Visibility = System.Windows.Visibility.Visible;
                    this.AdditionDetailsPanelBorder.Height = double.NaN;
                    var progressBar = new TMGProgressBar()
                    {
                        Background = new SolidColorBrush(Color.FromArgb((byte)0x22, (byte)0x22, (byte)0x22, (byte)0x22)),
                        Maximum = 10000,
                        Minimum = 0,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        Height = 15
                    };
                    if (toAdd.Colour != null)
                    {
                        progressBar.SetForgroundColor(Color.FromRgb(toAdd.Colour.Item1, toAdd.Colour.Item2, toAdd.Colour.Item3));
                    }
                    this.SubProgressBars.Add(new SubProgress()
                    {
                        Name = new Label() { Content = toAdd.Name, Foreground = Brushes.White },
                        ProgressBar = progressBar
                    });
                }));
            }
            else if (e.ListChangedType == ListChangedType.ItemDeleted)
            {
                if (this.ProgressReports.Count == 0)
                {
                    this.Dispatcher.Invoke(new Action(delegate ()
                    {
                        this.AdditionDetailsPanelBorder.Visibility = System.Windows.Visibility.Collapsed;
                        this.AdditionDetailsPanelBorder.Height = 0;
                    }));
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
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    var toRemove = this.SubProgressBars[e.NewIndex];
                    this.AdditionDetailsPanel.Remove(toRemove.Name);
                    this.AdditionDetailsPanel.Remove(toRemove.ProgressBar);
                }));
            }
        }

        private void SubProgressBars_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                var toAdd = this.SubProgressBars[e.NewIndex];
                this.AdditionDetailsPanel.Add(toAdd.Name);
                this.AdditionDetailsPanel.Add(toAdd.ProgressBar);
            }
        }
    }
}
