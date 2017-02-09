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
            internal Type type;

            public Model(Type type)
            {
                this.type = type;
            }

            public string Name { get { return type.Name; } }

            public string Text { get { return type.FullName; } }

            public event PropertyChangedEventHandler PropertyChanged;

            internal static Task<ObservableCollection<Model>> CreateModel(ICollection<Type> types)
            {

                return Task.Run(() => new ObservableCollection<Model>(types.AsParallel().Select(t => new Model(t)).OrderBy(t => t.Name).ToList()));
            }
        }

        private async void FreeVariableEntry_Loaded(object sender, RoutedEventArgs e)
        {
            var temp = await Model.CreateModel(Session.GetValidGenericVariableTypes(Conditions));
            Display.ItemsSource = (AvailableModules = temp);
            FilterBox.Filter = CheckAgainstFilter;
            FilterBox.Display = Display;
        }

        public Type SelectedType { get; private set; }

        private ObservableCollection<Model> AvailableModules;

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

        private void BorderIconButton_Clicked(object obj)
        {
            Select();
        }

        private void Select()
        {
            var index = Display.SelectedItem;
            if (index == null) return;
            SelectModel(index as Model);
        }

        private Model GetFirstItem()
        {
            if (Display.ItemContainerGenerator.Items.Count > 0)
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


        private void SelectModel(Model model)
        {
            if (model != null)
            {
                SelectedType = model.type;
                if (SelectedType == null)
                {
                }
                else
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

        private IEnumerable<Type> GetFreeVariables(Type selectedType)
        {
            return selectedType.GetGenericArguments().Where(t => t.IsGenericParameter);
        }

        private bool ContainsFreeVariables(Type selectedType)
        {
            return selectedType.IsGenericType && selectedType.GetGenericArguments().Any(t => t.IsGenericParameter);
        }

        int TimesLoaded;

        private void BorderIconButton_Loaded(object sender, RoutedEventArgs e)
        {
            TimesLoaded++;
        }
    }
}
