﻿/*
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
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for LinkedParameterDisplay.xaml
    /// </summary>
    public partial class LinkedParameterDisplay : Window
    {
        ObservableCollection<LinkedParameterDisplayModel> Items;
        LinkedParametersModel LinkedParameters;

        private bool AssignMode;
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
            AssignMode = assignLinkedParameter;
            Display.SelectionChanged += Display_SelectionChanged;
            Loaded += LinkedParameterDisplay_Loaded;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            CleanupSelectedParameters();
            base.OnClosing(e);
        }

        private void LinkedParameterDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            LinkedParameterFilterBox.Focus();
            Keyboard.Focus(LinkedParameterFilterBox);
        }

        class ParameterDisplay
        {
            public string ParameterName { get; set; }
            public string ModuleName { get; set; }
            public bool KeepAttached { get; set; }

            public ParameterModel Parameter { get; set; }
        }

        private LinkedParameterDisplayModel CurrentlySelected = null;
        private List<ParameterDisplay> CurrentParameters = null;

        /// <summary>
        /// This will be set to true if there were any changes made to linked parameters when invoked
        /// </summary>
        public bool ChangesMade { get; private set; }

        private void Display_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CleanupSelectedParameters();
            var selectedLinkedParameter = CurrentlySelected = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selectedLinkedParameter != null)
            {
                var containedParameters = CurrentParameters = (from parameter in selectedLinkedParameter.LinkedParameter.GetParameters()
                                                               select new ParameterDisplay()
                                                               { ParameterName = parameter.Name,
                                                                   ModuleName = parameter.BelongsTo.Name,
                                                                   Parameter = parameter,
                                                                   KeepAttached = true
                                                               }).ToList();
                ContainedParameterDisplay.ItemsSource = new ObservableCollection<ParameterDisplay>(containedParameters);

            }
        }

        /// <summary>
        /// Call this function to make sure that parameters that have been requested to be removed are.
        /// </summary>
        private void CleanupSelectedParameters()
        {
            if (CurrentlySelected != null && CurrentParameters != null)
            {
                foreach (var parameter in CurrentParameters)
                {
                    if (!parameter.KeepAttached)
                    {
                        string error = null;
                        if (!CurrentlySelected.LinkedParameter.RemoveParameter(parameter.Parameter, ref error))
                        {
                            MessageBox.Show("There was an error trying to remove a parameter from a linked parameter!\r\n" + error, "Error removing parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        ChangesMade = true;
                    }
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
            if (AssignMode && !Renaming)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        Assign();
                        break;
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private void Assign()
        {
            var selected = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selected != null)
            {
                CleanupSelectedParameters();
                SelectedLinkParameter = selected.LinkedParameter;
                DialogResult = true;
                ChangesMade = true;
                Close();
            }
        }
        bool Renaming = false;

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
                Renaming = true;
                var adorn = new TextboxAdorner("Rename", (result) =>
                {
                    selected.Name = result;
                }, selectedModuleControl, selected.Name);
                adorn.Unloaded += Adorn_Unloaded;
                layer.Add(adorn);
                adorn.Focus();
            }
        }

        private void Adorn_Unloaded(object sender, RoutedEventArgs e)
        {
            Renaming = false;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return Display.ItemContainerGenerator.ContainerFromItem(Display.SelectedItem) as UIElement;
        }

        private void NewLinkedParameter_Clicked(object obj)
        {
            var request = new StringRequest("Name the new Linked Parameter", (s) => true);
            if (request.ShowDialog() == true)
            {
                string error = null;
                if (!LinkedParameters.NewLinkedParameter(request.Answer, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to create new Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SetupLinkedParameters(LinkedParameters);
                ChangesMade = true;
            }
        }

        private void RemoveCurrentlySelectedParameter()
        {
            var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;
            if (selectedLinkedParameter != null)
            {
                string error = null;
                var index = LinkedParameters.GetLinkedParameters().IndexOf(selectedLinkedParameter.LinkedParameter);
                if (!LinkedParameters.RemoveLinkedParameter(selectedLinkedParameter.LinkedParameter, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to remove Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var items = Display.ItemsSource as ObservableCollection<LinkedParameterDisplayModel>;
                items.RemoveAt(index);
                ChangesMade = true;
            }
        }

        private void RemoveLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            RemoveCurrentlySelectedParameter();
        }

        private void BorderIconButton_DoubleClicked(object obj)
        {
            if (AssignMode)
            {
                Assign();
            }
        }
    }
}
