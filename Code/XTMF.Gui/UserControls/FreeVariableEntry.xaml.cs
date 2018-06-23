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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for FreeVariableEntry.xaml
    /// </summary>
    public partial class FreeVariableEntry : Window
    {
        private readonly Type[] Conditions;

        private readonly ModelSystemEditingSession Session;

        public FreeVariableEntry(Type freeVariable, ModelSystemEditingSession session)
        {
            InitializeComponent();
            Session = session;
            Conditions = freeVariable.GetGenericParameterConstraints();
            Loaded += FreeVariableEntry_Loaded;
        }

        class Model : INotifyPropertyChanged
        {
            internal Type Type;

            public Model(Type type) => Type = type;

            public string Name => Type.Name;

            public string Text => Type.FullName;

            public event PropertyChangedEventHandler PropertyChanged;

            internal static Task<ObservableCollection<Model>> CreateModel(ICollection<Type> types) =>
                Task.Run(() => new ObservableCollection<Model>(types.AsParallel().Select(t => new Model(t)).OrderBy(t => t.Name).ToList()));
        }

        private async void FreeVariableEntry_Loaded(object sender, RoutedEventArgs e)
        {
            var temp = await Model.CreateModel(Session.GetValidGenericVariableTypes(Conditions));
            Display.ItemsSource = (_availableModules = temp);
            FilterBox.Filter = CheckAgainstFilter;
            FilterBox.Display = Display;
        }

        public Type SelectedType { get; private set; }

        private ObservableCollection<Model> _availableModules;

        private bool CheckAgainstFilter(object o, string text)
        {
            var model = o as Model;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (model == null) return false;
            return model.Name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 || model.Text.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0;
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

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Handled == false)
            {
                switch(e.Key)
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

        private Model GetFirstItem()
        {
            return Display.ItemContainerGenerator.Items.Count > 0 ?
                Display.ItemContainerGenerator.Items[0] as Model : null;
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


        private void SelectModel(Model model)
        {
            if (model != null)
            {
                SelectedType = model.Type;
                if(SelectedType != null)
                {
                    if(ContainsFreeVariables(SelectedType))
                    {
                        // then we need to fill in the free parameters
                        List<Type> selectedForFreeVariables = new List<Type>();
                        foreach (var variable in GetFreeVariables(SelectedType))
                        {
                            var dialog = new FreeVariableEntry(variable, Session) { Owner = this };
                            if (dialog.ShowDialog() != true)
                            {
                                return;
                            }
                            selectedForFreeVariables.Add(dialog.SelectedType);
                        }
                        SelectedType = CreateConcreteType(SelectedType, selectedForFreeVariables);
                    }
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

        private IEnumerable<Type> GetFreeVariables(Type selectedType) => selectedType.GetGenericArguments().Where(t => t.IsGenericParameter);

        private bool ContainsFreeVariables(Type selectedType) => selectedType.IsGenericType && selectedType.GetGenericArguments().Any(t => t.IsGenericParameter);

        private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => Select();

        private void Display_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => Select();

    }
}
