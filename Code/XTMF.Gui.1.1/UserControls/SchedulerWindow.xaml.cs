/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Util;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for SchedulerWindow.xaml
    /// </summary>
    public partial class SchedulerWindow : UserControl, INotifyPropertyChanged
    {
        private FrameworkElement _activeContent;

        private List<RunWindow> _runWindows;

        public SchedulerWindow()
        {
            InitializeComponent();

            _runWindows = new List<RunWindow>();

            ActiveRunContent.DataContext = Resources["DefaultDisplay"];
        }

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

        public event PropertyChangedEventHandler PropertyChanged;


        /// <summary>
        ///     Removes a RunWindow from the SchedulerWindow
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

        /// <summary>
        ///     Adds a new run to the scheduler window
        /// </summary>
        /// <param name="run"></param>
        public void AddRun(RunWindow run)
        {
            Dispatcher.Invoke(() =>
            {
                ActiveContent = run;
                ScheduledRuns.Items.Add(new SchedulerRunItem(run, this));
                ActiveRunContent.DataContext = run;
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var runWindow = (ScheduledRuns.SelectedItem as SchedulerRunItem)?.RunWindow;
            ActiveRunContent.DataContext = runWindow;
            ActiveContent = runWindow;
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
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).Tag as SchedulerRunItem;
            Dispatcher.Invoke(() => { FinishedRuns.Items.Remove(item); });
        }

        /// <summary>
        /// </summary>
        /// <param name="runItem"></param>
        public void RemoveFromActiveRuns(SchedulerRunItem runItem)
        {
            Dispatcher.Invoke(() =>
            {
                ScheduledRuns.Items.Remove(runItem);
                FinishedRuns.Items.Insert(0, runItem);

                var defaultd = Resources["DefaultDisplay"];
                Dispatcher.Invoke(() => { ActiveRunContent.DataContext = Resources["DefaultDisplay"]; });
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenOutput_OnClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            var item = b.Tag as SchedulerRunItem;
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
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAllRunsButton_OnClick(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FinishedRuns.Items.Clear();
                ;

                ActiveRunContent.DataContext = FindResource("DefaultDisplay");
            });
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listView = sender as ListView;
            var menu = listView?.ContextMenu;
            if (listView.SelectedItem != null)
            {
                var menuItem = new MenuItem();
                menuItem.Header = "Remove run from list";

                Dispatcher.Invoke(() => { menu.Items.Clear(); });

                menuItem.Click += (o, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        FinishedRuns.Items.RemoveAt(FinishedRuns.SelectedIndex);
                        //FinishedRuns.Items.Remove(item);
                    });
                };
                menu?.Items.Add(menuItem);
            }
        }

        /// <summary>
        ///     Finished runs ListView selection changed event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var runWindow = (FinishedRuns.SelectedItem as SchedulerRunItem)?.RunWindow;
            ActiveRunContent.DataContext = runWindow;
            ActiveContent = runWindow;
        }

        /// <summary>
        /// </summary>
        public class SchedulerRunItem : INotifyPropertyChanged
        {
            private string _elapsedTime = "--";
            private PackIconKind _icon = PackIconKind.TimerSand;

            private bool _isRunStarted;

            private float _progress;
            private readonly SchedulerWindow _schedulerWindow;
            private string _startTime = "--";

            private string _statusText = string.Empty;

            public Action RunFinished;


            /// <summary>
            ///     Constructor of the ScheduleRunItem, takes in the RunWindow (run control) in the constructor.
            /// </summary>
            /// <param name="runWindow"></param>
            public SchedulerRunItem(RunWindow runWindow, SchedulerWindow schedulerWindow)
            {
                Name = runWindow.Run.RunName;
                RunWindow = runWindow;
                _schedulerWindow = schedulerWindow;

                runWindow.UpdateRunStatus = UpdateRunStatus;
                runWindow.UpdateElapsedTime = val => { ElapsedTime = val; };
                runWindow.UpdateRunProgress = val => { Progress = val; };
                runWindow.UpdateStartTime = UpdateStartTime;
                runWindow.OnRuntimeValidationError = OnRuntimeValidationError;


                runWindow.OnRunFinished = OnRunFinished;
                //runWindow.OnRuntimeError = OnRuntimeError;
                runWindow.OnValidationError = OnValidationError;
                runWindow.RuntimeError = RuntimeError;
                runWindow.OnRunStarted = OnRunStarted;
                runWindow.OnRuntimeError = OnRuntimeError;

                StatusText = "Queud";

                //StartTime = (string) $"{RunWindow.StartTime:g}";
                Progress = 0;
            }

            /// <summary>
            /// 
            /// </summary>
            private void OnRuntimeError()
            {
                XtmfNotificationIcon.ShowNotificationBalloon(Name + " encountered a runtime exception.",
                    () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model system run exception");

                _schedulerWindow.RemoveFromActiveRuns(this);


                Icon = PackIconKind.Exclamation;

            }

            public RunWindow RunWindow { get; set; }

            public bool IsRunStarted
            {
                get => _isRunStarted;
                set
                {
                    _isRunStarted = value;
                    OnPropertyChanged(nameof(IsRunStarted));
                }
            }

            public PackIconKind Icon
            {
                get => _icon;
                set
                {
                    _icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }

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

            public string Name { get; set; }

            /// <summary>
            ///     StatusText property
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

            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            ///     This method is called when a runtime validation error occurs in the model system run.
            /// </summary>
            private void OnRuntimeValidationError()
            {
                XtmfNotificationIcon.ShowNotificationBalloon(Name + " encountered a runtime exception.",
                    () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model system run exception");

                Icon = PackIconKind.AlertBox;
            }

            /// <summary>
            /// </summary>
            /// <param name="s"></param>
            private void UpdateRunStatus(string s)
            {
                StatusText = s;
            }

            /// <summary>
            /// </summary>
            /// <param name="s"></param>
            private void UpdateStartTime(string s)
            {
                StartTime = $"{s:g}";
            }

            private void OnRunStarted()
            {
                Icon = PackIconKind.Run;
                IsRunStarted = true;
            }


            /// <summary>
            /// </summary>
            /// <param name="runSuccess"></param>
            private void OnRunFinished(bool runSuccess)
            {
                _schedulerWindow.RemoveFromActiveRuns(this);

                if (runSuccess)
                {
                    MainWindow.Us.GlobalStatusSnackBar.MessageQueue.Enqueue("Model system run finished (" + Name + ")",
                        "SCHEDULER",
                        () => MainWindow.Us.ShowSchedulerWindow());

                    XtmfNotificationIcon.ShowNotificationBalloon(Name + " has finished executing.",
                        () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model System Run Finished");
                    Icon = PackIconKind.CheckCircleOutline;
                }
            }

            /// <summary>
            /// </summary>
            /// <param name="errorWithPath"></param>
            private void RuntimeError(ErrorWithPath errorWithPath)
            {
                StatusText = errorWithPath.Message;
         

            }

            /// <summary>
            /// </summary>
            /// <param name="errorWithPaths"></param>
            private void OnValidationError(List<ErrorWithPath> errorWithPaths)
            {
                StatusText = "Validation error occured";
                _schedulerWindow.RemoveFromActiveRuns(this);
                Icon = PackIconKind.Alert;
            }


            [NotifyPropertyChangedInvocator]
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}