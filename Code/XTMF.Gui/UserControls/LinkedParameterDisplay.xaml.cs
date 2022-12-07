/*
    Copyright 2017-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for LinkedParameterDisplay.xaml
    /// </summary>
    public partial class LinkedParameterDisplay : UserControl
    {
        private LinkedParameterDisplayModel _currentlySelected;

        private List<ParameterDisplay> _currentParameters;

        private LinkedParametersModel _linkedParametersModel;

        public Action<IModelSystemStructure> GoToModule;

        public Action OnCloseDisplay;

        public bool IsAssignMode = false;

        public LinkedParameterDisplay(LinkedParametersModel linkedParameters)
        {
            InitializeComponent();
            ChangesMade = false;
            _linkedParametersModel = linkedParameters;
            SetupLinkedParameters(linkedParameters);
        }

        public LinkedParameterDisplay()
        {
            InitializeComponent();
            ChangesMade = false;
            LinkedParameterValue.PreviewKeyDown += LinkedParameterValue_PreviewKeyDown;
        }

        public LinkedParametersModel LinkedParametersModel
        {
            get => _linkedParametersModel;
            set
            {
                _linkedParametersModel = value;
                SetupLinkedParameters(_linkedParametersModel);
            }
        }

        public DialogOpenedEventArgs DialogOpenedEventArgs { get; set; }

        /// <summary>
        ///     This will be set to true if there were any changes made to linked parameters when invoked
        /// </summary>
        public bool ChangesMade { get; private set; }

        internal LinkedParameterModel SelectedLinkParameter { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public void InitNewDisplay()
        {
            _currentlySelected = null;
            SelectedLinkParameter = null;
            LinkedParameterFilterBox.RetriveFocus();
            FocusManager.SetFocusedElement(this, LinkedParameterFilterBox);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkedParameterValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled && e.Key == Key.Delete)
            {
                if (Display.IsKeyboardFocusWithin)
                {
                    var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;
                    var messageBoxResult =
                        MessageBox.Show("Are you sure you wish to delete the selected linked parameter?",
                            "Delete Confirmation [" + selectedLinkedParameter?.Name + "]",
                            MessageBoxButton.YesNoCancel);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        RemoveCurrentlySelectedParameter(sender, e);
                    }
                }
            }
            else if (!e.Handled && e.Key == Key.Enter)
            {
                e.Handled = true;
                AssignCurrentlySelected();
                ChangesMade = true;
                CleanupSelectedParameters();
                DialogOpenedEventArgs.Session.Close();
                OnCloseDisplay.BeginInvoke(null, null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            else
            {
                ContainedParameterDisplay.ItemsSource = new ObservableCollection<ParameterDisplay>();
                LinkedParameterValue.Text = "";
                LinkedParameterName.Text = "";
            }
        }

        /// <summary>
        ///  Call this function to make sure that parameters that have been requested to be removed are.
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
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
                            "There was an error assigning the value '" + text + "' to the linked parameter!\r\n" +
                            error,
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
            LinkedParameterFilterBox.Display = Display;
            LinkedParameterFilterBox.Filter = (o, text) =>
            {
                var model = o as LinkedParameterDisplayModel;
                return model.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };
            Display.SelectionChanged += Display_SelectionChanged;
        }

        /// <summary>
        /// 
        /// </summary>
        private void AssignCurrentlySelected()
        {
            if (Display.SelectedItem is LinkedParameterDisplayModel selected)
            {
                Select(selected, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selected"></param>
        /// <param name="cleanup"></param>
        private void Select(LinkedParameterDisplayModel selected, bool cleanup)
        {
            _currentlySelected = selected;
            if (cleanup)
            {
                CleanupSelectedParameters();
            }

            SelectedLinkParameter = selected.LinkedParameter;
            ChangesMade = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            Rename();
        }

        /// <summary>
        /// 
        /// </summary>
        private async void Rename()
        {
            if (Display.SelectedItem is LinkedParameterDisplayModel selected)
            {
                var selectedModuleControl = GetCurrentlySelectedControl();
                var dialog = new StringRequestDialog(RootDialogHost, "Set LinkedParameter Name", (value) => !String.IsNullOrWhiteSpace(value), selected.Name);
                await dialog.ShowAsync();
                if(dialog.DidComplete)
                {
                    selected.Name = dialog.UserInput;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private UIElement GetCurrentlySelectedControl()
        {
            return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private async void NewLinkedParameter_Clicked(object obj)
        {
            var dialog = new StringRequestDialog(RootDialogHost, "New LinkedParameter Name", (value) => !String.IsNullOrWhiteSpace(value), String.Empty);
            await dialog.ShowAsync();

            if (dialog.DidComplete)
            {
                string error = null;
                if (!_linkedParametersModel.NewLinkedParameter(dialog.UserInput, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to create new Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                SetupLinkedParameters(_linkedParametersModel);
                ChangesMade = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveCurrentlySelectedParameter(object sender, RoutedEventArgs e)
        {
            if (Display.SelectedItem is LinkedParameterDisplayModel selectedLinkedParameter)
            {
                string error = null;
                if (!_linkedParametersModel.RemoveLinkedParameter(selectedLinkedParameter.LinkedParameter, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to remove Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                var items = Display.ItemsSource as ObservableCollection<LinkedParameterDisplayModel>;
                items?.Remove(selectedLinkedParameter);
                ChangesMade = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            RemoveCurrentlySelectedParameter(sender, e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private LinkedParameterDisplayModel GetFirstItem()
        {
            return Display.ItemContainerGenerator.Items.Count > 0 ? Display.ItemContainerGenerator.Items[0] as LinkedParameterDisplayModel : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                    ContainedParameterDisplay.ItemsSource =
                        new ObservableCollection<ParameterDisplay>(containedParameters);
                    break;
                }
            }
        }

        private void Unlink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            UnlinkParameter((sender as Label).Tag.ToString());
        }

        private void ContainedParameterDisplay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                UnlinkParameter(((sender as ListView).SelectedItem as ParameterDisplay).ParameterName);
            }
            else if (e.Key == Key.Enter)
            {
                GoToCurrentContainedParameter();
            }
        }

        private MessageBoxResult ConfirmUnlinkParameterMessageBox(ParameterDisplay parameterDisplay)
        {
            return MessageBox.Show(MainWindow.Us, "Are you sure you wish to unlink the selected parameter?",
                "Confirm Unlink [" + parameterDisplay.ParameterName + "]",
                MessageBoxButton.OKCancel);
        }

        /// <summary>
        ///     Window key listener
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                if (DialogOpenedEventArgs != null)
                {
                    DialogOpenedEventArgs.Session.Close();
                    OnCloseDisplay.BeginInvoke(null, null);
                }
            }
        }

        public void Show()
        {
            //((FrameworkElement)Parent).Visibility = Visibility.Visible;
            //Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke((Action)delegate { Keyboard.Focus(LinkedParameterFilterBox); },
                DispatcherPriority.Render);
        }

        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            e.Handled = true;
            AssignCurrentlySelected();
            ChangesMade = true;
            CleanupSelectedParameters();
            DialogOpenedEventArgs.Session.Close();
            OnCloseDisplay.BeginInvoke(null, null);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Display.Focus();
        }

        private void ListViewControl_KeyDown(object sender, KeyEventArgs e)
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

            OnKeyDown(e);
        }
        private void ContainedParameterDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                Rename();
            }
        }

        /// <summary>
        /// </summary>
        private void GoToCurrentContainedParameter()
        {
            var moduleToGoTo = (ContainedParameterDisplay.SelectedItem as ParameterDisplay)?.Parameter.BelongsTo;
            if (moduleToGoTo != null)
            {
                /* ((FrameworkElement)Parent).Visibility = Visibility.Collapsed;
                 Visibility = Visibility.Collapsed; */

                if (DialogOpenedEventArgs != null)
                {
                    DialogOpenedEventArgs.Session.Close();
                    OnCloseDisplay.BeginInvoke(null, null);
                }

                GoToModule?.Invoke(moduleToGoTo);
            }
        }

        private void ContainedParameterDisplay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            GoToCurrentContainedParameter();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Display_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Control_OnMouseDoubleClick(sender, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewLinkedParameterButton_OnClick(object sender, RoutedEventArgs e)
        {
            NewLinkedParameter_Clicked(sender);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DialogOpenedEventArgs != null)
            {
                DialogOpenedEventArgs.Session.Close();
                OnCloseDisplay.BeginInvoke(null, null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            InitNewDisplay();
        }
    }

    public class ParameterDatatemplateSelector : DataTemplateSelector
    {
    }
}