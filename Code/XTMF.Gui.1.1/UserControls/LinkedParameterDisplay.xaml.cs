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
        }

        class ParameterDisplay
        {
            public string ParameterName { get; set; }
            public string ModuleName { get; set; }
        }

        private void Display_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;
            if(selectedLinkedParameter != null)
            {
                var containedParameters = from parameter in selectedLinkedParameter.LinkedParameter.GetParameters()
                                          select new ParameterDisplay(){ ParameterName = parameter.Name, ModuleName = parameter.BelongsTo.Name };
                ContainedParameterDisplay.ItemsSource = new ObservableCollection<ParameterDisplay>(containedParameters);

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
            if(AssignMode && !Renaming)
            {
                switch(e.Key)
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
            if(selected != null)
            {
                SelectedLinkParameter = selected.LinkedParameter;
                DialogResult = true;
                Close();
            }
        }
        bool Renaming = false;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.Handled == false)
            {
                switch(e.Key)
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

        private void Rename()
        {
            var selected = Display.SelectedItem as LinkedParameterDisplayModel;
            if(selected != null)
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
            if(request.ShowDialog() == true)
            {
                string error = null;
                if(!LinkedParameters.NewLinkedParameter(request.Answer, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to create new Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SetupLinkedParameters(LinkedParameters);
            }
        }

        private void RemoveLinkedParameter_Clicked(object obj)
        {
            var selectedLinkedParameter = Display.SelectedItem as LinkedParameterDisplayModel;
            if(selectedLinkedParameter != null)
            {
                string error = null;
                var index = LinkedParameters.GetLinkedParameters().IndexOf(selectedLinkedParameter.LinkedParameter);
                if(!LinkedParameters.RemoveLinkedParameter(selectedLinkedParameter.LinkedParameter, ref error))
                {
                    MessageBox.Show(MainWindow.Us, error, "Failed to remove Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var items = Display.ItemsSource as ObservableCollection<LinkedParameterDisplayModel>;
                items.RemoveAt(index);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
