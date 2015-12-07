/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics;
using System.Linq;
using System.Text;
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
using Xceed.Wpf.AvalonDock.Layout;
using XTMF.Gui.Controllers;
using XTMF.Gui.UserControls;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += FrameworkElement_Loaded;
            Us = this;
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
                    }));
            });
            ShowStart_Click(this, null);
        }

        internal static MainWindow Us;

        public static void SetStatusText(string text)
        {
            Us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Us.StatusDisplay.Text = text;
                }));
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
            var open = new OpenWindow()
            {
                Owner = this
            };
            open.OpenProject(EditorController.Runtime);
            Task.Factory.StartNew(() =>
            {
                if (open.LoadTask != null)
                {
                    OperationProgressing progressing = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        progressing = new OperationProgressing()
                        {
                            Owner = this
                        };
                    }));
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressing.ShowDialog();
                    }));

                    open.LoadTask.Wait();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressing.Close();
                    }));
                }
                EditProject(open.ProjectSession);
            });
        }

        private void EditProject(ProjectEditingSession projectSession)
        {
            if (projectSession != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var display = new ProjectDisplay()
                    {
                        Session = projectSession,
                    };
                    display.InitiateModelSystemEditingSession += (editingSession) => EditModelSystem(editingSession);
                    var doc = AddNewWindow("Project - " + projectSession.Project.Name, display, () => { projectSession.Dispose(); });
                    doc.IsSelected = true;
                    PropertyChangedEventHandler onRename = (o, e) =>
                    {
                        doc.Title = "Project - " + projectSession.Project.Name;
                    };
                    projectSession.NameChanged += onRename;
                    display.RequestClose += (ignored) =>
                    {
                        doc.Close();
                        display.Model.Unload();
                        projectSession.NameChanged -= onRename;
                    };
                    display.Focus();
                    SetStatusText("Ready");
                }
                ));
            }
        }

        private void OpenModelSystem_Click(object sender, RoutedEventArgs e)
        {
            OpenModelSystem();
        }

        public void OpenModelSystem()
        {
            OpenWindow openWindow = new OpenWindow()
            {
                Owner = this
            };
            openWindow.OpenModelSystem(EditorController.Runtime);
            Task.Factory.StartNew(() =>
            {
                if (openWindow.LoadTask != null)
                {
                    OperationProgressing progressing = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        progressing = new OperationProgressing()
                        {
                            Owner = this
                        };
                    }));
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressing.ShowDialog();
                    }));
                    openWindow.LoadTask.Wait();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressing.Close();
                    }));
                }
                var session = openWindow.ModelSystemSession;
                if (session != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        EditModelSystem(session);
                        SetStatusText("Ready");
                    }));
                }
            });
        }


        /// <summary>
        /// The pages that are currently open from the main window
        /// </summary>
        private List<LayoutDocument> OpenPages = new List<LayoutDocument>();

        private LayoutDocument AddNewWindow(string name, UIElement content, Action onClose = null)
        {
            var document = new LayoutDocument()
            {
                Title = name,
                Content = content
            };
            document.Closed += (source, ev) =>
            {
                //integrate into the main window
                OpenPages.Remove(source as LayoutDocument);
                if (OpenPages.Count <= 0)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetSaveButtons(null);
                        UndoButton.IsEnabled = false;
                        RedoButton.IsEnabled = false;
                        CloseMenu.IsEnabled = false;
                    }));
                }
                // run the default code
                if (onClose != null)
                {
                    onClose();
                }
                Focus();
            };
            OpenPages.Add(document);
            DocumentPane.Children.Add(document);
            document.IsActiveChanged += Document_IsActive;
            // initialize the new window
            Document_IsActive(document, null);
            return document;
        }

        private void SetSaveButtons(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                SaveMenu.Header = "_Save";
                SaveAsMenu.Header = "Save _As";
                SaveMenu.IsEnabled = false;
                SaveAsMenu.IsEnabled = false;
                UndoButton.IsEnabled = false;
                RedoButton.IsEnabled = false;
            }
            else
            {
                if (name.Length > 20)
                {
                    name = name.Substring(0, 17) + "...";
                }
                SaveMenu.Header = "_Save " + name;
                SaveAsMenu.Header = "Save " + name + " _As";
                SaveMenu.Header = "_Save " + name;
                SaveAsMenu.Header = "Save " + name + " _As";
                SaveMenu.IsEnabled = true;
                SaveAsMenu.IsEnabled = true;
                UndoButton.IsEnabled = true;
                RedoButton.IsEnabled = true;
            }
        }

        private void Document_IsActive(object sender, EventArgs e)
        {
            var document = sender as LayoutDocument;
            if (document != null)
            {
                CurrentDocument = document;
                SaveMenu.IsEnabled = false;
                SaveAsMenu.IsEnabled = false;
                CloseMenu.IsEnabled = true;
                SetupSaveButtons(document);
                SetupRunButton(document);
            }
        }

        private void SetupRunButton(LayoutDocument document)
        {
            var modelSystem = document.Content as ModelSystemDisplay;
            RunMenu.IsEnabled = false;
            RunLabel.IsEnabled = false;
            if (modelSystem != null)
            {
                var session = modelSystem.Session;
                if (session.CanRun)
                {
                    RunMenu.IsEnabled = true;
                    RunLabel.IsEnabled = true;
                    _CurrentRun = () =>
                    {
                        var runName = "Run Name";
                        StringRequest req = new StringRequest("Run Name", ValidateName);
                        var trueWindow = Window.GetWindow(document.Content as DependencyObject);
                        var testWindow = GetWindow(document.Content as DependencyObject);
                        var vis = document.Content as UserControl;
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
                            string error = null;
                            if (!RunAlreadyExists(runName, session) || MessageBox.Show("This run name has been previously used.  Continue?", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                            {
                                var run = session.Run(runName, ref error);
                                if (run != null)
                                {
                                    RunWindow window = new RunWindow(session, run, runName);
                                    var doc = AddNewWindow("New Run", window);
                                    doc.Closing += (o, e) =>
                                    {
                                        if (!window.CloseRequested())
                                        {
                                            e.Cancel = true;
                                            return;
                                        }
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
                    };
                }
            }
        }

        private bool RunAlreadyExists(string runName, ModelSystemEditingSession session)
        {
            return session.RunNameExists(runName);
        }

        private Action _CurrentRun;
        private LayoutDocument CurrentDocument;

        private void SetupSaveButtons(LayoutDocument document)
        {
            _CurrentRun = null;
            //Setup anything that needs to happen when we change focus
            var projectPage = document.Content as ProjectDisplay;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (projectPage != null)
            {
                // you can't save a project (but we need to reset the menu)
                SetSaveButtons(null);
            }
            else if (modelSystem != null)
            {
                var name = modelSystem.ModelSystemName;
                SetSaveButtons(name);
            }
            else
            {
                SetSaveButtons(null);
            }
        }

        private void SaveMenu_Click(object sender, RoutedEventArgs e)
        {
            var document = CurrentDocument;
            var projectPage = document.Content as ProjectDisplay;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (projectPage != null)
            {
                // TODO
            }
            else if (modelSystem != null)
            {
                modelSystem.SaveRequested(false);
            }
        }

        private void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            var document = CurrentDocument;
            var projectPage = document.Content as ProjectDisplay;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (projectPage != null)
            {
                // TODO
            }
            else if (modelSystem != null)
            {
                modelSystem.SaveRequested(true);
            }
        }

        public static string OpenFile(string title, KeyValuePair<string, string>[] extensions, bool alreadyExists)
        {
            string filter = string.Join("|",
                from element in extensions
                select element.Key + "|*." + element.Value
                );
            if (alreadyExists)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = title;
                dialog.Filter = filter;
                if (dialog.ShowDialog() == true)
                {
                    return dialog.FileName;
                }
                return null;
            }
            else
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Title = "Save As";
                dialog.FileName = title;
                dialog.Filter = filter;
                if (dialog.ShowDialog() == true)
                {
                    return dialog.FileName;
                }
                return null;
            }
        }

        private void ImportModelSystem_Click(object sender, RoutedEventArgs e)
        {
            var fileName = OpenFile("Import Model System", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Model System File", "xml") }, true);
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
                                if (!EditorController.Runtime.ModelSystemController.ImportModelSystem(fileName, true, ref error))
                                {
                                    MessageBox.Show(this, error, "Unable to import", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else
                                {
                                    MessageBox.Show(this, "The model system has been successfully imported from '" + fileName + "'.", "Model System Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            var document = CurrentDocument;
            var projectPage = document.Content as ProjectDisplay;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (projectPage != null)
            {
                // TODO
            }
            else if (modelSystem != null)
            {
                modelSystem.UndoRequested();
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            var document = CurrentDocument;
            var projectPage = document.Content as ProjectDisplay;
            var modelSystem = document.Content as ModelSystemDisplay;
            if (projectPage != null)
            {
                // TODO
            }
            else if (modelSystem != null)
            {
                modelSystem.RedoRequested();
            }
        }

        private void NewModelSystemButton_Click(object sender, RoutedEventArgs e)
        {
            NewModelSystem();
        }

        public void NewModelSystem()
        {
            StringRequest req = new StringRequest("Model System Name", ValidateName);
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
            StringRequest req = new StringRequest("Project Name", ValidateName) { Owner = this };
            if (req.ShowDialog() == true)
            {
                var name = req.Answer;
                string error = null;
                var ms = EditorController.Runtime.ProjectController.LoadOrCreate(name, ref error);
                EditProject(EditorController.Runtime.ProjectController.EditProject(ms));
            }
        }

        private bool ValidateName(string name)
        {
            return Project.ValidateProjectName(name);
        }

        private void EditModelSystem(ModelSystemEditingSession modelSystemSession)
        {
            if (modelSystemSession != null)
            {
                var display = new ModelSystemDisplay()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ModelSystem = modelSystemSession.ModelSystemModel,
                    Session = modelSystemSession,
                };

                var titleBarName = modelSystemSession.EditingProject ?
                     modelSystemSession.ProjectEditingSession.Name + " - " + modelSystemSession.ModelSystemModel.Name
                    : "Model System - " + modelSystemSession.ModelSystemModel.Name;
                var doc = AddNewWindow(titleBarName, display);
                PropertyChangedEventHandler onRename = (o, e) =>
                {
                    Dispatcher.Invoke(() =>
                   {
                       doc.Title = modelSystemSession.EditingProject ?
                        modelSystemSession.ProjectEditingSession.Name + " - " + modelSystemSession.ModelSystemModel.Name
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
            var us = MainWindow.Us;
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
            foreach (var document in OpenPages.Select(page => page.Content))
            {
                var modelSystemPage = document as ModelSystemDisplay;
                var runPage = document as RunWindow;
                if (modelSystemPage != null)
                {
                    if (!modelSystemPage.CloseRequested())
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                if (runPage != null)
                {
                    if (!runPage.CloseRequested())
                    {
                        e.Cancel = true;
                        return;
                    }
                }

            }
            EditorController.Unregister(this);
            base.OnClosing(e);
            if (!e.Cancel)
            {
                Task.Run(() =>
               {
                   if (LaunchUpdate)
                   {
                       string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                       try
                       {
                           Process.Start(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), UpdateProgram), "\"" + path + "\"");
                       }
                       catch
                       {
                           Dispatcher.Invoke(() =>
                          {
                              MessageBox.Show("We were unable to find XTMF.Update2.exe!", "Updater Missing!", MessageBoxButton.OK, MessageBoxImage.Error);
                          });
                       }
                   }
                   Application.Current.Shutdown();
                   Environment.Exit(0);
               });
            }
        }

        private void AboutXTMF_Click(object sender, RoutedEventArgs e)
        {
            new AboutXTMF()
            {
                Owner = this
            }.ShowDialog();
        }

        private void ShowStart_Click(object sender, RoutedEventArgs e)
        {
            AddNewWindow("Start", new StartWindow(), null);
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            LayoutDocument activeDocument = OpenPages.FirstOrDefault(x => x.IsActive);
            if (activeDocument != null)
            {
                activeDocument.Close();
            }
        }

        private string DocumentationName = "TMG XTMF Documentation.pdf";

        private void ShowDocumentation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Process.Start(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), DocumentationName));
            }
            catch
            {
                MessageBox.Show("We were unable to find the documentation", "Documentation Missing!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            var doc = AddNewWindow("Documentation - " + documentationControl.TypeNameText, documentationControl);
            documentationControl.RequestClose += (ignored) => doc.Close();
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
            var doc = AddNewWindow("Launch Remote Client", remoteWindow);
            remoteWindow.RequestClose += (ignored) => doc.Close();
            doc.IsSelected = true;
            Keyboard.Focus(remoteWindow);
            remoteWindow.Focus();
        }

        public void ExecuteRun()
        {
            if (_CurrentRun != null)
            {
                _CurrentRun();
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
            if (page != null)
            {
                page.Close();
            }
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
            var document = AddNewWindow("Help", helpUI);
            document.IsSelected = true;
            Keyboard.Focus(helpUI);
        }

        private void LaunchSettingsPage()
        {
            var settingsPage = new UserControls.SettingsPage(EditorController.Runtime.Configuration);
            var document = AddNewWindow("Settings", settingsPage);
            document.Closing += (o, e) =>
            {
                settingsPage.Close();
            };
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
    }
}
