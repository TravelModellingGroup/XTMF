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
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTypeSelect.xaml
    /// </summary>
    public partial class ModuleTypeSelect : Window
    {
        private ModelSystemStructureModel _selectedModule;

        private ModelSystemEditingSession _modelSystemSession;

        private class Model : INotifyPropertyChanged
        {
            internal Type type;

            public Model(Type type) => this.type = type;

            public string Name => type.Name;

            public string Text => type.FullName;

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ModuleTypeSelect() => InitializeComponent();

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

        private List<Model> _availableModules;

        public Type SelectedType { get; private set; }

        /// <summary>
        /// Figure out what types we are going to be restricted by
        /// </summary>
        private void BuildRequirements(ModelSystemEditingSession session)
        {
            Display.ItemsSource = _availableModules = Convert(session.GetValidModules(_selectedModule));
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

        private void BorderIconButton_Clicked(object obj) => Select();

        private void Select()
        {
            var index = Display.SelectedItem;
            if (index == null) return;
            SelectModel(index as Model);
        }

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

        private void FilterBox_EnterPressed(object sender, EventArgs e)
        {
            var selected = Display.SelectedItem as Model;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            SelectModel(selected);
        }

        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = Display.SelectedItem as Model;
            if (selected == null)
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
        private void Display_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = Display.SelectedItem as Model;
            if (selected == null)
            {
                selected = GetFirstItem();
            }
            SelectModel(selected);
        }
    }
}
