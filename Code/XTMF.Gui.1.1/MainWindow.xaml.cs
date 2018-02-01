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
using Dragablz;
using MahApps.Metro.Controls;
using Xceed.Wpf.AvalonDock.Layout;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls;
using XTMF.Gui.UserControls.Help;
using XTMF.Gui.UserControls.Interfaces;
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

        private MouseButtonEventHandler _LastAdded;

        private StartWindow _StartWindow;

        private LayoutDocument CurrentDocument;

        private readonly ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel> DisplaysForLayout =
            new ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel>();

        private readonly string DocumentationName = "TMG XTMF Documentation.pdf";

        public bool IsLocalConfig;

        public bool IsNonDefaultConfig;

        private bool LaunchUpdate;

        private readonly ActiveEditingSessionDisplayModel NullEditingDisplayModel;

        /// <summary>
        ///     The pages that are currently open from the main window
        /// </summary>
        private readonly List<LayoutDocument> OpenPages = new List<LayoutDocument>();

        private OperationProgressing operationProgressing;

        public MainWindow()
        {
            ViewModelBase = new ViewModelBase();

            EditingDisplayModel = NullEditingDisplayModel = new ActiveEditingSessionDisplayModel(false);
            ParseCommandLineArgs();
            if (!IsNonDefaultConfig) CheckHasLocalConfiguration();
            //MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight - 9;
            //MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth - 4;
            ThemeController = new ThemeController(ConfigurationFilePath == null
                ? Path.GetDirectoryName(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XTMF", "Configuration.xml"))
                : Path.GetDirectoryName(ConfigurationFilePath));
            InitializeComponent();
            Loaded += FrameworkElement_Loaded;
            Us = this;
            operationProgressing = new OperationProgressing();

            SchedulerWindow = new SchedulerWindow();

            ViewDockPanel.DataContext = ViewModelBase;
            ContentControl.DataContext = ViewModelBase;
            FilterBox.DataContext = ContentControl.DataContext;
            ViewTitleBlock.DataContext = ContentControl.DataContext;

            DockManager.InterTabController.InterTabClient = new InterTabClient();

            DockManager.ClosingItemCallback = ClosingItemCallback;

            WorkspaceProjects = new Dictionary<Project, UserControl>();
        }

        public string ConfigurationFilePath { get; private set; }

        public ThemeController ThemeController { get; }

        public IDictionary<Project, UserControl> WorkspaceProjects { get; set; }

        public ViewModelBase ViewModelBase { get; set; }

        public ActiveEditingSessionDisplayModel EditingDisplayModel
        {
            get => (ActiveEditingSessionDisplayModel) GetValue(EditingDisplayModelProperty);
            set => SetValue(EditingDisplayModelProperty, value);
        }

        public ProjectDisplay.ProjectModel.ContainedModelSystemModel ClipboardModel { get; set; }

        public SchedulerWindow SchedulerWindow { get; }

        public List<string> RecentProjects => EditorController.Runtime.Configuration.RecentProjects;

        public bool RuntimeAvailable => EditorController.Runtime != null;

        public bool ShowMetaModuleHiddenParameters { get; set; }


        public UserControl CurrentViewModel { get; set; }

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
                    //projectDisplay.Session.NameChanged -= onRename;
                    projectDisplay.Session.Dispose();
                    WorkspaceProjects.Remove(projectDisplay.Session.Project);
                }
            }

            if ((args.DragablzItem.Content as TabItem)?.Content is ITabCloseListener closeListener &&
                !closeListener.HandleTabClose()) args.Cancel();
        }

        /// <summary>
        ///     Determines if a local Configuration file exists.
        /// </summary>
        /// <returns></returns>
        private bool CheckHasLocalConfiguration()
        {
            if (File.Exists(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml")))
            {
                IsNonDefaultConfig = true;
                IsLocalConfig = true;
                ConfigurationFilePath =
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml");
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Parses command line arguments
        /// </summary>
        private void ParseCommandLineArgs()
        {
            /* Check for existence of configuration command line argument
            * to override location of Configuration.xml */
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(arguments, p => p == "--configuration");
            if (index >= 0)
                if (index + 1 < arguments.Length)
                    try
                    {
                        ConfigurationFilePath =
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, arguments[index + 1]);
                        IsNonDefaultConfig = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(Properties.Resources
                            .MainWindow_ParseCommandLineArgs_Invalid_path_passed_with_configuration_argument_);
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
        ///     Reloads XTMF using the default configuration
        /// </summary>
        public void ReloadWithDefaultConfiguration()
        {
            if (!ClosePages())
            {
                IsEnabled = false;
                StatusDisplay.Text = "Loading XTMF";
                ConfigurationFilePath = null;
                IsNonDefaultConfig = false;
                EditorController.Register(this, () =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsEnabled = true;
                        StatusDisplay.Text = "Ready";
                        //RecentProjects.Clear();
                        UpdateRecentProjectsMenu();
                        ShowStart_Click(this, null);
                    }));
                });
            }
        }

        private bool ClosePages()
        {
            foreach (var document in OpenPages.Select(page => page.Content))
                if (document is ProjectDisplay prj)
                {
                    prj.Model.Unload();
                }
                else if (document is ModelSystemDisplay msd)
                {
                    msd.Session.Dispose();
                    msd.Session.ProjectEditingSession.Dispose();
                    if (!msd.CloseRequested()) return true;
                }
                else if (document is RunWindow run)
                {
                    if (!run.CloseRequested()) return true;
                }

            EditorController.Runtime.ProjectController.ClearEditingSessions();
            EditorController.Unregister(this);
            //DockManager.RemoveFromSource();
            //DocumentPane.Children.Clear();
            OpenPages.Clear();
            DisplaysForLayout.Clear();
            EditorController.FreeRuntime();
            return false;
        }

        /// <summary>
        ///     Reloads Configuration using the specified names
        /// </summary>
        /// <param name="name"></param>
        public void ReloadWithConfiguration(string name)
        {
            if (!ClosePages())
            {
                IsEnabled = false;
                StatusDisplay.Text = "Loading XTMF";
                IsNonDefaultConfig = true;
                ConfigurationFilePath = name;
                var configuration = new Configuration(name);
                EditorController.Register(this, () =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsEnabled = true;
                        StatusDisplay.Text = "Ready";
                        UpdateRecentProjectsMenu();
                        ShowStart_Click(this, null);
                    }));
                });
            }
        }

        private void FrameworkElement_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            StatusDisplay.Text = "Loading XTMF";
            ShowStart_Click(this, null);
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
            var project = new Project(projectName, EditorController.Runtime.Configuration, false);
            //WorkspaceProjects['projectN']
            LoadProject(project);
        }

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
                    // progressing.Dispatcher.BeginInvoke(new Action(() => { progressing.ShowDialog(); }));
                    try
                    {
                        ProjectEditingSession session = null;
                        var loadingTask = Task.Run(() =>
                        {
                            session = EditorController.Runtime.ProjectController.EditProject(project);
                        });
                        loadingTask.Wait();
                        //progressing.Close();
                        if (session != null) Us.EditProject(session);
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

                    /*progressing.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Application.Current.Dispatcher.Invoke((Action)delegate { progressing.Close(); });
                            //progressing.Visibility = Visibility.Hidden;

                            if(WorkspaceProjects.ContainsKey(project))
                            {
                                SetDisplayActive(WorkspaceProjects[project], project.Name);
                            }

                            else {
                                ProjectDisplay display = new ProjectDisplay();
                                SetDisplayActive(display, project.Name);
                            }
                            

                            
                            /*var item = OpenPages.Find(doc => doc.Title == "Project - " + project.Name);
                            if (item != null)
                            {
                                item.IsSelected = true;
                            } 
                        }));*/
                    EditorController.Runtime.Configuration.AddRecentProject(project.Name);
                    EditorController.Runtime.Configuration.Save();
                    UpdateRecentProjectsMenu();
                });
            }
            else if (EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                var projectContorl = WorkspaceProjects[project];
                var visible = false;
                foreach (TabItem tabItem in DockManager.Items)
                    if (tabItem.Content == projectContorl)
                    {
                        visible = true;
                        break;
                    }

                if (!visible) SetDisplayActive(projectContorl, "Project - " + project.Name);
            }
        }

        public void SetStatusLink(string text, Action clickAction)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLinkLabel.Visibility = Visibility.Visible;

                var handler = new MouseButtonEventHandler((e, a) => { clickAction.BeginInvoke(null, null); });

                if (_LastAdded != null) StatusLinkLabel.MouseDown -= _LastAdded;
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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            OpenProject();
        }

        public void OpenProject()
        {
            SetDisplayActive(new ProjectsDisplay(EditorController.Runtime), "Projects");
        }

        internal void EditProject(ProjectEditingSession projectSession)
        {
            if (projectSession != null)
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
                        /*var doc = AddNewWindow("Project - " + projectSession.Project.Name, display,
                            typeof(ActiveEditingSessionDisplayModel), () => { projectSession.Dispose(); });
                        doc.IsSelected = true; */
                        PropertyChangedEventHandler onRename = (o, e) =>
                        {
                            //doc.Title = "Project - " + projectSession.Project.Name;
                        };
                        projectSession.NameChanged += onRename;


                        SetStatusText("Ready");
                    }
                ));
        }

        private void OpenModelSystem_Click(object sender, RoutedEventArgs e)
        {
            OpenModelSystem();
        }

        public void OpenModelSystem()
        {
            AddNewWindow("Model Systems", new ModelSystemsDisplay(EditorController.Runtime),
                typeof(ActiveEditingSessionDisplayModel));
        }

        internal LayoutDocument AddNewWindow(string name, UIElement content, Type typeOfController,
            Action onClose = null, string
                contentGuid = null, ActiveEditingSessionDisplayModel model = null)
        {
            var document = new LayoutDocument
            {
                Title = name,
                Content = content,
                ContentId = contentGuid
            };
            if (model != null) DisplaysForLayout.TryAdd(document, model);
            document.Closed += (source, ev) =>
            {
                //integrate into the main window
                var layout = source as LayoutDocument;
                OpenPages.Remove(layout);
                ActiveEditingSessionDisplayModel _;
                DisplaysForLayout.TryRemove(layout, out _);
                onClose?.Invoke();
                Focus();
            };
            //var insertedTo = DocumentPane.Children.Count;
            //DocumentPane.InsertChildAt(0, document);
            var tabItem = new TabItem();
            tabItem.Header = name;
            tabItem.Content = document.Content;
            tabItem.IsSelected = true;
            DockManager.AddToSource(tabItem);
            OpenPages.Add(document);
            document.IsActiveChanged += Document_IsActive;
            if (typeof(ActiveEditingSessionDisplayModel) == typeOfController)
                DisplaysForLayout.TryAdd(document, new ActiveEditingSessionDisplayModel(true));
            // initialize the new window
            Document_IsActive(document, null);
            document.IsSelected = true;
            return document;
        }

        private void Document_IsActive(object sender, EventArgs e)
        {
            if (sender is LayoutDocument document)
            {
                CurrentDocument = document;
                EditingDisplayModel = DisplaysForLayout.TryGetValue(CurrentDocument, out var displayModel)
                    ? displayModel
                    : NullEditingDisplayModel;
            }
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
                ? (FileDialog) new OpenFileDialog
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
                if (result.Result) return result.FileName;
            }
            else
            {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) return dialog.SelectedPath;
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
                    switch (MessageBox.Show(this, error + "\r\nWould you like to overwrite?",
                        "Unable to import", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No))
                    {
                        case MessageBoxResult.Yes:
                        {
                            if (!EditorController.Runtime.ModelSystemController.ImportModelSystem(fileName, true,
                                ref error))
                                MessageBox.Show(this, error, "Unable to import", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            else
                                MessageBox.Show(this,
                                    "The model system has been successfully imported from '" + fileName + "'.",
                                    "Model System Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                            break;
                    }
            }
        }

        private void ImportModelSystem_Click(object sender, RoutedEventArgs e)
        {
            ImportModelSystem();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.Redo();
        }

        private async void NewModelSystemButton_Click(object sender, RoutedEventArgs e)
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

        internal void EditModelSystem(ModelSystemEditingSession modelSystemSession, string titleBar = null)
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
                // var displayModel = new ModelSystemEditingSessionDisplayModel(display);

                var titleBarName = titleBar ?? (modelSystemSession.EditingProject
                                       ? modelSystemSession.ProjectEditingSession.Name + " - " +
                                         modelSystemSession.ModelSystemModel.Name
                                       : "Model System - " + modelSystemSession.ModelSystemModel.Name);
                SetDisplayActive(display, titleBarName);

                //DisplaysForLayout.TryAdd(doc, displayModel);
                PropertyChangedEventHandler onRename = (o, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        //doc.Title = modelSystemSession.EditingProject
                        //    ? modelSystemSession.ProjectEditingSession.Name + " - " +
                        //      modelSystemSession.ModelSystemModel.Name
                        //    : "Model System - " + modelSystemSession.ModelSystemModel.Name;
                    });
                };
                modelSystemSession.NameChanged += onRename;
                /*doc.Closing += (o, e) =>
                {
                    e.Cancel = !display.CloseRequested();
                    if (e.Cancel == false)
                    {
                        modelSystemSession.NameChanged -= onRename;
                    }
                };
                doc.Closed += (o, e) => { modelSystemSession.Dispose(); };
                display.RequestClose += (ignored) => doc.Close();
                doc.IsSelected = true; */
                Keyboard.Focus(display);
                display.Focus();
            }
        }

        internal static void MakeWindowActive(UIElement switchTo)
        {
            var us = Us;
            foreach (var page in us.OpenPages)
                if (page.Content == switchTo)
                {
                    page.IsSelected = true;
                    return;
                }
        }

        internal static void ShowPageContaining(object content)
        {
            var result = Us.OpenPages.FirstOrDefault(page => page.Content == content);
            if (result != null) result.IsActive = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            foreach (var document in OpenPages.Select(page => page.Content).ToList())
                if (document is ModelSystemDisplay modelSystemPage)
                {
                    if (!modelSystemPage.CloseRequested())
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (document is RunWindow runPage)
                {
                    if (!runPage.CloseRequested())
                    {
                        e.Cancel = true;
                        return;
                    }
                }

            if (!e.Cancel)
            {
                EditorController.Unregister(this);
                if (LaunchUpdate)
                    Task.Run(() =>
                        {
                            var path = Assembly.GetExecutingAssembly().Location;
                            try
                            {
                                Process.Start(
                                    Path.Combine(Path.GetDirectoryName(path), UpdateProgram),
                                    Process.GetCurrentProcess().Id + " \"" + path + "\"");
                            }
                            catch
                            {
                                Dispatcher.Invoke(() =>
                                    MessageBox.Show("We were unable to find XTMF.Update2.exe!", "Updater Missing!",
                                        MessageBoxButton.OK, MessageBoxImage.Error));
                            }
                        })
                        .Wait();
            }

            base.OnClosing(e);
            if (!e.Cancel)
            {
                Application.Current.Shutdown(0);
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var handel = Process.GetCurrentProcess().Handle;
                    TerminateProcess(handel, 0);
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


        private void ShowStart_Click(object sender, RoutedEventArgs e)
        {
            var item = OpenPages.Find(doc => doc.Title == "Start");
            if (item != null)
            {
                item.IsSelected = true;
            }
            else
            {
                _StartWindow = new StartWindow();
                var doc = AddNewWindow("Start", _StartWindow, typeof(ActiveEditingSessionDisplayModel));
            }
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            var activeDocument = OpenPages.FirstOrDefault(x => x.IsActive);
            if (activeDocument != null)
            {
                ActiveEditingSessionDisplayModel _;
                if (DisplaysForLayout.TryRemove(activeDocument, out _)) EditingDisplayModel = NullEditingDisplayModel;
                activeDocument.Close();
            }
        }

        private void ShowDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    DocumentationName));
            }
            catch
            {
                MessageBox.Show("We were unable to find the documentation", "Documentation Missing!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            SetDisplayActive(SchedulerWindow, "Scheduler", false);

            // DockManager.Items.Add((OpenPages.Find((doc) => doc.Content == this._schedulerWindow).Content as SchedulerWindow))
        }

        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <returns></returns>
        internal RunWindow CreateRunWindow(ModelSystemEditingSession session, XTMFRun run, string runName)
        {
            var runWindow = new RunWindow(session, run, runName);

            return runWindow;
        }

        private void UpdateXTMF_Click(object sender, RoutedEventArgs e)
        {
            LaunchUpdate = true;
            Close();
        }

        private void ReleaseMemory_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        internal void NewDocumentationWindow(DocumentationControl documentationControl)
        {
            var doc = AddNewWindow("Documentation - " + documentationControl.TypeNameText, documentationControl,
                typeof(ActiveEditingSessionDisplayModel));
            documentationControl.RequestClose += ignored => doc.Close();
            doc.IsSelected = true;
            Keyboard.Focus(documentationControl);
            documentationControl.Focus();
        }

        private void RunMenu_Click(object sender, RoutedEventArgs e)
        {
            ExecuteRun();
        }

        private void RunModel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ExecuteRun();
        }

        private void RunRemoteMenu_Click(object sender, RoutedEventArgs e)
        {
            var remoteWindow = new LaunchRemoteClientWindow();
            var doc = AddNewWindow("Launch Remote Client", remoteWindow, typeof(ActiveEditingSessionDisplayModel));
            remoteWindow.RequestClose += ignored => doc.Close();
            doc.IsSelected = true;
            Keyboard.Focus(remoteWindow);
            remoteWindow.Focus();
        }

        public void ExecuteRun()
        {
            var document = CurrentDocument;
            if (document.Content is ModelSystemDisplay modelSystem) modelSystem.ExecuteRun();
        }

        internal void CloseWindow(UIElement window)
        {
            var page = OpenPages.FirstOrDefault(p =>
            {
                if (p.Content == window)
                {
                    if (!p.CanClose) p.CanClose = true;
                    return true;
                }

                return false;
            });
            page?.Close();
        }

        internal void SetWindowName(object window, string newName)
        {
            var page = OpenPages.FirstOrDefault(p =>
            {
                if (p.Content == window) return true;
                return false;
            });
            if (page != null) page.Title = newName;
        }

        /// <summary>
        /// </summary>
        /// <param name="getHelpFor"></param>
        public void LaunchHelpWindow(ModelSystemStructureModel getHelpFor = null)
        {
            var helpUI = new HelpDialog(EditorController.Runtime.Configuration);
            if (getHelpFor != null) helpUI.SelectModuleContent(getHelpFor);
            var document = AddNewWindow("Help", helpUI, typeof(ActiveEditingSessionDisplayModel));
            document.IsSelected = true;
            Keyboard.Focus(helpUI);
        }

        public void LoadPageId(string id)
        {
            Dispatcher.BeginInvoke((MethodInvoker) delegate
            {
                var item = OpenPages.Find(doc => doc.ContentId == id);
                if (item != null) item.IsSelected = true;
            });
        }

        private void LaunchSettingsPage()
        {
            var item = OpenPages.Find(doc => doc.Title == "Settings");
            if (item != null)
            {
                item.IsSelected = true;
            }
            else
            {
                var settingsPage = new SettingsPage(EditorController.Runtime.Configuration);
                var document = AddNewWindow("Settings", settingsPage, typeof(ActiveEditingSessionDisplayModel));
                document.Closing += (o, e) => { settingsPage.Close(); };
                document.IsSelected = true;
                Keyboard.Focus(settingsPage);
            }
        }

        private void LaunchHelpWindow_Click(object sender, RoutedEventArgs e)
        {
            LaunchHelpWindow();
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            NewProject();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            LaunchSettingsPage();
        }

        private void MetaModuleHiddenParametersToggle_Click(object sender, RoutedEventArgs e)
        {
            if (EditingDisplayModel != NullEditingDisplayModel)
            {
                ShowMetaModuleHiddenParameters = !ShowMetaModuleHiddenParameters;
                var document = CurrentDocument;
                if (document.Content is ModelSystemDisplay)
                    (document.Content as ModelSystemDisplay)?.ExternalUpdateParameters();
            }
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
                    if (tab.Content == display)
                    {
                        exists = true;
                        tab.IsSelected = true;
                    }

                if (!exists)
                {
                    ((ViewModelBase) ContentControl.DataContext).ViewModelControl = display;
                    ((ViewModelBase) ContentControl.DataContext).ViewTitle = title;
                    ((ViewModelBase) ContentControl.DataContext).IsSearchBoxVisible = searchable;

                    var tabItem = new TabItem();
                    tabItem.Content = display;
                    tabItem.Header = title;
                    DockManager.Items.Add(tabItem);
                    tabItem.IsSelected = true;
                }
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new SettingsPage(EditorController.Runtime.Configuration), "Settings");
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
            if (DockManager.SelectedItem != null) DockManager.Items.Remove(DockManager.SelectedItem);
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
            // XTMFWorkspaceListBox.UnselectAll();
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
        private void WindowGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DrawerHost.IsLeftDrawerOpen) DrawerHost.IsLeftDrawerOpen = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DrawerHost.IsLeftDrawerOpen) DrawerHost.IsLeftDrawerOpen = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void XtmfWorkspacesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XTMFWorkspaceListBox.UnselectAll();
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
                if (tabItem.Content is ITabCloseListener closeListener && !closeListener.HandleTabClose())
                {
                    e.Cancel = true;
                    break;
                }
        }
    }
}