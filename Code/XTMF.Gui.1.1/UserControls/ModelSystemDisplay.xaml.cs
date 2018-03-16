/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls.Interfaces;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ModelSystemDisplay.xaml
    /// </summary>
    public partial class ModelSystemDisplay : UserControl, ITabCloseListener, INotifyPropertyChanged, IResumableControl
    {
        public static readonly DependencyProperty ModelSystemProperty = DependencyProperty.Register("ModelSystem",
            typeof(ModelSystemModel), typeof(ModelSystemDisplay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnModelSystemChanged));

        public static readonly DependencyProperty ModelSystemNameProperty = DependencyProperty.Register(
            "ModelSystemName", typeof(string), typeof(ModelSystemDisplay),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CanRunModelSystemDependencyProperty =
            DependencyProperty.Register("CanRunModelSystem", typeof(bool), typeof(ModelSystemDisplay),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ParameterWidthDependencyProperty =
            DependencyProperty.Register("ParameterWidth", typeof(double), typeof(ModelSystemDisplay),
                new PropertyMetadata(100.0));

        private static int FilterNumber;

        private static readonly PropertyInfo IsSelectionChangeActiveProperty = typeof(TreeView).GetProperty(
            "IsSelectionChangeActive", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<ModelSystemStructureDisplayModel> CurrentlySelected =
            new List<ModelSystemStructureDisplayModel>();

        private readonly BindingList<LinkedParameterDisplayModel> RecentLinkedParameters =
            new BindingList<LinkedParameterDisplayModel>();

        private bool _canSaveModelSystem;

        private bool _disableMultipleSelectOnce;

        private bool _loadedOnce;

        private Semaphore _saveSemaphor;

        private ParameterDisplayModel _selectedParameterDisplayModel;

        private ModelSystemEditingSession _Session;

        private ModelSystemStructureDisplayModel DisplayRoot;

        private readonly LinkedParameterDisplay LinkedParameterDisplayOverlay;

        private object SaveLock = new object();

        public ModelSystemDisplay()
        {
            _saveSemaphor = new Semaphore(1, 1);
            DataContext = this;
            InitializeComponent();
            AllowMultiSelection(ModuleDisplay);
            Loaded += ModelSystemDisplay_Loaded;
            ModuleDisplay.SelectedItemChanged += ModuleDisplay_SelectedItemChanged;
            DisabledModules = new ObservableCollection<ModelSystemStructureDisplayModel>();
            DisabledModulesList.ItemsSource = DisabledModules;
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


            LinkedParameterDisplayOverlay = new LinkedParameterDisplay();
            //LinkedParameterDisplayOverlay item =
            LinkedParameterDisplayOverlay.GoToModule += module =>
            {
                if (module != null)
                {
                    GoToModule((ModelSystemStructure) module);
                }
            };
            LinkedParameterDisplayOverlay.OnCloseDisplay += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    var newLP = LinkedParameterDisplayOverlay.SelectedLinkParameter;
                    if (AddCurrentParameterToLinkedParameter(newLP))
                    {
                        LinkedParameterDisplayModel matched;
                        if ((matched = RecentLinkedParameters.FirstOrDefault(lpdm => lpdm.LinkedParameter == newLP)) !=
                            null)
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

                    RefreshParameters();
                    _selectedParameterDisplayModel = null;
                });
            };
        }


        public bool CanRunModelSystem
        {
            get => (bool) GetValue(CanRunModelSystemDependencyProperty);
            set => SetValue(CanRunModelSystemDependencyProperty, value);
        }

        public string ContentGuid { get; set; }

        public double ParameterWidth
        {
            get => (double) GetValue(ParameterWidthDependencyProperty);
            set => SetValue(ParameterWidthDependencyProperty, value);
        }

        public ModelSystemEditingSession Session
        {
            get => _Session;
            set
            {
                if (_Session != null)
                {
                    _Session.ProjectWasExternallySaved -= ProjectWasExternalSaved;
                    _Session.CommandExecuted += SessionOnCommandExecuted;
                    _Session.Saved += _Session_Saved;
                }

                _Session = value;
                if (value != null)
                {
                    value.ProjectWasExternallySaved += ProjectWasExternalSaved;
                }

                CanRunModelSystem = _Session.ProjectEditingSession != null;
            }
        }

        public bool CanSaveModelSystem
        {
            get => _canSaveModelSystem;
            set
            {
                _canSaveModelSystem = value;
                OnPropertyChanged(nameof(CanSaveModelSystem));
            }
        }

        /// <summary>
        ///     The model system to display
        /// </summary>
        public ModelSystemModel ModelSystem
        {
            get => (ModelSystemModel) GetValue(ModelSystemProperty);
            set => SetValue(ModelSystemProperty, value);
        }

        public string ModelSystemName
        {
            get => (string) GetValue(ModelSystemNameProperty);
            private set => SetValue(ModelSystemNameProperty, value);
        }

        public ObservableCollection<ModelSystemStructureDisplayModel> DisabledModules { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Restores this view with the passed data. If ErrorWithPath is passed as the data, the selected
        ///     module is attempted to be brought into view by the path data.
        /// </summary>
        /// <param name="data"></param>
        public void RestoreWithData(object data)
        {
            if (data is ErrorWithPath error)
            {
                var current = DisplayRoot;

                var fail = false;
                for (var i = 0; i < error.Path.Count; i++)
                {
                    current.IsExpanded = true;
                    if (current.Children.Count > error.Path[i])
                    {
                        current = current.Children[error.Path[i]];
                    }
                    else
                    {
                        fail = true;
                        break;
                    }
                }

                if (!fail)
                {
                    if (current != null) //should not happen...
                    {
                        BringSelectedIntoView(current);
                        current.IsSelected = true;
                    }
                }
                else
                {
                    MessageBox.Show("Referenced module is unable to be found in the current state of the model system.",
                        "Error Displaying Module", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        ///     Return
        /// </summary>
        /// <returns></returns>
        public bool HandleTabClose()
        {
            var value = !Session.CloseWillTerminate || !CanSaveModelSystem
                                                    || MessageBox.Show(
                                                        "The model system has not been saved, closing this window will discard the changes!",
                                                        "Are you sure?", MessageBoxButton.OKCancel,
                                                        MessageBoxImage.Question,
                                                        MessageBoxResult.Cancel) == MessageBoxResult.OK;
            if (value)
            {
                string error = null;
                if (!Session.Close(ref error))
                {
                    MessageBox.Show(error, "Failed to close the model system.", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            return value;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void SessionOnCommandExecuted(object sender, EventArgs eventArgs)
        {
            CanSaveModelSystem = _Session.HasChanged;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _Session_Saved(object sender, EventArgs e)
        {
            CanSaveModelSystem = false;
        }

        private void ProjectWasExternalSaved(object sender, EventArgs e)
        {
            // If the project was saved we need to reload in the new model system model
            Dispatcher.Invoke(() => { ModelSystem = _Session.ModelSystemModel; });
        }

        private bool CheckFilterRec(ModelSystemStructureDisplayModel module, string filterText,
            bool parentExpanded = true, bool parentVisible = false, bool parentPassed = false)
        {
            var children = module.Children;
            var thisParentPassed = module.Name.IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                                   module.Type != null &&
                                   module.Type.FullName.IndexOf(filterText,
                                       StringComparison.CurrentCultureIgnoreCase) >= 0;
            var childrenPassed = false;
            if (children != null)
            {
                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        if (CheckFilterRec(child, filterText, module.IsExpanded, thisParentPassed | parentVisible,
                            thisParentPassed | parentPassed))
                        {
                            childrenPassed = true;
                        }
                    }
                }
            }

            var show = thisParentPassed | childrenPassed | parentVisible;
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                module.IsExpanded = childrenPassed;
            }

            module.ModuleVisibility = thisParentPassed | childrenPassed | parentPassed
                ? Visibility.Visible
                : Visibility.Collapsed;
            return thisParentPassed | childrenPassed;
        }

        private UIElement GetCurrentlySelectedControl()
        {
            return GetCurrentlySelectedControl(DisplayRoot,
                ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel);
        }

        private void Run_RuntimeError(ErrorWithPath error)
        {
            Dispatcher.Invoke(() =>
            {
                ModuleValidationErrorListView.Items.Clear();
                ModuleRuntimeErrorListView.Items.Add(new ValidationErrorDisplayModel(DisplayRoot, error.Message,
                    error.Path));
                ParameterTabControl.SelectedIndex = 2;
                ModuleRuntimeValidationErrorListView.UpdateLayout();
            });
        }


        private void Run_RuntimeValidationError(List<ErrorWithPath> errorList)
        {
            Dispatcher.Invoke(() =>
            {
                ModuleValidationErrorListView.Items.Clear();
                foreach (var error in errorList)
                {
                    ModuleRuntimeValidationErrorListView.Items.Add(
                        new ValidationErrorDisplayModel(DisplayRoot, error.Message, error.Path));
                }

                ParameterTabControl.SelectedIndex = 2;
                ModuleRuntimeValidationErrorListView.UpdateLayout();
            });
        }

        private void Run_ValidationError(List<ErrorWithPath> errorList)
        {
            Dispatcher.Invoke(() =>
            {
                ModuleValidationErrorListView.Items.Clear();
                foreach (var error in errorList)
                {
                    ModuleValidationErrorListView.Items.Add(
                        new ValidationErrorDisplayModel(DisplayRoot, error.Message, error.Path));
                }

                ParameterTabControl.SelectedIndex = 2;
                ModuleValidationErrorListView.UpdateLayout();
            });
        }

        private UIElement GetCurrentlySelectedControl(ModelSystemStructureDisplayModel current,
            ModelSystemStructureDisplayModel lookingFor, TreeViewItem previous = null)
        {
            var children = current.Children;
            var container = (previous == null
                ? ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current)
                : previous.ItemContainerGenerator.ContainerFromItem(current)) as TreeViewItem;
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

        private void UsOnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.F5 && IsKeyboardFocusWithin && !LinkedParameterDisplayOverlay.IsVisible)
            {
                SaveCurrentlySelectedParameters();
                ExecuteRun();
            }
        }

        private void MDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.PreviewKeyDown -= UsOnPreviewKeyDown;
        }

        private void ModelSystemDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainWindow.Us.PreviewKeyDown += UsOnPreviewKeyDown;
                FilterBox.Focus();
            }));
            UpdateQuickParameters();
            EnumerateDisabled(ModuleDisplay.Items.GetItemAt(0) as ModelSystemStructureDisplayModel);
            ModuleContextControl.ModuleContextChanged += ModuleContextControlOnModuleContextChanged;
        }

        /// <summary>
        ///     Callback for when the Module Context control changes the active "selected module
        /// </summary>
        /// <param name="sender1"></param>
        /// <param name="eventArgs"></param>
        private void ModuleContextControlOnModuleContextChanged(object sender1, ModuleContextChangedEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                if (eventArgs.Module != null)
                {
                    ExpandToRoot(eventArgs.Module);
                    eventArgs.Module.IsSelected = true;
                    ModuleDisplay.Focus();
                    Keyboard.Focus(ModuleDisplay);
                }
            });
        }

        private void EnumerateDisabled(ModelSystemStructureDisplayModel model)
        {
            if (model.IsDisabled)
            {
                DisabledModules.Add(model);
            }

            if (model.Children != null)
            {
                foreach (var child in model.Children)
                {
                    EnumerateDisabled(child);
                }
            }
        }

        private void ModelSystemDisplay_ParametersChanged(object arg1, ParametersModel parameters)
        {
            UpdateParameters();
        }

        private bool FilterParameters(object arg1, string arg2)
        {
            return arg1 is ParameterDisplayModel parameter &&
                   (string.IsNullOrWhiteSpace(arg2) ||
                    parameter.Name.IndexOf(arg2, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void OnTreeExpanded(object sender, RoutedEventArgs e)
        {
            var tvi = (TreeViewItem) sender;
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
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel displayParameter)
            {
                displayParameter.QuickParameter = !displayParameter.QuickParameter;
            }
        }

        private void RecentLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject selected)
            {
                var currentMenu = ParameterTabControl.SelectedItem == QuickParameterTab
                    ? QuickParameterRecentLinkedParameters
                    : ParameterRecentLinkedParameters;
                if (currentMenu.ItemContainerGenerator.ItemFromContainer(selected) is LinkedParameterDisplayModel
                    selectedLinkedParameter)
                {
                    AddCurrentParameterToLinkedParameter(selectedLinkedParameter.LinkedParameter);
                    RecentLinkedParameters.RemoveAt(RecentLinkedParameters.IndexOf(selectedLinkedParameter));
                    RecentLinkedParameters.Insert(0, selectedLinkedParameter);
                }
            }
        }

        /// <summary>
        ///     Shows the Linked Parameter dialog
        /// </summary>
        /// <param name="assign"></param>
        private void ShowLinkedParameterDialog(bool assign = false)
        {
            var s = new LinkedParameterDisplay();
            LinkedParameterDisplayOverlay.LinkedParametersModel = ModelSystem.LinkedParameters;
            RunHost.DialogContent = LinkedParameterDisplayOverlay;
            object x = RunHost.ShowDialog(LinkedParameterDisplayOverlay, OpenedEventHandler);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void OpenedEventHandler(object sender, DialogOpenedEventArgs eventArgs)
        {
            LinkedParameterDisplayOverlay.DialogOpenedEventArgs = eventArgs;
            LinkedParameterDisplayOverlay.InitNewDisplay();
        }

        private bool AddCurrentParameterToLinkedParameter(LinkedParameterModel newLP)
        {
            var displayParameter = _selectedParameterDisplayModel;
            if (displayParameter != null)
            {
                string error = null;
                if (!displayParameter.AddToLinkedParameter(newLP, ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to set to Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
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
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                string error = null;
                if (!currentParameter.RemoveLinkedParameter(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to remove from Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
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
                if (CurrentlySelected.Any(c => c.BaseModel.ParentFieldType !=
                                               CurrentlySelected[0].BaseModel.ParentFieldType))
                {
                    MessageBox.Show(GetWindow(), "All selected modules must be for the same type.",
                        "Failed add module to collection", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var findReplacement = new ModuleTypeSelect(Session, CurrentlySelected[0].BaseModel)
                {
                    Owner = GetWindow()
                };
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
                                            MessageBox.Show(GetWindow(), error, "Failed add module to collection",
                                                MessageBoxButton.OK, MessageBoxImage.Error);
                                        }
                                    }
                                    else
                                    {
                                        selectedModule.Type = selectedType;
                                    }
                                }
                            });
                        CanSaveModelSystem = true;
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
                            MessageBox.Show(GetWindow(), error, "Failed to convert meta module.", MessageBoxButton.OK,
                                MessageBoxImage.Error);
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

        private ObservableCollection<ModelSystemStructureDisplayModel> CreateDisplayModel(
            ModelSystemStructureModel root)
        {
            var s = new ObservableCollection<ModelSystemStructureDisplayModel>
            {
                (DisplayRoot = new ModelSystemStructureDisplayModel(root, null, 0))
                
            };

          

            return s;
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled == false)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        if (EditorController.IsShiftDown() && EditorController.IsControlDown())
                        {
                            MoveCurrentModule(1);
                            e.Handled = true;
                        }

                        break;
                    case Key.Up:
                        if (EditorController.IsShiftDown() && EditorController.IsControlDown())
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
                if (EditorController.IsShiftDown() && EditorController.IsControlDown())
                {
                    switch (e.Key)
                    {
                        case Key.F:
                            if (ParameterTabControl.SelectedIndex == 0)
                            {
                                QuickParameterDialogHost.IsOpen = true;
                            }
                            else if (ParameterTabControl.SelectedIndex == 1)
                            {
                                ModuleParameterDialogHost.IsOpen = true;
                            }

                            break;
                    }
                }

                if (EditorController.IsControlDown())
                {
                    switch (e.Key)
                    {
                        case Key.M:
                            if (EditorController.IsAltDown())
                            {
                                SetMetaModuleStateForSelected(false);
                            }
                            else if (EditorController.IsShiftDown())
                            {
                                SetMetaModuleStateForSelected(true);
                            }
                            else
                            {
                                SelectReplacement();
                            }

                            e.Handled = true;
                            break;
                        case Key.R:
                            ParameterTabControl.SelectedIndex = 2;
                            //Mo.Focus();
                            // Keyboard.Focus(ParameterFilterBox);
                            e.Handled = true;
                            break;
                        case Key.P:
                            ParameterTabControl.SelectedIndex = 1;
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
                            ShowLinkedParameterDialog(true);
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
                            if (ModuleParameterTab.IsKeyboardFocusWithin)
                            {
                                SelectDirectoryForCurrentParameter();
                            }

                            if (ModuleDisplay.IsKeyboardFocusWithin)
                            {
                                ToggleDisableModule();
                            }

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
                        case Key.F2:
                            if (EditorController.IsShiftDown())
                            {
                                RenameDescription();
                            }
                            else
                            {
                                RenameParameter();
                            }

                            break;
                        case Key.F1:
                            ShowDocumentation();
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            if (ModuleDisplay.IsKeyboardFocusWithin)
                            {
                                RemoveSelectedModules();
                                e.Handled = true;
                            }

                            break;
                        case Key.F5:
                            e.Handled = true;
                            SaveCurrentlySelectedParameters();
                            ExecuteRun();

                            break;
                        case Key.Escape:
                            FilterBox.Box.Text = string.Empty;
                            break;
                    }
                }
            }
        }

        private bool ValidateName(string name)
        {
            return Project.ValidateProjectName(name);
        }

        /// <summary>
        /// </summary>
        /// <param name="executeNow"></param>
        public async void ExecuteRun(bool executeNow = true)
        {
            var runName = string.Empty;
            string error = null;
            var dialog = new SelectRunDateTimeDialog();
            try
            {
                var result = await dialog.ShowAsync(RunHost);
            }
            catch (Exception e)
            {
            }


            //LinkedParametersDialogHost.DialogContent = dialog;
            if (dialog.DidComplete)
            {
                runName = (dialog.DataContext as RunConfigurationDisplayModel)?.UserInput;
                var runQuestion = MessageBoxResult.Yes;
                if (Session.RunNameExists(runName))
                {
                    runQuestion = MessageBox.Show(
                        "This run name has been previously used. Do you wish to delete the previous output?",
                        "Run Name Already Exists", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                        MessageBoxResult.No);
                }

                if (runQuestion == MessageBoxResult.Yes || runQuestion == MessageBoxResult.No)
                {
                    var run = Session.Run(runName, ref error, runQuestion == MessageBoxResult.Yes ? true : false,
                        !dialog.IsQueueRun, false);

                    if (run != null)
                    {
                        ModuleValidationErrorListView.Items.Clear();
                        ModuleRuntimeValidationErrorListView.Items.Clear();
                        ModuleRuntimeErrorListView.Items.Clear();
                        MainWindow.Us.UpdateStatusDisplay("Running Model System");

                        //pass this as launchedFrom display in case model system run encounters an error
                        var runWindow = MainWindow.Us.CreateRunWindow(Session, run, runName, !dialog.IsQueueRun, this);
                        MainWindow.Us.AddRunToSchedulerWindow(runWindow);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Unable to start run.\r\n" + error,
                            "Unable to start run", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        private void ShowQuickParameters()
        {
            QuickParameterDisplay.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel
                .GetQuickParameters()
                .OrderBy(n => n.Name));
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ParameterTabControl.SelectedIndex = 0;
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
            Dispatcher.BeginInvoke(new Action(() => { RequestClose?.Invoke(this); }));
        }

        /// <summary>
        ///     Get permission from the user to close the window
        /// </summary>
        /// <returns>True if we have gained permission to close, false otherwise</returns>
        internal bool CloseRequested()
        {
            SaveCurrentlySelectedParameters();
            Dispatcher.Invoke(() => { MainWindow.ShowPageContaining(this); });
            var result = false;
            Dispatcher.Invoke(() =>
            {
                if (!Session.CloseWillTerminate || !Session.HasChanged
                                                || MessageBox.Show(
                                                    "The model system has not been saved, closing this window will discard the changes!",
                                                    "Are you sure?", MessageBoxButton.OKCancel,
                                                    MessageBoxImage.Question,
                                                    MessageBoxResult.Cancel) == MessageBoxResult.OK)
                {
                    result = true;
                }
            }, DispatcherPriority.Input);
            return result;
        }

        public event Action<object> RequestClose;


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
                    var be = textBox.GetBindingExpression(TextBox.TextProperty);
                    if (be != null)
                    {
                        be.UpdateSource();
                    }
                }
            }
        }

        public static T GetChildOfType<T>(DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null)
            {
                return null;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = child as T ?? GetChildOfType<T>(child);
                if (result != null)
                {
                    return result;
                }
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
                        var box = ParameterDisplay.IsKeyboardFocusWithin
                            ? ParameterFilterBox.Box
                            : QuickParameterFilterBox.Box;
                        if (box.Text == string.Empty)
                        {
                            e.Handled = false;
                        }
                        else
                        {
                            box.Text = string.Empty;
                            e.Handled = true;
                        }

                        break;
                    case Key.E:
                        if (ctrlDown)
                        {
                            ExpandParameterDocumentation(sender);
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
            // Change keyboard focus.
            if (Keyboard.FocusedElement is UIElement elementWithFocus)
            {
                elementWithFocus.MoveFocus(
                    new TraversalRequest(up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down));
            }
        }

        public void SaveRequested(bool saveAs)
        {
            string error = null;
            SaveCurrentlySelectedParameters();
            if (saveAs)
            {
                var sr = new StringRequest("Save Model System As?",
                    newName => { return Project.ValidateProjectName(newName); });
                if (sr.ShowDialog() == true)
                {
                    if (!Session.SaveAs(sr.Answer, ref error))
                    {
                        MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    ButtonProgressAssist.SetIsIndicatorVisible(SaveModelSystemButton, true);
                    ButtonProgressAssist.SetIsIndeterminate(SaveModelSystemButton, true);
                    ButtonProgressAssist.SetIndicatorBackground(SaveModelSystemButton,
                        (Brush) FindResource("MaterialDesignPaper"));
                    ButtonProgressAssist.SetIndicatorForeground(SaveModelSystemButton,
                        (Brush) FindResource("SecondaryAccentBrush"));
                    SaveModelSystemButton.Style = (Style) FindResource("MaterialDesignFloatingActionMiniDarkButton");
                });
                MainWindow.SetStatusText("Saving...");
                Task.Run(async () =>
                {
                    if (Session.SaveWait())
                    {
                        try
                        {
                            var watch = Stopwatch.StartNew();
                            if (!Session.Save(ref error))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }

                            watch.Stop();
                            var displayTimeRemaining = 1000 - (int) watch.ElapsedMilliseconds;
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
                                MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + e.Message, "Unable to Save",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            MainWindow.SetStatusText("Ready");
                            CanSaveModelSystem = false;
                            Session.SaveRelease();

                            Dispatcher.Invoke(() =>
                            {
                                StatusSnackBar.MessageQueue.Enqueue("Model system finished saving");
                                SaveModelSystemButton.Background = Brushes.Transparent;
                                SaveModelSystemButton.BorderBrush = Brushes.Transparent;
                                ButtonProgressAssist.SetIsIndicatorVisible(SaveModelSystemButton, false);
                                ButtonProgressAssist.SetIsIndeterminate(SaveModelSystemButton, false);
                                ButtonProgressAssist.SetIndicatorBackground(SaveModelSystemButton, Brushes.Transparent);
                                ButtonProgressAssist.SetIndicatorForeground(SaveModelSystemButton, Brushes.Transparent);
                                //SaveModelSystemButton.Style = (Style)FindResource("MaterialDesignFloatingActionMiniButton");
                            });
                        }
                    }
                });
            }
        }

        private ModelSystemStructureDisplayModel GetModelFor(ModelSystemStructureModel model)
        {
            bool IsContainedWithin(ModelSystemStructureModel current, ModelSystemStructureModel toFind)
            {
                if (current == toFind)
                {
                    return true;
                }

                foreach (var c in current.Children)
                {
                    if (IsContainedWithin(c, toFind))
                    {
                        return true;
                    }
                }

                return false;
            }

            ModelSystemStructureDisplayModel find(ModelSystemStructureDisplayModel current,
                ModelSystemStructureModel toFind)
            {
                if (current.BaseModel == toFind)
                {
                    return current;
                }

                if (current.IsMetaModule)
                {
                    return IsContainedWithin(current.BaseModel, toFind) ? current : null;
                }

                foreach (var c in current.Children)
                {
                    var ret = find(c, toFind);
                    if (ret != null)
                    {
                        return ret;
                    }
                }

                return null;
            }

            return find(DisplayRoot, model);
        }

        /// <summary>
        /// </summary>
        /// <param name="mss"></param>
        private void GoToModule(ModelSystemStructure mss)
        {
            var displayModel = GetModelFor(ModelSystem.GetModelFor(mss));
            Dispatcher.Invoke(() =>
            {
                CurrentlySelected.Clear();
                ExpandToRoot(displayModel);
                displayModel.IsSelected = true;
            });
        }

        private void GotoSelectedParameterModule()
        {
            if (ParameterTabControl.SelectedItem == QuickParameterTab &&
                QuickParameterDisplay.SelectedItem is ParameterDisplayModel currentParameter)
            {
                GoToModule((ModelSystemStructure) currentParameter.BelongsTo);
            }
        }

        private void ExpandParameterDocumentation(object sender)
        {
            var current = sender as DependencyObject;
            while (current != null)
            {
                if (current is StackPanel container)
                {
                    if (container.Children[0] is Expander exp)
                    {
                        exp.IsExpanded = !exp.IsExpanded;
                        return;
                    }
                }

                current = VisualTreeHelper.GetParent(current);
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
            var pasteText = Clipboard.GetText();
            var any = false;
            if (pasteText != null)
            {
                foreach (var selected in CurrentlySelected.ToList())
                {
                    string error = null;
                    if (!selected.Paste(Session, pasteText, ref error))
                    {
                        MessageBox.Show(MainWindow.Us, "Failed to Paste.\r\n" + error, "Unable to Paste",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    any = true;
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
            UpdateParameters();
        }

        /// <summary>
        /// </summary>
        private void UpdateParameters()
        {
            var parameters = GetActiveParameters();
            if (parameters != null)
            {
                Task.Factory.StartNew(() =>
                {
                    var source =
                        ParameterDisplayModel.CreateParameters(
                            parameters.OrderBy(el => el.Name).OrderBy(el => el.Index).OrderBy(el => el.IsHidden),
                            CurrentlySelected.Count > 1);
                    if (!MainWindow.Us.ShowMetaModuleHiddenParameters)
                    {
                        if (CurrentlySelected.Count == 1)
                        {
                            if (CurrentlySelected[0].BaseModel.IsMetaModule)
                            {
                                foreach (var s in source.Where(p => p.RealParameter.IsHidden).ToList())
                                {
                                    source.Remove(s);
                                }
                            }
                        }
                    }

                    Dispatcher.InvokeAsync(() =>
                    {
                        CleanUpParameters();
                        ParameterDisplay.ItemsSource = source;
                        ParameterFilterBox.Display = ParameterDisplay;
                        ParameterFilterBox.Filter = FilterParameters;
                        ParameterFilterBox.RefreshFilter();


                        var type = CurrentlySelected.Count == 1 ? CurrentlySelected[0].Type : null;
                        if (type != null)
                        {
                            SelectedName.Text = CurrentlySelected.Count == 1
                                ? CurrentlySelected[0].Name
                                : "Multiple Modules Selected";
                            SelectedNamespace.Text = type.FullName;
                            var attr =
                                (ModuleInformationAttribute)
                                Attribute.GetCustomAttribute(type, typeof(ModuleInformationAttribute));

                            if (attr != null)
                            {
                                SelectedDescription.Text = attr.Description;
                                SelectedDescription.Visibility = Visibility.Visible;
                                DescriptionExpander.Visibility = Visibility.Visible;
                                DescriptionExpander.IsExpanded = false;
                            }
                            else
                            {
                                SelectedDescription.Visibility = Visibility.Collapsed;
                                SelectedDescription.Text = "No description available.";
                                DescriptionExpander.Visibility = Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            SelectedName.Text = CurrentlySelected.Count > 1 ? "Multiple Selected" : "None Selected";
                            SelectedNamespace.Text = string.Empty;
                            SelectedDescription.Text = "No description available.";
                            SelectedDescription.Visibility = Visibility.Collapsed;
                            DescriptionExpander.Visibility = Visibility.Collapsed;
                        }

                        ParameterDisplay.Opacity = 1.0;
                    }, DispatcherPriority.Render);
                });
            }
            else
            {
                ParameterDisplay.ItemsSource = null;
                SelectedName.Text = "None Selected";
                SelectedNamespace.Text = string.Empty;
                SelectedDescription.Text = "No description available.";
                SelectedDescription.Visibility = Visibility.Collapsed;
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
            return CurrentlySelected.SelectMany(m => m.GetParameters()
                    .Where(p => allParameters.All(list => list.Any(q => p.Name == q.Name && p.Type == q.Type))))
                .ToList();
        }

        private void ModuleDisplay_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ModelSystemStructureDisplayModel module)
            {
                RefreshParameters();
                if (ParameterTabControl.SelectedIndex != 1)
                {
                    ParameterTabControl.SelectedIndex = 1;
                }

                //update the module context control
                ModuleContextControl.ActiveDisplayModule = (ModelSystemStructureDisplayModel) e.NewValue;
            }
        }

        private void Help_Clicked(object sender, RoutedEventArgs e)
        {
            ShowDocumentation();
        }

        private void ShowDocumentation()
        {
            if (ModuleDisplay.SelectedItem is ModelSystemStructureDisplayModel selectedModule)
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
            RenameSelectedModule();
        }

        private void Description_Clicked(object sender, RoutedEventArgs e)
        {
            RenameDescription();
        }

        private void ToggleDisableModule()
        {
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel)?.BaseModel;
            var selectedModuleControl = GetCurrentlySelectedControl();
            if (selectedModuleControl != null && selected != null)
            {
                string error = null;
                Session.ExecuteCombinedCommands(selected.IsDisabled ? "Enable Module" : "Disable Module", () =>
                {
                    foreach (var sel in CurrentlySelected)
                    {
                        if (!sel.SetDisabled(!sel.IsDisabled, ref error))
                        {
                            return;
                        }

                        if (sel.IsDisabled)
                        {
                            if (!DisabledModules.Contains(sel))
                            {
                                DisabledModules.Add(sel);
                            }
                        }
                    }
                });
                if (error != null)
                {
                    MessageBox.Show(MainWindow.Us, error,
                        selected.IsDisabled ? "Unable to Enable" : "Unable to Disable", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void RenameSelectedModule()
        {
            var selected = (ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel).BaseModel;
            var selectedModuleControl = GetCurrentlySelectedControl();
            if (selectedModuleControl != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(selectedModuleControl);
                var adorn = new TextboxAdorner("Rename", result =>
                {
                    string error = null;
                    Session.ExecuteCombinedCommands(
                        "Rename ModelSystem",
                        () =>
                        {
                            if (CurrentlySelected.Any(sel => !sel.BaseModel.SetName(result.Trim(), ref error)))
                            {
                                throw new Exception(error);
                            }
                        });
                }, selectedModuleControl, selected.Name, true);
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
                var adorn = new TextboxAdorner("Rename Description", result =>
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
                    SystemSounds.Asterisk.Play();
                    return;
                }

                var mul = deltaPosition < 0 ? 1 : -1;
                var moveOrder = CurrentlySelected
                    .Select((c, i) => new {Index = i, ParentIndex = parent.Children.IndexOf(c.BaseModel)})
                    .OrderBy(i => mul * i.ParentIndex);
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
                                SystemSounds.Asterisk.Play();
                                break;
                            }
                        }
                    });
                BringSelectedIntoView(CurrentlySelected[first.Index]);
            }
        }

        private void BringSelectedIntoView(ModelSystemStructureDisplayModel selected)
        {
            var ansestry = DisplayRoot.BuildChainTo(selected);
            if (ansestry != null)
            {
                var currentContainer = ModuleDisplay.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                for (var i = 1; i < ansestry.Count; i++)
                {
                    if (currentContainer != null)
                    {
                        if (i + 1 < ansestry.Count)
                        {
                            currentContainer =
                                currentContainer.ItemContainerGenerator.ContainerFromItem(ansestry[i]) as TreeViewItem;
                        }
                        else
                        {
                            currentContainer =
                                currentContainer.ItemContainerGenerator.ContainerFromItem(ansestry[i]) as TreeViewItem;
                            if (currentContainer != null)
                            {
                                currentContainer.BringIntoView();
                            }

                            return;
                        }
                    }
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
                            /* Re order the children from parent node */
                            /* Remove the module from selected items */
                            //CurrentlySelected.Remove(selected);
                            UpdateParameters();
                            Keyboard.Focus(ModuleDisplay);
                        }

                        string error = null;
                        if (!ModelSystem.Remove(selected.BaseModel, ref error))
                        {
                            SystemSounds.Asterisk.Play();
                        }
                        else
                        {
                            if (selected.IsCollection)
                            {
                                selected.Children.Clear();
                            }
                            else if (selected.Parent != null && !selected.Parent.IsCollection)
                            {
                            }
                            else if (selected.Parent != null)
                            {
                                var index = 0;
                                for (var i = 0; i < selected.Parent.Children.Count; i++)
                                {
                                    var sibling = selected.Parent.Children[i];
                                    if (sibling == selected)
                                    {
                                        selected.Parent.Children.RemoveAt(i);
                                        i = i - 1;
                                    }
                                    else
                                    {
                                        sibling.Index = index;
                                        index++;
                                    }
                                }
                            }

                            CanSaveModelSystem = true;
                        }
                    }
                });
        }

        private void CleanUpParameters()
        {
            // ParameterDisplay.BeginAnimation(OpacityProperty, null);
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
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                string error = null;
                if (!currentParameter.ResetToDefault(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to reset parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void CopyParameterName()
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                Clipboard.SetText(currentParameter.Name);
            }
        }

        private void SetCurrentParameterHidden(bool hidden)
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                string error = null;
                currentParameter.SetHidden(hidden, ref error);
            }
        }

        private void RenameParameter()
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                var selectedContainer =
                    (UIElement) ParameterDisplay.ItemContainerGenerator.ContainerFromItem(currentParameter);
                if (selectedContainer != null)
                {
                    var layer = AdornerLayer.GetAdornerLayer(selectedContainer);
                    var adorn = new TextboxAdorner("Rename", result =>
                    {
                        string error = null;
                        if (!currentParameter.SetName(result.Trim(), ref error))
                        {
                            MessageBox.Show(GetWindow(), error, "Unable to Set Parameter Name", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        else
                        {
                            RefreshParameters();
                        }
                    }, selectedContainer, currentParameter.GetBaseName(), true);
                    layer.Add(adorn);
                    adorn.Focus();
                }
            }
        }

        private void ResetParameterName()
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
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

            directory = GetInputDirectory(currentRoot, out var inputParameter);
            return inputParameter;
        }

        private void OpenParameterFileLocation(bool openWith, bool openDirectory)
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                    ? QuickParameterDisplay.SelectedItem
                    : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter &&
                ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel != null)
            {
                var inputParameter =
                    GetInputParameter((ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel).BaseModel,
                        out var inputDirectory);
                if (inputParameter != null)
                {
                    // Check to see if the parameter that contains the input directory IS this parameter
                    var isInputParameter = inputParameter == currentParameter.RealParameter;
                    var pathToFile = GetRelativePath(inputDirectory, currentParameter.Value, isInputParameter);
                    if (openDirectory)
                    {
                        pathToFile = Path.GetDirectoryName(pathToFile);
                    }

                    try
                    {
                        var toRun = new Process();
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
                        MessageBox.Show(GetWindow(), "Unable to load the file at '" + pathToFile + "'!",
                            "Unable to Load", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SelectDirectoryForCurrentParameter()
        {
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                var _ = GetInputParameter(
                    Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure),
                    out var inputDirectory);
                if (inputDirectory != null)
                {
                    var directoryName = MainWindow.OpenDirectory();
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
            if ((ParameterTabControl.SelectedItem == QuickParameterTab
                ? QuickParameterDisplay.SelectedItem
                : ParameterDisplay.SelectedItem) is ParameterDisplayModel currentParameter)
            {
                var _ = GetInputParameter(
                    Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure),
                    out var inputDirectory);
                if (inputDirectory != null)
                {
                    var fileName = MainWindow.OpenFile("Select File",
                        new[] {new KeyValuePair<string, string>("All Files", "*")}, true);
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
                Path.GetFullPath(
                    Path.Combine(Session.Configuration.ProjectDirectory, "AProject", "RunDirectory", inputDirectory)
                ) + Path.DirectorySeparatorChar;
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
                var parameterName = ((ParameterAttribute) attributes[0]).Name;
                var parameters = root.Parameters.GetParameters();
                for (var i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].Name == parameterName)
                    {
                        parameter = parameters[i];
                        return parameters[i].Value;
                    }
                }
            }

            parameter = null;
            return null;
        }

        private string GetRelativePath(string inputDirectory, string parameterValue, bool isInputParameter)
        {
            var parameterRooted = Path.IsPathRooted(parameterValue);
            var inputDirectoryRooted = Path.IsPathRooted(inputDirectory);
            if (parameterRooted)
            {
                return RemoveRelativeDirectories(parameterValue);
            }

            if (inputDirectoryRooted)
            {
                return RemoveRelativeDirectories(Path.Combine(inputDirectory, parameterValue));
            }

            return RemoveRelativeDirectories(Path.Combine(Session.Configuration.ProjectDirectory,
                Session.ProjectEditingSession.Name,
                "RunDirectory", inputDirectory, isInputParameter ? "" : parameterValue));
        }

        private string RemoveRelativeDirectories(string path)
        {
            var parts = path.Split('\\', '/');
            var finalPath = new StringBuilder();
            var currentlyOn = new Stack<string>();
            for (var i = 0; i < parts.Length; i++)
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
                    finalPath.Append(Path.DirectorySeparatorChar);
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
            //DisplayRoot.
            if (QuickParameterDisplay != null)
            {
                QuickParameterDisplay.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel
                    .GetQuickParameters()
                    .OrderBy(n => n.Name));
                QuickParameterFilterBox.Display = QuickParameterDisplay;
                QuickParameterFilterBox.Filter = FilterParameters;
                QuickParameterFilterBox.RefreshFilter();
            }
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as TreeViewItem;
        }

        private void DisplayButton_RightClicked(object obj)
        {
            if (obj is BorderIconButton button)
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

        /// <summary>
        /// </summary>
        /// <param name="treeView"></param>
        /// <see cref="http://stackoverflow.com/questions/1163801/wpf-treeview-with-multiple-selection" />
        public void AllowMultiSelection(TreeView treeView)
        {
            if (IsSelectionChangeActiveProperty == null)
            {
                return;
            }

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
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                    return;
                }

                var treeViewItem = VisualUpwardSearch(module);
                if (treeViewItem == null)
                {
                    return;
                }

                var disableMultiple = _disableMultipleSelectOnce;
                _disableMultipleSelectOnce = false;
                var currentItem = treeView.SelectedItem as ModelSystemStructureDisplayModel;
                // allow multiple selection
                // when control key is pressed
                if (!disableMultiple && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    // suppress selection change notification
                    // select all selected items
                    // then restore selection change notifications
                    var isSelectionChangeActive = IsSelectionChangeActiveProperty.GetValue(treeView, null);
                    IsSelectionChangeActiveProperty.SetValue(treeView, true, null);
                    selectedItems.ForEach(item => item.IsSelected =
                        item != treeViewItem || !selectedItems.Contains(treeViewItem));
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                }
                else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
                         CurrentlySelected.Count > 0)
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
                        var minIndex = Math.Min(lastSelectedIndex, currentSelectedIndex);
                        var maxIndex = Math.Max(lastSelectedIndex, currentSelectedIndex);
                        for (var i = minIndex; i <= maxIndex; i++)
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
                    IsSelectionChangeActiveProperty.SetValue(treeView, isSelectionChangeActive, null);
                    return;
                }
                else
                {
                    // deselect all selected items (current one will be re-added)
                    CurrentlySelected.Clear();
                    selectedItems.ForEach(item => item.IsSelected = item == treeViewItem);
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

        private void DisableModuleMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleDisableModule();
        }

        private void ModuleTreeViewItem_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ModuleTreeViewItem treeViewItem)
            {
                var menu = treeViewItem.ContextMenu;
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (menuItem.Name == "DisableModuleMenuItem")
                        {
                            if (treeViewItem.BackingModel.BaseModel.CanDisable)
                            {
                                menuItem.Header = treeViewItem.BackingModel.BaseModel.IsDisabled
                                    ? "Enable Module (Ctrl + D)"
                                    : "Disable Module (Ctrl + D)";
                            }
                            else
                            {
                                menuItem.IsEnabled = false;
                            }
                        }
                        else if (menuItem.Name == "ModuleMenuItem")
                        {
                            menuItem.Header = treeViewItem.BackingModel.BaseModel.IsCollection
                                ? "Add Module (Ctrl + M)"
                                : "Set Module (Ctrl + M)";
                        }
                    }
                }
            }
        }

        private ModelSystemStructureDisplayModel FindNextAncestor(ModelSystemStructureDisplayModel item)
        {
            if (item.Parent == null)
            {
                return item.Children != null && item.Children.Count > 0 ? item.Children[0] : item;
            }

            if (item.Index < item.Parent.Children.Count - 1)
            {
                return item.Parent.Children[item.Index + 1];
            }

            return FindNextAncestor(item.Parent);
        }

        private ModelSystemStructureDisplayModel FindMostExpandedItem(ModelSystemStructureDisplayModel item)
        {
            return !item.IsExpanded || item.Children == null || item.Children.Count == 0
                ? item
                : FindMostExpandedItem(item.Children[item.Children.Count - 1]);
        }

        private void ModuleDisplayNavigateDown(ModelSystemStructureDisplayModel item)
        {
            if (item.IsExpanded && item.Children != null && item.Children.Count > 0)
            {
                item.Children[0].IsSelected = true;
            }
            else
            {
                var toSelect = FindNextAncestor(item);
                if (item.Parent == toSelect.Parent && item.Index < item.Parent.Children.Count - 1
                    || item.Parent != toSelect.Parent)
                {
                    toSelect.IsSelected = true;
                }
            }
        }

        private void ModuleDisplayNavigateUp(ModelSystemStructureDisplayModel item)
        {
            // make sure we are not the root module
            if (item.Parent != null)
            {
                // if parent item has a single child
                if (item.Index == 0 || item.Parent.Children.Count == 1)
                {
                    item.Parent.IsSelected = true;
                }
                // if parent item has multiple children
                else if (item.Parent.Children.Count > 1)
                {
                    // find the most expanded "deepest" subchild of sibling element
                    var toSelect = FindMostExpandedItem(item.Parent.Children[item.Index - 1]);
                    toSelect.IsSelected = true;
                }
            }
        }

        private void ModuleDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var item = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
            e.Handled = false;
            switch (e.Key)
            {
                case Key.F2:
                    RenameSelectedModule();
                    break;
                case Key.Up:
                    ModuleDisplayNavigateUp(item);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ModuleDisplayNavigateDown(item);
                    e.Handled = true;
                    break;
            }
        }

        private void ParameterDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ParameterWidth = ParameterDisplay.ActualWidth - 24;
        }

        private void QuickParameterDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ParameterWidth = QuickParameterDisplay.ActualWidth - 24;
        }

        private void ValidationErrorDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ParameterWidth = ModuleValidationErrorListView.ActualWidth - 24;
        }

        private void GridCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ModuleDisplay.Focus();
        }

        private void LinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            ShowLinkedParameterDialog();
        }

        private void RunModelSystem_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentlySelectedParameters();
            ExecuteRun();
        }

        private void ParameterDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListView) sender).SelectedItem is ParameterDisplayModel s)
            {
                _selectedParameterDisplayModel = s;
            }
        }

        private void QuickParameterDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListView) sender).SelectedItem is ParameterDisplayModel s)
            {
                _selectedParameterDisplayModel = s;
            }
        }

        private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement keyboardFocus)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                var tRequest = new TraversalRequest(FocusNavigationDirection.Previous);
                if (Keyboard.FocusedElement is UIElement keyboardFocus)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                MoveFocusNext(e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift));
            }

            base.OnPreviewKeyDown(e);
        }

        private void ExpandModule(ModelSystemStructureDisplayModel module, bool collapse = true)
        {
            if (module != null)
            {
                var toProcess = new Queue<ModelSystemStructureDisplayModel>();
                toProcess.Enqueue(module);
                while (toProcess.Count > 0)
                {
                    module = toProcess.Dequeue();
                    module.IsExpanded = collapse;
                    foreach (var child in module.Children)
                    {
                        toProcess.Enqueue(child);
                    }
                }
            }
        }

        private void ExpandAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ModuleDisplay.SelectedItem != null)
            {
                if (ModuleDisplay.Items.Count > 0)
                {
                    ExpandModule((ModelSystemStructureDisplayModel) ModuleDisplay.SelectedItem);
                }
            }
        }

        private void CollapseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ModuleDisplay.SelectedItem != null)
            {
                if (ModuleDisplay.Items.Count > 0)
                {
                    ExpandModule((ModelSystemStructureDisplayModel) ModuleDisplay.SelectedItem, false);
                }
            }
        }

        private void ModelSystemInformation_EnableModuleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (((Label) sender).Tag is ModelSystemStructureDisplayModel module)
            {
                var error = string.Empty;
                module.SetDisabled(!module.IsDisabled, ref error);
                DisabledModulesList.InvalidateArrange();
            }
        }

        private void Path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (((Border) sender).Tag is ModelSystemStructureDisplayModel module)
            {
                DisabledModules.Remove(module);
            }
        }

        private void ValidationListModuleNameMouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as ValidationErrorListControl;
            if (label?.Tag is ModelSystemStructureDisplayModel model)
            {
                ExpandToRoot(model);
                model.IsSelected = true;
            }
        }

        private void ExpandToRoot(ModelSystemStructureDisplayModel module)
        {
            // don't expand the bottom node
            module = module?.Parent;
            while (module != null)
            {
                module.IsExpanded = true;
                module = module.Parent;
            }
        }

        private void ParameterTabControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            item.BringIntoView();
        }

        private void ModuleRuntimeValidationErrorListView_LostFocus(object sender, RoutedEventArgs e)
        {
            (sender as ListView).SelectedItem = null;
        }

        private void ModuleValidationErrorListView_LostFocus(object sender, RoutedEventArgs e)
        {
            (sender as ListView).SelectedItem = null;
        }

        private void GoToModule_Click(object sender, RoutedEventArgs e)
        {
            GotoSelectedParameterModule();
        }

        private void ModuleDisplay_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi)
            {
                tvi.BringIntoView();
            }
        }

        private void QuickParameterDialogHost_OnDialogOpened(object sender, DialogOpenedEventArgs eventargs)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                QuickParameterFilterBox.Focus();
                Keyboard.Focus(QuickParameterFilterBox);
            }));
        }

        /// <summary>
        ///     Click handler for save button / icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            SaveRequested(false);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventargs"></param>
        private void LinkedParametersDialogHost_OnDialogOpened(object sender, DialogOpenedEventArgs eventargs)
        {
            LinkedParameterDisplayOverlay.DialogOpenedEventArgs = eventargs;
            LinkedParameterDisplayOverlay.InitNewDisplay();
        }

        /// <summary>
        ///     Event handler for schedule model system run button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduleModuleSystemButton_OnClick(object sender, RoutedEventArgs e)
        {
            ExecuteRun(false);
        }

        private int GetNewIndex(ListView view, KeyEventArgs e)
        {
            var shift = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift);
            switch (e.Key)
            {
                case Key.Down:
                    return Math.Min(view.SelectedIndex + 1, view.Items.Count - 1);
                case Key.Enter:
                case Key.Tab:
                    return shift
                        ? Math.Max(view.SelectedIndex - 1, 0)
                        : Math.Min(view.SelectedIndex + 1, view.Items.Count - 1);
                case Key.Up:
                    return Math.Max(view.SelectedIndex - 1, 0);
                default:
                    return view.SelectedIndex;
            }
        }

        private void SelectParameterChildControl(UIElement selected)
        {
            var textbox = selected.FindChild<TextBox>("TextBox");
            if (textbox != null)
            {
                textbox.Focus();
                Keyboard.Focus(textbox);
            }
            else
            {
                var comboBox = selected.FindChild<ComboBox>("ComboBox");
                comboBox.Focus();
                Keyboard.Focus(comboBox);
            }
        }

        public void B_OnGotFocus(object sender, RoutedEventArgs e)
        {
            SelectParameterChildControl(sender as UIElement);
        }

        private void Parameter_GotFocus(object sender, RoutedEventArgs e)
        {
            var current = sender as UIElement;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current) as UIElement;
                if (current is ListViewItem lvi)
                {
                    SelectParameterChildControl(current);
                    return;
                }
            }
        }

        private void ProcessParameterDisplayKeyDown(ListView display, KeyEventArgs e)
        {
            var oldIndex = display.SelectedIndex;
            var newIndex = GetNewIndex(display, e);
            if (newIndex == oldIndex)
            {
                return;
            }

            if (Keyboard.FocusedElement is UIElement current)
            {
                current.MoveFocus(new TraversalRequest(oldIndex > newIndex
                    ? FocusNavigationDirection.Up
                    : FocusNavigationDirection.Down));
                if (Keyboard.FocusedElement is UIElement selected)
                {
                    SelectParameterChildControl(selected);
                }
            }
        }

        private void ProcessOnPreviewKeyboardForParameter(ListView view, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var item = ModuleDisplay.SelectedItem as ModelSystemStructureDisplayModel;
                if (e.Key == Key.Up)
                {
                    _disableMultipleSelectOnce = true;
                    ModuleDisplayNavigateUp(item);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        view.SelectedIndex = 0;
                        SelectParameterChildControl(
                            (UIElement) view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex));
                    }), DispatcherPriority.Input);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    _disableMultipleSelectOnce = true;
                    ModuleDisplayNavigateDown(item);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        view.SelectedIndex = 0;
                        SelectParameterChildControl(
                            (UIElement) view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex));
                    }));
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Tab || e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter)
            {
                ProcessParameterDisplayKeyDown(view, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void B_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            ProcessOnPreviewKeyboardForParameter(ParameterDisplay, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterDisplay_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            ProcessOnPreviewKeyboardForParameter(QuickParameterDisplay, e);
        }

        /// <summary>
        ///     When the module value textbox receives focus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParameterValueTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var current = sender as UIElement;
            if (current as TextBox != null)
            {
                (current as TextBox).SelectionStart = (current as TextBox).Text.Length;
            }

            while (current != null)
            {
                if (current is ListViewItem lvi)
                {
                    lvi.IsSelected = true;
                    return;
                }

                current = VisualTreeHelper.GetParent(current) as UIElement;
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     Called when module parameter text changes - makes session pseudo dirty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            CanSaveModelSystem = true;
        }

        /// <summary>
        ///     Called when an enumeration module parameter changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var parameterSource = (e.OriginalSource as ComboBox)?.Tag as ParameterDisplayModel;
            if (ParameterDisplay.Items.Contains(parameterSource) && _loadedOnce)
            {
                CanSaveModelSystem = true;
            }
            else
            {
                _loadedOnce = true;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenProjectFolderToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(Session.Configuration.ProjectDirectory, Session.ProjectEditingSession.Project.Name);
            Process.Start(path);
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterDialogHost_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ModuleParameterDialogHost.IsOpen = false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterDialogHost_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                QuickParameterDialogHost.IsOpen = false;
            }
        }

        private void ParameterDisplay_SourceUpdated(object sender, DataTransferEventArgs e)
        {
        }
    }
}