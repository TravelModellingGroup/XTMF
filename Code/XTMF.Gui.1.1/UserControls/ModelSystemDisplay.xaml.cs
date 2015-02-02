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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{


    /// <summary>
    /// Interaction logic for ModelSystemDisplay.xaml
    /// </summary>
    public partial class ModelSystemDisplay : UserControl
    {
        public static readonly DependencyProperty ModelSystemProperty = DependencyProperty.Register("ModelSystem", typeof(ModelSystemModel), typeof(ModelSystemDisplay),
    new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnModelSystemChanged));

        public static readonly DependencyProperty ModelSystemNameProperty = DependencyProperty.Register("ModelSystemName", typeof(string), typeof(ModelSystemDisplay),
    new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public ModelSystemEditingSession Session { get; set; }

        /// <summary>
        /// The model system to display
        /// </summary>
        public ModelSystemModel ModelSystem
        {
            get
            {
                return (ModelSystemModel)GetValue(ModelSystemProperty);
            }
            set
            {
                SetValue(ModelSystemProperty, value);
            }
        }

        public string ModelSystemName
        {
            get
            {
                return (string)GetValue(ModelSystemNameProperty);
            }
            private set
            {
                SetValue(ModelSystemNameProperty, value);
            }
        }

        private bool CheckFilterRec(ModelSystemStructureModel module, string filterText, TreeViewItem previous = null)
        {
            var children = module.Children;
            var show = false;
            var contianer = (previous == null ? ModuleDisplay.ItemContainerGenerator.ContainerFromItem(module) : previous.ItemContainerGenerator.ContainerFromItem(module)) as TreeViewItem;
            if(children != null)
            {
                foreach(var child in children)
                {
                    if(CheckFilterRec(child, filterText, contianer))
                    {
                        show = true;
                    }
                }
            }
            if(!show)
            {
                show = module.Name.IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }

            if(contianer != null)
            {
                contianer.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            return show;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return GetCurrentlySelectedControl(ModelSystem.Root, ModuleDisplay.SelectedItem as ModelSystemStructureModel);
        }

        private UIElement GetCurrentlySelectedControl(ModelSystemStructureModel current, ModelSystemStructureModel lookingFor, TreeViewItem previous = null)
        {
            var children = current.Children;
            var contianer = (previous == null ? ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current) : previous.ItemContainerGenerator.ContainerFromItem(current)) as TreeViewItem;
            if(current == lookingFor && contianer != null)
            {
                return contianer;
            }
            if(children != null)
            {
                foreach(var child in children)
                {
                    var childResult = GetCurrentlySelectedControl(child, lookingFor, contianer);
                    if(childResult != null)
                    {
                        return childResult;
                    }
                }
            }
            return null;
        }

        public ModelSystemDisplay()
        {
            DataContext = this;
            InitializeComponent();
            FilterBox.Filter = (o, text) =>
            {
                var module = o as ModelSystemStructureModel;
                bool ret = false;
                ret = CheckFilterRec(module, text);
                return ret;
            };
        }

        private void ModelSystemDisplay_ParametersChanged(object arg1, ParametersModel parameters)
        {
            UpdateParameters(parameters);
        }

        private bool FilterParameters(object arg1, string arg2)
        {
            var parameter = arg1 as ParameterModel;
            if(parameter != null)
            {
                return (String.IsNullOrWhiteSpace(arg2) || parameter.Name.IndexOf(arg2, StringComparison.InvariantCultureIgnoreCase) >= 0);
            }
            return false;
        }

        private void OnTreeExpanded(object sender, RoutedEventArgs e)
        {
            var tvi = (TreeViewItem)sender;
            e.Handled = true;
            FilterBox.RefreshFilter();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterBox.Focus();
        }

        private Window GetWindow()
        {
            var current = this as DependencyObject;
            while(current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        private void SelectReplacement()
        {
            var selectedModule = ModuleDisplay.SelectedItem as ModelSystemStructureModel;
            if(Session == null)
            {
                throw new InvalidOperationException("Session has not been set before operating.");
            }
            if(selectedModule != null)
            {
                ModuleTypeSelect findReplacement = new ModuleTypeSelect(Session, selectedModule);
                findReplacement.Owner = GetWindow();
                if(findReplacement.ShowDialog() == true)
                {
                    if((var selectedType = findReplacement.SelectedType) != null)
                    {
                        selectedModule.Type = selectedType;
                        UpdateParameters(selectedModule.Parameters);
                    }
                }
            }
        }

        private static void OnModelSystemChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ModelSystemDisplay;
            var newModelSystem = e.NewValue as ModelSystemModel;
            if(newModelSystem != null)
            {
                us.ModuleDisplay.ItemsSource = new ObservableCollection<ModelSystemStructureModel>() { newModelSystem.Root };
                us.ModelSystemName = newModelSystem.Name;
                us.ModuleDisplay.Items.MoveCurrentToFirst();
                us.FilterBox.Display = us.ModuleDisplay;
            }
            else
            {
                us.ModuleDisplay.DataContext = null;
                us.ModelSystemName = "No model loaded";
                us.FilterBox.Display = null;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.Handled == false)
            {
                if(Controllers.EditorController.IsControlDown())
                {
                    switch(e.Key)
                    {
                        case Key.M:
                            SelectReplacement();
                            e.Handled = true;
                            break;
                        case Key.P:
                            ParameterFilterBox.Focus();
                            e.Handled = true;
                            break;
                        case Key.E:
                            FilterBox.Focus();
                            e.Handled = true;
                            break;
                        case Key.W:
                            Close();
                            break;
                    }
                }
                else
                {
                    switch(e.Key)
                    {
                        case Key.F1:
                            ShowDocumentation();
                            break;
                        case Key.Delete:
                            RemoveCurrentModule();
                            break;
                        case Key.F2:
                            Rename();
                            break;
                    }
                }
            }
        }

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

        public event Action<object> RequestClose;

        private void SetFocus(StackPanel border)
        {
            if(border.Background.IsFrozen)
            {
                border.Background = border.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation(border.IsKeyboardFocusWithin ? Color.FromRgb(120, 150, 120) : Color.FromRgb(120, 175, 120),
                new Duration(new TimeSpan(0, 0, 0, 0, 250)));
            border.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
        }

        new private void LostFocus(StackPanel border)
        {
            if(border.Background.IsFrozen)
            {
                border.Background = border.Background.CloneCurrentValue();
            }
            if(!border.IsKeyboardFocusWithin)
            {
                var background = (Color)Application.Current.FindResource("ControlBackgroundColour");
                ColorAnimation setFocus = new ColorAnimation(background, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
                border.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
            }
        }

        private void ParameterBorder_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = (sender as StackPanel);
            if(border == null) return;
            SetFocus(border);
        }

        private void ParameterBorder_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = (sender as StackPanel);
            if(border == null) return;
            LostFocus(border);
        }

        private void ParameterBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = (sender as StackPanel);
            if(border == null) return;
            SetFocus(border);
        }

        private void ParameterBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = (sender as StackPanel);
            if(border == null) return;
            LostFocus(border);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
        }
        private void TextBox_SourceUpdated(object sender, DataTransferEventArgs e)
        {
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textbox = (sender as TextBox);
            if(textbox == null) return;
            if(textbox.Background.IsFrozen)
            {
                textbox.Background = textbox.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 250)));
            textbox.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as TextBox;
            BindingExpression be = box.GetBindingExpression(TextBox.TextProperty);
            be.UpdateSource();
            var textbox = (sender as TextBox);
            if(textbox == null) return;
            if(textbox.Background.IsFrozen)
            {
                textbox.Background = textbox.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 250)));
            textbox.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
        }

        private void HintedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        public void SaveRequested()
        {

        }

        public void CloneRequested(string clonedName)
        {

        }

        private void ModuleDisplay_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var module = (e.NewValue as ModelSystemStructureModel);
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if(module != null)
                {
                    ModelSystemDisplay_ParametersChanged(sender, module.Parameters);
                }
            }));
        }

        private void Help_Clicked(object sender, RoutedEventArgs e)
        {
            ShowDocumentation();
        }

        private void ShowDocumentation()
        {
            XTMF.Gui.Controllers.ModelSystemEditingController.HelpRequested(ModuleDisplay.SelectedItem as ModelSystemStructureModel);
        }

        private void Module_Clicked(object sender, RoutedEventArgs e)
        {
            SelectReplacement();
        }

        private void Rename_Clicked(object sender, RoutedEventArgs e)
        {
            Rename();
        }

        private void Rename()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureModel;
            var selectedModuleControl = GetCurrentlySelectedControl();
            if(selectedModuleControl != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                var adorn = new TextboxAdorner("Rename", (result) =>
                {
                    string error = null;
                    if(!selected.SetName(result, ref error))
                    {
                        throw new Exception(error);
                    }
                }, selectedModuleControl, selected.Name);
                layer.Add(adorn);
                adorn.Focus();
            }
            else
            {
                throw new InvalidAsynchronousStateException("The current module could not be found!");
            }
        }

        private void Remove_Clicked(object sender, RoutedEventArgs e)
        {
            RemoveCurrentModule();
        }

        private void RemoveCurrentModule()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureModel;
            if(selected != null)
            {
                string error = null;
                if(!ModelSystem.Remove(selected, ref error))
                {
                    throw new Exception(error);
                }
                UpdateParameters(selected.Parameters);
            }
        }

        private void UpdateParameters(ParametersModel parameters)
        {
            if(parameters != null)
            {
                ParameterDisplay.ItemsSource = new ObservableCollection<ParameterModel>(parameters.GetParameters().OrderBy(el => el.Name));
                ParameterFilterBox.Display = ParameterDisplay;
                ParameterFilterBox.Filter = FilterParameters;
                ParameterFilterBox.RefreshFilter();
            }
            else
            {
                ParameterDisplay.ItemsSource = null;
            }
        }
    }
}
