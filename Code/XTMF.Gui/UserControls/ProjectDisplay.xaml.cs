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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using XTMF.Annotations;
using XTMF.Gui.Controllers;
using XTMF.Gui.UserControls.Interfaces;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ProjectDisplay.xaml
    /// </summary>
    public partial class ProjectDisplay : UserControl, INotifyPropertyChanged, ITabCloseListener
    {
        public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register("Project",
            typeof(Project), typeof(ProjectDisplay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnProjectChanged));

        public static readonly DependencyProperty ProjectModelProperty = DependencyProperty.Register("Model",
            typeof(ProjectModel), typeof(ProjectDisplay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnProjectModelChanged));

        private ProjectEditingSession _session;

        public ProjectDisplay()
        {
            InitializeComponent();
            Loaded += ProjectDisplay_Loaded;
            // ContextMenu.PlacementTarget = ModelSystemDisplay;
        }

        private Project Project
        {
            get => GetValue(ProjectProperty) as Project;
            set => SetValue(ProjectProperty, value);
        }

        public ProjectEditingSession Session
        {
            get => _session;
            set
            {
                _session = value;
                Project = _session.Project;
            }
        }

        public ProjectModel Model
        {
            get => GetValue(ProjectModelProperty) as ProjectModel;
            set => SetValue(ProjectModelProperty, value);
        }

        public bool IsRenameActive { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool HandleTabClose()
        {
            _session.EndSession();
            return true;
        }

        public event Action<ModelSystemEditingSession> InitiateModelSystemEditingSession;


        private static void OnProjectChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ProjectDisplay;
            us.DataContext = us.Project;
            us.Model = new ProjectModel(us.Project, us.Session);
            us.Session.ModelSystemNameChanged += us.Session_ModelSystemNameChanged;
            us.Session.ModelSystemSaved += us.Session_ModelSystemSaved;
        }

        private void RefreshModelSystems()
        {
            Dispatcher.BeginInvoke(new Action(() => 
            {
                Model.RefreshModelSystems();
            }));
        }

        private void Session_ModelSystemSaved(object sender, EventArgs e)
        {
            RefreshModelSystems();
        }

        private void Session_ModelSystemNameChanged(object sender, EventArgs e)
        {
            RefreshModelSystems();
        }

        private bool FilterMS(object e, string text)
        {
            var element = e as ProjectModel.ContainedModelSystemModel;
            return CheckString(element.Name, text) | CheckString(element.Description, text);
        }

        private bool FilterRuns(object e, string text)
        {
            var element = e as ProjectModel.PreviousRun;
            return CheckString(element.Name, text) | CheckString(element.TimeStamp, text);
        }

        private static bool CheckString(string str, string filter)
        {
            return str == null ? false : str.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static void OnProjectModelChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ProjectDisplay;
            us.DataContext = us.Project;
            us.Model.PropertyChanged += (obj, ev) =>
            {
                us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    us.ModelSystemsDataGrid.ItemsSource = us.Model.ContainedModelSystems;
                    // us.ModelSystemsDataGrid.ItemsSource = us.Model.ContainedModelSystems;
                    us.PastRunDisplay.ItemsSource = us.Model.PreviousRuns;
                    us.FilterModelSystemsBox.RefreshFilter();
                    us.FilterPastRunsBox.RefreshFilter();
                }));
            };
            lock (us.Model.ContainedModelSystems)
            {
                // us.ModelSystemDisplay.ItemsSource = us.Model.ContainedModelSystems;
                us.ModelSystemsDataGrid.ItemsSource = us.Model.ContainedModelSystems;
            }

            lock (us.Model.PreviousRuns)
            {
                us.PastRunDisplay.ItemsSource = us.Model.PreviousRuns;
            }

            us.FilterModelSystemsBox.Display = us.ModelSystemsDataGrid;
            us.FilterModelSystemsBox.Filter = us.FilterMS;
            us.FilterPastRunsBox.Display = us.PastRunDisplay;
            us.FilterPastRunsBox.Filter = us.FilterRuns;
        }

        /// <summary>
        /// </summary>
        /// <param name="e"></param>
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterModelSystemsBox.Focus();
        }

        /// <summary>
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (true)
            {
                switch (e.Key)
                {
                    case Key.W:
                        if (EditorController.IsControlDown())
                        {
                            Close();
                            e.Handled = true;
                        }

                        break;
                    case Key.E:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            Keyboard.Focus(FilterModelSystemsBox);
                            e.Handled = true;
                        }

                        break;
                    case Key.O:
                        if (EditorController.IsControlDown())
                        {
                            OpenProjectFolder();
                            e.Handled = true;
                        }

                        break;
                    case Key.Enter:
                        {
                            if (!IsRenameActive)
                            {
                                LoadModelSystem();
                                e.Handled = true;
                            }
                            else
                            {
                                IsRenameActive = false;
                            }

                        }
                        break;
                    case Key.F2:
                        {
                            RenameCurrentModelSystem();
                            e.Handled = true;
                        }
                        break;
                    case Key.C:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CloneCurrentModelSystem();
                        }

                        break;
                    case Key.Delete:
                        DeleteCurrentModelSystem();
                        break;
                    case Key.N:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CreateNewModelSystem();
                        }

                        break;
                    case Key.S:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            SaveCurrentAsModelSystem(true);
                        }

                        break;
                    case Key.V:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            PasteModelSystem_OnClick(null, null);
                            e.Handled = true;
                        }

                        break;
                }
            }
        }

        public event Action<object> RequestClose;

        private void Close()
        {
            var e = RequestClose;
            if (e != null)
            {
                if (MessageBox.Show("Are you sure that you want to close this window?", "Are you sure?",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Dispatcher.BeginInvoke(new Action(() => { e(this); }));
                }
            }
        }

        private void LoadModelSystem()
        {
            var selected = ModelSystemsDataGrid.SelectedItem as ProjectModel.ContainedModelSystemModel;
            LoadModelSystem(selected);
        }

        private void LoadModelSystem(ProjectModel.ContainedModelSystemModel selected)
        {
            if (selected != null)
            {
                var invoke = InitiateModelSystemEditingSession;
                if (invoke != null)
                {
                    var index = selected.RealIndex;
                    invoke(Session.EditModelSystem(index));
                }

                ModelSystemsDataGrid.SelectedItem = null;
            }
        }


        private void OpenPreviousRun_Click(object sender, RoutedEventArgs e)
        {
            var previousRun = (ProjectModel.PreviousRun)PastRunDisplay.SelectedItem;
            if (previousRun != null)
            {
                var directoryName = Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name,
                    previousRun.Name);
                try
                {
                    if (Project != null && Directory.Exists(directoryName))
                    {
                        Process.Start(directoryName);
                    }
                }
                catch
                {
                    MessageBox.Show(MainWindow.Us, "We were unable to open the run directory '" + directoryName + "'!",
                        "Unable to Open");
                }
            }
        }

        private void OpenProjectFolder()
        {
            var directoryName = Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name);
            try
            {
                if (Project != null && Directory.Exists(directoryName))
                {
                    Process.Start(directoryName);
                }
            }
            catch
            {
                MessageBox.Show(MainWindow.Us, "We were unable to open the project directory '" + directoryName + "'!",
                    "Unable to Open");
            }
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


        private void DeleteModelSystem_Click(object sender, RoutedEventArgs e)
        {
            DeleteCurrentModelSystem();
        }

        private void DeleteCurrentModelSystem()
        {
            if (ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected)
            {
                var index = selected.RealIndex;
                string error = null;
                if (MessageBox.Show("Are you sure you wish to delete '" + selected.Name + "'?  This can not be undone!",
                        "Confirm Delete!",
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    if (!Session.RemoveModelSystem(index, ref error))
                    {
                        MessageBox.Show(error, "Unable to Delete Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else
                    {
                        Model.RefreshModelSystems();
                    }
                }
            }
        }

        private void DeletePreviousRun_Click(object sender, RoutedEventArgs e)
        {
            if (PastRunDisplay.SelectedItem is ProjectModel.PreviousRun previousRun)
            {
                try
                {
                    var directoryName = Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name,
                        previousRun.Name);
                    var dir = new DirectoryInfo(directoryName);
                    if (dir.Exists)
                    {
                        dir.Delete(true);
                    }

                    Model.RefreshPastRuns(Session);
                }
                catch (IOException error)
                {
                    MessageBox.Show(error.Message, "Unable to Delete Previous Run", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>

        private async void RenameCurrentModelSystem()
        {
            if (ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected)
            {
                var dialog = new StringRequestDialog(RootDialogHost, "Rename Model System", (value) => !String.IsNullOrWhiteSpace(value), selected.Name);
                await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    string error = null;
                    if (!selected.SetName(dialog.UserInput, ref error))
                    {
                        MessageBox.Show(error, "Unable to Rename Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private void RenameModelSystem_Click(object sender, RoutedEventArgs e)
        {
            RenameCurrentModelSystem();
        }

        private void SaveModelSystemAs_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentAsModelSystem(false);
        }

        private void ExportModelSystemAs_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentAsModelSystem(true);
        }

        private async void SaveCurrentAsModelSystem(bool exportToFile)
        {
            if (ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected)
            {
                if (exportToFile)
                {
                    // save as a model system in an external file
                    var fileName = MainWindow.OpenFile(selected.Name,
                        new[] { new KeyValuePair<string, string>("Model System File", "xml") }, false);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        string error = null;
                        if (!selected.ExportModelSystem(fileName, ref error))
                        {
                            MessageBox.Show(Window.GetWindow(this), error, "Unable to Export Model System",
                                MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        }
                    }
                }
                else
                {
                    var dialog = new StringRequestDialog(RootDialogHost, "Save Model System As?",
                    newName => { return Session.ValidateModelSystemName(newName); },
                    selected.Name);
                    var result = await dialog.ShowAsync(true);

                    if (dialog.DidComplete)
                    {
                        string error = null;

                        if (!selected.CloneModelSystem(dialog.UserInput, ref error))
                        {
                            MessageBox.Show(error, "Unable to Save Model System", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyModelSystem_Click(object sender, RoutedEventArgs e)
        {
            // SetValue(ModelSystemListView.IsCanPasteModelSystemDependencyProperty,true);
            // ModelSystemsDataGrid.IsCanPasteModelSystem = true;

            CloneCurrentModelSystem();

            foreach (var m in Model.ContainedModelSystems)
            {
                m.IsCanPasteModelSystem = true;
            }

            // ((ProjectModel) this.DataContext)IsCanPasteModelSystem = true;
        }

        private void CloneCurrentModelSystem()
        {
            if (ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected)
            {
                MainWindow.Us.ClipboardModel = selected;
            }
        }

        private void NewModelSystem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewModelSystem();
        }

        private void CreateNewModelSystem_Clicked(object obj)
        {
            CreateNewModelSystem();
        }

        /// <summary>
        ///     Attempts to create a new model system
        /// </summary>
        private async void CreateNewModelSystem()
        {
            var dialog = new StringRequestDialog(RootDialogHost, "Name of New Model System",
                newName => { return Session.ValidateModelSystemName(newName); }, null);
            var result = await dialog.ShowAsync(true);
            if (dialog.DidComplete)
            {
                string error = null;
                if (!Session.AddModelSystem(dialog.UserInput, ref error))
                {
                    MessageBox.Show(error, "Unable to create New Model System", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else
                {
                    Model.RefreshModelSystems();
                }
            }
        }

        private ProjectModel.ContainedModelSystemModel GetFirstItem()
        {
            return ModelSystemsDataGrid.ItemContainerGenerator.Items.Any()
                ? ModelSystemsDataGrid.ItemContainerGenerator.Items[0] as ProjectModel.ContainedModelSystemModel
                : null;
        }

        private void FilterModelSystemsBox_EnterPressed(object sender, EventArgs e)
        {
            if (!(ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected))
            {
                selected = GetFirstItem();
            }
            LoadModelSystem(selected);
        }


        private void PastRuns_MouseButton(object sender, MouseButtonEventArgs e)
        {
            if (PastRunDisplay.SelectedItem is ProjectModel.PreviousRun selected)
            {
                var invoke = InitiateModelSystemEditingSession;
                if (invoke != null)
                {
                    string error = null;
                    if (Session.LoadPreviousRun(selected.Path, ref error, out var newSession))
                    {
                        if (newSession != null)
                        {
                            newSession.PreviousRunName = selected.Name;
                            invoke(newSession);
                        }
                    }
                    else
                    {
                        MessageBox.Show(error, "Unable to Open Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }

                PastRunDisplay.SelectedItem = null;
            }
        }

        private void ListViewControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LoadModelSystem();
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PasteModelSystem_OnClick(object sender, RoutedEventArgs e)
        {
            string error = null;
            if (MainWindow.Us.ClipboardModel != null)
            {
                var cloned = Session.CloneModelSystem(MainWindow.Us.ClipboardModel.ModelSystemStructure, ref error);
                /* var sr = new StringRequest("Paste: Model System's Name?",
                     newName => { return Session.ValidateModelSystemName(newName); })
                 {
                     Owner = GetWindow()
                 }; */


                var dialog = new StringRequestDialog(RootDialogHost, "Paste: Model System's Name?",
                    newName => Session.ValidateModelSystemName(newName), cloned.Name);

                var aresult = await dialog.ShowAsync(false);

                if (dialog.DidComplete)
                {
                    if (!Session.AddExternalModelSystem(cloned, dialog.UserInput, ref error))
                    {
                        MessageBox.Show(error, "Unable to Paste Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else
                    {
                        Model.RefreshModelSystems();
                    }
                }
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
        private void ModelSystemDisplay_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(ModelSystemsDataGrid.SelectedItem is ProjectModel.ContainedModelSystemModel selected))
            {
                selected = GetFirstItem();
            }
            LoadModelSystem(selected);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            OpenProjectFolder();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ImportButton_OnClick(object sender, RoutedEventArgs e)
        {
            var xtmf = Session.GetRuntime();
            var openMS = new OpenWindow
            {
                Owner = GetWindow()
            };
            using (var modelSystemSession = openMS.OpenModelSystem(xtmf))
            {
                var loading = openMS.LoadTask;
                if (loading != null)
                {
                    loading.Wait();
                    using (var realSession = openMS.ModelSystemSession)
                    {
                        if (realSession != null)
                        {
                            string error = null;

                            var dialog = new StringRequestDialog(RootDialogHost, "Model System Name",
                                newName => Session.ValidateModelSystemName(newName), realSession.Name);

                            var result = await dialog.ShowAsync(false);
                            if (dialog.DidComplete)
                            {
                                if (!Session.AddModelSystem(realSession.ModelSystemModel, dialog.UserInput, ref error))
                                {
                                    MessageBox.Show(GetWindow(), error, "Unable to Import Model System",
                                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                                    return;
                                }

                                Model.RefreshModelSystems();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ImportFromFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            var result = fileDialog.ShowDialog();
            switch (result)
            {
                case DialogResult.OK:
                    string error = null;
                    try
                    {
                        var modelSystem = ModelSystem.LoadDetachedModelSystem(fileDialog.OpenFile(),
                            EditorController.Runtime.Configuration,
                            ref error);
                        var dialog = new StringRequestDialog(RootDialogHost, "Save Model System As?",
                            newName => Session.ValidateModelSystemName(newName), modelSystem?.ModelSystemStructure?.Name);
                        var aresult = await dialog.ShowAsync(false);
                        if (dialog.DidComplete)
                        {
                            if (!Session.AddModelSystem(modelSystem, dialog.UserInput, ref error))
                            {
                                MessageBox.Show(GetWindow(), error, "Unable to Import Model System",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error, MessageBoxResult.OK);
                                return;
                            }

                            Model.RefreshModelSystems();
                        }
                    }
                    catch
                    {
                        MessageBox.Show(GetWindow(), "There was an error importing the model system.",
                            "Unable to Import Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.OK);
                    }

                    break;
            }
        }

        private void StackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenProjectFolder();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportModelSystemStackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImportButton_OnClick(sender, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportModelSystemFileStackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImportFromFileButton_OnClick(sender, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateNewModelSystemStackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            NewModelSystem_Click(sender, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshRunsStackPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Model.RefreshPastRuns(Session);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshPastRunsButton_OnClick(object sender, RoutedEventArgs e)
        {
            Model.RefreshPastRuns(Session);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopupBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBoxItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProjectDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            Dispatcher.BeginInvoke(new Action(() => { FilterModelSystemsBox.Focus(); }));
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EventSetter_OnHandler(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemsDataGrid_OnKeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemsDataGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e);
        }

        public class ProjectModel : INotifyPropertyChanged
        {
            private readonly ProjectEditingSession Session;

            public List<ContainedModelSystemModel> ContainedModelSystems;

            public List<PreviousRun> PreviousRuns = new List<PreviousRun>();

            private IProject Project;

            public ProjectModel(IProject project, ProjectEditingSession session)
            {
                Session = session;
                Project = project;
                session.ProjectWasExternallySaved += Session_ProjectWasExternallySaved;
                RefreshModelSystems();
                RefreshPastRuns(session);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void Session_ProjectWasExternallySaved(object sender, EventArgs e)
            {
                // re-setup the page for the new project
                Project = Session.Project;
                RefreshModelSystems();
            }

            public void RefreshPastRuns(ProjectEditingSession session)
            {
                lock (PreviousRuns)
                {
                    PreviousRuns.Clear();
                }

                Task.Factory.StartNew(() =>
                {
                    lock (PreviousRuns)
                    {
                        var list = new List<PreviousRun>();
                        PreviousRuns.Clear();
                        foreach (var pastRun in session.GetPreviousRunPaths())
                        {
                            var info = new DirectoryInfo(pastRun);
                            var fileInfo = new FileInfo(Path.Combine(pastRun, "RunParameters.xml"));
                            list.Add(new PreviousRun
                            {
                                Name = info.Name,
                                Path = pastRun,
                                Time = fileInfo.LastWriteTime
                            });
                        }

                        PreviousRuns.AddRange(from entry in list
                                              orderby entry.Time descending
                                              select entry);
                    }

                    ModelHelper.PropertyChanged(PropertyChanged, this, "PreviousRuns");
                });
            }

            internal void RefreshModelSystems()
            {
                if (ContainedModelSystems == null)
                {
                    ContainedModelSystems = new List<ContainedModelSystemModel>();
                }
                else
                {
                    lock (ContainedModelSystems)
                    {
                        ContainedModelSystems.Clear();
                    }
                }

                Task.Factory.StartNew(() =>
                {
                    lock (ContainedModelSystems)
                    {
                        ContainedModelSystems.AddRange(from ms in Project.ModelSystemStructure
                                                       orderby ms.Name
                                                       select new ContainedModelSystemModel(Session, ms, Project));
                    }

                    ModelHelper.PropertyChanged(PropertyChanged, this, "ContainedModelSystems");
                });
            }

            public void Unload()
            {
                Session.ProjectWasExternallySaved -= Session_ProjectWasExternallySaved;
            }

            public class ContainedModelSystemModel : INotifyPropertyChanged
            {
                private bool _IsSelected;

                private readonly IProject _project;
                private readonly ProjectEditingSession _session;

                public ContainedModelSystemModel(ProjectEditingSession session, IModelSystemStructure ms,
                    IProject project)
                {
                    ModelSystemStructure = ms;
                    RealIndex = ((Project)project).IndexOf(ms);
                    _project = project;
                    _session = session;
                    FindMissingModules(ms);
                }

                public IModelSystemStructure ModelSystemStructure { get; }

                public string Name => ModelSystemStructure.Name;

                public string StatusText => IsMissingModules
                    ? "This module requires additional setup, or a required module is not present."
                    : null;

                public string Description => ModelSystemStructure.Description;

                public bool IsMissingModules { get; private set; }

                public int RealIndex { get; }

                /// <summary>
                ///     Returns a string representation of the last modified date. N/A is returned if last modified
                ///     information is missing.
                /// </summary>
                public string LastModified
                {
                    get
                    {
                        return ((Project)_project).GetLastModified(ModelSystemStructure) is DateTime lm 
                            && lm.Year > 1 ? lm.ToString("dddd, dd MMMM yyyy") : "N/A";                        
                    }
                }

                public string LastModifiedTime
                {
                    get
                    {
                        return ((Project)_project).GetLastModified(ModelSystemStructure) is DateTime lm
                            && lm.Year > 1 ? lm.ToString("H:mm tt") : String.Empty;
                    }
                }

                public bool IsCanPasteModelSystem
                {
                    get => MainWindow.Us.ClipboardModel != null;
                    set => OnPropertyChanged(nameof(IsCanPasteModelSystem));
                }

                public bool IsSelected
                {
                    get => _IsSelected;
                    set
                    {
                        if (_IsSelected != value)
                        {
                            _IsSelected = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "IsSelected");
                        }
                    }
                }

                public event PropertyChangedEventHandler PropertyChanged;

                [Annotations.NotifyPropertyChangedInvocator]
                protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }

                /// <summary>
                /// </summary>
                /// <param name="ms"></param>
                private void FindMissingModules(IModelSystemStructure ms)
                {
                    var loadTask = Task.Run(() =>
                    {
                        try
                        {
                            if (ms.Type == null && ms.Required && !ms.IsCollection)
                            {
                                IsMissingModules = true;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    });

                    if (ms.Children != null)
                    {
                        foreach (var subModule in ms.Children)
                        {
                            if (ms is IModelSystemStructure2 ms2)
                            {
                                if (ms2.IsDisabled)
                                {
                                    continue;
                                }
                            }

                            FindMissingModules(subModule);
                        }
                    }
                }

                internal bool SetName(string newName, ref string error)
                {
                    var ret = _session.RenameModelSystem(ModelSystemStructure, newName, ref error);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                    return ret;
                }

                internal bool CloneModelSystem(string name, ref string error)
                {
                    return _session.CloneModelSystemAs(ModelSystemStructure, name, ref error);
                }

                internal bool CloneModelSystemToProject(string name, ref string error)
                {
                    return _session.CloneModelSystemToProjectAs(ModelSystemStructure, name, ref error);
                }


                internal bool ExportModelSystem(string fileName, ref string error)
                {
                    return _session.ExportModelSystem(RealIndex, fileName, ref error);
                }
            }

            public class PreviousRun : INotifyPropertyChanged
            {
                public string Name { get; internal set; }

                public string Path { get; internal set; }

                internal DateTime Time { get; set; }

                public string TimeStamp => Time.ToString(CultureInfo.InvariantCulture);

                public event PropertyChangedEventHandler PropertyChanged;

                [Annotations.NotifyPropertyChangedInvocator]
                protected virtual void OnPropertyChanged1([CallerMemberName] string propertyName = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if(ModelSystemsDataGrid.SelectedItem == null)
            {
                e.Handled = true;
                if(e.Source is FrameworkElement fe)
                {
                    fe.ContextMenu.IsOpen = false;
                }
            }
        }
    }
}