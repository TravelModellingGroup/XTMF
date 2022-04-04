/*
    Copyright 2014-2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Util;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListView = System.Windows.Controls.ListView;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for SchedulerWindow.xaml
    /// </summary>
    public partial class SchedulerWindow : UserControl, INotifyPropertyChanged
    {
        private FrameworkElement _activeContent;

        public SchedulerWindow()
        {
            InitializeComponent();
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
        ///     OnInitialized
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            //allow the stack trace text box to only be as large as 80% of the primary screen
            StackTraceTextBox.MaxWidth = Screen.PrimaryScreen.Bounds.Width * 0.8;
        }

        /// <summary>
        ///     copy to clipboard (stack trace)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            var error = (sender as FrameworkContentElement)?.Tag as ModelSystemErrorDisplayModel;
            Clipboard.SetText(error.StackTrace == "Unavailable"
                ? error.Description
                : error.Description + "\r\n" + error.StackTrace);
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
                ScheduledRuns.Items.Add(new SchedulerRunItemDisplayModel(run, this));
                ActiveRunContent.DataContext = run;
            });
        }

        public void AddDelayedRun(RunWindow run, DateTime delayedStartTime)
        {
            Dispatcher.Invoke(() =>
            {
                ActiveContent = run;
                var itemDisplayModel = new SchedulerRunItemDisplayModel(run, this)
                {
                    StatusText = "Delayed Run",
                    StartTime = delayedStartTime.ToString("MM/dd/yyyy H:mm")
                };
                ScheduledRuns.Items.Add(itemDisplayModel);
                ActiveRunContent.DataContext = run;
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var runWindow = (ScheduledRuns.SelectedItem as SchedulerRunItemDisplayModel)?.RunWindow;
            if (runWindow != null)
            {
                ActiveRunContent.DataContext = runWindow;
                ActiveContent = runWindow;
                runWindow?.ScrollToBottomOfConsole();
            }
            else
            {
                ActiveRunContent.DataContext = Resources["DefaultDisplay"];
            }
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
            if ((sender as Button).Tag is SchedulerRunItemDisplayModel item)
            {
                item.ClearRun();
                Dispatcher.Invoke(() => { FinishedRuns.Items.Remove(item); });
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="runItemDisplayModel"></param>
        public void RemoveFromActiveRuns(SchedulerRunItemDisplayModel runItemDisplayModel)
        {
            Dispatcher.Invoke(() =>
            {
                ScheduledRuns.Items.Remove(runItemDisplayModel);
                if (!FinishedRuns.Items.Contains(runItemDisplayModel))
                {
                    FinishedRuns.Items.Insert(0, runItemDisplayModel);
                    var defaultd = Resources["DefaultDisplay"];
                    Dispatcher.Invoke(() => { ActiveRunContent.DataContext = Resources["DefaultDisplay"]; });
                }
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenOutput_OnClick(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            var item = b.Tag as SchedulerRunItemDisplayModel;
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
                foreach (var item in FinishedRuns.Items)
                {
                    if (item is SchedulerRunItemDisplayModel run)
                    {
                        run.RunWindow.ClearRun();
                    }
                }
                FinishedRuns.Items.Clear();
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
                var menuItem = new MenuItem
                {
                    Header = "Remove run from list"
                };
                Dispatcher.Invoke(() => { menu.Items.Clear(); });
                menuItem.Click += (o, args) =>
                {
                    Dispatcher.Invoke(() => { FinishedRuns.Items.RemoveAt(FinishedRuns.SelectedIndex); });
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
            var runWindow = (FinishedRuns.SelectedItem as SchedulerRunItemDisplayModel)?.RunWindow;
            if (runWindow != null)
            {
                ActiveRunContent.DataContext = runWindow;
                ActiveContent = runWindow;
                runWindow?.ScrollToBottomOfConsole();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FinishedRuns_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var runWindow = (FinishedRuns.SelectedItem as SchedulerRunItemDisplayModel)?.RunWindow;
            if (runWindow != null)
            {
                ActiveRunContent.DataContext = runWindow;
                ActiveContent = runWindow;
                runWindow?.ScrollToBottomOfConsole();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRuns_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var runWindow = (ScheduledRuns.SelectedItem as SchedulerRunItemDisplayModel)?.RunWindow;
            if (runWindow != null)
            {
                ActiveRunContent.DataContext = runWindow;
                ActiveContent = runWindow;
                runWindow?.ScrollToBottomOfConsole();
            }
        }

        /// <summary>
        ///     OnClick listener for the stack trace dialog close button / link.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseDialogHyperLink_OnClick(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { StackTraceDialogHost.IsOpen = false; }));
        }

        /// <summary>
        ///     Loads the ModelSystemDisplay editor with the passed module navigated to (and selected).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemNameLink_OnClick(object sender, RoutedEventArgs e)
        {
            var runWindow = ((e.Source as FrameworkContentElement)?.DataContext as ModelSystemErrorDisplayModel)
                ?.RunWindow;

            runWindow?.NavigateToModelSystemDisplay((sender as FrameworkContentElement)?.Tag);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StackTraceLink_OnClick(object sender, RoutedEventArgs e)
        {
            var errorDataContext = (ModelSystemErrorDisplayModel)(sender as FrameworkContentElement)?.DataContext;
            ShowTrackTraceError(errorDataContext);
        }

        /// <summary>
        /// </summary>
        /// <param name="errorDataContext"></param>
        public void ShowTrackTraceError(ModelSystemErrorDisplayModel errorDataContext)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StackTraceDialogHost.DataContext = errorDataContext;
                StackTraceDialogHost.IsOpen = true;
            }));
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyErrorLink_OnMouseDown(object sender, RoutedEventArgs e)
        {
            var errorDataContext = (ModelSystemErrorDisplayModel)(sender as FrameworkElement)?.Tag;
            Clipboard.SetText(errorDataContext?.StackTrace == "Unavailable"
                ? $"Description: {errorDataContext.Description}"
                : $"Module: {errorDataContext?.ModelSystemName}\r\n" +
                  $"Description:\r\n {errorDataContext?.Description} " +
                  $"\r\nStack Trace:\r\n{errorDataContext?.StackTrace}");
            MainWindow.Us.GlobalStatusSnackBar.MessageQueue.Enqueue("Error information copied to clipboard",
                "SCHEDULER",
                () => MainWindow.Us.ShowSchedulerWindow());
        }

        private void StackTraceGroup_OnMouseDown(object sender, RoutedEventArgs e)
        {
            var errorDataContext = (ModelSystemErrorDisplayModel)(sender as FrameworkElement)?.Tag;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StackTraceDialogHost.DataContext = errorDataContext;
                StackTraceDialogHost.IsOpen = true;
            }));
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private int CountNonQueuedRuns()
        {
            return ScheduledRuns.Items.Cast<object>().Count(i => i is SchedulerRunItemDisplayModel item && item.IsRunStarted);
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool CanMoveQueueDown()
        {
            var runItem = ScheduledRuns.SelectedItem as SchedulerRunItemDisplayModel;
            return !(ScheduledRuns.SelectedIndex == ScheduledRuns.Items.Count - 1 || runItem.IsRunStarted);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRunItemListItemContainer_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                if (CanMoveQueueDown())
                {
                    MoveQueueDown();
                }
            }
            else if (e.Key == Key.Up && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                if (CanMoveQueueUp())
                {
                    MoveQueueUp();
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool CanMoveQueueUp()
        {
            var nonQueue = CountNonQueuedRuns();
            return !(ScheduledRuns.SelectedIndex == 0 || ScheduledRuns.SelectedIndex == nonQueue);
        }

        /// <summary>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="fromIndex"></param>
        /// <param name="toIndex"></param>
        public void MoveQueueInsert(SchedulerRunItemDisplayModel item, int fromIndex, int toIndex)
        {
            var nonQueuedRuns = CountNonQueuedRuns();
            ScheduledRuns.Items.RemoveAt(fromIndex);
            ScheduledRuns.Items.Insert(toIndex, item);
            item.RunWindow.ReorderRun(toIndex - nonQueuedRuns);
            ScheduledRuns.SelectedIndex = toIndex;
        }

        /// <summary>
        /// </summary>
        public void MoveQueueDown()
        {
            MoveQueueInsert((SchedulerRunItemDisplayModel)ScheduledRuns.SelectedItem, ScheduledRuns.SelectedIndex,
                ScheduledRuns.SelectedIndex + 1);
        }

        /// <summary>
        ///     Move selected itemDisplayModel in list view
        /// </summary>
        public void MoveQueueUp()
        {
            MoveQueueInsert((SchedulerRunItemDisplayModel)ScheduledRuns.SelectedItem, ScheduledRuns.SelectedIndex,
                ScheduledRuns.SelectedIndex - 1);
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StatusTextCopyMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is TextBlock source)
            {
                Clipboard.SetText(source.Text);
            }
        }

        /// <summary>
        /// </summary>
        public class SchedulerRunItemDisplayModel : INotifyPropertyChanged
        {
            private readonly SchedulerWindow _schedulerWindow;
            private string _elapsedTime = "--";
            private PackIconKind _icon = PackIconKind.TimerSand;

            private bool _isRunStarted;

            private float _progress;
            private string _startTime = "--";
            public event PropertyChangedEventHandler PropertyChanged;
            private string _statusText = string.Empty;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="runWindow"></param>
            /// <param name="schedulerWindow"></param>
            public SchedulerRunItemDisplayModel(RunWindow runWindow, SchedulerWindow schedulerWindow)
            {
                Name = runWindow.Run.RunName;
                RunWindow = runWindow;
                _schedulerWindow = schedulerWindow;
                runWindow.UpdateRunStatus = UpdateRunStatus;
                runWindow.UpdateElapsedTime = val => { ElapsedTime = val; };
                runWindow.UpdateRunProgress = val => { Progress = val; };
                runWindow.UpdateStartTime = UpdateStartTime;
                runWindow.OnRuntimeValidationError = OnRuntimeValidationError;
                runWindow.SchedulerRunItemDisplayModel = this;
                runWindow.OnRunFinished = OnRunFinished;
                runWindow.OnValidationError = OnValidationError;
                runWindow.RuntimeError = RuntimeError;
                runWindow.OnRunStarted = OnRunStarted;
                runWindow.OnRuntimeError = OnRuntimeError;
                ModelSystemErrors = new ObservableCollection<ModelSystemErrorDisplayModel>();
                Progress = 0;
            }

            public ObservableCollection<ModelSystemErrorDisplayModel> ModelSystemErrors { get; set; }

            public bool HasError { get; set; }

            /// <summary>
            /// </summary>
            public Visibility RunErrorInformationVisibility
            {
                set => OnPropertyChanged(nameof(RunErrorInformationVisibility));
                get => HasError ? Visibility.Visible : Visibility.Collapsed;
            }

            public RunWindow RunWindow { get; set; }

            /// <summary>
            /// </summary>
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

            /// <summary>
            /// </summary>
            /// <param name="errorWithPath"></param>
            private void RuntimeError(ErrorWithPath errorWithPath)
            {
                StatusText = "Runtime exception occurred.";
                _schedulerWindow.RemoveFromActiveRuns(this);
                Icon = PackIconKind.Alert;
                HasError = true;
            }

            /// <summary>
            /// </summary>
            /// <param name="errorWithPaths"></param>
            private void OnValidationError(List<ErrorWithPath> errorWithPaths)
            {
                StatusText = "Validation error occurred";
                _schedulerWindow.RemoveFromActiveRuns(this);
                Icon = PackIconKind.Alert;
                HasError = true;
            }

            /// <summary>
            /// </summary>
            private void OnRuntimeError()
            {
                XtmfNotificationIcon.ShowNotificationBalloon(Name + " encountered a runtime exception.",
                    () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model system run exception");
                Icon = PackIconKind.Exclamation;
                HasError = true;
            }

            /// <summary>
            ///     This method is called when a runtime validation error occurs in the model system run.
            /// </summary>
            private void OnRuntimeValidationError()
            {
                XtmfNotificationIcon.ShowNotificationBalloon(Name + " encountered a validation exception.",
                    () => { MainWindow.Us.ShowSchedulerWindow(); }, "Model system run exception");
                Icon = PackIconKind.AlertBox;
                HasError = true;
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

            /// <summary>
            /// 
            /// </summary>
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
                    StatusText = "Run finished successfully.";
                }
            }

            [NotifyPropertyChangedInvocator]
            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            internal void ClearRun()
            {
                RunWindow.ClearRun();
            }
        }
    }
}
