/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Dragablz;
using MahApps.Metro.Controls;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls;
using XTMF.Gui.UserControls.Help;
using XTMF.Gui.UserControls.Interfaces;
using XTMF.Gui.Util;
using Application = System.Windows.Application;
using FileDialog = Microsoft.Win32.FileDialog;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace XTMF.Gui
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private const string UpdateProgram = "XTMF.Update2.exe";

        // Using a DependencyProperty as the backing store for EditingDisplayModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EditingDisplayModelProperty =
            DependencyProperty.Register("EditingDisplayModel", typeof(ActiveEditingSessionDisplayModel),
                typeof(MainWindow), new PropertyMetadata(null));

        /// <summary>
        ///     The Singleton instance of the GUI window
        /// </summary>
        internal static MainWindow Us;

        private readonly ActiveEditingSessionDisplayModel NullEditingDisplayModel;

        private SettingsPage _settingsPage;

        private MouseButtonEventHandler _LastAdded;

        private OperationProgressing operationProgressing;

        private bool LaunchUpdate = false;

        public MainWindow()
        {
            ViewModelBase = new ViewModelBase();
            EditingDisplayModel = NullEditingDisplayModel = new ActiveEditingSessionDisplayModel(false);
            ThemeController = new ThemeController(GetConfigurationFilePath());
            InitializeComponent();
            // I am changing the code here with a comment
            //do you see any console window
            Loaded += MainWindow_Loaded;
            Us = this;
            operationProgressing = new OperationProgressing();
            SchedulerWindow = new SchedulerWindow();
            ViewDockPanel.DataContext = ViewModelBase;
            ContentControl.DataContext = ViewModelBase;
            DockManager.InterTabController.InterTabClient = new InterTabClient();
            DockManager.ClosingItemCallback = ClosingItemCallback;
            DockManager.SelectionChanged += DockManagerOnSelectionChanged;
            WorkspaceProjects = new Dictionary<Project, UserControl>();
            XtmfNotificationIcon.InitializeNotificationIcon();
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });


            SetDisplayActive(new StartWindow(), "Start");

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static string GetConfigurationFilePath()
        {
            var localConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocalXTMFConfiguration.xml");
            if (File.Exists(localConfigPath))
            {
                return localConfigPath;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XTMF", "Configuration.xml");
        }

        public ThemeController ThemeController { get; }

        private Dictionary<Project, UserControl> WorkspaceProjects { get; set; }

        public ViewModelBase ViewModelBase { get; set; }

        public ActiveEditingSessionDisplayModel EditingDisplayModel
        {
            get => (ActiveEditingSessionDisplayModel)GetValue(EditingDisplayModelProperty);
            set => SetValue(EditingDisplayModelProperty, value);
        }

        public ProjectDisplay.ProjectModel.ContainedModelSystemModel ClipboardModel { get; set; }

        public SchedulerWindow SchedulerWindow { get; }

        public List<string> RecentProjects => EditorController.Runtime.Configuration.RecentProjects;

        public bool RuntimeAvailable => EditorController.Runtime != null;

        public bool ShowMetaModuleHiddenParameters { get; set; }

        public UserControl CurrentViewModel { get; set; }

        /// <summary>
        /// </summary>
        /// <param name="sender1"></param>
        /// <param name="selectionChangedEventArgs"></param>
        private void DockManagerOnSelectionChanged(object sender1, SelectionChangedEventArgs selectionChangedEventArgs)
        {
            (DockManager.SelectedContent as UserControl)?.Focus();
            Keyboard.Focus(DockManager.SelectedContent as UserControl);
            if (DockManager.Items.Count == 0)
            {
                SetDisplayActive(new StartWindow(), "Start");
            }


        }

        public event EventHandler RecentProjectsUpdated;

        /// <summary>
        /// </summary>
        /// <param name="args"></param>
        private void ClosingItemCallback(ItemActionCallbackArgs<TabablzControl> args)
        {
            if ((args.DragablzItem.Content as TabItem)?.Content is ProjectDisplay)
            {
                var projectDisplay = (args.DragablzItem.Content as TabItem)?.Content as ProjectDisplay;
                projectDisplay?.Model.Unload();
                if (projectDisplay != null)
                {
                    //projectDisplay.Session.Dispose();
                    WorkspaceProjects.Remove(projectDisplay.Session.Project);
                    projectDisplay.Session.EndSession();
                    Console.WriteLine("h");
                }
            }
            if ((args.DragablzItem.Content as TabItem)?.Content is ITabCloseListener closeListener &&
                !closeListener.HandleTabClose())
            {
                args.Cancel();
            }
        }

        /// <summary>
        ///     Updates GUI with recently opened projects.
        /// </summary>
        public void UpdateRecentProjectsMenu()
        {
            var recentProjects = EditorController.Runtime.Configuration.RecentProjects;
            recentProjects.Reverse();
            Dispatcher.Invoke(() =>
            {
                //RecentProjectsMenuItem.Items.Clear();
                foreach (var recentProject in recentProjects)
                {
                    var recentProjectMenuItem = new MenuItem
                    {
                        Header = recentProject
                    };
                    // RecentProjectsMenuItem.Items.Add(recentProjectMenuItem);

                    recentProjectMenuItem.Click += (sender, EventArgs) =>
                    {
                        RecentProjectMenuItem_Click(sender, EventArgs, recentProject);
                    };
                }

                RecentProjectsUpdated?.Invoke(this, new EventArgs());
            });
        }

        private void RecentProjectMenuItem_Click(object sender, RoutedEventArgs e, string projectName)
        {
            LoadProjectByName(projectName);
        }


        /// <summary>
        ///     Updates the Status display text
        /// </summary>
        /// <param name="text"></param>
        public void UpdateStatusDisplay(string text)
        {
            StatusDisplay.Text = text;
        }

        /// <summary>
        /// Reloads XTMF using the default configuration
        /// </summary>
        public void Reload()
        {
            IsEnabled = false;
            StatusDisplay.Text = "Loading XTMF";
            EditorController.Register(this, () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    IsEnabled = true;
                    StatusDisplay.Text = "Ready";
                    UpdateRecentProjectsMenu();
                }));
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            StatusDisplay.Text = "Loading XTMF";
            Dispatcher.Invoke(() => { ExternalGrid.Focus(); });

            _settingsPage = new SettingsPage();
        }

        public void ApplyTheme(ThemeController.Theme theme)
        {
            ThemeController.SetThemeActive(theme);
        }

        /// <summary>
        /// </summary>
        /// <param name="projectName"></param>
        public void LoadProjectByName(string projectName)
        {
            string error = null;
            var project = EditorController.Runtime.ProjectController.LoadOrCreate(projectName, ref error);
            if (project != null)
            {
                LoadProject(project);
                return;
            }
            MessageBox.Show(this, "Unable to load project!", error);
        }

        /// <summary>
        /// Loads a project.
        /// </summary>
        /// <param name="project"></param>
        public void LoadProject(Project project)
        {
            var progressing = new OperationProgressing
            {
                Owner = this
            };
            if (project != null && !EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                Task.Run(() =>
                {
                    try
                    {
                        ProjectEditingSession session = null;
                        var loadingTask = Task.Run(() =>
                        {
                            session = EditorController.Runtime.ProjectController.EditProject(project);
                        });
                        loadingTask.Wait();
                        if (session != null)
                        {
                            Us.EditProject(session);
                        }
                    }
                    catch (Exception e)
                    {
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            progressing.Close();
                            var inner = e.InnerException;
                            MessageBox.Show(this, inner.Message, "Error Loading Project", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                    EditorController.Runtime.Configuration.AddRecentProject(project.Name);
                    EditorController.Runtime.Configuration.Save();
                    UpdateRecentProjectsMenu();
                });
            }
            else if (EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                if (WorkspaceProjects.TryGetValue(project, out var projectContorl))
                {
                    var visible = false;
                    foreach (TabItem tabItem in DockManager.Items)
                    {
                        if (tabItem.Content == projectContorl && tabItem.IsSelected)
                        {
                            visible = true;
                            break;
                        }
                    }
                    if (!visible)
                    {
                        SetDisplayActive(projectContorl, "Project - " + project.Name);
                    }
                }
            }
        }

        public void SetStatusLink(string text, Action clickAction)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLinkLabel.Visibility = Visibility.Visible;
                var handler = new MouseButtonEventHandler((e, a) => { clickAction.BeginInvoke(null, null); });
                if (_LastAdded != null)
                {
                    StatusLinkLabel.MouseDown -= _LastAdded;
                }
                StatusLinkLabel.MouseDown += handler;
                StatusLinkLabel.Content = text;
                _LastAdded = handler;
            });
        }

        public void HideStatusLink()
        {
            StatusLinkLabel.Visibility = Visibility.Collapsed;
        }

        public static void SetStatusText(string text)
        {
            Us.Dispatcher.BeginInvoke(new Action(() => { Us.StatusDisplay.Text = text; }));
        }

        public void OpenProject()
        {
            SetDisplayActive(new ProjectsDisplay(EditorController.Runtime), "Projects");
        }

        internal void EditProject(ProjectEditingSession projectSession)
        {
            if (projectSession != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var display = new ProjectDisplay
                        {
                            Session = projectSession
                        };
                        display.InitiateModelSystemEditingSession += editingSession => EditModelSystem(editingSession,
                            editingSession.PreviousRunName == null
                                ? null
                                : editingSession.ProjectEditingSession.Name + " - " + editingSession.PreviousRunName);


                        SetDisplayActive(display, projectSession.Name);
                        WorkspaceProjects.Add(projectSession.Project, display);
                        SetStatusText("Ready");
                    }
                ));
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void OpenModelSystem()
        {
            SetDisplayActive(new ModelSystemsDisplay(EditorController.Runtime), "Model Systems");
        }

        private bool RunAlreadyExists(string runName, ModelSystemEditingSession session)
        {
            return session.RunNameExists(runName);
        }

        private void SaveMenu_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.Save();
        }

        private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.SaveAs();
        }

        public static string OpenFile(string title, KeyValuePair<string, string>[] extensions, bool alreadyExists)
        {
            var filter = string.Join("|",
                from element in extensions
                select element.Key + "|*." + element.Value
            );
            var dialog = alreadyExists
                ? (FileDialog)new OpenFileDialog
                {
                    Title = title,
                    Filter = filter
                }
                : new SaveFileDialog
                {
                    Title = "Save As",
                    FileName = title,
                    Filter = filter
                };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static string OpenDirectory()
        {
            var showAdvanced = Environment.OSVersion.Version.Major >= 6;
            if (showAdvanced)
            {
                var result = Win32Helper.VistaDialog.Show(new WindowInteropHelper(Us).Handle, null, "Select Directory");
                if (result.Result)
                {
                    return result.FileName;
                }
            }
            else
            {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        public void ImportModelSystem()
        {
            var fileName = OpenFile("Import Model System",
                new[]
                    {new KeyValuePair<string, string>("Model System File", "xml")}, true);
            string error = null;
            if (fileName != null)
            {
                var msName = Path.GetFileName(fileName);
                if (!EditorController.Runtime.ModelSystemController.ImportModelSystem(fileName, false, ref error))
                {
                    switch (MessageBox.Show(this, error + "\r\nWould you like to overwrite?",
                        "Unable to import", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No))
                    {
                        case MessageBoxResult.Yes:
                            {
                                if (!EditorController.Runtime.ModelSystemController.ImportModelSystem(fileName, true,
                                    ref error))
                                {
                                    MessageBox.Show(this, error, "Unable to import", MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                                else
                                {
                                    MessageBox.Show(this,
                                        "The model system has been successfully imported from '" + fileName + "'.",
                                        "Model System Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void NewModelSystemButton_Click(object sender, RoutedEventArgs e)
        {
            NewModelSystem();
        }

        /// <summary>
        /// </summary>
        public async void NewModelSystem()
        {
            var dialog = new StringRequestDialog("Model System Name", ValidateName);
            var result = await dialog.ShowAsync();
            if (dialog.DidComplete)
            {
                var name = dialog.UserInput;
                var ms = EditorController.Runtime.ModelSystemController.LoadOrCreate(name);
                ms.ModelSystemStructure.Name = dialog.UserInput;
                EditModelSystem(EditorController.Runtime.ModelSystemController.EditModelSystem(ms));
            }
        }

        /// <summary>
        ///     Creates a dialog for the inputs required to create a new project
        /// </summary>
        public async void NewProject()
        {
            var dialog = new StringRequestDialog("Project Name", ValidateName);
            var result = await dialog.ShowAsync();
            if (dialog.DidComplete)
            {
                var name = dialog.UserInput;
                string error = null;
                var ms = EditorController.Runtime.ProjectController.LoadOrCreate(name, ref error);
                EditProject(EditorController.Runtime.ProjectController.EditProject(ms));
                EditorController.Runtime.Configuration.AddRecentProject(ms.Name);
                EditorController.Runtime.Configuration.Save();
                UpdateRecentProjectsMenu();
            }
        }

        private bool ValidateName(string name)
        {
            return Project.ValidateProjectName(name);
        }

        internal ModelSystemDisplay EditModelSystem(ModelSystemEditingSession modelSystemSession, string titleBar = null)
        {
            if (modelSystemSession != null)
            {
                var display = new ModelSystemDisplay
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Session = modelSystemSession,
                    ModelSystem = modelSystemSession.ModelSystemModel
                };
                display.ContentGuid = Guid.NewGuid().ToString();
                var titleBarName = titleBar ?? (modelSystemSession.EditingProject
                                       ? modelSystemSession.ProjectEditingSession.Name + " - " +
                                         modelSystemSession.ModelSystemModel.Name
                                       : "Model System - " + modelSystemSession.ModelSystemModel.Name);
                SetDisplayActive(display, titleBarName);
                Keyboard.Focus(display);
                display.Focus();
                return display;
            }
            return null;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                if (LaunchUpdate)
                {
                    Task.Run(() =>
                        {
                            var path = Assembly.GetExecutingAssembly().Location;
                            try
                            {
                                Process.Start(
                                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), UpdateProgram),
                                    Process.GetCurrentProcess().Id + " \"" + path + "\"");
                            }
                            catch
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show("We were unable to find XTMF.Update2.exe!", "Updater Missing!",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }
                        })
                        .Wait();
                }

                Application.Current.Shutdown(0);
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    TerminateProcess(Process.GetCurrentProcess().Handle, 0);
                }
                else
                {
                    Process.GetCurrentProcess().Kill();
                }
            }
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool TerminateProcess(IntPtr processHandle, int exitCode);

        /// <summary>
        ///     Adds the run session to the scheduler window. A new scheduler window is created if it does not already exist.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        internal void AddRunToSchedulerWindow(RunWindow runWindow)
        {
            //create a new scheduler window if one does not exist
            SchedulerWindow.AddRun(runWindow);
            SetDisplayActive(SchedulerWindow, "Model System Runs", false);
        }

        internal void AddDelayedRunToSchedulerWindow(RunWindow runWindow, DateTime delayedStartTime)
        {
            //create a new scheduler window if one does not exist
            SchedulerWindow.AddDelayedRun(runWindow, delayedStartTime);
        }

        /// <summary>
        ///     Shows the scheduler window
        /// </summary>
        public void ShowSchedulerWindow()
        {

            SetDisplayActive(SchedulerWindow, "Scheduler", false);
        }

        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <returns></returns>
        internal RunWindow CreateRunWindow(ModelSystemEditingSession session, XTMFRun run, string runName,
            bool immediateRun = false, ModelSystemDisplay launchDisplay = null, SchedulerWindow schedulerWindow = null)
        {
            return new RunWindow(session, run, runName, immediateRun, launchDisplay, schedulerWindow);
        }

        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <returns></returns>
        internal RunWindow CreateDelayedRunWindow(ModelSystemEditingSession session, XTMFRun run, string runName,
            DateTime delayedStartTime, ModelSystemDisplay launchDisplay = null, SchedulerWindow schedulerWindow = null)
        {
            return new RunWindow(session, run, runName, delayedStartTime, launchDisplay, schedulerWindow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="documentationControl"></param>
        internal void NewDocumentationWindow(DocumentationControl documentationControl)
        {
            SetDisplayActive(documentationControl, "Documentation - " + documentationControl.TypeNameText);
            Keyboard.Focus(documentationControl);
            documentationControl.Focus();
        }

        /// <summary>
        /// </summary>
        /// <param name="getHelpFor"></param>
        public void LaunchHelpWindow(ModelSystemStructureModel getHelpFor = null)
        {
            var helpUI = new HelpDialog(EditorController.Runtime.Configuration);
            if (getHelpFor != null)
            {
                helpUI.SelectModuleContent(getHelpFor);
            }
            SetDisplayActive(helpUI, "Help");
            Keyboard.Focus(helpUI);
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            NewProject();
        }

        /// <summary>
        /// </summary>
        /// <param name="display"></param>
        /// <param name="title"></param>
        /// <param name="searchable"></param>
        private void SetDisplayActive(UserControl display, string title, bool searchable = false)
        {
            Dispatcher.Invoke(() =>
            {
                var exists = false;
                foreach (TabItem tab in DockManager.Items)
                {
                    if (tab.Content == display)
                    {
                        exists = true;
                        tab.IsSelected = true;
                    }
                }
                if (!exists)
                {
                    if (ContentControl.DataContext is ViewModelBase)
                    {
                        ((ViewModelBase)ContentControl.DataContext).ViewModelControl = display;
                    }
                    var tabItem = new TabItem();
                    tabItem.Content = display;
                    tabItem.Header = title;
                    DockManager.Items.Add(tabItem);
                    tabItem.IsSelected = true;
                }
                display.Focus();
                Keyboard.Focus(display);
            });

        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(_settingsPage, "Settings");
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            MenuToggleButton.IsChecked = false;
            LaunchHelpWindow(null);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenProjectGlobalMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new ProjectsDisplay(EditorController.Runtime), "Projects");
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenModelSystemGlobalMenuItem_Selected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new ModelSystemsDisplay(EditorController.Runtime), "Model Systems");
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseTabButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DockManager.SelectedItem != null)
            {
                DockManager.Items.Remove(DockManager.SelectedItem);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XTMFSideMenuListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XTMFSideMenuListBox.UnselectAll();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DocumentationMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new HelpDialog(EditorController.Runtime.Configuration), "Documentation");
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XTMFWorkspaceListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XTMFWorkspaceListBox.UnselectAll();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SchedulerMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(SchedulerWindow, "Scheduler");
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        ///     Launches remote menu item window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LaunchRemoteMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            var remoteWindow = new LaunchRemoteClientWindow();
            SetDisplayActive(remoteWindow, "Launch Remote Client");
            MenuToggleButton.IsChecked = false;
            Keyboard.Focus(remoteWindow);
            MenuToggleButton.IsChecked = false;
            remoteWindow.Focus();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateXtmfMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            LaunchUpdate = true;
            Close();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DrawerHost.IsLeftDrawerOpen && e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
            {
                DrawerHost.IsLeftDrawerOpen = false;
                e.Handled = true;
            }
            if (e.KeyboardDevice.IsKeyDown(Key.W) &&
                (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl)))
            {
                //before closing, attempt to save / interrupt if control supports
                var tabItem = DockManager.SelectedItem as TabItem;
                if (!(tabItem.Content is ITabCloseListener closeListener && !closeListener.HandleTabClose()))
                {
                    Dispatcher.InvokeAsync(new Action(() => { DockManager.Items.Remove(DockManager.SelectedItem); }));
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XtmfWorkspacesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XtmfWorkspacesListBox.UnselectAll();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            MenuToggleButton.IsChecked = false;
            new AboutXTMF
            {
                Owner = this
            }.ShowDialog();
        }

        /// <summary>
        ///     Closing event of the main window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XtmfWindow_Closing(object sender, CancelEventArgs e)
        {
            foreach (TabItem tabItem in DockManager.Items)
            {
                if (tabItem.Content is ITabCloseListener closeListener && !closeListener.HandleTabClose())
                {
                    e.Cancel = true;
                    break;
                }
            }
            XtmfNotificationIcon.ClearNotificationIcon();
        }

        /// <summary>
        ///     Key listener for the drawer host of the main window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawerHost_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DrawerHost.IsLeftDrawerOpen)
            {
                DrawerHost.IsLeftDrawerOpen = false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XtmfWindow_Initialized(object sender, EventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
        }

        private void NewProjectButton_Click(object sender, MouseButtonEventArgs e)
        {
            NewProject();
        }

        /// <summary>
        ///     Attempts to bring a display into view. Nothing occurs when the display is not already
        ///     created.
        /// </summary>
        /// <param name="display"></param>
        /// <param name="extraData"></param>
        /// <returns></returns>
        public async Task<bool> BringDisplayIntoView(UserControl display, object extraData)
        {
            var isFound = false;
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (TabItem tab in DockManager.Items)
                {
                    if (tab.Content == display)
                    {
                        tab.IsSelected = true;
                        isFound = true;
                        (display as IResumableControl)?.RestoreWithData(extraData);
                        break;
                    }
                }
            });


            return isFound;
        }

        private void OpenModelSystemGlobalMenuItem_Selected(object sender, MouseButtonEventArgs e)
        {

        }
    }
}