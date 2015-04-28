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

        public RunWindow(ModelSystemEditingSession session)
        {
            InitializeComponent();
            Session = session;
            session.SessionClosed += Session_SessionClosed;
            var runName = "Run Name";
            StringRequest req = new StringRequest("Run Name", ValidateName);
            if(req.ShowDialog() == true)
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
                !(from c in System.IO.Path.GetInvalidFileNameChars()
                  where arg.Contains(c)
                  select c).Any();
        }

        private void StartRun(ModelSystemEditingSession session, string runName)
        {
            string error = null;
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainWindow.Us.SetWindowName(this, "Run - " + runName);
                }));
            Run = session.Run(runName, ref error);
            ProgressReports = Run.Configuration.ProgressReports;
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
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var major = Environment.OSVersion.Version.Major;
                if(major > 6 || (major >= 6 && Environment.OSVersion.Version.Minor >= 1))
                {
                    Windows7OrAbove = true;
                    TaskbarInformation = new TaskbarItemInfo();
                }
            }
            StartRunAsync();
            Timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if(IsActive)
                {
                    if(Run != null)
                    {
                        if(!(IsFinished))
                        {
                            float progress = 1;
                            Tuple<byte, byte, byte> colour = ErrorColour;
                            try
                            {
                                var status = Run.PollStatusMessage(); ;
                                if(status != null)
                                {
                                    StatusLabel.Text = status;
                                }
                                progress = Run.PollProgress();
                                colour = Run.PollColour();
                            }
                            catch
                            { }
                            progress = progress * 10000;

                            if(progress > 10000) progress = 10000;
                            if(progress < 0) progress = 0;
                            if(colour != null)
                            {
                                ProgressBar.SetForgroundColor(Color.FromRgb(colour.Item1, colour.Item2, colour.Item3));
                            }
                            ProgressBar.Value = progress;
                            if(Windows7OrAbove)
                            {
                                TaskbarInformation.ProgressValue = ((progress / 10000));
                            }
                            int subProgressBarLength = SubProgressBars.Count;
                            for(int i = 0; i < subProgressBarLength; i++)
                            {
                                try
                                {
                                    progress = ProgressReports[i].GetProgress();
                                    progress = progress * 10000;
                                    if(progress > 10000) progress = 10000;
                                    if(progress < 0) progress = 0;
                                    SubProgressBars[i].ProgressBar.Value = progress;
                                }
                                catch
                                {
                                }
                            }
                            var elapsedTime = (DateTime.Now - StartTime);
                            int days = elapsedTime.Days;
                            elapsedTime = new TimeSpan(elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                            if(days < 1)
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
            (new ErrorWindow() { ErrorMessage = header + "\r\n" + message}).ShowDialog();
        }

        private void ShowErrorMessage(string v, Exception error)
        {
            (new ErrorWindow() { Exception = error }).ShowDialog();
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
            while(ret.InnerException != null)
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
            if(WasCanceled)
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
            if(System.IO.Directory.Exists(RunDirectory))
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
    }
}
