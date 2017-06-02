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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for OpenWindow.xaml
    /// </summary>
    public partial class OpenWindow : Window
    {

        private class Model : INotifyPropertyChanged
        {
            private string _currentName;
            public string CurrentName
            {
                get
                {
                    return _currentName;
                }
                set
                {
                    if (_currentName != value)
                    {
                        _currentName = value;
                        ModelHelper.PropertyChanged(PropertyChanged, this, "CurrentName");
                    }
                }
            }

            private string _currentText;
            public string CurrentText
            {
                get
                {
                    return _currentText;
                }
                set
                {
                    if (_currentText != value)
                    {
                        _currentText = value;
                        ModelHelper.PropertyChanged(PropertyChanged, this, "CurrentText");
                    }
                }
            }

            public class ModelElement : INotifyPropertyChanged
            {
                private Model Root;

                internal object Data;

                public ModelElement(string name, string text, object data, Model root)
                {
                    Root = root;
                    Name = name;
                    Text = text;
                    Data = data;
                }

                public string Name { get; private set; }
                public string Text { get; private set; }

                public event PropertyChangedEventHandler PropertyChanged;

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
                            if (value)
                            {
                                Root.CurrentName = Name;
                                Root.CurrentText = Text;
                            }
                        }
                    }
                }
            }

            public List<ModelElement> Data = new List<ModelElement>();

            public event PropertyChangedEventHandler PropertyChanged;

            public void Initialize(IEnumerable<IProject> projects)
            {
                Data.Clear();
                Task.Factory.StartNew(() =>
                {
                    lock (Data)
                    {
                        foreach (var project in projects)
                        {
                            Data.Add(new ModelElement(project.Name, project.Description, project, this));
                        }
                    }
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Data");
                });
            }

            public void Initialize(IEnumerable<IModelSystem> modelSystems)
            {
                Data.Clear();
                Task.Factory.StartNew(() =>
                {
                    lock (Data)
                    {
                        foreach (var modelSystem in modelSystems)
                        {
                            Data.Add(new ModelElement(modelSystem.Name, modelSystem.Description, modelSystem, this));
                        }
                    }
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Data");
                });
            }
        }

        private Model InternalModel = new Model();

        public OpenWindow()
        {
            InitializeComponent();
            InternalModel.PropertyChanged += (o, e) =>
            {
                FilterBox.RefreshFilter();
            };
        }

        private ModelSystemEditingSession _msEditSession;
        private ProjectEditingSession _pEditSession;
        XTMFRuntime Runtime;

        public ModelSystemEditingSession OpenModelSystem(XTMFRuntime runtime)
        {
            ExportButton.IsEnabled = true;
            Runtime = runtime;
            _msEditSession = null;
            InternalModel.Initialize(runtime.ModelSystemController.GetModelSystems());
            DataContext = InternalModel;
            lock (InternalModel.Data)
            {
                Display.ItemsSource = InternalModel.Data;
            }
            FilterBox.Display = Display;
            FilterBox.Filter = Filter;
            ShowDialog();
            return _msEditSession;
        }

        public ProjectEditingSession OpenProject(XTMFRuntime runtime)
        {
            ExportButton.IsEnabled = false;
            Runtime = runtime;
            _pEditSession = null;
            InternalModel.Initialize(runtime.ProjectController.GetProjects());
            DataContext = InternalModel;
            lock (InternalModel.Data)
            {
                Display.ItemsSource = InternalModel.Data;
            }
            FilterBox.Display = Display;
            FilterBox.Filter = Filter;
            ShowDialog();
            return _pEditSession;
        }

        private bool Filter(object e, string text)
        {
            var element = e as Model.ModelElement;
            return CheckString(element.Name, text) | CheckString(element.Text, text);
        }

        private static bool CheckString(string str, string filter)
        {
            return str == null ? false : str.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        internal Task LoadTask;

        private void Select()
        {
            var index = Display.SelectedIndex;
            if (index < 0) return;
            IsEnabled = false;

            var result = InternalModel.Data[InternalModel.Data.IndexOf((Model.ModelElement)Display.SelectedItem)].Data;
            if (result is Project)
            {
                MainWindow.SetStatusText("Loading Project...");
            }
            else
            {
                MainWindow.SetStatusText("Loading Model System...");
            }
            LoadTask = Task.Factory.StartNew(() =>
                {
                    if (result is Project)
                    {
                        ProjectSession = _pEditSession = Runtime.ProjectController.EditProject(result as Project);
                    }
                    else
                    {

                        ModelSystem a = result as ModelSystem;
                        _msEditSession = Runtime.ModelSystemController.EditModelSystem(result as ModelSystem);

                        ModelSystemSession = _msEditSession;
                    }
                });
            Close();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var index = Display.SelectedIndex;
            if (index < 0) return;
            IsEnabled = false;

            var result = InternalModel.Data[InternalModel.Data.IndexOf((Model.ModelElement)Display.SelectedItem)].Data;
            string error = null;
            if (result is Project)
            {
                if (MessageBox.Show(GetWindow(this),
                "Are you sure you want to delete the project '" + (result as Project).Name + "' ?", "Delete Project", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    if (!Runtime.ProjectController.DeleteProject(result as Project, ref error))
                    {
                        MessageBox.Show(GetWindow(this), error, "Unable to Delete Project", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
                Close();
            }
            else
            {
                if (MessageBox.Show(GetWindow(this),
                "Are you sure you want to delete the model system '" + (result as ModelSystem).Name + "'?", "Delete Model System", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    if (!Runtime.ModelSystemController.Delete(result as ModelSystem, ref error))
                    {
                        MessageBox.Show(GetWindow(this), error, "Unable to Delete Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
                Close();
            }
        }

        internal ProjectEditingSession ProjectSession;

        internal ModelSystemEditingSession ModelSystemSession;

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            FilterBox.Focus();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterBox.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
            {
                if (e.Key == Key.Enter)
                {
                    Select();
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Handled == false && e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        

        private void Display_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Display.SelectedItem == null)
            {
                var numberOfItems = Display.Items.Count;
                if (numberOfItems >= 0)
                {
                    Display.SelectedIndex = 0;
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var item = (Display.SelectedItem as Model.ModelElement);
            if (item != null)
            {
                string fileName = MainWindow.OpenFile(item.Name, new[]{ new KeyValuePair<string, string>("Model System File", "xml") }, false);
                if (!String.IsNullOrWhiteSpace(fileName))
                {
                    string error = null;
                    var modelSystem = item.Data as IModelSystem;
                    if (!Runtime.ModelSystemController.ExportModelSystem(modelSystem, fileName, ref error))
                    {
                        MessageBox.Show(GetWindow(this), error, "Unable to Export Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
            }
        }

        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Select();
        }
    }
}
