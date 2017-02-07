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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using XTMF.Gui.Models;

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

        private ModelSystemEditingSession _Session;
        public ModelSystemEditingSession Session
        {
            get
            {
                return _Session;
            }
            set
            {
                if (_Session != null)
                {
                    _Session.ProjectWasExternallySaved -= ProjectWasExternalSaved;
                }
                _Session = value;
                if (value != null)
                {
                    value.ProjectWasExternallySaved += ProjectWasExternalSaved;
                }
            }
        }

        private void ProjectWasExternalSaved(object sender, EventArgs e)
        {

            // If the project was saved we need to reload in the new model system model
            Dispatcher.Invoke(() =>
           {
               ModelSystem = _Session.ModelSystemModel;
           });
        }

        private ModelSystemStructureDisplayModel DisplayRoot;

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

        private bool CheckFilterRec(ModelSystemStructureDisplayModel module, string filterText, bool parentExpanded = true, bool parentVisible = false, bool parentPassed = false)
        {
            var children = module.Children;
            var thisParentPassed = module.Name.IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0;
            var childrenPassed = false;
            if (children != null)
            {
                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        if (CheckFilterRec(child, filterText, module.IsExpanded, thisParentPassed | parentVisible, thisParentPassed | parentPassed))
                        {
                            childrenPassed = true;
                        }
                    }
                }
            }
            var show = thisParentPassed | childrenPassed | parentVisible;
            if (!String.IsNullOrWhiteSpace(filterText))
            {
                module.IsExpanded = childrenPassed;
            }
            if (thisParentPassed | childrenPassed | parentPassed)
            {
                module.ModuleVisibility = Visibility.Visible;
            }
            else
            {
                module.ModuleVisibility = Visibility.Collapsed;
            }
            return thisParentPassed | childrenPassed;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return GetCurrentlySelectedControl(DisplayRoot, ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel);
        }

        private UIElement GetCurrentlySelectedControl(ModelSystemStructureDisplayModel current, ModelSystemStructureDisplayModel lookingFor, TreeViewItem previous = null)
        {
            var children = current.Children;
            var container = (previous == null ? ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current) : previous.ItemContainerGenerator.ContainerFromItem(current)) as TreeViewItem;
            if (current == lookingFor && container != null)
            {
                return container;
            }
            if (children != null)
            {
                foreach (var child in children)
                {
                    var childResult = GetCurrentlySelectedControl(child, lookingFor, container);
                    if (childResult != null)
                    {
                        return childResult;
                    }
                }
            }
            return null;
        }

        static int FilterNumber = 0;

        public ModelSystemDisplay()
        {
            DataContext = this;
            InitializeComponent();
            AllowMultiSelection(ModuleDisplay);
            Loaded += ModelSystemDisplay_Loaded;
            ModuleDisplay.SelectedItemChanged += ModuleDisplay_SelectedItemChanged;
            FilterBox.Filter = (o, text) =>
           {
               var module = o as ModelSystemStructureDisplayModel;
               Task.Run(() =>
              {
                  var ourNumber = Interlocked.Increment(ref FilterNumber);
                  var waitTask = Task.Delay(400);
                  waitTask.Wait();
                  Thread.MemoryBarrier();
                  if (ourNumber == FilterNumber)
                  {
                      CheckFilterRec(module, text);
                  }
              });
               return true;
           };
        }

        private void ModelSystemDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FilterBox.Focus();
            }));
        }

        private void ModelSystemDisplay_ParametersChanged(object arg1, ParametersModel parameters)
        {
            UpdateParameters();
        }

        private bool FilterParameters(object arg1, string arg2)
        {
            var parameter = arg1 as ParameterDisplayModel;
            if (parameter != null)
            {
                return (string.IsNullOrWhiteSpace(arg2) || parameter.Name.IndexOf(arg2, StringComparison.InvariantCultureIgnoreCase) >= 0);
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
            while (current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        private void ToggleQuickParameter()
        {
            var displayParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (displayParameter != null)
            {
                displayParameter.QuickParameter = !displayParameter.QuickParameter;
            }
        }

        public BindingList<LinkedParameterDisplayModel> RecentLinkedParameters = new BindingList<LinkedParameterDisplayModel>();

        private void RecentLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            var selected = sender as DependencyObject;
            if (selected != null)
            {
                var currentMenu = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterRecentLinkedParameters : ParameterRecentLinkedParameters);
                var selectedLinkedParameter = currentMenu.ItemContainerGenerator.ItemFromContainer(selected) as LinkedParameterDisplayModel;
                if (selectedLinkedParameter != null)
                {
                    AddCurrentParameterToLinkedParameter(selectedLinkedParameter.LinkedParameter);
                    RecentLinkedParameters.RemoveAt(RecentLinkedParameters.IndexOf(selectedLinkedParameter));
                    RecentLinkedParameters.Insert(0, selectedLinkedParameter);
                }
            }

        }

        private void ShowLinkedParameterDialog(bool assign = false)
        {
            var linkedParameterDialog = new LinkedParameterDisplay(ModelSystem.LinkedParameters, assign);
            linkedParameterDialog.Owner = GetWindow();
            if (linkedParameterDialog.ShowDialog() == true && assign)
            {
                // assign the selected linked parameter
                var newLP = linkedParameterDialog.SelectedLinkParameter;

                if (AddCurrentParameterToLinkedParameter(newLP))
                {
                    LinkedParameterDisplayModel matched;
                    if ((matched = RecentLinkedParameters.FirstOrDefault(lpdm => lpdm.LinkedParameter == newLP)) != null)
                    {
                        RecentLinkedParameters.Remove(matched);
                    }
                    RecentLinkedParameters.Insert(0, new LinkedParameterDisplayModel(newLP));
                    if (RecentLinkedParameters.Count > 5)
                    {
                        RecentLinkedParameters.RemoveAt(5);
                    }
                    ParameterRecentLinkedParameters.IsEnabled = true;
                    QuickParameterRecentLinkedParameters.IsEnabled = true;
                }
            }
            if (linkedParameterDialog.ChangesMade)
            {
                RefreshParameters();
            }
        }

        private bool AddCurrentParameterToLinkedParameter(LinkedParameterModel newLP)
        {
            var displayParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (displayParameter != null)
            {

                string error = null;
                if (!displayParameter.AddToLinkedParameter(newLP, ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to set to Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                // if the selected parameter is also a quick parameter update that parameter in the quick parameters
                UpdateQuickParameterEquivalent(displayParameter);
                return true;
            }
            return false;
        }

        private void UpdateQuickParameterEquivalent(ParameterDisplayModel displayParameter)
        {
            if (displayParameter.QuickParameter)
            {
                QuickParameterDisplay.Items.Refresh();
            }
        }

        private void RemoveFromLinkedParameter()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string error = null;
                if (!currentParameter.RemoveLinkedParameter(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to remove from Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                UpdateQuickParameterEquivalent(currentParameter);
            }
        }

        private void SelectReplacement()
        {
            if (Session == null)
            {
                throw new InvalidOperationException("Session has not been set before operating.");
            }
            if (CurrentlySelected.Count > 0)
            {
                if (CurrentlySelected.Any(c => c.BaseModel.ParentFieldType != CurrentlySelected[0].BaseModel.ParentFieldType))
                {
                    MessageBox.Show(GetWindow(), "All selected modules must be for the same type.", "Failed add module to collection", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ModuleTypeSelect findReplacement = new ModuleTypeSelect(Session, CurrentlySelected[0].BaseModel);
                findReplacement.Owner = GetWindow();
                if (findReplacement.ShowDialog() == true)
                {
                    var selectedType = findReplacement.SelectedType;
                    if (selectedType != null)
                    {
                        Session.ExecuteCombinedCommands(
                            "Set Module Types",
                            () =>
                       {
                           foreach (var selectedModule in CurrentlySelected)
                           {
                               if (selectedModule.BaseModel.IsCollection)
                               {
                                   string error = null;
                                   if (!selectedModule.BaseModel.AddCollectionMember(selectedType, ref error))
                                   {
                                       MessageBox.Show(GetWindow(), error, "Failed add module to collection", MessageBoxButton.OK, MessageBoxImage.Error);
                                   }
                               }
                               else
                               {
                                   selectedModule.Type = selectedType;
                               }
                           }
                       });
                        RefreshParameters();
                    }
                }
            }

        }

        private void SetMetaModuleStateForSelected(bool set)
        {
            Session.ExecuteCombinedCommands(
                set ? "Compose to Meta-Modules" : "Decompose Meta-Modules",
                () =>
           {
               foreach (var selected in CurrentlySelected)
               {
                   string error = null;
                   if (!selected.SetMetaModule(set, ref error))
                   {
                       MessageBox.Show(GetWindow(), error, "Failed to convert meta module.", MessageBoxButton.OK, MessageBoxImage.Error);
                   }
               }
           });
            UpdateParameters();
        }

        private void RefreshParameters()
        {
            UpdateParameters();
        }

        private static void OnModelSystemChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ModelSystemDisplay;
            var newModelSystem = e.NewValue as ModelSystemModel;
            us.RecentLinkedParameters.Clear();
            newModelSystem.LinkedParameters.LinkedParameterRemoved += us.LinkedParameters_LinkedParameterRemoved;
            if (newModelSystem != null)
            {
                us.ModelSystemName = newModelSystem.Name;
                Task.Run(() =>
                {
                    var displayModel = us.CreateDisplayModel(newModelSystem.Root);
                    us.Dispatcher.Invoke(() =>
                    {
                        us.ParameterDisplay.ContextMenu.DataContext = us;
                        us.QuickParameterDisplay.ContextMenu.DataContext = us;
                        us.ModuleDisplay.ItemsSource = displayModel;
                        us.ModelSystemName = newModelSystem.Name;
                        us.ModuleDisplay.Items.MoveCurrentToFirst();
                        us.FilterBox.Display = us.ModuleDisplay;

                        us.ParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;
                        us.QuickParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;

                    });
                });
            }
            else
            {
                us.ModuleDisplay.DataContext = null;
                us.ModelSystemName = "No model loaded";
                us.FilterBox.Display = null;
                us.ParameterLinkedParameterMenuItem.ItemsSource = null;
            }
        }

        private void LinkedParameters_LinkedParameterRemoved(object sender, CollectionChangeEventArgs e)
        {
            if (e.Element != null)
            {
                var lpRemoved = e.Element as LinkedParameterModel;
                Dispatcher.Invoke(() =>
               {
                   foreach (var item in RecentLinkedParameters.Where(rlp => rlp.LinkedParameter == lpRemoved).ToList())
                   {
                       RecentLinkedParameters.Remove(item);
                   }
                   if (RecentLinkedParameters.Count <= 0)
                   {
                       ParameterRecentLinkedParameters.IsEnabled = false;
                       QuickParameterRecentLinkedParameters.IsEnabled = false;
                   }
               });
            }
        }

        private ObservableCollection<ModelSystemStructureDisplayModel> CreateDisplayModel(ModelSystemStructureModel root)
        {
            var ret = new ObservableCollection<ModelSystemStructureDisplayModel>()
            {
                (DisplayRoot = new ModelSystemStructureDisplayModel(root))
            };
            return ret;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        if (Controllers.EditorController.IsShiftDown() && Controllers.EditorController.IsControlDown())
                        {
                            MoveCurrentModule(1);
                            e.Handled = true;
                        }
                        break;
                    case Key.Up:
                        if (Controllers.EditorController.IsShiftDown() && Controllers.EditorController.IsControlDown())
                        {
                            MoveCurrentModule(-1);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
            {
                if (Controllers.EditorController.IsControlDown())
                {
                    switch (e.Key)
                    {
                        case Key.M:
                            if (Controllers.EditorController.IsAltDown())
                            {
                                SetMetaModuleStateForSelected(false);
                            }
                            else if (Controllers.EditorController.IsShiftDown())
                            {
                                SetMetaModuleStateForSelected(true);
                            }
                            else
                            {
                                SelectReplacement();
                            }
                            e.Handled = true;
                            break;
                        case Key.P:
                            ModuleParameterTab.Focus();
                            Keyboard.Focus(ParameterFilterBox);
                            e.Handled = true;
                            break;
                        case Key.E:
                            FilterBox.Focus();
                            e.Handled = true;
                            break;
                        case Key.W:
                            Close();
                            e.Handled = true;
                            break;
                        case Key.L:
                            ShowLinkedParameterDialog();
                            e.Handled = true;
                            break;
                        case Key.N:
                            CopyParameterName();
                            e.Handled = true;
                            break;
                        case Key.O:
                            OpenParameterFileLocation(false, false);
                            e.Handled = true;
                            break;
                        case Key.F:
                            SelectFileForCurrentParameter();
                            e.Handled = true;
                            break;
                        case Key.D:
                            SelectDirectoryForCurrentParameter();
                            e.Handled = true;
                            break;
                        case Key.Z:
                            Undo();
                            e.Handled = true;
                            break;
                        case Key.Y:
                            Redo();
                            e.Handled = true;
                            break;
                        case Key.S:
                            SaveRequested(false);
                            e.Handled = true;
                            break;
                        case Key.C:
                            CopyCurrentModule();
                            e.Handled = true;
                            break;
                        case Key.V:
                            PasteCurrentModule();
                            e.Handled = true;
                            break;
                        case Key.Q:
                            ShowQuickParameters();
                            e.Handled = true;
                            break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.F1:
                            ShowDocumentation();
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            RemoveSelectedModules();
                            e.Handled = true;
                            break;
                        case Key.F2:
                            if (Controllers.EditorController.IsShiftDown())
                            {
                                RenameDescription();
                            }
                            else
                            {
                                Rename();
                            }
                            e.Handled = true;
                            break;
                        case Key.F5:
                            SaveCurrentlySelectedParameters();
                            MainWindow.Us.ExecuteRun();
                            e.Handled = true;
                            break;
                        case Key.Escape:
                            FilterBox.Box.Text = String.Empty;
                            break;
                    }
                }
            }
        }

        private void ShowQuickParameters()
        {
            QuickParameterDisplay.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel.GetQuickParameters().OrderBy(n => n.Name));
            Dispatcher.BeginInvoke(new Action(() =>
           {
               ParameterTabControl.SelectedIndex = 1;
               QuickParameterFilterBox.Focus();
               Keyboard.Focus(QuickParameterFilterBox);
           }));
        }

        public void Redo()
        {
            string error = null;
            Session.Redo(ref error);
            UpdateParameters();
        }

        public void Undo()
        {
            string error = null;
            Session.Undo(ref error);
            UpdateParameters();
        }

        public void Close()
        {
            var e = RequestClose;
            if (e != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    e(this);
                }));
            }
        }

        /// <summary>
        /// Get permission from the user to close the window
        /// </summary>
        /// <returns>True if we have gained permission to close, false otherwise</returns>
        internal bool CloseRequested()
        {
            SaveCurrentlySelectedParameters();
            this.Dispatcher.Invoke(() =>
            {
                MainWindow.ShowPageContaining(this);
            });
            var result = false;
            this.Dispatcher.Invoke(() =>
            {
                if (!Session.CloseWillTerminate || !Session.HasChanged
                    || MessageBox.Show("The model system has not been saved, closing this window will discard the changes!",
                    "Are you sure?", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
                {
                    result = true;
                }
            }, DispatcherPriority.Input);
            return result;
        }

        public event Action<object> RequestClose;

        private void SetFocus(StackPanel border)
        {
              if (border.Background.IsFrozen)
              {

                  border.Background = Brushes.Transparent;
                  //border.Background = border.Background.CloneCurrentValue();



              }

              /*
             // else
              //{
                  ColorAnimation setFocus = new ColorAnimation(border.IsKeyboardFocusWithin
                          ? (Color) Application.Current.FindResource("FocusColour")
                          : Colors.Transparent,
                      new Duration(new TimeSpan(0, 0, 0, 0, 100)));
                  border.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
             // } */

          
        }

        new private void LostFocus(StackPanel border)
        {
            if (border.Background.IsFrozen)
            {
                //border.Background = new SolidColorBrush((Color)Application.Current.FindResource("FocusColour"));

                border.Background = Brushes.Transparent;
            }

            if (!border.IsKeyboardFocusWithin)
            {
                //var background = (Color)Application.Current.FindResource("ControlBackgroundColour");

                var background = Colors.Transparent;
                ColorAnimation setFocus = new ColorAnimation(background, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
              // border.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
            }
        }

        private void ParameterBorder_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = (sender as StackPanel);
            if (border == null) return;
            SetFocus(border);
        }

        private void ParameterBorder_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = (sender as StackPanel);
            if (border == null) return;
            LostFocus(border);
        }

        private void ParameterBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = (sender as StackPanel);
            if (border == null) return;
            SetFocus(border);
        }

        private void ParameterBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = (sender as StackPanel);
            if (border == null) return;
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
            if (textbox == null) return;
            if (textbox.Background.IsFrozen)
            {
                textbox.Background = textbox.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 100)));
            textbox.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as TextBox;
            if (box != null)
            {
                BindingExpression be = box.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
                if (box == null) return;
                if (box.Background.IsFrozen)
                {
                    box.Background = box.Background.CloneCurrentValue();
                }
                ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 100)));
                box.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
            }
        }

        private void SaveCurrentlySelectedParameters()
        {
            if (ParameterDisplay.IsKeyboardFocusWithin)
            {
                SaveCurrentlySelectedParameters(ParameterDisplay);
            }
            else if (QuickParameterDisplay.IsKeyboardFocusWithin)
            {
                SaveCurrentlySelectedParameters(QuickParameterDisplay);
            }
        }

        private void SaveCurrentlySelectedParameters(ListView parameterDisplay)
        {
            var index = parameterDisplay.SelectedIndex;
            if (index >= 0)
            {
                var container = parameterDisplay.ItemContainerGenerator.ContainerFromIndex(index);
                var textBox = GetChildOfType<TextBox>(container);
                if (textBox != null)
                {
                    BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                    be.UpdateSource();
                }
            }
        }

        public static T GetChildOfType<T>(DependencyObject depObj)
                where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void HintedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                var shiftDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift);
                var ctrlDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);
                switch (e.Key)
                {
                    case Key.Escape:
                        var box = ParameterDisplay.IsKeyboardFocusWithin ? ParameterFilterBox.Box : QuickParameterFilterBox.Box;
                        if (box.Text == String.Empty)
                        {
                            e.Handled = false;
                        }
                        else
                        {
                            box.Text = String.Empty;
                            e.Handled = true;
                        }
                        break;
                    case Key.F2:
                        RenameParameter();
                        e.Handled = true;
                        break;
                    case Key.H:
                        if (ctrlDown)
                        {
                            SetCurrentParameterHidden(!shiftDown);
                            e.Handled = true;
                        }
                        break;
                    case Key.Enter:
                        MoveFocusNext(shiftDown);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        if (shiftDown)
                        {
                            if (ctrlDown)
                            {
                                MoveCurrentModule(-1);
                            }
                            else
                            {
                                MoveFocusNextModule(true);
                            }
                            e.Handled = true;
                        }
                        else
                        {
                            MoveFocusNext(true);
                            e.Handled = true;
                        }
                        break;
                    case Key.Down:
                        if (shiftDown)
                        {
                            if (ctrlDown)
                            {
                                MoveCurrentModule(1);
                            }
                            else
                            {
                                MoveFocusNextModule(false);
                            }
                        }
                        else
                        {
                            MoveFocusNext(false);
                        }
                        e.Handled = true;
                        break;
                    case Key.L:
                        if (ctrlDown)
                        {
                            ShowLinkedParameterDialog(true);
                            e.Handled = true;
                        }
                        else
                        {
                            e.Handled = false;
                        }
                        break;
                    case Key.T:
                        {
                            if (ctrlDown)
                            {
                                ToggleQuickParameter();
                                e.Handled = true;
                            }
                            else
                            {
                                e.Handled = false;
                            }
                        }
                        break;
                    default:
                        e.Handled = false;
                        break;
                }
            }
        }


        private void MoveFocusNextModule(bool up)
        {
            Keyboard.Focus(ModuleDisplay);
            MoveFocusNext(up);
        }

        private void MoveFocusNext(bool up)
        {
            TraversalRequest request;
            if (up)
            {
                request = new TraversalRequest(FocusNavigationDirection.Up);
            }
            else
            {
                request = new TraversalRequest(FocusNavigationDirection.Down);
            }

            // Gets the element with keyboard focus.
            UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;

            // Change keyboard focus.
            if (elementWithFocus != null)
            {
                elementWithFocus.MoveFocus(request);
            }
        }

        object SaveLock = new object();

        public void SaveRequested(bool saveAs)
        {
            string error = null;
            SaveCurrentlySelectedParameters();
            if (saveAs)
            {
                StringRequest sr = new StringRequest("Save Model System As?", (newName) =>
                {
                    return Project.ValidateProjectName(newName);
                });
                if (sr.ShowDialog() == true)
                {
                    if (!Session.SaveAs(sr.Answer, ref error))
                    {
                        MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                Monitor.Enter(SaveLock);
                MainWindow.SetStatusText("Saving...");
                Task.Run(async () =>
                    {
                        try
                        {
                            var watch = Stopwatch.StartNew();
                            if (!Session.Save(ref error))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }
                            watch.Stop();
                            var displayTimeRemaining = 1000 - (int)watch.ElapsedMilliseconds;
                            if (displayTimeRemaining > 0)
                            {
                                MainWindow.SetStatusText("Saved");
                                await Task.Delay(displayTimeRemaining);
                            }
                        }
                        catch (Exception e)
                        {
                            Dispatcher.Invoke(() =>
                           {
                               MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + e.Message, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Error);
                           });
                        }
                        finally
                        {
                            MainWindow.SetStatusText("Ready");
                            Monitor.Exit(SaveLock);
                        }
                    });
            }
        }

        private void CopyCurrentModule()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if (CurrentlySelected.Count == 1)
            {
                if (selected != null)
                {
                    selected.CopyModule();
                }
            }
            else
            {
                ModelSystemStructureDisplayModel.CopyModules(CurrentlySelected);
            }
        }

        private void PasteCurrentModule()
        {
            string pasteText = Clipboard.GetText();
            var any = false;
            if (pasteText != null)
            {
                foreach (var selected in CurrentlySelected.ToList())
                {
                    string error = null;
                    if (!selected.Paste(Session, pasteText, ref error))
                    {
                        MessageBox.Show(MainWindow.Us, "Failed to Paste.\r\n" + error, "Unable to Paste", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    else
                    {
                        any = true;
                    }
                }
            }
            if (any)
            {
                UpdateParameters();
                UpdateQuickParameters();
            }
        }

        public void ExternalUpdateParameters()
        {

            this.UpdateParameters();

        }

        private void UpdateParameters()
        {
            var parameters = GetActiveParameters();


            if (parameters != null)
            {
                FadeOut();
                Task.Factory.StartNew(() =>
                {
                    var source = ParameterDisplayModel.CreateParameters(parameters.OrderBy(el => el.Name).OrderBy(el => el.IsHidden), CurrentlySelected.Count > 1);



                    if (!MainWindow.Us.ShowMetaModuleHiddenParameters)
                    {
                        if (CurrentlySelected.Count == 1)
                        {



                            if (CurrentlySelected[0].BaseModel.IsMetaModule)
                            {

                                foreach (var s in source.Where(p => p.RealParameter.IsHidden == true).ToList())
                                {

                                    source.Remove(s);
                                }
                            }
                        }
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CleanUpParameters();
                        ParameterDisplay.ItemsSource = source;
                        ParameterFilterBox.Display = ParameterDisplay;
                        ParameterFilterBox.Filter = FilterParameters;
                        ParameterFilterBox.RefreshFilter();
                        var type = CurrentlySelected.Count == 1 ? CurrentlySelected[0].Type : null;

                        
                        

                        if (type != null)
                        {
                            SelectedName.Text = type.Name;
                            SelectedNamespace.Text = type.FullName;




                        }
                        else
                        {
                            SelectedName.Text = CurrentlySelected.Count > 1 ? "Multiple Selected" : "None Selected";
                            SelectedNamespace.Text = string.Empty;
                        }
                        DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
                        ParameterDisplay.BeginAnimation(OpacityProperty, fadeIn);
                    }));
                });
            }
            else
            {
                ParameterDisplay.ItemsSource = null;
                SelectedName.Text = "None Selected";
                SelectedNamespace.Text = string.Empty;
            }
        }

        private List<ParameterModel> GetActiveParameters()
        {
            switch (CurrentlySelected.Count)
            {
                case 0:
                    return null;
                case 1:
                    return CurrentlySelected[0].GetParameters().ToList();
                default:
                    return GetParameterIntersection();
            }
        }

        private List<ParameterModel> GetParameterIntersection()
        {
            var allParameters = CurrentlySelected.Select(m => m.GetParameters());
            return CurrentlySelected.SelectMany(m => m.GetParameters().Where(p => allParameters.All(list => list.Any(q => p.Name == q.Name && p.Type == q.Type)))).ToList();
        }

        private void ModuleDisplay_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var module = (e.NewValue as ModelSystemStructureDisplayModel);
            if (module != null)
            {
                RefreshParameters();
                if (ParameterTabControl.SelectedIndex != 0)
                {
                    ParameterTabControl.SelectedIndex = 0;
                }
            }
        }

        private void Help_Clicked(object sender, RoutedEventArgs e)
        {
            ShowDocumentation();
        }

        private void ShowDocumentation()
        {
            var selectedModule = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if (selectedModule != null)
            {
                MainWindow.Us.LaunchHelpWindow(selectedModule.BaseModel);
            }
        }

        private void Module_Clicked(object sender, RoutedEventArgs e)
        {
            SelectReplacement();
        }

        private void Rename_Clicked(object sender, RoutedEventArgs e)
        {
            Rename();
        }

        private void Description_Clicked(object sender, RoutedEventArgs e)
        {
            RenameDescription();
        }

        private void Rename()
        {
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel).BaseModel;
            var selectedModuleControl = GetCurrentlySelectedControl();
            if (selectedModuleControl != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                var adorn = new TextboxAdorner("Rename", (result) =>
                {
                    string error = null;
                    Session.ExecuteCombinedCommands(
                        "Rename ModelSystem",
                        () =>
                   {
                       foreach (var sel in CurrentlySelected)
                       {
                           if (!sel.BaseModel.SetName(result, ref error))
                           {
                               throw new Exception(error);
                           }
                       }
                   });
                }, selectedModuleControl, selected.Name);
                layer.Add(adorn);
                adorn.Focus();
            }
            else
            {
                throw new InvalidAsynchronousStateException("The current module could not be found!");
            }
        }
        private void RenameDescription()
        {
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel).BaseModel;
            var selectedModuleControl = GetCurrentlySelectedControl();
            if (selectedModuleControl != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                var adorn = new TextboxAdorner("Rename Description", (result) =>
                {
                    string error = null;
                    Session.ExecuteCombinedCommands(
                        "Set ModelSystem Description",
                        () =>
                    {
                        foreach (var sel in CurrentlySelected)
                        {
                            if (!sel.BaseModel.SetDescription(result, ref error))
                            {
                                throw new Exception(error);
                            }
                        }
                    });
                }, selectedModuleControl, selected.Description);
                layer.Add(adorn);
                adorn.Focus();
            }
            else
            {
                throw new InvalidAsynchronousStateException("The current module could not be found!");
            }
        }

        private void MoveCurrentModule(int deltaPosition)
        {
            if (CurrentlySelected.Count > 0)
            {
                var parent = Session.GetParent(CurrentlySelected[0].BaseModel);
                // make sure they all have the same parent
                if (CurrentlySelected.Any(m => Session.GetParent(m.BaseModel) != parent))
                {
                    // if not ding and exit
                    System.Media.SystemSounds.Asterisk.Play();
                    return;
                }
                var mul = deltaPosition < 0 ? 1 : -1;
                var moveOrder = CurrentlySelected.Select((c, i) => new { Index = i, ParentIndex = parent.Children.IndexOf(c.BaseModel) }).OrderBy(i => mul * i.ParentIndex);
                var first = moveOrder.First();
                Session.ExecuteCombinedCommands(
                    "Move Selected Modules",
                    () =>
               {
                   foreach (var el in moveOrder)
                   {
                       var selected = CurrentlySelected[el.Index];
                       string error = null;
                       if (!selected.BaseModel.MoveModeInParent(deltaPosition, ref error))
                       {
                           //MessageBox.Show(GetWindow(), error, "Unable to move", MessageBoxButton.OK, MessageBoxImage.Error);
                           System.Media.SystemSounds.Asterisk.Play();
                           break;
                       }
                   }
               });
                BringSelectedIntoView(CurrentlySelected[first.Index]);
            }
        }

        private void BringSelectedIntoView(ModelSystemStructureDisplayModel selected)
        {
            List<ModelSystemStructureDisplayModel> ansestry = DisplayRoot.BuildChainTo(selected);
            if (ansestry == null)
            {
                return;
            }
            var currentContainer = ModuleDisplay.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
            for (int i = 1; i < ansestry.Count; i++)
            {
                if (currentContainer == null)
                {
                    return;
                }
                if (i + 1 < ansestry.Count)
                {
                    currentContainer = currentContainer.ItemContainerGenerator.ContainerFromItem(ansestry[i]) as TreeViewItem;
                }
                else
                {
                    currentContainer = currentContainer.ItemContainerGenerator.ContainerFromItem(ansestry[i]) as TreeViewItem;
                    currentContainer.BringIntoView();
                    return;
                }
            }
        }

        private void Remove_Clicked(object sender, RoutedEventArgs e)
        {
            RemoveSelectedModules();
        }

        private void RemoveSelectedModules()
        {
            ModelSystemStructureDisplayModel first = null;
            ModelSystemStructureModel parent = null;
            // we need to make a copy of the currently selected in
            // order to not operate on the list as it is changing
            Session.ExecuteCombinedCommands(
                "Remove Selected Modules",
                () =>
           {
               foreach (var selected in CurrentlySelected.ToList())
               {
                   if (first == null)
                   {
                       first = selected;
                       parent = Session.GetParent(selected.BaseModel);
                       Dispatcher.Invoke(() =>
                      {
                          if (!first.IsCollection)
                          {
                              // do this so we don't lose our place
                              if (parent.IsCollection)
                              {
                                  if (parent.Children.IndexOf(first.BaseModel) < parent.Children.Count - 1)
                                  {
                                      MoveFocusNext(false);
                                  }
                                  else
                                  {
                                      MoveFocusNext(true);
                                  }
                              }
                          }
                      });

                       /* Remove the module from selected items */
                       CurrentlySelected.Remove(selected);


                       UpdateParameters();
                       Keyboard.Focus(ModuleDisplay);
                   }
                   string error = null;
                   if (!ModelSystem.Remove(selected.BaseModel, ref error))
                   {
                       System.Media.SystemSounds.Asterisk.Play();
                   }
               }
           });
        }

        private void CleanUpParameters()
        {
            ParameterDisplay.BeginAnimation(OpacityProperty, null);
        }

        private void FadeOut()
        {
            DoubleAnimation fadeOut = new DoubleAnimation(0.0, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
            ParameterDisplay.BeginAnimation(OpacityProperty, null);
            ParameterDisplay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void LinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            ShowLinkedParameterDialog();
        }

        private void AssignLinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            ShowLinkedParameterDialog(true);
        }

        private void RemoveLinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromLinkedParameter();
        }

        private void ResetParameter_Click(object sender, RoutedEventArgs e)
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string error = null;
                if (!currentParameter.ResetToDefault(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to reset parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyParameterName()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                Clipboard.SetText(currentParameter.Name);
            }
        }

        private void SetCurrentParameterHidden(bool hidden)
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string error = null;
                currentParameter.SetHidden(hidden, ref error);
            }
        }


        private void RenameParameter()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                var selectedContainer = (UIElement)ParameterDisplay.ItemContainerGenerator.ContainerFromItem(currentParameter);
                if (selectedContainer != null)
                {
                    var layer = AdornerLayer.GetAdornerLayer(selectedContainer);
                    var adorn = new TextboxAdorner("Rename", (result) =>
                    {
                        string error = null;
                        if (!currentParameter.SetName(result, ref error))
                        {
                            MessageBox.Show(GetWindow(), error, "Unable to Set Parameter Name", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            RefreshParameters();
                        }
                    }, selectedContainer, currentParameter.GetBaseName());
                    layer.Add(adorn);
                    adorn.Focus();
                }
            }
        }

        private void ResetParameterName()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string error = null;
                currentParameter.RevertNameToDefault(ref error);
                UpdateParameters();
            }
        }

        private ParameterModel GetInputParameter(ModelSystemStructureModel currentRoot, out string directory)
        {
            directory = null;
            ModelSystemStructureModel previousRoot = null;
            do
            {
                currentRoot = Session.GetRoot(currentRoot);
                if (currentRoot.Type.GetInterfaces().Any(t => t == typeof(IModelSystemTemplate)))
                {
                    break;
                }
                // detect a loop
                if (previousRoot == currentRoot)
                {
                    // just terminate
                    return null;
                }
                previousRoot = currentRoot;
            } while (true);
            ParameterModel inputParameter = null;
            directory = GetInputDirectory(currentRoot, out inputParameter);
            return inputParameter;
        }

        private void OpenParameterFileLocation(bool openWith, bool openDirectory)
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            var currentModule = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if (currentParameter != null && currentModule != null)
            {
                string inputDirectory;
                ParameterModel inputParameter = GetInputParameter(currentModule.BaseModel, out inputDirectory);
                if (inputParameter != null)
                {
                    // Check to see if the parameter that contains the input directory IS this parameter
                    var isInputParameter = inputParameter == currentParameter.RealParameter;
                    var pathToFile = GetRelativePath(inputDirectory, currentParameter.Value, isInputParameter);
                    if (openDirectory)
                    {
                        pathToFile = System.IO.Path.GetDirectoryName(pathToFile);
                    }
                    try
                    {
                        Process toRun = new Process();
                        if (openWith)
                        {
                            toRun.StartInfo.FileName = "Rundll32.exe";
                            toRun.StartInfo.Arguments = "Shell32.dll,OpenAs_RunDLL " + pathToFile;
                        }
                        else
                        {
                            toRun.StartInfo.FileName = pathToFile;
                        }
                        toRun.Start();
                    }
                    catch
                    {
                        MessageBox.Show(GetWindow(), "Unable to load the file at '" + pathToFile + "'!", "Unable to Load", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SelectDirectoryForCurrentParameter()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string inputDirectory;
                ParameterModel _ = GetInputParameter(Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure), out inputDirectory);
                if (inputDirectory != null)
                {
                    string directoryName = MainWindow.OpenDirectory();
                    if (directoryName == null)
                    {
                        return;
                    }
                    TransformToRelativePath(inputDirectory, ref directoryName);
                    currentParameter.Value = directoryName;
                }
            }
        }

        private void SelectFileForCurrentParameter()
        {
            var currentParameter = (ParameterTabControl.SelectedItem == QuickParameterTab ? QuickParameterDisplay.SelectedItem : ParameterDisplay.SelectedItem) as ParameterDisplayModel;
            if (currentParameter != null)
            {
                string inputDirectory;
                ParameterModel _ = GetInputParameter(Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure), out inputDirectory);
                if (inputDirectory != null)
                {
                    string fileName = MainWindow.OpenFile("Select File", new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("All Files", "*") }, true);
                    if (fileName == null)
                    {
                        return;
                    }
                    TransformToRelativePath(inputDirectory, ref fileName);
                    currentParameter.Value = fileName;
                }
            }
        }

        private void TransformToRelativePath(string inputDirectory, ref string fileName)
        {
            var runtimeInputDirectory =
                System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Session.Configuration.ProjectDirectory, "AProject", "RunDirectory", inputDirectory)
                ) + System.IO.Path.DirectorySeparatorChar;
            if (fileName.StartsWith(runtimeInputDirectory))
            {
                fileName = fileName.Substring(runtimeInputDirectory.Length);
            }
        }

        private string GetInputDirectory(ModelSystemStructureModel root, out ParameterModel parameter)
        {
            var inputDir = root.Type.GetProperty("InputBaseDirectory");
            var attributes = inputDir.GetCustomAttributes(typeof(ParameterAttribute), true);
            if (attributes != null && attributes.Length > 0)
            {
                var parameterName = ((ParameterAttribute)attributes[0]).Name;
                var parameters = root.Parameters.GetParameters();
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].Name == parameterName)
                    {
                        parameter = parameters[i];
                        return parameters[i].Value.ToString();
                    }
                }
            }
            parameter = null;
            return null;
        }

        private string GetRelativePath(string inputDirectory, string parameterValue, bool isInputParameter)
        {
            var parameterRooted = System.IO.Path.IsPathRooted(parameterValue);
            var inputDirectoryRooted = System.IO.Path.IsPathRooted(inputDirectory);
            if (parameterRooted)
            {
                return RemoveRelativeDirectories(parameterValue);
            }
            else if (inputDirectoryRooted)
            {
                return RemoveRelativeDirectories(System.IO.Path.Combine(inputDirectory, parameterValue));
            }
            return RemoveRelativeDirectories(System.IO.Path.Combine(Session.Configuration.ProjectDirectory, Session.ProjectEditingSession.Name,
            "RunDirectory", inputDirectory, isInputParameter ? "" : parameterValue));
        }

        private string RemoveRelativeDirectories(string path)
        {
            var parts = path.Split('\\', '/');
            StringBuilder finalPath = new StringBuilder();
            Stack<string> currentlyOn = new Stack<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                {
                    // make sure we don't go too far back
                    if (currentlyOn.Count <= 0)
                    {
                        return null;
                    }
                    var previousString = currentlyOn.Pop();
                    var removeLength = previousString.Length + 1;
                    finalPath.Remove(finalPath.Length - removeLength, removeLength);

                }
                else if (parts[i] == ".")
                {
                    // do nothing
                }
                else
                {
                    finalPath.Append(parts[i]);
                    finalPath.Append(System.IO.Path.DirectorySeparatorChar);
                    currentlyOn.Push(parts[i]);
                }
            }
            return finalPath.ToString(0, finalPath.Length - 1);
        }


        private void CopyParameterName_Click(object sender, RoutedEventArgs e)
        {
            CopyParameterName();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenParameterFileLocation(false, false);
        }

        private void OpenWith_Click(object sender, RoutedEventArgs e)
        {
            OpenParameterFileLocation(true, false);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenParameterFileLocation(false, true);
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            SelectFileForCurrentParameter();
        }

        private void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            SelectDirectoryForCurrentParameter();
        }

        private void CopyModule_Click(object sender, RoutedEventArgs e)
        {
            CopyCurrentModule();
        }

        private void PasteModule_Click(object sender, RoutedEventArgs e)
        {
            PasteCurrentModule();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == ParameterTabControl)
            {
                if (ParameterTabControl.SelectedItem == QuickParameterTab)
                {
                    UpdateQuickParameters();
                }
            }
        }

        private void UpdateQuickParameters()
        {
            QuickParameterDisplay.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel.GetQuickParameters().OrderBy(n => n.Name));
            QuickParameterFilterBox.Display = QuickParameterDisplay;
            QuickParameterFilterBox.Filter = FilterParameters;
            QuickParameterFilterBox.RefreshFilter();
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        private void DisplayButton_RightClicked(object obj)
        {
            var button = obj as BorderIconButton;
            if (button != null)
            {
                var menu = button.ContextMenu;
                if (menu != null)
                {
                    menu.PlacementTarget = button;
                    menu.IsOpen = true;
                }
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveCurrentModule(-1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveCurrentModule(1);
        }

        ///
        private static readonly PropertyInfo IsSelectionChangeActiveProperty = typeof(TreeView).GetProperty(
                                              "IsSelectionChangeActive",
                                              BindingFlags.NonPublic | BindingFlags.Instance
                                            );

        List<ModelSystemStructureDisplayModel> CurrentlySelected = new List<ModelSystemStructureDisplayModel>();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="treeView"></param>
        /// <see cref="http://stackoverflow.com/questions/1163801/wpf-treeview-with-multiple-selection"/>
        public void AllowMultiSelection(TreeView treeView)
        {
            if (IsSelectionChangeActiveProperty == null) return;

            var selectedItems = new List<TreeViewItem>();
            treeView.SelectedItemChanged += (a, b) =>
            {
                var module = GetCurrentlySelectedControl();
                if (module == null)
                {
                    // disable the event to avoid recursion
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    selectedItems.ForEach(item => item.IsSelected = true);
                    // enable the event to avoid recursion
                    IsSelectionChangeActiveProperty.SetValue
                    (
                      treeView,
                      isSelectionChangeActive,
                      null
                    );
                    return;
                }
                var treeViewItem = VisualUpwardSearch(module as DependencyObject) as TreeViewItem;
                if (treeViewItem == null) return;

                var currentItem = treeView.SelectedItem as ModelSystemStructureDisplayModel;

                // allow multiple selection
                // when control key is pressed
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    // suppress selection change notification
                    // select all selected items
                    // then restore selection change notifications
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);

                    selectedItems.ForEach(item => item.IsSelected = (item != treeViewItem || !selectedItems.Contains(treeViewItem)));

                    IsSelectionChangeActiveProperty.SetValue
                    (
                      treeView,
                      isSelectionChangeActive,
                      null
                    );
                }
                else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && CurrentlySelected.Count > 0)
                {
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    // select the range
                    var lastSelected = CurrentlySelected.Last();
                    var lastTreeItem = selectedItems.Last();
                    var currentParent = VisualUpwardSearch(VisualTreeHelper.GetParent(treeViewItem));
                    var lastParent = VisualUpwardSearch(VisualTreeHelper.GetParent(lastTreeItem));
                    if (currentParent != null && currentParent == lastParent)
                    {
                        var itemGenerator = currentParent.ItemContainerGenerator;
                        var lastSelectedIndex = itemGenerator.IndexFromContainer(lastTreeItem);
                        var currentSelectedIndex = itemGenerator.IndexFromContainer(treeViewItem);
                        int minIndex = Math.Min(lastSelectedIndex, currentSelectedIndex);
                        int maxIndex = Math.Max(lastSelectedIndex, currentSelectedIndex);
                        for (int i = minIndex; i <= maxIndex; i++)
                        {
                            var innerTreeViewItem = itemGenerator.ContainerFromIndex(i) as TreeViewItem;
                            var innerModule = itemGenerator.Items[i] as ModelSystemStructureDisplayModel;
                            if (CurrentlySelected.Contains(innerModule))
                            {
                                CurrentlySelected.Remove(innerModule);
                            }
                            CurrentlySelected.Add(innerModule);
                            selectedItems.Add(innerTreeViewItem);
                        }
                    }
                    // select all of the modules that should be selected
                    selectedItems.ForEach(item => item.IsSelected = true);
                    IsSelectionChangeActiveProperty.SetValue
                    (
                      treeView,
                      isSelectionChangeActive,
                      null
                    );
                    return;
                }
                else
                {
                    // deselect all selected items (current one will be re-added)
                    CurrentlySelected.Clear();
                    selectedItems.ForEach(item => item.IsSelected = (item == treeViewItem));
                    selectedItems.Clear();
                }

                if (!selectedItems.Contains(treeViewItem))
                {
                    selectedItems.Add(treeViewItem);
                    CurrentlySelected.Add(currentItem);
                }
                else
                {
                    // deselect if already selected
                    CurrentlySelected.Remove(currentItem);
                    treeViewItem.IsSelected = false;
                    selectedItems.Remove(treeViewItem);
                }
            };
        }

        private void ConvertToMetaModule_Click(object sender, RoutedEventArgs e)
        {
            SetMetaModuleStateForSelected(true);
        }

        private void ConvertFromMetaModule_Click(object sender, RoutedEventArgs e)
        {
            SetMetaModuleStateForSelected(false);
        }

        private void RenameParameter_Click(object sender, RoutedEventArgs e)
        {
            RenameParameter();
        }

        private void ResetParameterName_Click(object sender, RoutedEventArgs e)
        {
            ResetParameterName();
        }

        private void HideParameter_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentParameterHidden(true);
        }

        private void ShowParameter_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentParameterHidden(false);
        }
    }
}
