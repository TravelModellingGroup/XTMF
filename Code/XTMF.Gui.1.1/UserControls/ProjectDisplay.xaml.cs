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
using XTMF.Gui.Annotations;
using XTMF.Gui.Controllers;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ProjectDisplay.xaml
    /// </summary>
    public partial class ProjectDisplay : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register("Project", typeof(Project), typeof(ProjectDisplay),
    new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnProjectChanged));

        public static readonly DependencyProperty ProjectModelProperty = DependencyProperty.Register("Model", typeof(ProjectModel), typeof(ProjectDisplay),
    new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnProjectModelChanged));

        public event Action<ModelSystemEditingSession> InitiateModelSystemEditingSession;


      



        public class ProjectModel : INotifyPropertyChanged
        {
            public class ContainedModelSystemModel : INotifyPropertyChanged
            {
                private IModelSystemStructure Root;

                private ProjectEditingSession _session;

                public IModelSystemStructure ModelSystemStructure
                {
                    get { return Root; }
                }

                public event PropertyChangedEventHandler PropertyChanged;

                public string Name { get { return Root.Name; } }

                public string StatusText {
                    get {

                        if (!_IsMissingModules)
                        {
                            return null;
                        }
                        else
                        {
                            return "This module requires additional setup, or a required module is not present.";
                        }
                    }
                }

                public string Description { get { return Root.Description; } }

                public bool IsMissingModules
                {
                    get
                    {
                        return _IsMissingModules;
                    }
                    
                }

                public int RealIndex { get; private set; }

                private bool _IsMissingModules = false;

                private IProject _project;
                private bool _IsSelected;
                public bool IsSelected
                {
                    get
                    {
                        return _IsSelected;
                    }
                    set
                    {
                        if (_IsSelected != value)
                        {
                            _IsSelected = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "IsSelected");
                        }
                    }
                }

                public ContainedModelSystemModel(ProjectEditingSession session, IModelSystemStructure ms, IProject project)
                {
                    Root = ms;
                    RealIndex = project.ModelSystemStructure.IndexOf(ms);
                    this._project = project;
                    _session = session;


                    FindMissingModules(ms);
                }

                private void FindMissingModules(IModelSystemStructure ms)
                {
                    var loadTask = Task.Run(() =>
                    {
                        try
                        {
                            
                            
                            if(ms.Type == null && ms.Required && !ms.IsCollection)
                            {
                                _IsMissingModules = true;
                            }
                            else
                            {
                               
                            }


                        }
                        catch (Exception error)
                        {
                            
                        }
                    });

                    if (ms.Children != null)
                    {
                        foreach (var subModule in ms.Children)
                        {
                            FindMissingModules(subModule);
                        }
                    }

                }

                internal bool SetName(ProjectEditingSession session, string newName, ref string error)
                {
                    var ret = session.RenameModelSystem(Root, newName, ref error);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Name");
                    return ret;
                }

                internal bool CloneModelSystem(ProjectEditingSession session, string name, ref string error)
                {
                    return session.CloneModelSystemAs(Root, name, ref error);
                }

                internal bool CloneModelSystemToProject(ProjectEditingSession session, string name, ref string error)
                {
                    return session.CloneModelSystemToProjectAs(Root, name, ref error);
                }

              

                internal bool ExportModelSystem(ProjectEditingSession session, string fileName, ref string error)
                {
                    return session.ExportModelSystem(RealIndex, fileName, ref error);
                }
            }

            public class PreviousRun : INotifyPropertyChanged
            {
                public string Name { get; internal set; }
                public string Path { get; internal set; }
                internal DateTime Time { get; set; }
                public string TimeStamp => Time.ToString(CultureInfo.InvariantCulture);

                public event PropertyChangedEventHandler PropertyChanged;

                [NotifyPropertyChangedInvocator]
                protected virtual void OnPropertyChanged1([CallerMemberName] string propertyName = null)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            public List<ContainedModelSystemModel> ContainedModelSystems;

            public List<PreviousRun> PreviousRuns = new List<PreviousRun>();

            private IProject Project;

            public event PropertyChangedEventHandler PropertyChanged;

            ProjectEditingSession Session;

            public ProjectModel(IProject project, ProjectEditingSession session)
            {
                Session = session;
                Project = project;
                session.ProjectWasExternallySaved += Session_ProjectWasExternallySaved;
                RefreshModelSystems();
                RefreshPastRuns(session);
            }

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
                        foreach (var pastRun in session.GetPreviousRuns())
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
                    var modelSystems = (from ms in Project.ModelSystemStructure
                                        orderby ms.Name ascending
                                        select new ContainedModelSystemModel(Session,ms, Project));
                    lock (ContainedModelSystems)
                    {
                        ContainedModelSystems.AddRange(modelSystems);
                    }
                    ModelHelper.PropertyChanged(PropertyChanged, this, "ContainedModelSystems");
                });
            }

            public void Unload()
            {
                Session.ProjectWasExternallySaved -= Session_ProjectWasExternallySaved;
            }
        }

        private Project Project
        {
            get
            {
                return GetValue(ProjectProperty) as Project;
            }
            set
            {
                SetValue(ProjectProperty, value);
            }
        }

        private ProjectEditingSession _session;

        public ProjectEditingSession Session
        {
            get
            {
                return _session;
            }
            set
            {
                _session = value;
                Project = _session.Project;
            }
        }

        public ProjectModel Model
        {
            get
            {
                return GetValue(ProjectModelProperty) as ProjectModel;
            }
            set
            {
                SetValue(ProjectModelProperty, value);
            }
        }

       

        public ProjectDisplay()
        {
            InitializeComponent();
            Loaded += ProjectDisplay_Loaded;
            ContextMenu.PlacementTarget = ModelSystemDisplay;
        }

        private void ProjectDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FilterModelSystemsBox.Focus();
            }));
        }

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
                    us.ModelSystemDisplay.ItemsSource = us.Model.ContainedModelSystems;
                    us.PastRunDisplay.ItemsSource = us.Model.PreviousRuns;
                    us.FilterModelSystemsBox.RefreshFilter();
                    us.FilterPastRunsBox.RefreshFilter();
                }));
            };
            lock (us.Model.ContainedModelSystems)
            {
                us.ModelSystemDisplay.ItemsSource = us.Model.ContainedModelSystems;
            }
            lock (us.Model.PreviousRuns)
            {
                us.PastRunDisplay.ItemsSource = us.Model.PreviousRuns;
            }

            us.FilterModelSystemsBox.Display = us.ModelSystemDisplay;
            us.FilterModelSystemsBox.Filter = us.FilterMS;

            us.FilterPastRunsBox.Display = us.PastRunDisplay;
            us.FilterPastRunsBox.Filter = us.FilterRuns;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterModelSystemsBox.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
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
                            LoadModelSystem();
                            e.Handled = true;
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
                            PasteModelSystem_OnClick(null,null);
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
                if (MessageBox.Show("Are you sure that you want to close this window?", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        e(this);
                    }));
                }
            }
        }

       

        private void ModelSystemDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            var s = ModelSystemDisplay.SelectedItem;
            
        }

        private void LoadModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
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
                ModelSystemDisplay.SelectedItem = null;
            }
        }

        private void RefreshPreviousRuns_Clicked(object obj)
        {
            Model.RefreshPastRuns(Session);
        }

        private void OpenProjectFolder_Clicked(object obj)
        {
            OpenProjectFolder();
        }

        private void OpenPreviousRun_Click(object sender, RoutedEventArgs e)
        {
            var previousRun = (ProjectModel.PreviousRun)PastRunDisplay.SelectedItem;
            if (previousRun != null)
            {
                var directoryName = Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name, previousRun.Name);
                try
                {
                    if (Project != null && Directory.Exists(directoryName))
                    {
                        Process.Start(directoryName);
                    }
                }
                catch
                {
                    MessageBox.Show(MainWindow.Us, "We were unable to open the run directory '" + directoryName + "'!", "Unable to Open");
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
                MessageBox.Show(MainWindow.Us, "We were unable to open the project directory '" + directoryName + "'!", "Unable to Open");
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

        private void ImportModelSystem_Clicked(object obj)
        {
            var xtmf = Session.GetRuntime();
            var openMS = new OpenWindow();
            openMS.Owner = GetWindow();
            using (var modelSystemSession = openMS.OpenModelSystem(xtmf))
            {
                var loading = openMS.LoadTask;
                if (loading != null)
                {
                    loading.Wait();
                    using (var realSession = openMS.ModelSystemSession)
                    {
                        if (realSession == null)
                        {
                            return;
                        }
                        string error = null;
                        StringRequest sr = new StringRequest("Save Model System As?", newName =>
                        {
                            return Session.ValidateModelSystemName(newName);
                        });
                        sr.Owner = GetWindow();
                        if (sr.ShowDialog() == true)
                        {
                            if (!Session.AddModelSystem(realSession.ModelSystemModel, sr.Answer, ref error))
                            {
                                MessageBox.Show(GetWindow(), error, "Unable to Import Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                                return;
                            }
                            Model.RefreshModelSystems();
                        }
                    }
                }
            }
        }

        private void DeleteModelSystem_Click(object sender, RoutedEventArgs e)
        {
            DeleteCurrentModelSystem();
        }

        private void DeleteCurrentModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
            if (selected != null)
            {
                int index = selected.RealIndex;
                string error = null;
                if (MessageBox.Show("Are you sure you wish to delete '" + selected.Name + "'?  This can not be undone!", "Confirm Delete!",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    if (!Session.RemoveModelSystem(index, ref error))
                    {
                        MessageBox.Show(error, "Unable to Delete Model System", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var previousRun = PastRunDisplay.SelectedItem as ProjectModel.PreviousRun;
            if(previousRun != null)
            {
                try
                {
                    var directoryName = Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name, previousRun.Name);
                    DirectoryInfo dir = new DirectoryInfo(directoryName);
                    if(dir.Exists)
                    {
                        dir.Delete(true);
                    }
                    Model.RefreshPastRuns(Session);
                }
                catch(IOException error)
                {
                    MessageBox.Show(error.Message, "Unable to Delete Previous Run", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RenameCurrentModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
            if (selected != null)
            {
                var container = ModelSystemDisplay.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem;
                var layer = AdornerLayer.GetAdornerLayer(container);
                var adorn = new TextboxAdorner("Rename", result =>
                {
                    string error = null;
                    if (!selected.SetName(Session, result, ref error))
                    {
                        MessageBox.Show(error, "Unable to Rename Model System", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }, container, selected.Name);
                layer.Add(adorn);
                adorn.Focus();
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

        private void SaveCurrentAsModelSystem(bool exportToFile)
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
            if (selected != null)
            {
                if (exportToFile)
                {
                    // save as a model system in an external file
                    string fileName = MainWindow.OpenFile(selected.Name, new[] { new KeyValuePair<string, string>("Model System File", "xml") }, false);
                    if (!String.IsNullOrWhiteSpace(fileName))
                    {
                        string error = null;
                        if (!selected.ExportModelSystem(Session, fileName, ref error))
                        {
                            MessageBox.Show(Window.GetWindow(this), error, "Unable to Export Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        }
                    }
                }
                else
                {
                    // save as a model system within XTMF
                    StringRequest sr = new StringRequest("Save Model System As?", newName =>
                    {
                        return Session.ValidateModelSystemName(newName);
                    });
                    sr.Owner = GetWindow();
                    if (sr.ShowDialog() == true)
                    {
                        string error = null;

                        if (!selected.CloneModelSystem(Session, sr.Answer, ref error))
                        {
                            MessageBox.Show(error, "Unable to Save Model System", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void CopyModelSystem_Click(object sender, RoutedEventArgs e)
        {
           // SetValue(ModelSystemListView.IsCanPasteModelSystemDependencyProperty,true);
            ModelSystemDisplay.IsCanPasteModelSystem = true;
            
            CloneCurrentModelSystem();
        }

        private void CloneCurrentModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;


          
            if (selected != null)
            {
                var error = string.Empty;
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

        private void CreateNewModelSystem()
        {
            StringRequest sr = new StringRequest("New Model System's Name?", newName =>
            {
                return Session.ValidateModelSystemName(newName);
            });




            sr.Owner = Window.GetWindow(this);

            if (sr.ShowDialog() == true)
            {
                string error = null;
                if (!Session.AddModelSystem(sr.Answer, ref error))
                {
                    MessageBox.Show(error, "Unable to create New Model System", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    Model.RefreshModelSystems();
                }
            }
        }

        private ProjectModel.ContainedModelSystemModel GetFirstItem()
        {
            if (ModelSystemDisplay.ItemContainerGenerator.Items.Count > 0)
            {
                return ModelSystemDisplay.ItemContainerGenerator.Items[0] as ProjectModel.ContainedModelSystemModel;
            }
            return null;
        }

        private void FilterModelSystemsBox_EnterPressed(object sender, EventArgs e)
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            LoadModelSystem(selected);
        }


        private void PastRuns_MouseButton(object sender, MouseButtonEventArgs e)
        {
            var selected = PastRunDisplay.SelectedItem as ProjectModel.PreviousRun;


            if (selected != null)
            {
                var invoke = InitiateModelSystemEditingSession;
                if (invoke != null)
                {
                    string error = null;
                    ModelSystemEditingSession newSession;



                    if (Session.LoadPreviousRun(selected.Path, ref error, out newSession))
                    {
                        if (newSession != null)
                        {
                            newSession.PreviousRunName = selected.Name;
                            invoke(newSession);
                        }
                    }
                    else
                    {
                        MessageBox.Show(error, "Unable to Open Model System", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                PastRunDisplay.SelectedItem = null;
            }

        }

        private void ListViewControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LoadModelSystem();
        }

        private void ImportModelSystemFromFile_OnClicked(object obj)
        {
            var fileDialog = new System.Windows.Forms.OpenFileDialog();
            var result = fileDialog.ShowDialog();

            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    string error = null;

                    try
                    {
                        ModelSystem modelSystem = ModelSystem.LoadDetachedModelSystem(fileDialog.OpenFile(), EditorController.Runtime.Configuration,
                       ref error);


                        StringRequest sr = new StringRequest("Save Model System As?", newName => Session.ValidateModelSystemName(newName));
                        sr.Owner = GetWindow();
                        if (sr.ShowDialog() == true)
                        {
                            if (!Session.AddModelSystem(modelSystem, sr.Answer, ref error))
                            {
                                MessageBox.Show(GetWindow(), error, "Unable to Import Model System", MessageBoxButton.OK,
                                    MessageBoxImage.Error, MessageBoxResult.OK);
                                return;
                            }
                            Model.RefreshModelSystems();
                        }

                    }
                    catch
                    {
                        MessageBox.Show(GetWindow(), "There was an error importing the model system.", "Unable to Import Model System", MessageBoxButton.OK,
                                    MessageBoxImage.Error, MessageBoxResult.OK);
                    }


                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        private void PasteModelSystem_OnClick(object sender, RoutedEventArgs e)
        {
         
            string error = null;
            if (MainWindow.Us.ClipboardModel != null)
            {

                ModelSystem cloned = Session.CloneModelSystem(MainWindow.Us.ClipboardModel.ModelSystemStructure, ref error);
                StringRequest sr = new StringRequest("Paste: Model System's Name?", newName =>
                {
                    return Session.ValidateModelSystemName(newName);
                });
                sr.Owner = GetWindow();
                if (sr.ShowDialog() == true)
                {
                    

                    if (
                        !Session.AddExternalModelSystem(cloned, sr.Answer,
                            ref error))
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

        public event PropertyChangedEventHandler PropertyChanged;

        [XTMF.Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
