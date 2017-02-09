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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for LinkedParameterDisplay.xaml
    /// </summary>
    public partial class LinkedParameterDisplay : Window
    {
        private ObservableCollection<LinkedParameterDisplayModel> Items;
        private readonly LinkedParametersModel LinkedParameters;

        private readonly bool _assignMode;

        public LinkedParameterDisplay(LinkedParametersModel linkedParameters, bool assignLinkedParameter = false)
        {
            InitializeComponent();
            ChangesMade = false;
            LinkedParameters = linkedParameters;
            SetupLinkedParameters(linkedParameters);
            LinkedParameterFilterBox.Display = Display;
            LinkedParameterFilterBox.Filter = (o, text) =>
            {
                var model = o as LinkedParameterDisplayModel;
                return model.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };
            _assignMode = assignLinkedParameter;
            Display.SelectionChanged += Display_SelectionChanged;
            Loaded += LinkedParameterDisplay_Loaded;
            LinkedParameterValue.PreviewKeyDown += LinkedParameterValue_PreviewKeyDown;
        }

        public LinkedParameterDisplay()
        {
        }

        private void LinkedParameterValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled && e.Key == Key.Delete)
            {
                if (Display.IsKeyboardFocusWithin)
                {
                    var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;
                    var messageBoxResult =
                        MessageBox.Show("Are you sure you wish to delete the selected linked parameter?",
                            "Delete Confirmation [" + selectedLinkedParameter?.Name + "]", MessageBoxButton.YesNoCancel);


                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        RemoveCurrentlySelectedParameter(sender, e);
                    }
                }
            }
            else if (!e.Handled && e.Key == Key.Enter)
            {
                e.Handled = true;
                CleanupSelectedParameters();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            CleanupSelectedParameters();
            foreach (var item in Items)
            {
                item.Dispose();
            }
            base.OnClosing(e);
        }

        private void LinkedParameterDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            LinkedParameterFilterBox.Focus();
            Keyboard.Focus(LinkedParameterFilterBox);
        }

        public class ParameterDisplay
        {
            public string ParameterName { get; set; }
            public string ModuleName { get; set; }
            public bool KeepAttached { get; set; }

            public ParameterModel Parameter { get; set; }
        }


        private class BlankParameterDisplay : ParameterDisplay
        {
        }

        private LinkedParameterDisplayModel _currentlySelected;
        private List<ParameterDisplay> _currentParameters;

        /// <summary>
        ///     This will be set to true if there were any changes made to linked parameters when invoked
        /// </summary>
        public bool ChangesMade { get; private set; }

        private void Display_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CleanupSelectedParameters();
            var selectedLinkedParameter = _currentlySelected = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selectedLinkedParameter != null)
            {
                LinkedParameterValue.Text = selectedLinkedParameter.LinkedParameter.GetValue();
                var containedParameters =
                    _currentParameters = (from parameter in selectedLinkedParameter.LinkedParameter.GetParameters()
                        select new ParameterDisplay
                        {
                            ParameterName = parameter.Name,
                            ModuleName = parameter.BelongsTo.Name,
                            Parameter = parameter,
                            KeepAttached = true
                        }).ToList();
                ContainedParameterDisplay.ItemsSource = new ObservableCollection<ParameterDisplay>(containedParameters);


                LinkedParameterName.Text = selectedLinkedParameter.LinkedParameter.Name;
            }
        }

        /// <summary>
        ///     Call this function to make sure that parameters that have been requested to be removed are.
        /// </summary>
        private void CleanupSelectedParameters()
        {
            if (_currentlySelected != null && _currentParameters != null)
            {
                // save the value for the linked parameter
                AssignLinkedParameterValue(LinkedParameterValue.Text);
                // save the parameters
                foreach (var parameter in _currentParameters)
                {
                    if (!parameter.KeepAttached)
                    {
                        string error = null;
                        if (!_currentlySelected.LinkedParameter.RemoveParameter(parameter.Parameter, ref error))
                        {
                            MessageBox.Show(
                                "There was an error trying to remove a parameter from a linked parameter!\r\n" + error,
                                "Error removing parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        ChangesMade = true;
                    }
                }
            }
        }

        private void AssignLinkedParameterValue(string text)
        {
            if (_currentlySelected != null)
            {
                string error = null;
                if (_currentlySelected.LinkedParameter.GetValue() != text)
                {
                    if (!_currentlySelected.LinkedParameter.SetValue(text, ref error))
                    {
                        MessageBox.Show(
                            "There was an error assigning the value '" + text + "' to the linked parameter!\r\n" + error,
                            "Error setting value", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    ChangesMade = true;
                }
            }
        }

        private void SetupLinkedParameters(LinkedParametersModel linkedParameters)
        {
            var items = LinkedParameterDisplayModel.CreateDisplayModel(linkedParameters.GetLinkedParameters());
            Display.ItemsSource = items;
            Items = items;
        }

        internal LinkedParameterModel SelectedLinkParameter { get; set; }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_assignMode && !_renaming)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        AssignCurrentlySelected();
                        break;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private void AssignCurrentlySelected()
        {
            var selected = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selected != null)
            {
                Select(selected, true);
            }
        }

        private void Select(LinkedParameterDisplayModel selected, bool cleanup)
        {
            _currentlySelected = selected;
            if (cleanup)
            {
                CleanupSelectedParameters();
            }
            SelectedLinkParameter = selected.LinkedParameter;
            DialogResult = true;
            ChangesMade = true;
            Close();
        }

        private bool _renaming;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case Key.F2:
                    {
                        Rename();
                    }
                        e.Handled = true;
                        break;
                }
            }
            base.OnKeyDown(e);
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            Rename();
        }

        private void Rename()
        {
            var selected = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selected != null)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                _renaming = true;
                var adorn = new TextboxAdorner("Rename", result => { selected.Name = result; }, selectedModuleControl,
                    selected.Name);
                adorn.Unloaded += Adorn_Unloaded;
                layer.Add(adorn);
                adorn.Focus();
            }
        }

        private void Adorn_Unloaded(object sender, RoutedEventArgs e)
        {
            _renaming = false;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
        }

        private void NewLinkedParameter_Clicked(object obj)
        {
            var request = new StringRequest("Name the new Linked Parameter", s => true);
            request.Owner = this;
            request.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (request.ShowDialog() == true)
            {
                string error = null;
                if (!LinkedParameters.NewLinkedParameter(request.Answer, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to create new Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                SetupLinkedParameters(LinkedParameters);
                ChangesMade = true;
            }
        }

        private void RemoveCurrentlySelectedParameter(object sender, RoutedEventArgs e)
        {
            var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;


            if (selectedLinkedParameter == null)
            {
                return;
            }
            string error = null;
            var index = LinkedParameters.GetLinkedParameters().IndexOf(selectedLinkedParameter.LinkedParameter);
            if (!LinkedParameters.RemoveLinkedParameter(selectedLinkedParameter.LinkedParameter, ref error))
            {
                MessageBox.Show(MainWindow.Us, error, "Failed to remove Linked Parameter", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            var items = Display.ItemsSource as ObservableCollection<LinkedParameterDisplayModel>;
            items?.Remove(selectedLinkedParameter);
            ChangesMade = true;
        }

        private void RemoveLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            RemoveCurrentlySelectedParameter(sender, e);
        }

        private void BorderIconButton_DoubleClicked(object obj)
        {
            if (_assignMode)
            {
                AssignCurrentlySelected();
            }
        }

        private LinkedParameterDisplayModel GetFirstItem()
        {
            if (Display.ItemContainerGenerator.Items.Count > 0)
            {
                return Display.ItemContainerGenerator.Items[0] as LinkedParameterDisplayModel;
            }
            return null;
        }

        private void LinkedParameterFilterBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.Key == Key.Enter)
                {
                    var first = GetFirstItem();
                    if (first != null)
                    {
                        Select(first, false);
                        e.Handled = true;
                    }
                }
            }
        }

        private void BorderIconButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void UnlinkParameter(string parameterName)
        {
            foreach (var parameter in _currentParameters)
            {
                if (parameter.ParameterName == parameterName)
                {
                    string error = null;


                    if (ConfirmUnlinkParameterMessageBox(parameter) == MessageBoxResult.Cancel)
                    {
                        return;
                    }

                    if (!_currentlySelected.LinkedParameter.RemoveParameter(parameter.Parameter, ref error))
                    {
                        MessageBox.Show(
                            "There was an error trying to remove a parameter from a linked parameter!\r\n" + error,
                            "Error removing parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    ChangesMade = true;

                    var containedParameters =
                        _currentParameters = (from parameter2 in _currentlySelected.LinkedParameter.GetParameters()
                            select new ParameterDisplay
                            {
                                ParameterName = parameter2.Name,
                                ModuleName = parameter2.BelongsTo.Name,
                                Parameter = parameter2,
                                KeepAttached = true
                            }).ToList();

                    // ContainedParameterDisplay.Items.Clear();
                    ContainedParameterDisplay.ItemsSource =
                        new ObservableCollection<ParameterDisplay>(containedParameters);
                    break;
                }
            }
        }

        private void Unlink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;

            var parameterName = label.Tag;

            UnlinkParameter(parameterName.ToString());
        }

        private void ContainedParameterDisplay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var listView = sender as ListView;

                var parameterDisplay = listView.SelectedItem as ParameterDisplay;

                UnlinkParameter(parameterDisplay.ParameterName);
            }
        }

        private MessageBoxResult ConfirmUnlinkParameterMessageBox(ParameterDisplay parameterDisplay)
        {
            var result = MessageBox.Show(MainWindow.Us, "Are you sure you wish to unlink the selected parameter?",
                "Confirm Unlink [" + parameterDisplay.ParameterName + "]",
                MessageBoxButton.OKCancel);

            return result;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                Close();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {


                if (e.GetPosition(this).Y < 30)
                {



                    e.Handled = true;
                    DragMove();

                }

            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxNorm_OnClick(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ParameterDatatemplateSelector : DataTemplateSelector
    {
    }
}
