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
using System.Text;
using System.Threading;
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

        public ModelSystemEditingSession Session { get; set; }

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

        private bool CheckFilterRec(ModelSystemStructureDisplayModel module, string filterText, TreeViewItem previous = null)
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
            return GetCurrentlySelectedControl(DisplayRoot, ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel);
        }

        private UIElement GetCurrentlySelectedControl(ModelSystemStructureDisplayModel current, ModelSystemStructureDisplayModel lookingFor, TreeViewItem previous = null)
        {
            var children = current.Children;
            var container = (previous == null ? ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current) : previous.ItemContainerGenerator.ContainerFromItem(current)) as TreeViewItem;
            if(current == lookingFor && container != null)
            {
                return container;
            }
            if(children != null)
            {
                foreach(var child in children)
                {
                    var childResult = GetCurrentlySelectedControl(child, lookingFor, container);
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
                var module = o as ModelSystemStructureDisplayModel;
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
            var parameter = arg1 as ParameterDisplayModel;
            if(parameter != null)
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
            while(current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as Window;
        }

        private void ShowLinkedParameterDialog(bool assign = false)
        {
            var linkedParameterDialog = new LinkedParameterDisplay(ModelSystem.LinkedParameters, assign);
            linkedParameterDialog.Owner = GetWindow();
            if(linkedParameterDialog.ShowDialog() == true && assign)
            {
                // assign the selected linked parameter
                var newLP = linkedParameterDialog.SelectedLinkParameter;
                var displayParameter = ParameterDisplay.SelectedItem as ParameterDisplayModel;
                if(displayParameter != null)
                {
                    string error = null;
                    if(!displayParameter.AddToLinkedParameter(newLP, ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Failed to set to Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void RemoveFromLinkedParameter()
        {
            var currentParameter = ParameterDisplay.SelectedItem as ParameterDisplayModel;
            if(currentParameter != null)
            {
                string error = null;
                if(!currentParameter.RemoveLinkedParameter(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to remove from Linked Parameter", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectReplacement()
        {
            var selectedModule = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(Session == null)
            {
                throw new InvalidOperationException("Session has not been set before operating.");
            }
            if(selectedModule != null)
            {
                ModuleTypeSelect findReplacement = new ModuleTypeSelect(Session, selectedModule.BaseModel);
                findReplacement.Owner = GetWindow();
                if(findReplacement.ShowDialog() == true)
                {
                    var selectedType = findReplacement.SelectedType;
                    if (selectedType != null)
                    {
                        if(selectedModule.BaseModel.IsCollection)
                        {
                            string error = null;
                            if(!selectedModule.BaseModel.AddCollectionMember(selectedType, ref error))
                            {
                                throw new Exception(error);
                            }
                        }
                        else
                        {
                            selectedModule.BaseModel.Type = selectedType;
                        }
                        UpdateParameters(selectedModule.BaseModel.Parameters);
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
                us.ModelSystemName = newModelSystem.Name;
                Task.Run(() =>
                {
                    var displayModel = us.CreateDisplayModel(newModelSystem.Root);
                    us.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        us.ModuleDisplay.ItemsSource = displayModel;
                        us.ModelSystemName = newModelSystem.Name;
                        us.ModuleDisplay.Items.MoveCurrentToFirst();
                        us.FilterBox.Display = us.ModuleDisplay;
                    }));
                });
            }
            else
            {
                us.ModuleDisplay.DataContext = null;
                us.ModelSystemName = "No model loaded";
                us.FilterBox.Display = null;
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
            if(e.Handled == false)
            {
                switch(e.Key)
                {
                    case Key.Down:
                        if(Controllers.EditorController.IsShiftDown() && Controllers.EditorController.IsControlDown())
                        {
                            MoveCurrentModule(1);
                            e.Handled = true;
                        }
                        break;
                    case Key.Up:
                        if(Controllers.EditorController.IsShiftDown() && Controllers.EditorController.IsControlDown())
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
                        case Key.Z:
                            Undo();
                            e.Handled = true;
                            break;
                        case Key.Y:
                            Redo();
                            e.Handled = true;
                            break;
                        case Key.S:
                            SaveRequested();
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
                    switch(e.Key)
                    {
                        case Key.F1:
                            ShowDocumentation();
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            RemoveCurrentModule();
                            e.Handled = true;
                            break;
                        case Key.F2:
                            Rename();
                            e.Handled = true;
                            break;
                        case Key.F5:
                            MainWindow.Us.ExecuteRun();
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        private void ShowQuickParameters()
        {
            ParameterDisplay.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel.GetQuickParameters().OrderBy(n => n.Name));
        }

        private void Redo()
        {
            string error = null;
            Session.Redo(ref error);
        }

        private void Undo()
        {
            string error = null;
            Session.Undo(ref error);
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
            ColorAnimation setFocus = new ColorAnimation(border.IsKeyboardFocusWithin ?
                (Color)Application.Current.FindResource("FocusColour") :
                (Color)Application.Current.FindResource("SelectionBlue"),
                new Duration(new TimeSpan(0, 0, 0, 0, 100)));
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
                ColorAnimation setFocus = new ColorAnimation(background, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
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
            ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 100)));
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
            ColorAnimation setFocus = new ColorAnimation(Color.FromRgb(0xEE, 0xEE, 0xEE), new Duration(new TimeSpan(0, 0, 0, 0, 100)));
            textbox.Background.BeginAnimation(SolidColorBrush.ColorProperty, setFocus);
        }

        private void HintedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(!e.Handled)
            {
                var shiftDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift);
                var ctrlDown = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control);
                e.Handled = true;
                switch(e.Key)
                {
                    case Key.Enter:
                        MoveFocusNext(shiftDown);
                        break;
                    case Key.Up:
                        if(shiftDown)
                        {
                            if(ctrlDown)
                            {
                                MoveCurrentModule(-1);
                            }
                            else
                            {
                                MoveFocusNextModule(true);
                            }
                        }
                        else
                        {
                            MoveFocusNext(true);
                        }
                        break;
                    case Key.Down:
                        if(shiftDown)
                        {
                            if(ctrlDown)
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
                        break;
                    case Key.L:
                        if(ctrlDown)
                        {
                            ShowLinkedParameterDialog(true);
                            e.Handled = true;
                        }
                        else
                        {
                            e.Handled = false;
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
            if(up)
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
            if(elementWithFocus != null)
            {
                elementWithFocus.MoveFocus(request);
            }
        }

        object SaveLock = new object();

        public void SaveRequested()
        {
            string error = null;
            Monitor.Enter(SaveLock);
            MainWindow.SetStatusText("Saving...");
            Task.Run(async () =>
                {
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        if(!Session.Save(ref error))
                        {
                            MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        watch.Stop();
                        var displayTimeRemaining = 1000 - (int)watch.ElapsedMilliseconds;
                        if(displayTimeRemaining > 0)
                        {
                            MainWindow.SetStatusText("Saved");
                            await Task.Delay(displayTimeRemaining);
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + e.Message, "Unable to Save", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        MainWindow.SetStatusText("Ready");
                        Monitor.Exit(SaveLock);
                    }
                });

        }

        public void UndoRequested()
        {
            Undo();
        }

        public void RedoRequested()
        {
            Redo();
        }

        private void CopyCurrentModule()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(selected != null)
            {
                selected.CopyModule();
            }
        }

        private void PasteCurrentModule()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(selected != null)
            {
                string error = null;
                if(!selected.Paste(Clipboard.GetText(), ref error))
                {
                    MessageBox.Show(MainWindow.Us, "Failed to Paste.\r\n" + error, "Unable to Paste", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void CloneRequested(string clonedName)
        {

        }

        private void ModuleDisplay_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var module = (e.NewValue as ModelSystemStructureDisplayModel);
            if(module != null)
            {
                UpdateParameters(module.BaseModel.Parameters);
            }
        }

        private void Help_Clicked(object sender, RoutedEventArgs e)
        {
            ShowDocumentation();
        }

        private void ShowDocumentation()
        {
            Controllers.ModelSystemEditingController.HelpRequested(ModuleDisplay.SelectedItem as ModelSystemStructureModel);
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
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel).BaseModel;
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

        private void MoveCurrentModule(int deltaPosition)
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(selected != null)
            {
                string error = null;
                if(!selected.BaseModel.MoveModeInParent(deltaPosition, ref error))
                {
                    //MessageBox.Show(GetWindow(), error, "Unable to move", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
        }

        private void Remove_Clicked(object sender, RoutedEventArgs e)
        {
            RemoveCurrentModule();
        }

        private void RemoveCurrentModule()
        {
            var selected = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(selected != null)
            {
                string error = null;
                // do this so we don't lose our place
                var parent = Session.GetParent(selected.BaseModel);
                if(parent.Children.IndexOf(selected.BaseModel) < parent.Children.Count - 1)
                {
                    MoveFocusNext(false);
                }
                else
                {
                    MoveFocusNext(true);
                }
                if(!ModelSystem.Remove(selected.BaseModel, ref error))
                {
                    throw new Exception(error);
                }
                UpdateParameters(selected.BaseModel.Parameters);
                Keyboard.Focus(ModuleDisplay);
            }
        }

        private void UpdateParameters(ParametersModel parameters)
        {
            if(parameters != null)
            {
                FadeOut();
                Task.Factory.StartNew(() =>
                {
                    var source = ParameterDisplayModel.CreateParameters(parameters.GetParameters().OrderBy(el => el.Name));
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CleanUpParameters();
                        ParameterDisplay.ItemsSource = source;
                        ParameterFilterBox.Display = ParameterDisplay;
                        ParameterFilterBox.Filter = FilterParameters;
                        ParameterFilterBox.RefreshFilter();
                        var type = parameters.ModelSystemStructure.Type;
                        if(type != null)
                        {
                            SelectedName.Text = type.Name;
                            SelectedNamespace.Text = type.FullName;
                        }
                        else
                        {
                            SelectedName.Text = "None Selected";
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

        private void CopyParameterName()
        {
            var currentParameter = ParameterDisplay.SelectedItem as ParameterDisplayModel;
            if(currentParameter != null)
            {
                Clipboard.SetText(currentParameter.Name);
            }
        }

        private void OpenParameterFileLocation(bool openWith, bool openDirectory)
        {
            var currentParameter = ParameterDisplay.SelectedItem as ParameterDisplayModel;
            var currentModule = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(currentParameter != null && currentModule != null)
            {
                var currentRoot = Session.GetRoot(currentModule.BaseModel);
                ParameterModel inputParameter = null;
                var inputDirectory = GetInputDirectory(currentRoot, out inputParameter);
                var isInputParameter = inputParameter == currentParameter.RealParameter;
                var pathToFile = GetRelativePath(inputDirectory, currentParameter.Value, isInputParameter);
                if(openDirectory)
                {
                    pathToFile = System.IO.Path.GetDirectoryName(pathToFile);
                }
                try
                {
                    Process.Start(pathToFile);
                }
                catch
                {
                    MessageBox.Show(GetWindow(), "Unable to load the file at '" + pathToFile + "'!", "Unable to Load", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectFileForCurrentParameter()
        {
            var currentParameter = ParameterDisplay.SelectedItem as ParameterDisplayModel;
            var currentModule = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            if(currentParameter != null && currentModule != null)
            {
                var currentRoot = Session.GetRoot(currentModule.BaseModel);
                ParameterModel _;
                var inputDirectory = GetInputDirectory(currentRoot, out _);
                if(inputDirectory != null)
                {
                    string fileName = this.OpenFile();
                    if(fileName == null)
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
            if(fileName.StartsWith(runtimeInputDirectory))
            {
                fileName = fileName.Substring(runtimeInputDirectory.Length);
            }
        }

        private string OpenFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            if(dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        private string GetInputDirectory(ModelSystemStructureModel root, out ParameterModel parameter)
        {
            var inputDir = root.Type.GetProperty("InputBaseDirectory");
            var attributes = inputDir.GetCustomAttributes(typeof(ParameterAttribute), true);
            if(attributes != null && attributes.Length > 0)
            {
                var parameterName = ((ParameterAttribute)attributes[0]).Name;
                var parameters = root.Parameters.GetParameters();
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].Name == parameterName)
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
            if(parameterRooted)
            {
                return RemoveRelativeDirectories(parameterValue);
            }
            else if(inputDirectoryRooted)
            {
                return RemoveRelativeDirectories(System.IO.Path.Combine(inputDirectory, parameterValue));
            }
            return RemoveRelativeDirectories(System.IO.Path.Combine(Session.Configuration.ProjectDirectory, "AProject",
            "RunDirectory", inputDirectory, isInputParameter ? "" : parameterValue));
        }

        private string RemoveRelativeDirectories(string path)
        {
            var parts = path.Split('\\', '/');
            StringBuilder finalPath = new StringBuilder();
            int lastReal = 0;
            for(int i = 0; i < parts.Length; i++)
            {
                if(parts[i] == "..")
                {
                    if(lastReal > 0)
                    {
                        var removeLength = parts[--lastReal].Length + 1;
                        finalPath.Remove(finalPath.Length - removeLength, removeLength);
                    }
                    else
                    {
                        finalPath.Remove(0, finalPath.Length);
                    }
                }
                else if(parts[i] == ".")
                {
                    // do nothing
                }
                else
                {
                    finalPath.Append(parts[i]);
                    finalPath.Append(System.IO.Path.DirectorySeparatorChar);
                    lastReal++;
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

        private void CopyModule_Click(object sender, RoutedEventArgs e)
        {
            CopyCurrentModule();
        }

        private void PasteModule_Click(object sender, RoutedEventArgs e)
        {
            PasteCurrentModule();
        }
    }
}
