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
using System.IO;
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
using XTMF;

using Path = System.IO.Path;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ProjectDisplay.xaml
    /// </summary>
    public partial class ProjectDisplay : UserControl
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

                public event PropertyChangedEventHandler PropertyChanged;

                public string Name { get { return Root.Name; } }

                public string Description { get { return Root.Description; } }

                public int RealIndex { get; private set; }

                public ContainedModelSystemModel(IModelSystemStructure ms, IProject project)
                {
                    Root = ms;
                    RealIndex = project.ModelSystemStructure.IndexOf(ms);
                }
            }

            public class PreviousRun : INotifyPropertyChanged
            {
                public string Name { get; internal set; }
                public string Path { get; internal set; }
                public string TimeStamp { get; internal set; }

                public event PropertyChangedEventHandler PropertyChanged;
            }

            public List<ContainedModelSystemModel> ContainedModelSystems;

            public List<PreviousRun> PreviousRuns;

            private IProject Project;

            public event PropertyChangedEventHandler PropertyChanged;

            public ProjectModel(IProject project, ProjectEditingSession session)
            {
                Project = project;
                RefreshModelSystems();
                RefreshPastRuns(session);
            }

            public void RefreshPastRuns(ProjectEditingSession session)
            {

                if(PreviousRuns == null)
                {
                    PreviousRuns = new List<PreviousRun>();
                }
                else
                {
                    PreviousRuns.Clear();
                }
                Task.Factory.StartNew(() =>
                {

                    var list = new List<PreviousRun>();
                    PreviousRuns.Clear();
                    foreach(var pastRun in session.GetPreviousRuns())
                    {
                        DirectoryInfo info = new DirectoryInfo(pastRun);
                        list.Add(new PreviousRun()
                        {
                            Name = info.Name,
                            Path = pastRun,
                            TimeStamp = info.CreationTime.ToString()
                        });
                    }
                    lock (PreviousRuns)
                    {
                        PreviousRuns.AddRange(list);
                    }
                    ModelHelper.PropertyChanged(PropertyChanged, this, "PreviousRuns");
                });
            }

            private void RefreshModelSystems()
            {
                if(ContainedModelSystems == null)
                {
                    ContainedModelSystems = new List<ContainedModelSystemModel>();
                }
                else
                {
                    ContainedModelSystems.Clear();
                }
                Task.Factory.StartNew(() =>
                {
                    var modelSystems = (from ms in Project.ModelSystemStructure
                                        select new ContainedModelSystemModel(ms, Project)).OrderBy(x => x.Name);
                    lock (ContainedModelSystems)
                    {
                        ContainedModelSystems.AddRange(modelSystems);
                    }
                    ModelHelper.PropertyChanged(PropertyChanged, this, "ContainedModelSystems");
                });
            }
        }

        public Project Project
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

        private ProjectEditingSession _Session;

        public ProjectEditingSession Session
        {
            get
            {
                return _Session;
            }
            set
            {
                _Session = value;
                Project = _Session.Project;
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
        }

        private static void OnProjectChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ProjectDisplay;
            us.DataContext = us.Project;
            us.Model = new ProjectModel(us.Project, us.Session);
        }

        private bool FilterMS(object e, string text)
        {
            var element = e as ProjectDisplay.ProjectModel.ContainedModelSystemModel;
            return CheckString(element.Name, text) | CheckString(element.Description, text);
        }

        private bool FilterRuns(object e, string text)
        {
            var element = e as ProjectDisplay.ProjectModel.PreviousRun;
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
            if(e.Handled == false && Controllers.EditorController.IsControlDown())
            {
                switch(e.Key)
                {
                    case Key.W:
                        Close();
                        break;
                }
            }
        }

        public event Action<object> RequestClose;

        private void Close()
        {
            var e = RequestClose;
            if(e != null)
            {
                if(MessageBox.Show("Are you sure that you want to close this window?", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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
            LoadModelSystem();
        }

        private void LoadModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedItem as ProjectModel.ContainedModelSystemModel;
            if(selected != null)
            {
                var invoke = InitiateModelSystemEditingSession;
                if(invoke != null)
                {
                    var index = selected.RealIndex;
                    invoke(Session.EditModelSystem(index));
                }
                ModelSystemDisplay.SelectedItem = null;
            }
        }

        private void PastRunDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = PastRunDisplay.SelectedItem as ProjectModel.PreviousRun;
            if(selected != null)
            {
                var invoke = InitiateModelSystemEditingSession;
                if(invoke != null)
                {
                    string error = null;
                    var newSession = Session.LoadPreviousRun(selected.Path, ref error);
                    if(newSession != null)
                    {
                        invoke(newSession);
                    }
                }
                PastRunDisplay.SelectedItem = null;
            }
        }

        private void RefreshPreviousRuns_Clicked(object obj)
        {
            Model.RefreshPastRuns(Session);
        }

        private void RenameProject_Clicked(object obj)
        {

        }

        private void OpenProjectFolder_Clicked(object obj)
        {
            var directoryName = System.IO.Path.Combine(Session.GetConfiguration().ProjectDirectory, Project.Name);
            try
            {
                if(Project != null && System.IO.Directory.Exists(directoryName))
                {
                    System.Diagnostics.Process.Start(directoryName);
                }
            }
            catch
            {
                MessageBox.Show(MainWindow.Us, "Unable to Open", "We were unable to open the project directory '" + directoryName + "'!");
            }
        }

        private void DeleteProject_Clicked(object obj)
        {

        }

        private void CloneProject_Clicked(object obj)
        {

        }
    }
}
