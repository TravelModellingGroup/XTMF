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
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using XTMF.Gui.Collections;

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
            Display.ItemsSource = new ProxyList<IModelSystem>(modelSystemRepository.ModelSystems);
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

        private void ModelSystemRepository_ModelSystemRemoved(IModelSystem arg1, int arg2) => RefreshModelSystems();

        private void ModelSystemRepository_ModelSystemAdded(IModelSystem obj) => RefreshModelSystems();

        private void ModelSystemsDisplay_Loaded(object sender, RoutedEventArgs e)
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

        private void ModelSystem_DoubleClicked(object obj) => LoadCurrentModelSystem();

        private void LoadCurrentModelSystem() => LoadModelSystem(Display.SelectedItem as ModelSystem);

        private void LoadModelSystem(ModelSystem modelSystem)
        {
            if (modelSystem != null)
            {
                ModelSystemEditingSession session = null;
                OperationProgressing progressing = new OperationProgressing
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

        private void NewModelSystem_Click(object sender, RoutedEventArgs e) => CreateNewModelSystem();

        private void Rename_Click(object sender, RoutedEventArgs e) => RenameCurrentModelSystem();

        private void Clone_Click(object sender, RoutedEventArgs e) => CloneCurrentModelSystem();

        private void Delete_Click(object sender, RoutedEventArgs e) => DeleteCurrentModelSystem();

        private void DeleteModelSystem_Clicked(object obj) => DeleteCurrentModelSystem();

        private void CloneModelSystem_Clicked(object obj) => CloneCurrentModelSystem();

        private void RenameModelSystem_Clicked(object obj) => RenameCurrentModelSystem();

        private void NewModelSystem_Clicked(object sender) => CreateNewModelSystem();

        private void Export_Click(object sender, RoutedEventArgs e) => ExportCurrentModelSystem();

        private void Export_Clicked(object obj) => ExportCurrentModelSystem();

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
                        if (!Renaming)
                        {
                            LoadCurrentModelSystem();
                            e.Handled = true;
                        }
                        break;
                }
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

        private UIElement GetCurrentlySelectedControl() => Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;

        bool Renaming;

        private void RenameCurrentModelSystem()
        {
            if (Display.SelectedItem is ModelSystem modelSystem)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                Renaming = true;
                var adorn = new TextboxAdorner("Rename", result =>
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

        private void Adorn_Unloaded(object sender, RoutedEventArgs e) => Renaming = false;

        private void CreateNewModelSystem()
        {
            MainWindow.Us.NewModelSystem(RootDialogHost);
            RefreshModelSystems();
        }

        private async void CloneCurrentModelSystem()
        {
            if (Display.SelectedItem is ModelSystem modelSystem)
            {
                string error = null;
                var dialog = new StringRequestDialog(RootDialogHost, "Clone Model System As?", (newName) =>
                {
                    return Runtime.ModelSystemController.ValidateModelSystemName(newName, ref error);
                }, modelSystem.Name);
                await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    if (!Runtime.ModelSystemController.CloneModelSystem(modelSystem, dialog.UserInput, ref error))
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
            if (Display.SelectedItem is ModelSystem modelSystem)
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
            if (Display.SelectedItem is ModelSystem modelSystem)
            {
                string fileName = MainWindow.OpenFile(modelSystem.Name, new[] { new KeyValuePair<string, string>("Model System File", "xml") }, false);
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

        private ModelSystem GetFirstItem() =>
            Display.ItemContainerGenerator.Items.Count > 0 ?
                Display.ItemContainerGenerator.Items[0] as ModelSystem : null;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilterBox_EnterPressed(object sender, EventArgs e)
        {
            var selected = Display.SelectedItem as ModelSystem;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            LoadModelSystem(selected);
        }


        private void ListViewControl_MouseDoubleClick(object sender, MouseButtonEventArgs e) => LoadCurrentModelSystem();


        /// <summary>
        /// Mouse button listener for the new model system button in the floating action bar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.NewModelSystem(RootDialogHost);
            RefreshModelSystems();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.ImportModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportModelSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.ImportModelSystem();
        }

        private void NewModelSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.NewModelSystem(RootDialogHost);
            RefreshModelSystems();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.RenameCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameModelSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.RenameCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloneModelSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
           this.CloneCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloneModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.CloneCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.DeleteCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteModelSystemButon_OnClick(object sender, RoutedEventArgs e)
        {
            this.DeleteCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportModelSystemStackPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.ExportCurrentModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportModelSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.ExportCurrentModelSystem();
        }
    }
}
