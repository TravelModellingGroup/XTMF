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

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ProjectsDisplay.xaml
    /// </summary>
    public partial class ProjectsDisplay : UserControl
    {
        private XTMFRuntime Runtime;

        public ProjectsDisplay(XTMFRuntime runtime)
        {
            InitializeComponent();
            Runtime = runtime;
            var projectRepository = ((ProjectRepository)runtime.Configuration.ProjectRepository);
            Loaded += ProjectsDisplay_Loaded;
            Display.ItemsSource = new XTMF.Gui.Collections.ProxyList<IProject>(projectRepository.Projects);
            projectRepository.ProjectAdded += ProjectRepository_ProjectAdded;
            projectRepository.ProjectRemoved += ProjectRepository_ProjectRemoved;
            FilterBox.Display = Display;
            FilterBox.Filter = (o, filterString) =>
            {
                var project = o as IProject;
                return project.Name.IndexOf(filterString, StringComparison.InvariantCultureIgnoreCase) >= 0;
            };
        }

        private void ProjectRepository_ProjectRemoved(IProject removedProject, int position)
        {
            RefreshProjects();
        }

        private void ProjectRepository_ProjectAdded(IProject newProject)
        {
            RefreshProjects();
        }

        private void ProjectsDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // when the page is loaded give focus to the filter box
            Keyboard.Focus(FilterBox);
        }

        private Window GetWindow()
        {
            var current = this as DependencyObject;
            while (current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        private void Project_DoubleClicked(object obj)
        {
            LoadCurrentProject();
        }

        private void LoadCurrentProject()
        {
            var project = Display.SelectedItem as Project;
            LoadProject(project);
        }

        private void LoadProject(Project project)
        {
            if (project != null)
            {
                ProjectEditingSession session = null;
                OperationProgressing progressing = new OperationProgressing()
                {
                    Owner = GetWindow()
                };
                var loadingTask = Task.Run(() =>
                {
                    session = Runtime.ProjectController.EditProject(project);
                });
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressing.ShowDialog();
                }));
                loadingTask.Wait();
                if (session != null)
                {
                    MainWindow.Us.EditProject(session);
                }
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressing.Close();
                }));
            }
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            CreateNewProject();
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            RenameCurrentProject();
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            CloneCurrentProject();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DeleteCurrentProject();
        }

        private void DeleteProject_Clicked(object obj)
        {
            DeleteCurrentProject();
        }

        private void CloneProject_Clicked(object obj)
        {
            CloneCurrentProject();
        }

        private void RenameProject_Clicked(object obj)
        {
            RenameCurrentProject();
        }

        private void NewProject_Clicked(object sender)
        {
            CreateNewProject();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!e.Handled)
            {
                switch (e.Key)
                {
                    case Key.F2:
                        RenameCurrentProject();
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        DeleteCurrentProject();
                        e.Handled = true;
                        break;
                    case Key.C:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CloneCurrentProject();
                            e.Handled = true;
                        }
                        break;
                    case Key.N:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CreateNewProject();
                            e.Handled = true;
                        }
                        break;
                    case Key.Enter:
                        if (!Renaming)
                        {
                            LoadCurrentProject();
                            e.Handled = true;
                        }
                        break;
                }
            }
            base.OnKeyUp(e);
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
        }

        private void CreateNewProject()
        {
            MainWindow.Us.NewProject();
            RefreshProjects();
        }

        bool Renaming = false;

        private void RenameCurrentProject()
        {
            var project = Display.SelectedItem as Project;
            if (project != null)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                Renaming = true;
                var adorn = new TextboxAdorner("Rename", (result) =>
                {
                    string error = null;
                    if (!Runtime.ProjectController.RenameProject(project, result, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Rename Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                    else
                    {
                        RefreshProjects();
                    }
                }, selectedModuleControl, project.Name);
                adorn.Unloaded += Adorn_Unloaded;
                layer.Add(adorn);
                adorn.Focus();
            }
        }

        private void RefreshProjects()
        {
            var selected = Display.SelectedItem;
            Display.Items.Refresh();
            FilterBox.RefreshFilter();
            Display.SelectedItem = selected;
        }

        private void Adorn_Unloaded(object sender, RoutedEventArgs e)
        {
            Renaming = false;
        }

        private void CloneCurrentProject()
        {
            var project = Display.SelectedItem as Project;
            if (project != null)
            {
                string error = null;
                StringRequest sr = new StringRequest("Clone Project As?", (newName) =>
                {
                    return Runtime.ProjectController.ValidateProjectName(newName);
                });
                sr.Owner = GetWindow();
                if (sr.ShowDialog() == true)
                {
                    if (!Runtime.ProjectController.CloneProject(project, sr.Answer, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Clone Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        return;
                    }
                    RefreshProjects();
                }
            }
        }


        private void DeleteCurrentProject()
        {
            var project = Display.SelectedItem as Project;
            if (project != null)
            {
                if (MessageBox.Show(GetWindow(),
                    "Are you sure you want to delete the project '" + project.Name + "'?  This action cannot be undone!", "Delete Project", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string error = null;
                    if (!Runtime.ProjectController.DeleteProject(project, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Delete Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        return;
                    }
                    RefreshProjects();
                }
            }
        }

        private Project GetFirstItem()
        {
            if (Display.ItemContainerGenerator.Items.Count > 0)
            {
                return Display.ItemContainerGenerator.Items[0] as Project;
            }
            return null;
        }

        private void FilterBox_EnterPressed(object sender, EventArgs e)
        {
            var selected = Display.SelectedItem as Project;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            LoadProject(selected);
        }
    }
}
