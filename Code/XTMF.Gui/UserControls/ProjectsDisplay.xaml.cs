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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Collections;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Interaction logic for ProjectsDisplay.xaml
/// </summary>
public partial class ProjectsDisplay : UserControl
{
    private XTMFRuntime Runtime;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="runtime"></param>
    public ProjectsDisplay(XTMFRuntime runtime)
    {
        InitializeComponent();
        Runtime = runtime;
        var projectRepository = ((ProjectRepository)runtime.Configuration.ProjectRepository);
        projectRepository.ReloadRepository();
        Loaded += ProjectsDisplay_Loaded;
        Display.ItemsSource = new ProxyList<IProject>(projectRepository.Projects);
        projectRepository.ProjectAdded += ProjectRepository_ProjectAdded;
        projectRepository.ProjectRemoved += ProjectRepository_ProjectRemoved;
        FilterBox.Display = Display;
        FilterBox.Filter = (o, filterString) =>
        {
            var project = o as IProject;
            return project.Name.IndexOf(filterString, StringComparison.InvariantCultureIgnoreCase) >= 0;
        };
    }

    private void ProjectRepository_ProjectRemoved(IProject removedProject, int position) => RefreshProjects();

    private void ProjectRepository_ProjectAdded(IProject newProject) => RefreshProjects();

    private void ProjectsDisplay_Loaded(object sender, RoutedEventArgs e)
    {
        // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
        Dispatcher.BeginInvoke(new Action(() =>
       {
           FilterBox.Focus();
       }));
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

    private Type GetTopLevelType()
    {
        var current = this as DependencyObject;
        var prev = null as DependencyObject;
        while (current != null)
        {
            prev = current;
            current = VisualTreeHelper.GetParent(current);
        }
        return prev.GetType();
    }


    private void LoadCurrentProject() => LoadProject(Display.SelectedItem as Project);

    public void LoadProject(Project project) => MainWindow.Us.LoadProject(project);

    private void NewProject_Click(object sender, RoutedEventArgs e) => CreateNewProject();

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameCurrentProject();

    private void Clone_Click(object sender, RoutedEventArgs e) => CloneCurrentProject();

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteCurrentProject();


    private void ChangeDescription_Click(object sender, RoutedEventArgs e) => ChangeCurrentDescription();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled)
        {
            switch (e.Key)
            {
                case Key.F2:
                    if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        ChangeCurrentDescription();
                    }
                    else
                    {
                        RenameCurrentProject();
                    }
                    e.Handled = true;
                    break;
                case Key.E:
                    if(e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        Keyboard.Focus(FilterBox);
                        e.Handled = true;
                    }
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
        OnKeyUp(e);
    }

    private UIElement GetCurrentlySelectedControl()
    {
        return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
    }

    private void CreateNewProject()
    {
        MainWindow.Us.NewProject(RootDialogHost);
        RefreshProjects();
    }

    bool Renaming;

    private async void RenameCurrentProject()
    {
        if (Display.SelectedItem is Project project)
        {
            var dialog = new StringRequestDialog(RootDialogHost, "Rename Project", (value) => !String.IsNullOrWhiteSpace(value), project.Name);
            await dialog.ShowAsync();
            if (dialog.DidComplete)
            {
                var originalName = project.Name;
                string error = null;
                if (!Runtime.ProjectController.RenameProject(project, dialog.UserInput, ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to Rename Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }
                else
                {
                    Runtime.Configuration.RenameRecentProject(originalName, dialog.UserInput);
                    Runtime.Configuration.Save();
                    RefreshProjects();
                }
            }
        }
    }

    private void ChangeCurrentDescription()
    {
        if (Display.SelectedItem is Project project)
        {
            var selectedModuleControl = GetCurrentlySelectedControl();
            var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
            Renaming = true;
            var adorn = new TextboxAdorner("Change Description", result =>
            {
                string error = null;
                if (!Runtime.ProjectController.SetDescription(project, result, ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to Rename Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                }
                else
                {
                    RefreshProjects();
                }
            }, selectedModuleControl, project.Description);
            adorn.Unloaded += Adorn_Unloaded;
            layer.Add(adorn);
            adorn.Focus();
        }
    }

    private void RefreshProjects()
    {
        Display.Items.Refresh();
        FilterBox.RefreshFilter();
        Display.SelectedItem = Display.SelectedItem;
        MainWindow.Us.UpdateRecentProjectsMenu();
    }

    private void Adorn_Unloaded(object sender, RoutedEventArgs e) => Renaming = false;



    private  async void CloneCurrentProject()
    {
        if (Display.SelectedItem is Project project)
        {
            string error = null;
            StringRequestDialog dialog = 
                new(RootDialogHost, "Clone Project As?", newName => { return Runtime.ProjectController.ValidateProjectName(newName); }, null);
            var result = await dialog.ShowAsync();
            if (dialog.DidComplete)
            {
                if (!Runtime.ProjectController.CloneProject(project, dialog.StringInputTextBox.Text, ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to Clone Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    return;
                }
                Runtime.Configuration.AddRecentProject(dialog.StringInputTextBox.Text);
                Runtime.Configuration.Save();
                RefreshProjects();
            }
        }
    }

    private void DeleteCurrentProject()
    {
        if (Display.SelectedItem is Project project)
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
            }
            //remove from recent projects
            Runtime.Configuration.RemoveRecentProject(project.Name);
            Runtime.Configuration.Save();
            RefreshProjects();
        }
    }

    private Project GetFirstItem()
    {
        return Display.ItemContainerGenerator.Items.Any()
            ? Display.ItemContainerGenerator.Items[0] as Project : null;
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

    private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => LoadCurrentProject();
}
