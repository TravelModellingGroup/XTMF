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
    public partial class ModelSystemsDisplay : UserControl
    {
        private XTMFRuntime Runtime;

        public ModelSystemsDisplay(XTMFRuntime runtime)
        {
            InitializeComponent();
            Runtime = runtime;
            var modelSystemRepository = ((ModelSystemRepository)Runtime.Configuration.ModelSystemRepository);
            Display.ItemsSource = new XTMF.Gui.Collections.ProxyList<IModelSystem>(modelSystemRepository.ModelSystems);
            modelSystemRepository.ModelSystemAdded += ModelSystemRepository_ModelSystemAdded;
            modelSystemRepository.ModelSystemRemoved += ModelSystemRepository_ModelSystemRemoved;
            FilterBox.Display = Display;
            FilterBox.Filter = (o, filterString) =>
            {
                var modelSystem = o as ModelSystem;
                return modelSystem.Name.IndexOf(filterString, StringComparison.InvariantCultureIgnoreCase) >= 0;
            };
            Loaded += ModelSystemsDisplay_Loaded;
        }

        private void ModelSystemRepository_ModelSystemRemoved(IModelSystem arg1, int arg2)
        {
            RefreshModelSystems();
        }

        private void ModelSystemRepository_ModelSystemAdded(IModelSystem obj)
        {
            RefreshModelSystems();
        }

        private void ModelSystemsDisplay_Loaded(object sender, RoutedEventArgs e)
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

        private void ModelSystem_DoubleClicked(object obj)
        {
            LoadCurrentModelSystem();
        }

        private void LoadCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                ModelSystemEditingSession session = null;
                OperationProgressing progressing = new OperationProgressing()
                {
                    Owner = GetWindow()
                };
                var loadingTask = Task.Run(() =>
                {
                    session = Runtime.ModelSystemController.EditModelSystem(modelSystem);
                });
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressing.ShowDialog();
                }));
                loadingTask.Wait();
                if (session != null)
                {
                    MainWindow.Us.EditModelSystem(session);
                }
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressing.Close();
                }));
            }
        }

        private void NewModelSystem_Click(object sender, RoutedEventArgs e)
        {
            CreateNewModelSystem();
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            RenameCurrentModelSystem();
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            CloneCurrentModelSystem();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DeleteCurrentModelSystem();
        }

        private void DeleteModelSystem_Clicked(object obj)
        {
            DeleteCurrentModelSystem();
        }

        private void CloneModelSystem_Clicked(object obj)
        {
            CloneCurrentModelSystem();
        }

        private void RenameModelSystem_Clicked(object obj)
        {
            RenameCurrentModelSystem();
        }

        private void NewModelSystem_Clicked(object sender)
        {
            CreateNewModelSystem();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportCurrentModelSystem();
        }

        private void Export_Clicked(object obj)
        {
            ExportCurrentModelSystem();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!e.Handled)
            {
                switch (e.Key)
                {
                    case Key.F2:
                        RenameCurrentModelSystem();
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        DeleteCurrentModelSystem();
                        e.Handled = true;
                        break;
                    case Key.C:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CloneCurrentModelSystem();
                            e.Handled = true;
                        }
                        break;
                    case Key.N:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            CreateNewModelSystem();
                            e.Handled = true;
                        }
                        break;
                    case Key.Enter:
                        if (!Renaming)
                        {
                            LoadCurrentModelSystem();
                            e.Handled = true;
                        }
                        break;
                }
            }
            base.OnKeyUp(e);
        }

        private void RefreshModelSystems()
        {
            var selected = Display.SelectedItem;
            Display.Items.Refresh();
            FilterBox.RefreshFilter();
            Display.SelectedItem = selected;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
        }

        bool Renaming = false;

        private void RenameCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                Renaming = true;
                var adorn = new TextboxAdorner("Rename", (result) =>
                {
                    string error = null;
                    if (!Runtime.ModelSystemController.Rename(modelSystem, result, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Rename Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                    else
                    {
                        RefreshModelSystems();
                    }
                }, selectedModuleControl, modelSystem.Name);
                adorn.Unloaded += Adorn_Unloaded;
                layer.Add(adorn);
                adorn.Focus();
            }
        }

        private void Adorn_Unloaded(object sender, RoutedEventArgs e)
        {
            Renaming = false;
        }

        private void CreateNewModelSystem()
        {
            MainWindow.Us.NewModelSystem();
            RefreshModelSystems();
        }

        private void CloneCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                string error = null;
                StringRequest sr = new StringRequest("Clone Model System As?", (newName) =>
                {
                    string e = null;
                    return Runtime.ModelSystemController.ValidateModelSystemName(newName, ref e);
                });
                sr.Owner = GetWindow();
                if (sr.ShowDialog() == true)
                {
                    if (!Runtime.ModelSystemController.CloneModelSystem(modelSystem, sr.Answer, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Clone Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        return;
                    }
                    RefreshModelSystems();
                }
            }
        }

        private void DeleteCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                if (MessageBox.Show(GetWindow(),
                    "Are you sure you want to delete the model system '" + modelSystem.Name + "'?  This action cannot be undone!", "Delete ModelSystem", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string error = null;
                    if (!Runtime.ModelSystemController.Delete(modelSystem, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Delete ModelSystem", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        return;
                    }
                    RefreshModelSystems();
                }
            }
        }

        private void ExportCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                string fileName = MainWindow.OpenFile(modelSystem.Name, new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("Model System File", "xml") }, false);
                if (!String.IsNullOrWhiteSpace(fileName))
                {
                    string error = null;
                    if (!Runtime.ModelSystemController.ExportModelSystem(modelSystem, fileName, ref error))
                    {
                        MessageBox.Show(Window.GetWindow(this), error, "Unable to Export Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
            }
        }
    }
}
