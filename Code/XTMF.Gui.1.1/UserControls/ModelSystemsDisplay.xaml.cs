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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using XTMF.Gui.Collections;

namespace XTMF.Gui.UserControls
{
    public partial class ModelSystemsDisplay
    {
        private readonly XTMFRuntime _runtime;

        public ModelSystemsDisplay(XTMFRuntime runtime)
        {
            InitializeComponent();
            _runtime = runtime;
            var modelSystemRepository = (ModelSystemRepository) _runtime.Configuration.ModelSystemRepository;
            Display.ItemsSource = new ProxyList<IModelSystem>(modelSystemRepository.ModelSystems);
            modelSystemRepository.ModelSystemAdded += ModelSystemRepository_ModelSystemAdded;
            modelSystemRepository.ModelSystemRemoved += ModelSystemRepository_ModelSystemRemoved;
            FilterBox.Display = Display;
            FilterBox.Filter = (o, filterString) =>
            {
                var modelSystem = o as ModelSystem;
                return modelSystem != null && modelSystem.Name.IndexOf(filterString, StringComparison.InvariantCultureIgnoreCase) >= 0;
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
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            Dispatcher.BeginInvoke(new Action(() => { FilterBox.Focus(); }));
        }

        private Window GetWindow()
        {
            var current = this as DependencyObject;
            while (current != null && !(current is Window))
                current = VisualTreeHelper.GetParent(current);
            return current as Window;
        }

        private void ModelSystem_DoubleClicked(object obj)
        {
            LoadCurrentModelSystem();
        }

        private void LoadCurrentModelSystem()
        {
            LoadModelSystem(Display.SelectedItem as ModelSystem);
        }

        private void LoadModelSystem(ModelSystem modelSystem)
        {
            if (modelSystem != null)
            {
                ModelSystemEditingSession session = null;
                var progressing = new OperationProgressing
                {
                    Owner = GetWindow()
                };
                var loadingTask =
                    Task.Run(() => { session = _runtime.ModelSystemController.EditModelSystem(modelSystem); });
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() => { progressing.ShowDialog(); }));
                loadingTask.Wait();
                if (session != null)
                    MainWindow.Us.EditModelSystem(session);
                MainWindow.Us.Dispatcher.BeginInvoke(new Action(() => { progressing.Close(); }));
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
                switch (e.Key)
                {
                    case Key.F2:
                        RenameCurrentModelSystem();
                        e.Handled = true;
                        break;
                    case Key.E:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            Keyboard.Focus(FilterBox);
                            e.Handled = true;
                        }
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
                        if (!_renaming)
                        {
                            LoadCurrentModelSystem();
                            e.Handled = true;
                        }
                        break;
                }
            OnKeyUp(e);
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

        private bool _renaming;

        private void RenameCurrentModelSystem()
        {
            var modelSystem = Display.SelectedItem as ModelSystem;
            if (modelSystem != null)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                _renaming = true;
                var adorn = new TextboxAdorner("Rename", result =>
                {
                    string error = null;
                    if (!_runtime.ModelSystemController.Rename(modelSystem, result, ref error))
                        MessageBox.Show(GetWindow(), error, "Unable to Rename Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.OK);
                    else
                        RefreshModelSystems();
                }, selectedModuleControl, modelSystem.Name);
                adorn.Unloaded += Adorn_Unloaded;
                layer.Add(adorn);
                adorn.Focus();
            }
        }

        private void Adorn_Unloaded(object sender, RoutedEventArgs e)
        {
            _renaming = false;
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
                var sr = new StringRequest("Clone Model System As?", newName =>
                {
                    string e = null;
                    return _runtime.ModelSystemController.ValidateModelSystemName(newName, ref e);
                }) {Owner = GetWindow()};
                if (sr.ShowDialog() == true)
                {
                    if (!_runtime.ModelSystemController.CloneModelSystem(modelSystem, sr.Answer, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Clone Model System", MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.OK);
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
                        "Are you sure you want to delete the model system '" + modelSystem.Name +
                        "'?  This action cannot be undone!", "Delete ModelSystem", MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string error = null;
                    if (!_runtime.ModelSystemController.Delete(modelSystem, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Delete ModelSystem", MessageBoxButton.OK,
                            MessageBoxImage.Error, MessageBoxResult.OK);
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
                var fileName = MainWindow.OpenFile(modelSystem.Name,
                    new[] {new KeyValuePair<string, string>("Model System File", "xml")}, false);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    string error = null;
                    if (!_runtime.ModelSystemController.ExportModelSystem(modelSystem, fileName, ref error))
                        MessageBox.Show(owner: Window.GetWindow(this), messageBoxText: error, caption: "Unable to Export Model System",
                            button: MessageBoxButton.OK, icon: MessageBoxImage.Error, defaultResult: MessageBoxResult.OK);
                }
            }
        }

        private ModelSystem GetFirstItem()
        {
            if (Display.ItemContainerGenerator.Items.Count > 0)
                return Display.ItemContainerGenerator.Items[0] as ModelSystem;
            return null;
        }

        private void FilterBox_EnterPressed(object sender, EventArgs e)
        {
            var selected = Display.SelectedItem as ModelSystem;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            LoadModelSystem(selected);
        }
    }
}
