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
using MahApps.Metro.Controls;
using Xceed.Wpf.AvalonDock.Layout;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private StartWindow _StartWindow;

        public bool IsNonDefaultConfig = false;

        public bool IsLocalConfig = false;

        private OperationProgressing operationProgressing;

        public string ConfigurationFilePath { get; private set; }

        public event EventHandler RecentProjectsUpdated;

        public ThemeController ThemeController { get; private set; }

        public IDictionary<Project, System.Windows.Controls.UserControl> WorkspaceProjects { get; set; }

        public ActiveEditingSessionDisplayModel EditingDisplayModel
        {
            get => (ActiveEditingSessionDisplayModel)GetValue(EditingDisplayModelProperty);
            set => SetValue(EditingDisplayModelProperty, value);
        }

        // Using a DependencyProperty as the backing store for EditingDisplayModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EditingDisplayModelProperty =
            DependencyProperty.Register("EditingDisplayModel", typeof(ActiveEditingSessionDisplayModel),
                typeof(MainWindow), new PropertyMetadata(null));

        private ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel> DisplaysForLayout =
            new ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel>();

        private ActiveEditingSessionDisplayModel NullEditingDisplayModel;

        private SchedulerWindow _schedulerWindow;

        public ProjectDisplay.ProjectModel.ContainedModelSystemModel ClipboardModel { get; set; }

        public MainWindow()
        {
            // start it with a blank editing display model
            DataContext = this;
            EditingDisplayModel = NullEditingDisplayModel = new ActiveEditingSessionDisplayModel(false);
            ParseCommandLineArgs();
            if (!IsNonDefaultConfig)
            {
                CheckHasLocalConfiguration();
            }
            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight - 9;
            MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth - 4;
            ThemeController = new ThemeController(ConfigurationFilePath == null
                ? System.IO.Path.GetDirectoryName(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XTMF", "Configuration.xml"))
                : System.IO.Path.GetDirectoryName(ConfigurationFilePath));
            InitializeComponent();
            Loaded += FrameworkElement_Loaded;
            Us = this;
            operationProgressing = new OperationProgressing();


            _schedulerWindow = new SchedulerWindow();

            ContentControl.DataContext = new ViewModelBase();
            ViewTitleBlock.DataContext = ContentControl.DataContext;

            SetDisplayActive(new StartWindow(),"XTMF");

            WorkspaceProjects = new Dictionary<Project, System.Windows.Controls.UserControl>();

      
        }



        private void OnCanResizeWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
        }

        private void OnCanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = ResizeMode != ResizeMode.NoResize;

        private void OnCloseWindow(object target, ExecutedRoutedEventArgs e) => SystemCommands.CloseWindow(this);

        private void OnMaximizeWindow(object target, ExecutedRoutedEventArgs e) => SystemCommands.MaximizeWindow(this);

        private void OnMinimizeWindow(object target, ExecutedRoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void OnRestoreWindow(object target, ExecutedRoutedEventArgs e) => SystemCommands.RestoreWindow(this);

        private bool CheckHasLocalConfiguration()
        {
            if (System.IO.File.Exists(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml")))
            {
                IsNonDefaultConfig = true;
                IsLocalConfig = true;
                ConfigurationFilePath =
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml");
                return true;
            }
            return false;
        }

        private void ParseCommandLineArgs()
        {
            /* Check for existence of configuration command line argument
            * to override location of Configuration.xml */
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(arguments, p => p == "--configuration");
            if (index >= 0)
            {
                if (index + 1 < arguments.Length)
                {
                    try
                    {
                        ConfigurationFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, arguments[index + 1]);
                        IsNonDefaultConfig = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(Properties.Resources
                            .MainWindow_ParseCommandLineArgs_Invalid_path_passed_with_configuration_argument_);
                    }
                }
            }
        }

        public List<string> RecentProjects => EditorController.Runtime.Configuration.RecentProjects;

        public bool RuntimeAvailable => EditorController.Runtime != null;

        public bool ShowMetaModuleHiddenParameters { get; set; }

        /// <summary>
        /// Updates GUI with recently opened projects.
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

        private void RecentProjectMenuItem_Click(object sender, RoutedEventArgs e, string projectName) => LoadProjectByName(projectName);

        public void UpdateStatusDisplay(string text) => StatusDisplay.Text = text;

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
            {
                if (document is ProjectDisplay prj)
                {
                    prj.Model.Unload();
                }
                else if (document is ModelSystemDisplay msd)
                {
                    msd.Session.Dispose();
                    msd.Session.ProjectEditingSession.Dispose();
                    if (!msd.CloseRequested())
                    {
                        return true;
                    }
                }
                else if (document is RunWindow run)
                {
                    if (!run.CloseRequested())
                    {
                        return true;
                    }
                }
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

        public void ApplyTheme(ThemeController.Theme theme) => ThemeController.SetThemeActive(theme);

        public void LoadProjectByName(string projectName)
        {
            var project = new Project(projectName, EditorController.Runtime.Configuration, false);
            LoadProject(project);
        }

        public void LoadProject(Project project)
        {
            var progressing = new OperationProgressing()
            {
                Owner = this
            };
            if (project != null && !EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                Task.Run(() =>
                {
                    progressing.Dispatcher.BeginInvoke(new Action(() => { progressing.ShowDialog(); }));
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
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            progressing.Close();
                            var inner = e.InnerException;
                            MessageBox.Show(this, inner.Message, "Error Loading Project", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    progressing.Dispatcher.BeginInvoke(new Action(() =>
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
                            } */
                        }));
                    EditorController.Runtime.Configuration.AddRecentProject(project.Name);
                    EditorController.Runtime.Configuration.Save();
                    UpdateRecentProjectsMenu();
                });
            }
            else if (EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                var item = OpenPages.Find(doc => doc.Title == "Project - " + project.Name);
                if (item != null)
                {
                    item.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// The Singleton instance of the GUI window
        /// </summary>
        internal static MainWindow Us;

        private MouseButtonEventHandler _LastAdded;

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

        public void HideStatusLink() => StatusLinkLabel.Visibility = Visibility.Collapsed;

        public static void SetStatusText(string text) => Us.Dispatcher.BeginInvoke(new Action(() => { Us.StatusDisplay.Text = text; }));

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenProject_Click(object sender, RoutedEventArgs e) => OpenProject();

        public void OpenProject()
        {
            var projectPage = OpenPages.FirstOrDefault(p => p.Content.GetType() == typeof(ProjectsDisplay));
            if (projectPage == null)
            {
                projectPage = AddNewWindow("Projects", new ProjectsDisplay(EditorController.Runtime),
                    typeof(ActiveEditingSessionDisplayModel));
            }
            projectPage.IsSelected = true;
            projectPage.IsActive = true;
        }

        internal void EditProject(ProjectEditingSession projectSession)
        {
            if (projectSession != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var display = new ProjectDisplay()
                        {
                            Session = projectSession,
                        };
                        display.InitiateModelSystemEditingSession += (editingSession) => EditModelSystem(editingSession,
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
                        display.RequestClose += (ignored) =>
                        {
                            //doc.Close();
                            display.Model.Unload();
                            projectSession.NameChanged -= onRename;

                        };
                        SetStatusText("Ready");
                    }
                ));
            }
        }

        private void OpenModelSystem_Click(object sender, RoutedEventArgs e) => OpenModelSystem();

        public void OpenModelSystem() => AddNewWindow("Model Systems", new ModelSystemsDisplay(EditorController.Runtime),
                typeof(ActiveEditingSessionDisplayModel));

        /// <summary>
        /// The pages that are currently open from the main window
        /// </summary>
        private List<LayoutDocument> OpenPages = new List<LayoutDocument>();

        internal LayoutDocument AddNewWindow(string name, UIElement content, Type typeOfController,
            Action onClose = null, string
                contentGuid = null, ActiveEditingSessionDisplayModel model = null)
        {
            var document = new LayoutDocument()
            {
                Title = name,
                Content = content,
                ContentId = contentGuid
            };
            if (model != null)
            {
                DisplaysForLayout.TryAdd(document, model);
            }
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
            TabItem tabItem = new TabItem();
            tabItem.Header = name;
            tabItem.Content = document.Content;
            tabItem.IsSelected = true;
            DockManager.AddToSource(tabItem);
            OpenPages.Add(document);
            document.IsActiveChanged += Document_IsActive;
            if (typeof(ActiveEditingSessionDisplayModel) == typeOfController)
            {
                DisplaysForLayout.TryAdd(document, new ActiveEditingSessionDisplayModel(true));
            }
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
                EditingDisplayModel = DisplaysForLayout.TryGetValue(CurrentDocument, out ActiveEditingSessionDisplayModel displayModel) ?
                    displayModel : NullEditingDisplayModel;
            }
        }

        private bool RunAlreadyExists(string runName, ModelSystemEditingSession session)
        {
            return session.RunNameExists(runName);
        }

        private LayoutDocument CurrentDocument;

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
            var dialog = alreadyExists ?
                (Microsoft.Win32.FileDialog)new Microsoft.Win32.OpenFileDialog
                {
                    Title = title,
                    Filter = filter
                } :
                new Microsoft.Win32.SaveFileDialog
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
                new KeyValuePair<string, string>[]
                    {new KeyValuePair<string, string>("Model System File", "xml")}, true);
            string error = null;
            if (fileName != null)
            {
                var msName = System.IO.Path.GetFileName(fileName);
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

        private void ImportModelSystem_Click(object sender, RoutedEventArgs e) => ImportModelSystem();

        private void Undo_Click(object sender, RoutedEventArgs e) => EditingDisplayModel.Undo();

        private void Redo_Click(object sender, RoutedEventArgs e) => EditingDisplayModel.Redo();

        private void NewModelSystemButton_Click(object sender, RoutedEventArgs e) => NewModelSystem();

        public void NewModelSystem()
        {
            var req = new StringRequest("Model System Name", ValidateName);
            if (req.ShowDialog() == true)
            {
                var name = req.Answer;
                var ms = EditorController.Runtime.ModelSystemController.LoadOrCreate(name);
                ms.ModelSystemStructure.Name = req.Answer;
                EditModelSystem(EditorController.Runtime.ModelSystemController.EditModelSystem(ms));
            }
        }

        public void NewProject()
        {
            var req = new StringRequest("Project Name", ValidateName) { Owner = this };
            if (req.ShowDialog() == true)
            {
                var name = req.Answer;
                string error = null;
                var ms = EditorController.Runtime.ProjectController.LoadOrCreate(name, ref error);
                EditProject(EditorController.Runtime.ProjectController.EditProject(ms));
                EditorController.Runtime.Configuration.AddRecentProject(ms.Name);
                EditorController.Runtime.Configuration.Save();
                UpdateRecentProjectsMenu();
            }
        }

        private bool ValidateName(string name) => Project.ValidateProjectName(name);

        internal void EditModelSystem(ModelSystemEditingSession modelSystemSession, string titleBar = null)
        {
            if (modelSystemSession != null)
            {
                var display = new ModelSystemDisplay()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Session = modelSystemSession,
                    ModelSystem = modelSystemSession.ModelSystemModel,
                };

                display.ContentGuid = Guid.NewGuid().ToString();
                var displayModel = new ModelSystemEditingSessionDisplayModel(display);

                var titleBarName = titleBar ?? (modelSystemSession.EditingProject
                        ? modelSystemSession.ProjectEditingSession.Name + " - " +
                          modelSystemSession.ModelSystemModel.Name
                        : "Model System - " + modelSystemSession.ModelSystemModel.Name)
;
                var doc = AddNewWindow(titleBarName, display, typeof(ModelSystemEditingSessionDisplayModel), null,
                    display.ContentGuid, displayModel);

                //DisplaysForLayout.TryAdd(doc, displayModel);
                PropertyChangedEventHandler onRename = (o, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        doc.Title = modelSystemSession.EditingProject
                            ? modelSystemSession.ProjectEditingSession.Name + " - " +
                              modelSystemSession.ModelSystemModel.Name
                            : "Model System - " + modelSystemSession.ModelSystemModel.Name;
                    });
                };
                modelSystemSession.NameChanged += onRename;
                doc.Closing += (o, e) =>
                {
                    e.Cancel = !display.CloseRequested();
                    if (e.Cancel == false)
                    {
                        modelSystemSession.NameChanged -= onRename;
                    }
                };
                doc.Closed += (o, e) => { modelSystemSession.Dispose(); };
                display.RequestClose += (ignored) => doc.Close();
                doc.IsSelected = true;
                Keyboard.Focus(display);
                display.Focus();
            }
        }

        internal static void MakeWindowActive(UIElement switchTo)
        {
            var us = Us;
            foreach (var page in us.OpenPages)
            {
                if (page.Content == switchTo)
                {
                    page.IsSelected = true;
                    return;
                }
            }
        }

        internal static void ShowPageContaining(object content)
        {
            var result = Us.OpenPages.FirstOrDefault((page) => page.Content == content);
            if (result != null)
            {
                result.IsActive = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            foreach (var document in OpenPages.Select(page => page.Content).ToList())
            {
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
            }
            if (!e.Cancel)
            {
                EditorController.Unregister(this);
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
                                    MessageBox.Show("We were unable to find XTMF.Update2.exe!", "Updater Missing!",
                                        MessageBoxButton.OK, MessageBoxImage.Error));
                            }
                        })
                        .Wait();
                }
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

        private void AboutXTMF_Click(object sender, RoutedEventArgs e)
        {
            new AboutXTMF()
            {
                Owner = this
            }.ShowDialog();
        }

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
                if (DisplaysForLayout.TryRemove(activeDocument, out _))
                {
                    EditingDisplayModel = NullEditingDisplayModel;
                }
                activeDocument.Close();
            }
        }

        private string DocumentationName = "TMG XTMF Documentation.pdf";

        private void ShowDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DocumentationName));
            }
            catch
            {
                MessageBox.Show("We were unable to find the documentation", "Documentation Missing!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Adds the run session to the scheduler window. A new scheduler window is created if it does not already exist.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        internal void AddRunToSchedulerWindow(RunWindow runWindow)
        {
            //create a new scheduler window if one does not exist
            if(!OpenPages.Exists((doc) => doc.Content == _schedulerWindow))
            {
                var doc = new LayoutDocument()
                {
                    Content = _schedulerWindow,
                    Title = "XTMF Run Scheduler"

                };

                OpenPages.Add(doc);
                DockManager.AddToSource(new TabItem());
                //DocumentPane.Children.Add(doc);
                doc.IsActive = true;
            }

            (OpenPages.Find((doc) => doc.Content == this._schedulerWindow).Content as SchedulerWindow).AddRun(runWindow);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="run"></param>
        /// <param name="runName"></param>
        /// <returns></returns>
        internal RunWindow CreateRunWindow(ModelSystemEditingSession session, XTMFRun run, string runName)
        {
            var runWindow = new RunWindow(session, run, runName);
            var doc = new LayoutDocument()
            {
                Content = runWindow,
                Title = "Run - " + runName
            };
            doc.Closing += (object sender, CancelEventArgs e) =>
            {
                e.Cancel = !runWindow.CloseRequested();
            };
            //OpenPages.Add(doc);
            //DocumentPane.Children.Add(doc);
            //doc.Float();
            return runWindow;
        }

        private const string UpdateProgram = "XTMF.Update2.exe";

        private bool LaunchUpdate = false;

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
            documentationControl.RequestClose += (ignored) => doc.Close();
            doc.IsSelected = true;
            Keyboard.Focus(documentationControl);
            documentationControl.Focus();
        }

        private void RunMenu_Click(object sender, RoutedEventArgs e) => ExecuteRun();

        private void RunModel_MouseUp(object sender, MouseButtonEventArgs e) => ExecuteRun();

        private void RunRemoteMenu_Click(object sender, RoutedEventArgs e)
        {
            var remoteWindow = new LaunchRemoteClientWindow();
            var doc = AddNewWindow("Launch Remote Client", remoteWindow, typeof(ActiveEditingSessionDisplayModel));
            remoteWindow.RequestClose += (ignored) => doc.Close();
            doc.IsSelected = true;
            Keyboard.Focus(remoteWindow);
            remoteWindow.Focus();
        }

        public void ExecuteRun()
        {
            var document = CurrentDocument;
            if (document.Content is ModelSystemDisplay modelSystem)
            {
                modelSystem.ExecuteRun();
            }
        }

        internal void CloseWindow(UIElement window)
        {
            var page = OpenPages.FirstOrDefault(p =>
            {
                if (p.Content == window)
                {
                    if (!p.CanClose)
                    {
                        p.CanClose = true;
                    }
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
                if (p.Content == window)
                {
                    return true;
                }
                return false;
            });
            if (page != null)
            {
                page.Title = newName;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="getHelpFor"></param>
        public void LaunchHelpWindow(ModelSystemStructureModel getHelpFor = null)
        {
            var helpUI = new UserControls.Help.HelpDialog(EditorController.Runtime.Configuration);
            if (getHelpFor != null)
            {
                helpUI.SelectModuleContent(getHelpFor);
            }
            var document = AddNewWindow("Help", helpUI, typeof(ActiveEditingSessionDisplayModel));
            document.IsSelected = true;
            Keyboard.Focus(helpUI);
        }

        public void LoadPageId(string id)
        {
            Dispatcher.BeginInvoke((MethodInvoker)delegate
           {
               var item = OpenPages.Find(doc => doc.ContentId == id);
               if (item != null)
               {
                   item.IsSelected = true;
               }
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

        private void LaunchHelpWindow_Click(object sender, RoutedEventArgs e) => LaunchHelpWindow();

        private void NewProjectButton_Click(object sender, RoutedEventArgs e) => NewProject();

        private void Settings_Click(object sender, RoutedEventArgs e) => LaunchSettingsPage();

        private void MetaModuleHiddenParametersToggle_Click(object sender, RoutedEventArgs e)
        {
            if (EditingDisplayModel != NullEditingDisplayModel)
            {
                ShowMetaModuleHiddenParameters = !ShowMetaModuleHiddenParameters;
                var document = CurrentDocument;
                if (document.Content is ModelSystemDisplay)
                {
                    (document.Content as ModelSystemDisplay)?.ExternalUpdateParameters();
                }
            }
        }

        /*
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                if (e.GetPosition(this).Y < 90)
                {
                    e.Handled = true;
                    DragMove();
                }
            }
        } */

        private void MaxNorm_OnClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else if (WindowState == WindowState.Normal)
            {
                ExternalGrid.Margin = new Thickness()
                {
                    Left = 0,
                    Top = 0,
                    Right = 0,
                    Bottom = 0,
                };
                SystemCommands.MaximizeWindow(this);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.Normal)
            {
                SystemCommands.MinimizeWindow(this);
            }
            else
            {
                SystemCommands.RestoreWindow(this);
            }
        }

        private void MainWindow_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.GetPosition(this).Y < 90)
                {
                    e.Handled = true;
                    //MaxNorm_OnClick(null, null);
                }
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Normal:
                    WindowGrid.Margin = new Thickness()
                    {
                        Left = 0,
                        Top = 0,
                        Right = 0,
                        Bottom = 0
                    };
                    break;
                case WindowState.Maximized:
                    WindowGrid.Margin = new Thickness()
                    {
                        Left = 7,
                        Top = 7,
                        Right = 7,
                        Bottom = 7
                    };
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="display"></param>
        /// <param name="title"></param>
        private void SetDisplayActive(System.Windows.Controls.UserControl display, string title)
        {
            ((ViewModelBase)ContentControl.DataContext).ViewModelControl = display;
            ((ViewModelBase)ContentControl.DataContext).ViewTitle = title;
        }

        private void DockManager_ActiveContentChanged(object sender, EventArgs e)
        {
            /*EditingDisplayModel =
                DisplaysForLayout.ContainsKey((LayoutDocument)DockManager.Layout.ActiveContent)
                    ? DisplaysForLayout[(LayoutDocument)DockManager.Layout.ActiveContent]
                    : NullEditingDisplayModel; */
        }

       public System.Windows.Controls.UserControl CurrentViewModel { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBoxItem_OnSelected(object sender, RoutedEventArgs e)
        {

            SetDisplayActive(new SettingsPage(EditorController.Runtime.Configuration),"Settings");

            MenuToggleButton.IsChecked = false;
            //this.Settings_Click(sender,e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HelpMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            MenuToggleButton.IsChecked = false;
            LaunchHelpWindow(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenProjectGlobalMenuItem_OnSelected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new ProjectsDisplay(EditorController.Runtime), "Projects" );
            MenuToggleButton.IsChecked = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenModelSystemGlobalMenuItem_Selected(object sender, RoutedEventArgs e)
        {
            SetDisplayActive(new ModelSystemsDisplay(EditorController.Runtime), "Model Systems");
            MenuToggleButton.IsChecked = false;
        }
    }

}
