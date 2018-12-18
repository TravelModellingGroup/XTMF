/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Xml;
using MaterialDesignThemes.Wpf;

using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTypeSelect.xaml
    /// </summary>
    public partial class ModuleTypeSelect : Window
    {
        private readonly ModelSystemStructureModel _selectedModule;

        private readonly ModelSystemEditingSession _modelSystemSession;

        private class Model : INotifyPropertyChanged
        {
            internal Type type;

            public Model(Type type)
            {
                this.type = type;
                foreach (var attr in type.GetCustomAttributes(true))
                {
                    if (attr.GetType() == typeof(ModuleInformationAttribute))
                    {
                        var info = (attr as ModuleInformationAttribute);
                        Description = (attr as ModuleInformationAttribute)?.Description;
                        Url = (attr as ModuleInformationAttribute)?.DocURL;
                        if (info.IconURI != null)
                        {
                            try
                            {
                                IconKind = (PackIconKind)System.Enum.Parse(typeof(PackIconKind), info.IconURI);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }

            public string Name => type.Name;

            public string Text => type.FullName;

            public string Description { get; }

            public string Url { get; }

            public PackIconKind IconKind { get; set; } = PackIconKind.Settings;

            #pragma warning disable CS0067
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ModuleTypeSelect()
        {
            InitializeComponent();
        }

        private bool CheckAgainstFilter(object o, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (o is Model model)
            {
                return model.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || model.Text.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
            return false;
        }

        public ModuleTypeSelect(ModelSystemEditingSession session, ModelSystemStructureModel selectedModule)
            : this()
        {
            _modelSystemSession = session;
            _selectedModule = selectedModule;
            BuildRequirements(session);
            FilterBox.Filter = CheckAgainstFilter;
            FilterBox.Display = Display;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            FilterBox.Focus();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            if (e.OriginalSource == this)
            {
                FilterBox.Focus();
            }
        }

        public Type SelectedType { get; private set; }

        /// <summary>
        /// Figure out what types we are going to be restricted by
        /// </summary>
        private void BuildRequirements(ModelSystemEditingSession session)
        {
            Display.ItemsSource = Convert(session.GetValidModules(_selectedModule));
        }

        private List<Model> Convert(List<Type> before)
        {
            var ret = before.Select(o => new Model(o)).ToList();
            ret.Sort((a, b) => a.Name.CompareTo(b.Name));
            return ret;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        e.Handled = true;
                        Close();
                        break;
                    case Key.E:
                        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            Keyboard.Focus(FilterBox);
                            e.Handled = true;
                        }
                        break;
                    case Key.Enter:
                        e.Handled = true;
                        Select();
                        break;
                }
            }
        }


        private void Select()
        {
            var index = Display.SelectedItem;
            if (index == null) return;
            SelectModel(index as Model);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        private void SelectModel(Model model)
        {
            if (model != null)
            {
                SelectedType = model.type;
                if (ContainsFreeVariables(SelectedType))
                {
                    // then we need to fill in the free parameters
                    List<Type> selectedForFreeVariables = new List<Type>();
                    foreach (var variable in GetFreeVariables(SelectedType))
                    {
                        var dialog = new FreeVariableEntry(variable, _modelSystemSession) { Owner = this };
                        if (dialog.ShowDialog() != true)
                        {
                            return;
                        }
                        selectedForFreeVariables.Add(dialog.SelectedType);
                    }
                    SelectedType = CreateConcreteType(SelectedType, selectedForFreeVariables);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    DialogResult = true;
                    Close();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectedType"></param>
        /// <param name="selectedForFreeVariables"></param>
        /// <returns></returns>
        private Type CreateConcreteType(Type selectedType, List<Type> selectedForFreeVariables)
        {
            var originalTypes = selectedType.GetGenericArguments();
            var newTypes = new Type[originalTypes.Length];
            int j = 0;
            for (int i = 0; i < originalTypes.Length; i++)
            {
                newTypes[i] = originalTypes[i].IsGenericParameter ? selectedForFreeVariables[j++] : originalTypes[i];
            }
            return selectedType.MakeGenericType(newTypes);
        }

        private IEnumerable<Type> GetFreeVariables(Type selectedType)
        {
            return selectedType.GetGenericArguments().Where(t => t.IsGenericParameter);
        }

        private bool ContainsFreeVariables(Type selectedType)
        {
            return selectedType.IsGenericType && selectedType.GetGenericArguments().Any(t => t.IsGenericParameter);
        }

        private Model GetFirstItem()
        {
            if (Display.ItemContainerGenerator.Items.Any())
            {
                return Display.ItemContainerGenerator.Items[0] as Model;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Display_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(Display.SelectedItem is Model selected))
            {
                selected = GetFirstItem();
            }
            SelectModel(selected);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Display_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Display.SelectedItem is Model selected)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {

                    ModuleNameTextBlock.Text = selected.Name ?? "No Module Selected";
                    var background = (SolidColorBrush)FindResource("MaterialDesignBackground");
                    var body = (SolidColorBrush)FindResource("MaterialDesignBody");
                    if (selected.Description != null)
                    {

                        var htmlString =
                            $"<style>body{{ background-color:\"#{background.Color.ToString().Substring(3)}\"; color:\"#{body.Color.ToString().Substring(3)}\"; " +
                            $"font-family: \"Segoe UI\"; }}</style><body>{selected.Description}</body>";
                        ModuleDescriptionTextBlock.NavigateToString(htmlString);
                    }
                    else
                    {
                        var htmlString =
                            $"<style>body{{ background-color:\"#{background.Color.ToString().Substring(3)}\"; color:\"#{body.Color.ToString().Substring(3)}\"; }}</style><body></body>";
                        ModuleDescriptionTextBlock.NavigateToString(htmlString);

                    }
                    ModuleUrlTextBlock.Text = selected.Url ?? "";
                    ModuleTypeTextBlock.Text = selected.Text;


                }));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleUrlTextBlock_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Display.SelectedItem is Model selected && selected.Url != null)
            {
                System.Diagnostics.Process.Start(selected.Url.StartsWith("http") ? selected.Url : $"http://{selected.Url}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDescriptionTextBlock_OnNavigated(object sender, NavigationEventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleDescriptionTextBlock_OnLoaded(object sender, RoutedEventArgs e)
        {
            var background = (SolidColorBrush)FindResource("MaterialDesignBackground");
            var body = (SolidColorBrush)FindResource("MaterialDesignBody");
            var htmlString =
                $"<style>body{{ background-color:\"#{background.Color.ToString().Substring(3)}\"; color:\"#{body.Color.ToString().Substring(3)}\"; }}</style><body></body>";
            ModuleDescriptionTextBlock.NavigateToString(htmlString);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilterBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {

                case Key.Enter:

                    e.Handled = true; if (!(Display.SelectedItem is Model selected))
                    {
                        selected = GetFirstItem();
                    }
                    SelectModel(selected);
                    // Select();
                    break;
            }
        }
    }
}
