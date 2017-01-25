/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Xceed.Wpf.AvalonDock.Layout;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls;
using XTMF.Gui.UserControls.Help;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace XTMF.Gui
{
    internal class ExtendedLayoutDocument : LayoutDocument
    {
        public enum DocumentType
        {
            Project,
            ModelSystem,
            Interface
        }

        private DocumentType type;
        private string identifier;
    }

    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private StartWindow _startWindow;

        public bool IsNonDefaultConfig;

        public bool IsLocalConfig;

        private OperationProgressing _operationProgressing;

        public string ConfigurationFilePath { get; private set; }

        public event EventHandler RecentProjectsUpdated;

        public ActiveEditingSessionDisplayModel EditingDisplayModel
        {
            get { return (ActiveEditingSessionDisplayModel) GetValue(EditingDisplayModelProperty); }
            set { SetValue(EditingDisplayModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for EditingDisplayModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EditingDisplayModelProperty =
            DependencyProperty.Register("EditingDisplayModel", typeof(ActiveEditingSessionDisplayModel),
                typeof(MainWindow), new PropertyMetadata(null));

        private readonly ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel> _displaysForLayout =
            new ConcurrentDictionary<LayoutDocument, ActiveEditingSessionDisplayModel>();

        private readonly ActiveEditingSessionDisplayModel _nullEditingDisplayModel;

        public MainWindow()
        {
            // start it with a blank editing display model
            DataContext = this;
            EditingDisplayModel = _nullEditingDisplayModel = new ActiveEditingSessionDisplayModel(false);

            ParseCommandLineArgs();
            if (!IsNonDefaultConfig)
                CheckHasLocalConfiguration();
            InitializeComponent();

            Loaded += FrameworkElement_Loaded;
            Us = this;

            _operationProgressing = new OperationProgressing();
            //operationProgressing.Owner = this;
        }

        private bool CheckHasLocalConfiguration()
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml")))
            {
                IsNonDefaultConfig = true;
                IsLocalConfig = true;
                ConfigurationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration.xml");
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

            if (index < 0) return;
            if (index + 1 >= arguments.Length) return;
            try
            {
                ConfigurationFilePath =
                    ConfigurationFilePath =
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, arguments[index + 1]);

                IsNonDefaultConfig = true;
            }
            catch (Exception)
            {
                Console.WriteLine(
                    Properties.Resources
                        .MainWindow_ParseCommandLineArgs_Invalid_path_passed_with_configuration_argument_);
            }
        }

        public List<string> RecentProjects => EditorController.Runtime.Configuration.RecentProjects;

        public bool RuntimeAvailable => EditorController.Runtime != null;

        public bool ShowMetaModuleHiddenParameters { get; set; }

        /// <summary>
        ///     Updates GUI with recently opened projects.
        /// </summary>
        public void UpdateRecentProjectsMenu()
        {
            var recentProjects = EditorController.Runtime.Configuration.RecentProjects;
            recentProjects.Reverse();
            Dispatcher.Invoke(() =>
            {
                RecentProjectsMenuItem.Items.Clear();
                foreach (var recentProject in recentProjects)
                {
                    var recentProjectMenuItem = new MenuItem {Header = recentProject};
                    RecentProjectsMenuItem.Items.Add(recentProjectMenuItem);

                    recentProjectMenuItem.Click +=
                        (sender, eventArgs) => { RecentProjectMenuItem_Click(sender, eventArgs, recentProject); };
                }


                RecentProjectsUpdated?.Invoke(this, new EventArgs());
            });
        }

        private void RecentProjectMenuItem_Click(object sender, RoutedEventArgs e, string projectName)
        {
            LoadProjectByName(projectName);
        }

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
                var modelSystemPage = document as ModelSystemDisplay;
                var runPage = document as RunWindow;

                if (document is ProjectDisplay)
                {
                    var projectDisplay = document as ProjectDisplay;

                    projectDisplay.Model.Unload();
                }
                if (modelSystemPage != null)
                {
                    modelSystemPage.Session.Dispose();
                    modelSystemPage.Session.ProjectEditingSession.Dispose();
                }


                if (modelSystemPage != null)
                    if (!modelSystemPage.CloseRequested())
                        return true;
                if (runPage != null)
                    if (!runPage.CloseRequested())
                        return true;
            }

            EditorController.Runtime.ProjectController.ClearEditingSessions();

            EditorController.Unregister(this);

            DocumentPane.Children.Clear();

            OpenPages.Clear();

            _displaysForLayout.Clear();

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
            EditorController.Register(this, () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    IsEnabled = true;
                    StatusDisplay.Text = "Ready";
                    UpdateRecentProjectsMenu();
                }));
            });
            ShowStart_Click(this, null);
        }

        public void LoadProjectByName(string projectName)
        {
            var project = new Project(projectName, EditorController.Runtime.Configuration, false);
            LoadProject(project);
        }

        public void LoadProject(Project project)
        {
            // EditorController.Runtime.ProjectController.Edi

            var progressing = new OperationProgressing
            {
                Owner = this
            };

            if (project != null && !EditorController.Runtime.ProjectController.IsEditSessionOpenForProject(project))
            {
                Task.Run(() =>
                {
                    progressing.Dispatcher.BeginInvoke(new Action(() => { progressing.ShowDialog(); }));
                    ProjectEditingSession session = null;
                    var loadingTask =
                        Task.Run(() => { session = EditorController.Runtime.ProjectController.EditProject(project); });

                    loadingTask.Wait();
                    if (session != null)
                        Us.EditProject(session);

                    progressing.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Application.Current.Dispatcher.Invoke(delegate { progressing.Close(); });
                        //progressing.Visibility = Visibility.Hidden;


                        var item = OpenPages.Find(doc => doc.Title == "Project - " + project.Name);

                        if (item != null)
                            item.IsSelected = true;
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
                    item.IsSelected = true;
            }
        }

        internal static MainWindow Us;

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
            if (OpenPages.Count(p => p.Title == "Projects") != 0)
            {
                OpenPages.Find(p => p.Title == "Projects").IsSelected = true;
            }
            else
            {
                var doc = AddNewWindow("Projects", new ProjectsDisplay(EditorController.Runtime),
                    typeof(ActiveEditingSessionDisplayModel));
                doc.IsActive = true;
            }
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
                        display.InitiateModelSystemEditingSession += editingSession => EditModelSystem(editingSession);
                        var doc = AddNewWindow("Project - " + projectSession.Project.Name, display,
                            typeof(ActiveEditingSessionDisplayModel), () => { projectSession.Dispose(); });
                        doc.IsSelected = true;
                        PropertyChangedEventHandler onRename =
                            (o, e) => { doc.Title = "Project - " + projectSession.Project.Name; };
                        projectSession.NameChanged += onRename;
                        display.RequestClose += ignored =>
                        {
                            doc.Close();
                            display.Model.Unload();
                            projectSession.NameChanged -= onRename;
                        };

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
            var doc = AddNewWindow("Model Systems", new ModelSystemsDisplay(EditorController.Runtime),
                typeof(ActiveEditingSessionDisplayModel));
            doc.IsActive = true;
        }


        /// <summary>
        ///     The pages that are currently open from the main window
        /// </summary>
        private readonly List<LayoutDocument> OpenPages = new List<LayoutDocument>();


        internal LayoutDocument AddNewWindow(string name, UIElement content, Type typeOfController,
            Action onClose = null)
        {
            var document = new LayoutDocument
            {
                Title = name,
                Content = content
            };


            document.Closed += (source, ev) =>
            {
                //integrate into the main window

                var layout = source as LayoutDocument;
                OpenPages.Remove(layout);
                ActiveEditingSessionDisplayModel _;
                _displaysForLayout.TryRemove(layout, out _);
                // run the default code


                onClose?.Invoke();
                Focus();
            };
            OpenPages.Add(document);
            DocumentPane.Children.Add(document);
            document.IsActiveChanged += Document_IsActive;
            if (typeof(ActiveEditingSessionDisplayModel) == typeOfController)
                _displaysForLayout.TryAdd(document, new ActiveEditingSessionDisplayModel(true));
            // initialize the new window


            Document_IsActive(document, null);
            document.IsSelected = true;

            return document;
        }

        private void Document_IsActive(object sender, EventArgs e)
        {
            var document = sender as LayoutDocument;
            if (document != null)
            {
                _currentDocument = document;
                ActiveEditingSessionDisplayModel displayModel;
                if (_displaysForLayout.TryGetValue(_currentDocument, out displayModel))
                {
                    EditingDisplayModel = displayModel;
                }
                else
                {
                    EditingDisplayModel = _nullEditingDisplayModel;
                }
            }
        }

        private bool RunAlreadyExists(string runName, ModelSystemEditingSession session)
        {
            return session.RunNameExists(runName);
        }

        private LayoutDocument _currentDocument;

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
            if (alreadyExists)
            {
                var dialog = new OpenFileDialog();
                dialog.Title = title;
                dialog.Filter = filter;
                if (dialog.ShowDialog() == true)
                    return dialog.FileName;
                return null;
            }
            else
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Save As";
                dialog.FileName = title;
                dialog.Filter = filter;
                if (dialog.ShowDialog() == true)
                    return dialog.FileName;
                return null;
            }
        }

        public static string OpenDirectory()
        {
            var showAdvanced = Environment.OSVersion.Version.Major >= 6;
            if (showAdvanced)
            {
                var result = Win32Helper.VistaDialog.Show(new WindowInteropHelper(Us).Handle, null, "Select Directory");
                if (result.Result)
                    return result.FileName;
            }
            else
            {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    return dialog.SelectedPath;
            }
            return null;
        }

        private void ImportModelSystem_Click(object sender, RoutedEventArgs e)
        {
            var fileName = OpenFile("Import Model System",
                new[] {new KeyValuePair<string, string>("Model System File", "xml")}, true);
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
                            if (
                                !EditorController.Runtime.ModelSystemController.ImportModelSystem(fileName, true,
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

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            EditingDisplayModel.Redo();
        }

        private void NewModelSystemButton_Click(object sender, RoutedEventArgs e)
        {
            NewModelSystem();
        }

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
            var req = new StringRequest("Project Name", ValidateName) {Owner = this};
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

        private bool ValidateName(string name)
        {
            return Project.ValidateProjectName(name);
        }

        internal void EditModelSystem(ModelSystemEditingSession modelSystemSession)
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
                var displayModel = new ModelSystemEditingSessionDisplayModel(display);
                var titleBarName = modelSystemSession.EditingProject
                    ? modelSystemSession.ProjectEditingSession.Name + " - " + modelSystemSession.ModelSystemModel.Name
                    : "Model System - " + modelSystemSession.ModelSystemModel.Name;
                var doc = AddNewWindow(titleBarName, display, typeof(ModelSystemEditingSessionDisplayModel));
                _displaysForLayout.TryAdd(doc, displayModel);
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
                        modelSystemSession.NameChanged -= onRename;
                };
                doc.Closed += (o, e) => { modelSystemSession.Dispose(); };
                display.RequestClose += ignored => doc.Close();
                doc.IsSelected = true;
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
            if (result != null)
                result.IsActive = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            foreach (var document in OpenPages.Select(page => page.Content))
            {
                var modelSystemPage = document as ModelSystemDisplay;
                var runPage = document as RunWindow;
                if (modelSystemPage != null)
                    if (!modelSystemPage.CloseRequested())
                    {
                        e.Cancel = true;
                        return;
                    }
                if (runPage != null)
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
                            Process.Start(Path.Combine(Path.GetDirectoryName(path), UpdateProgram),
                                Process.GetCurrentProcess().Id + " \"" + path + "\"");
                        }
                        catch
                        {
                            Dispatcher.Invoke(
                                () =>
                                {
                                    MessageBox.Show("We were unable to find XTMF.Update2.exe!", "Updater Missing!",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                        }
                    }).Wait();
            }
            base.OnClosing(e);
            Environment.Exit(0);
        }

        private void AboutXTMF_Click(object sender, RoutedEventArgs e)
        {
            new AboutXTMF
            {
                Owner = this
            }.ShowDialog();
        }

        private void ShowStart_Click(object sender, RoutedEventArgs e)
        {
            _startWindow = new StartWindow();
            var doc = AddNewWindow("Start", _startWindow, typeof(ActiveEditingSessionDisplayModel));
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            var activeDocument = OpenPages.FirstOrDefault(x => x.IsActive);
            if (activeDocument != null)
            {
                ActiveEditingSessionDisplayModel _;
                if (_displaysForLayout.TryRemove(activeDocument, out _))
                    EditingDisplayModel = _nullEditingDisplayModel;
                activeDocument.Close();
            }
        }

        private readonly string DocumentationName = "TMG XTMF Documentation.pdf";

        private void ShowDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Assembly.GetExecutingAssembly().Location;
                Process.Start(Path.Combine(Path.GetDirectoryName(path), DocumentationName));
            }
            catch
            {
                MessageBox.Show("We were unable to find the documentation", "Documentation Missing!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private const string UpdateProgram = "XTMF.Update2.exe";

        private bool LaunchUpdate;

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
            var document = _currentDocument;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (modelSystem != null)
            {
                var session = modelSystem.Session;
                var runName = "Run Name";
                var req = new StringRequest("Run Name", ValidateName);
                var trueWindow = GetWindow(document.Content as DependencyObject);
                var testWindow = GetWindow(document.Content as DependencyObject);
                var vis = document.Content as UserControl;
                if (vis != null && testWindow != trueWindow)
                {
                    var topLeft = vis.PointToScreen(new Point());
                    // Since the string request dialog isn't shown yet we need to use some defaults as width and height are not available.
                    req.Left = topLeft.X + (vis.ActualWidth - StringRequest.DefaultWidth) / 2;
                    req.Top = topLeft.Y + (vis.ActualHeight - StringRequest.DefaultHeight) / 2;
                }
                else
                {
                    req.Owner = trueWindow;
                }
                if (req.ShowDialog() == true)
                {
                    runName = req.Answer;
                    string error = null;
                    if (!RunAlreadyExists(runName, session) ||
                        MessageBox.Show("This run name has been previously used.  Continue?", "Continue?",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) ==
                        MessageBoxResult.Yes)
                    {
                        var run = session.Run(runName, ref error);
                        if (run != null)
                        {
                            var window = new RunWindow(session, run, runName);
                            var doc = AddNewWindow("New Run", window, typeof(ActiveEditingSessionDisplayModel));
                            doc.Closing += (o, e) =>
                            {
                                if (!window.CloseRequested())
                                    e.Cancel = true;
                            };
                            doc.CanClose = true;
                            doc.IsSelected = true;
                            Keyboard.Focus(window);
                            window.Focus();
                        }
                        else
                        {
                            MessageBox.Show(this, error, "Unable to run", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        internal void CloseWindow(UIElement window)
        {
            var page = OpenPages.FirstOrDefault(p =>
            {
                if (p.Content == window)
                {
                    if (!p.CanClose)
                        p.CanClose = true;
                    return true;
                }
                return false;
            });
            if (page != null)
                page.Close();
        }

        internal void SetWindowName(object window, string newName)
        {
            var page = OpenPages.FirstOrDefault(p =>
            {
                if (p.Content == window)
                    return true;
                return false;
            });
            if (page != null)
                page.Title = newName;
        }

        /// <summary>
        /// </summary>
        /// <param name="getHelpFor"></param>
        public void LaunchHelpWindow(ModelSystemStructureModel getHelpFor = null)
        {
            var helpUI = new HelpDialog(EditorController.Runtime.Configuration);
            if (getHelpFor != null)
                helpUI.SelectModuleContent(getHelpFor);
            var document = AddNewWindow("Help", helpUI, typeof(ActiveEditingSessionDisplayModel));
            document.IsSelected = true;
            Keyboard.Focus(helpUI);
        }

        private void LaunchSettingsPage()
        {
            var settingsPage = new SettingsPage(EditorController.Runtime.Configuration);
            var document = AddNewWindow("Settings", settingsPage, typeof(ActiveEditingSessionDisplayModel));
            document.Closing += (o, e) => { settingsPage.Close(); };
            document.IsSelected = true;
            Keyboard.Focus(settingsPage);
        }

        private void LaunchHelpWindow_Click(object sender, RoutedEventArgs e)
        {
            LaunchHelpWindow();
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            NewProject();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            LaunchSettingsPage();
        }

        private void MetaModuleHiddenParametersToggle_Click(object sender, RoutedEventArgs e)
        {
            if (EditingDisplayModel != _nullEditingDisplayModel)
            {
                ShowMetaModuleHiddenParameters = !ShowMetaModuleHiddenParameters;
                var document = _currentDocument;
                if (document.Content is ModelSystemDisplay)
                {
                    var modelSystem = document.Content as ModelSystemDisplay;

                    modelSystem.ExternalUpdateParameters();
                }
            }
        }


        private void LinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            var document = _currentDocument;
            var modelSystemDisplay = document.Content as ModelSystemDisplay;
            if (modelSystemDisplay != null)
            {
                var modelSystem = modelSystemDisplay;


                var linkedParameterDialog = new LinkedParameterDisplay(modelSystem.ModelSystem.LinkedParameters, false)
                {
                    Owner = Us
                };

                if (linkedParameterDialog.ShowDialog() == true)
                {
                }

                // modelSystem.ExternalUpdateParameters();

                //modelSystem.Lin
            }
            /*
            var linkedParameterDialog = new LinkedParameterDisplay(ModelSystem.LinkedParameters, false);
            linkedParameterDialog.Owner = Us;
            if (linkedParameterDialog.ShowDialog() == true && assign)
            {
                // assign the selected linked parameter
                var newLP = linkedParameterDialog.SelectedLinkParameter;

                if (AddCurrentParameterToLinkedParameter(newLP))
                {
                    LinkedParameterDisplayModel matched;
                    if ((matched = RecentLinkedParameters.FirstOrDefault(lpdm => lpdm.LinkedParameter == newLP)) != null)
                    {
                        RecentLinkedParameters.Remove(matched);
                    }
                    RecentLinkedParameters.Insert(0, new LinkedParameterDisplayModel(newLP));
                    if (RecentLinkedParameters.Count > 5)
                    {
                        RecentLinkedParameters.RemoveAt(5);
                    }
                    ParameterRecentLinkedParameters.IsEnabled = true;
                    QuickParameterRecentLinkedParameters.IsEnabled = true;
                }
            }
            if (linkedParameterDialog.ChangesMade)
            {
                RefreshParameters();
            }
            */
        }

        private void DocumentPane_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Console.WriteLine(e + " " + e.PropertyName);
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();

            e.Handled = true;
        }

        private void DockManager_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }
    }
}
