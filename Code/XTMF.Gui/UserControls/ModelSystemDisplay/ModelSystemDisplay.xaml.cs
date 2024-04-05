﻿/*
    Copyright 2014-2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Interfaces;
using XTMF.Gui.Models;
using XTMF.Gui.UserControls.Interfaces;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for ModelSystemDisplay.xaml
    /// </summary>
    public partial class ModelSystemDisplay : UserControl, ITabCloseListener, INotifyPropertyChanged, IResumableControl,
        IKeyShortcutHandler
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
                new PropertyMetadata(360.0));

        private static int FilterNumber;

        internal readonly List<ModelSystemStructureDisplayModel> CurrentlySelected = [];


        private readonly LinkedParameterDisplay LinkedParameterDisplayOverlay;

        private readonly BindingList<LinkedParameterDisplayModel> RecentLinkedParameters = [];

        private bool _canSaveModelSystem;

        private bool _loadedOnce;

        private ParameterDisplayModel _selectedParameterDisplayModel;

        private ModelSystemEditingSession _session;

        private ModelSystemStructureDisplayModel DisplayRoot;

        public Dictionary<ModelSystemStructure, ModelSystemStructureDisplayModel> ModelSystemDisplayModelMap;

        /// <summary>
        /// </summary>
        public ModelSystemDisplay()
        {
            //initialize sub displays for the model system
            TreeViewDisplay = new ModelSystemTreeViewDisplay(this);
            ActiveModelSystemView = TreeViewDisplay;
            DataContext = this;
            InitializeComponent();
            Loaded += ModelSystemDisplay_Loaded;
            DisabledModules = [];
            FilterBox.Filter = (o, text) =>
            {
                var module = o as ModelSystemStructureDisplayModel;
                Task.Run(() =>
                {
                    var ourNumber = Interlocked.Increment(ref FilterNumber);
                    var waitTask = Task.Delay(350);
                    waitTask.Wait();
                    Thread.MemoryBarrier();
                    if (ourNumber == FilterNumber)
                    {
                        // Lock the refreshing of the items until we have finished modifying everything
                        Dispatcher.Invoke(() =>
                        {
                            using var lockRender = this.TreeViewDisplay.ModuleDisplay.Items.DeferRefresh();
                            CheckFilterRec(module, text);
                        }, DispatcherPriority.Render);
                    }
                });
                return true;
            };
            LinkedParameterDisplayOverlay = new LinkedParameterDisplay();
            LinkedParameterDisplayOverlay.GoToModule += module =>
            {
                if (module != null)
                {
                    GoToModule((ModelSystemStructure)module);
                }
            };
            LinkedParameterDisplayOverlay.OnCloseDisplay += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    var newLP = LinkedParameterDisplayOverlay.SelectedLinkParameter;
                    if (LinkedParameterDisplayOverlay.IsAssignMode && AddCurrentParameterToLinkedParameter(newLP))
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
            ModelSystemDisplayModelMap = [];
            ToggleQuickParameterDisplay(0);
        }

        private ParameterDisplayModel SoftActiveParameterDisplay { get; set; }

        public IModelSystemView ActiveModelSystemView { get; set; }

        /// <summary>
        /// </summary>
        public ModelSystemTreeViewDisplay TreeViewDisplay { get; }

        /// <summary>
        /// </summary>
        public Brush QuickParameterToolBarForeground => QuickParameterDisplay2 == null
            ? (SolidColorBrush)TryFindResource("SecondaryHueMidBrush")
            : QuickParameterDisplay2.IsEnabled
                ? new SolidColorBrush
                {
                    Color = ((SolidColorBrush)TryFindResource("SecondaryHueMidBrush")).Color
                }
                : new SolidColorBrush
                {
                    Color = ((SolidColorBrush)TryFindResource("MaterialDesignBody")).Color
                };

        public Brush ModuleParameterToolBarForeground => ModuleParameterDisplay == null
            ? (SolidColorBrush)TryFindResource("SecondaryHueMidBrush")
            : new SolidColorBrush
            {
                Color = ((SolidColorBrush)TryFindResource(ModuleParameterDisplay.IsEnabled
                    ? "SecondaryHueMidBrush"
                    : "MaterialDesignBody")).Color
            };

        public bool CanRunModelSystem
        {
            get => (bool)GetValue(CanRunModelSystemDependencyProperty);
            set => SetValue(CanRunModelSystemDependencyProperty, value);
        }

        public string ContentGuid { get; set; }

        public double ParameterWidth
        {
            get => (double)GetValue(ParameterWidthDependencyProperty);
            set => SetValue(ParameterWidthDependencyProperty, value);
        }

        public ModelSystemEditingSession Session
        {
            get => _session;
            set
            {
                if (_session != null)
                {
                    _session.ProjectWasExternallySaved -= ProjectWasExternalSaved;
                    _session.CommandExecuted += SessionOnCommandExecuted;
                    _session.Saved += Session_Saved;
                }
                _session = value;
                ModelSystemEditingSessionChanged?.Invoke(this, new ModelSystemEditingSessionChangedEventArgs(_session));
                if (value != null)
                {
                    value.ProjectWasExternallySaved += ProjectWasExternalSaved;
                }
                CanRunModelSystem = _session.ProjectEditingSession != null;
            }
        }

        /// <summary>
        /// </summary>
        public bool CanSaveModelSystem
        {
            get => _canSaveModelSystem;
            set
            {
                if (_canSaveModelSystem != value)
                {
                    _canSaveModelSystem = value;
                    OnPropertyChanged(nameof(CanSaveModelSystem));
                }
            }
        }

        /// <summary>
        ///     The model system to display
        /// </summary>
        public ModelSystemModel ModelSystem
        {
            get => (ModelSystemModel)GetValue(ModelSystemProperty);
            set => SetValue(ModelSystemProperty, value);
        }

        public string ModelSystemName
        {
            get => (string)GetValue(ModelSystemNameProperty);
            private set => SetValue(ModelSystemNameProperty, value);
        }

        public ObservableCollection<ModelSystemStructureDisplayModel> DisabledModules { get; set; }

        /// <summary>
        /// </summary>
        public string DisabledModulesCountText => DisabledModules != null ? $"{DisabledModules.Count}" : "0";

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

                    // If we find a meta-module we actually have the correct module
                    if (current.IsMetaModule)
                    {
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
            string error = null;
            var value = Session.CloseWillTerminate && CanSaveModelSystem;
            if (value)
            {
                if (MessageBox.Show(
                        "The model system has not been saved, closing this window will discard the changes!",
                        "Are you sure?", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question,
                        MessageBoxResult.Cancel) == MessageBoxResult.OK)
                {
                    if (!Session.Close(ref error))
                    {
                        MessageBox.Show(error, "Failed to close the model system.", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    return true;
                }
                return false;
            }
            if (!Session.Close(ref error))
            {
                MessageBox.Show(error, "Failed to close the model system.", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            return true;
        }

        public event EventHandler<ModelSystemEditingSessionChangedEventArgs> ModelSystemEditingSessionChanged;

        /// <summary>
        /// </summary>
        /// <param name="count"></param>
        public void UpdateDisableModuleCount(int count)
        {
            StatusBarDisabledModulesText.Text = $"{count} disabled modules";
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock
            UpdateQuickParameters();
            ToggleModuleParameterDisplay(0);
            ToggleQuickParameterDisplay(0);
            DisabledModules.CollectionChanged += DisabledModulesOnCollectionChanged;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisabledModulesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<ModelSystemStructureDisplayModel> disabledModules)
            {
                UpdateDisableModuleCount(disabledModules.Count);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void SessionOnCommandExecuted(object sender, EventArgs eventArgs)
        {
            CanSaveModelSystem = _session.HasChanged;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Session_Saved(object sender, EventArgs e)
        {
            CanSaveModelSystem = false;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProjectWasExternalSaved(object sender, ProjectWasExternallySavedEventArgs e)
        {
            // If the project was saved we need to reload in the new model system model
            Dispatcher.Invoke(() =>
            {
                CurrentlySelected.Clear();
                ModelSystem = _session.ModelSystemModel;
                OnModelSystemChanged(this, e.ModelSystem);
                UpdateQuickParameters();
                UpdateParameters();
            });
        }

        /// <summary>
        /// </summary>
        /// <param name="module"></param>
        /// <param name="filterText"></param>
        /// <param name="parentExpanded"></param>
        /// <param name="parentVisible"></param>
        /// <param name="parentPassed"></param>
        /// <returns></returns>
        private bool CheckFilterRec(ModelSystemStructureDisplayModel module, string filterText,
            bool parentVisible = false, bool parentPassed = false)
        {
            var children = module.Children;
            var thisParentPassed = module.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)
                                    || (module.Type != null &&
                                        module.Type.FullName.Contains(filterText, StringComparison.CurrentCultureIgnoreCase))
                                    || (module.ParametersModel?.HasParameterContaining(filterText) ?? false);
            var childrenPassed = false;
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (CheckFilterRec(child, filterText, thisParentPassed | parentVisible,
                        thisParentPassed | parentPassed))
                    {
                        childrenPassed = true;
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                module.IsExpanded = childrenPassed;
            }
            module.ModuleVisibility = thisParentPassed | childrenPassed | parentPassed
                ? Visibility.Visible
                : Visibility.Collapsed;
            return thisParentPassed | childrenPassed;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public UIElement GetCurrentlySelectedControl()
        {
            return GetCurrentlySelectedControl(DisplayRoot,
                ActiveModelSystemView.SelectedModule);
        }

        /// <summary>
        /// </summary>
        /// <param name="current"></param>
        /// <param name="lookingFor"></param>
        /// <param name="previous"></param>
        /// <returns></returns>
        private UIElement GetCurrentlySelectedControl(ModelSystemStructureDisplayModel current,
            ModelSystemStructureDisplayModel lookingFor, TreeViewItem previous = null)
        {
            var children = current.Children;
            var container = (previous == null
                ? TreeViewDisplay.ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current)
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

        /// <summary>
        /// </summary>
        /// <param name="model"></param>
        private void EnumerateModules(ModelSystemStructureDisplayModel model)
        {
            ModelSystemDisplayModelMap.Add(model.BaseModel.RealModelSystemStructure, model);
            foreach (var m in model.Children)
            {
                EnumerateModules(m);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="model"></param>
        public void EnumerateDisabled(ModelSystemStructureDisplayModel model)
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

        private bool FilterParameters(object arg1, string arg2)
        {
            return arg1 is ParameterDisplayModel parameter &&
                   (string.IsNullOrWhiteSpace(arg2) ||
                    parameter.Name.Contains(arg2, StringComparison.InvariantCultureIgnoreCase));
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterBox.Focus();
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public Window GetWindow()
        {
            var current = this as DependencyObject;
            while (current is not null && current is not Window)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            // As a fallback, if there is somehow no window, just use the main one.
            return (current as Window) ?? MainWindow.Us;
        }

        /// <summary>
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="postToggleAction">An action to be performed after the display has been toggled.</param>
        public void ToggleQuickParameterDisplay(int duration = -1, Action postToggleAction = null)
        {
            var column = ContentDisplayGrid.ColumnDefinitions[2];
            QuickParameterDisplay2.IsEnabled = !QuickParameterDisplay2.IsEnabled;
            Dispatcher.Invoke(new Action(() =>
            {
                AnimateGridColumnWidth(column, column.ActualWidth,
                    QuickParameterDisplay2.IsEnabled ? 400 : 0, postToggleAction, duration);
            }));
        }

        /// <summary>
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="postToggleAction"></param>
        public void ToggleModuleParameterDisplay(int duration = -1, Action postToggleAction = null)
        {
            var column = ContentDisplayGrid.ColumnDefinitions[4];
            ModuleParameterDisplay.IsEnabled = !ModuleParameterDisplay.IsEnabled;
            Dispatcher.Invoke(new Action(() =>
            {
                AnimateGridColumnWidth(column,
                    column.ActualWidth,
                    ModuleParameterDisplay.IsEnabled ? 400 : 0, postToggleAction, duration);
            }));
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecentLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject selected)
            {
                var currentMenu = GetCurrentParameterDisplay() == ParameterDisplay
                    ? ParameterRecentLinkedParameters
                    : QuickParameterRecentLinkedParameters;

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
        internal async void ShowLinkedParameterDialog(bool assign = false)
        {
            LinkedParameterDisplayOverlay.LinkedParametersModel = ModelSystem.LinkedParameters;
            LinkedParameterDisplayOverlay.IsAssignMode = assign;
            await RootDialogHost.ShowDialog(LinkedParameterDisplayOverlay, OpenedEventHandler);
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

        /// <summary>
        /// </summary>
        /// <param name="newLP"></param>
        /// <returns></returns>
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

        /// <summary>
        /// </summary>
        /// <param name="displayParameter"></param>
        private void UpdateQuickParameterEquivalent(ParameterDisplayModel displayParameter)
        {
            if (displayParameter.QuickParameter)
            {
                QuickParameterListView.Items.Refresh();
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private ParameterDisplayModel GetCurrentParameterDisplayModelContext()
        {
            return (GetCurrentParameterDisplay().SelectedItem) as ParameterDisplayModel;
        }

        /// <summary>
        /// </summary>
        private void RemoveFromLinkedParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                string error = null;
                if (!currentParameter.RemoveLinkedParameter(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Failed to remove from Linked Parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                UpdateParameters();
                UpdateQuickParameterEquivalent(currentParameter);
            }
        }

        /// <summary>
        /// Sets, replaces, or adds a module to the currently selected module.
        /// </summary>
        internal async void SelectReplacement()
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
                var findReplacement = new ModuleTypeSelect(RootDialogHost, Session, CurrentlySelected[0].BaseModel);
                await findReplacement.ShowAsync();
                if (findReplacement.HasResult)
                {
                    var selectedType = findReplacement.SelectedType;
                    if (selectedType != null)
                    {
                        Session.ExecuteCombinedCommands(
                            "Set Module Types",
                            () =>
                            {
                                foreach (var selectedModule in CurrentlySelected.ToList())
                                {
                                    if (selectedModule.BaseModel.IsCollection)
                                    {
                                        string error = null;
                                        if (!selectedModule.BaseModel.AddCollectionMember(selectedType, ref error))
                                        {
                                            MessageBox.Show(GetWindow(), error, "Failed add module to collection",
                                                MessageBoxButton.OK, MessageBoxImage.Error);
                                        }
                                        else
                                        {
                                            var newlyAdded = selectedModule.Children.Last();
                                            newlyAdded.IsExpanded = true;
                                            GoToModule(newlyAdded);
                                        }
                                    }
                                    else
                                    {
                                        selectedModule.Type = selectedType;
                                        selectedModule.IsExpanded = true;
                                    }

                                    // selectedModule.BackingDisplayModel.PropertyChanged()
                                }

                            });
                        CanSaveModelSystem = true;
                        Dispatcher.Invoke(() =>
                        {
                            if (CurrentlySelected.Count == 1)
                            {
                                StatusBarModuleNameTextBlock.Text = $"{CurrentlySelected[0].BaseModel.Type} [{CurrentlySelected[0].Name}]";
                            }
                            else if (CurrentlySelected.Count > 1)
                            {
                                StatusBarModuleNameTextBlock.Text =
                                    $"{CurrentlySelected.Count} modules selected.";
                            }

                        });

                        RefreshParameters();
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        public void RefreshParameters()
        {
            UpdateParameters();
        }

        /// <summary>
        /// </summary>
        /// <param name="display"></param>
        /// <param name="newModelSystem"></param>
        private static void OnModelSystemChanged(ModelSystemDisplay display, ModelSystemModel newModelSystem)
        {
            var us = display;
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
                        us.QuickParameterListView.ContextMenu.DataContext = us;


                        //set the treeview to use regular model system items
                        us.ActiveModelSystemView.ViewItemsControl.ItemsSource = displayModel;
                        us.ModelSystemDisplayModelMap.Clear();
                        us.EnumerateModules(display.DisplayRoot);


                        us.ModelSystemName = newModelSystem.Name;
                        us.ActiveModelSystemView.ViewItemsControl.InvalidateVisual();

                        us.TreeViewDisplay.ModuleDisplay.Items.MoveCurrentToFirst();
                        us.FilterBox.Display = us.ActiveModelSystemView?.ViewItemsControl;

                        us.StatusBarModuleCountTextBlock.Text = $"{us.ModelSystemDisplayModelMap.Count} Modules";

                        us.DisabledModules.Clear();
                        us.EnumerateDisabled(display.DisplayRoot);
                        us.UpdateDisableModuleCount(us.DisabledModules.Count);


                        us.ParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;
                        us.QuickParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;
                    });
                });
            }
            else
            {
                us.ModelSystemName = "No model loaded";
                us.FilterBox.Display = null;
                us.ParameterLinkedParameterMenuItem.ItemsSource = null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private static void OnModelSystemChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            OnModelSystemChanged(source as ModelSystemDisplay, e.NewValue as ModelSystemModel);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private ObservableCollection<ModelSystemStructureDisplayModel> CreateDisplayModel(
            ModelSystemStructureModel root)
        {
            var s = new ObservableCollection<ModelSystemStructureDisplayModel>
            {
                (DisplayRoot = new ModelSystemStructureDisplayModel(root, null, 0))
            };
            DisplayRoot.IsExpanded = true;
            return s;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.PreviewKeyDown -= UsOnPreviewKeyDown;
            ModelSystemDisplayModelMap.Clear();
        }

        /// <summary>
        /// </summary>
        /// <param name="executeNow"></param>
        public async void ExecuteRun(bool executeNow = true)
        {
            string error = null;
            var dialog = new SelectRunDateTimeDialog(Session);
            try
            {
                var result = await dialog.ShowAsync(RootDialogHost);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            if (dialog.DidComplete)
            {
                string runName = (dialog.DataContext as RunConfigurationDisplayModel)?.UserInput;
                var delayedStartTime = (dialog.DataContext as RunConfigurationDisplayModel).ScheduleTime;
                var runQuestion = MessageBoxResult.Yes;

                if (!Session.IsValidRunName(runName))
                {
                    MessageBox.Show("You have entered an invalid run name.",
                        "Invalid run name entered",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (Session.RunNameExists(runName))
                {
                    runQuestion = MessageBox.Show(
                        "This run name has been previously used. Do you wish to delete the previous output?",
                        "Run Name Already Exists", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                        MessageBoxResult.No);

                }
                if (runQuestion == MessageBoxResult.Yes || runQuestion == MessageBoxResult.No)
                {
                    var isDelayed = (dialog.DataContext as RunConfigurationDisplayModel).SelectScheduleEnabled;
                    var run = Session.Run(runName, ref error, runQuestion == MessageBoxResult.Yes, !dialog.IsQueueRun,
                        false);
                    if (run != null)
                    {
                        if (isDelayed)
                        {
                            MainWindow.Us.AddDelayedRunToSchedulerWindow(
                                MainWindow.Us.CreateDelayedRunWindow(Session, run, runName, delayedStartTime, this,
                                    MainWindow.Us.SchedulerWindow)
                                , delayedStartTime);
                        }
                        else
                        {
                            MainWindow.Us.AddRunToSchedulerWindow(MainWindow.Us.CreateRunWindow(Session, run, runName,
                                !dialog.IsQueueRun, this, MainWindow.Us.SchedulerWindow));
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Unable to start run.\r\n" + error ?? "UNKNOWN ERROR",
                            "Unable to start run", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
            }
        }

        public void Redo()
        {
            string error = null;
            Session.Redo(ref error);
            UpdateParameters();
            UpdateQuickParameters();
        }

        public void Undo()
        {
            string error = null;
            Session.Undo(ref error);
            UpdateParameters();
            UpdateQuickParameters();
        }

        public void Close()
        {
            Dispatcher.BeginInvoke(new Action(() => { RequestClose?.Invoke(this); }));
        }

        public event Action<object> RequestClose;

        /// <summary>
        /// </summary>
        public void SaveCurrentlySelectedParameters()
        {
            if (ParameterDisplay.IsKeyboardFocusWithin)
            {
                SaveCurrentlySelectedParameters(ParameterDisplay);
            }
            else if (QuickParameterDisplay2.IsKeyboardFocusWithin)
            {
                SaveCurrentlySelectedParameters(QuickParameterListView);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parameterDisplay"></param>
        public static void SaveCurrentlySelectedParameters(ListView parameterDisplay)
        {
            var index = parameterDisplay.SelectedIndex;
            if (index >= 0)
            {
                var container = parameterDisplay.ItemContainerGenerator.ContainerFromIndex(index);
                var textBox = GetChildOfType<TextBox>(container);
                textBox?.GetBindingExpression(TextBox.TextProperty)
                        ?.UpdateSource();
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

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void HintedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
                        await RenameParameter();
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
                                //MoveCurrentModule(-1);
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
                                //MoveCurrentModule(1);
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
                    default:
                        e.Handled = false;
                        break;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="up"></param>
        internal static void MoveFocusNext(bool up)
        {
            // Change keyboard focus.
            if (Keyboard.FocusedElement is UIElement elementWithFocus)
            {
                elementWithFocus.MoveFocus(
                    new TraversalRequest(up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="saveAs"></param>
        public async void SaveRequested(bool saveAs)
        {
            string error = null;
            SaveCurrentlySelectedParameters();
            if (saveAs)
            {
                var dialog = new StringRequestDialog(RootDialogHost, "Save Model System As?", (newName) =>
                {
                    return Project.ValidateProjectName(newName);
                }, Session.Name);
                await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    if (!Session.SaveAs(dialog.UserInput, ref error))
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
                        (Brush)FindResource("MaterialDesignPaper"));
                    ButtonProgressAssist.SetIndicatorForeground(SaveModelSystemButton,
                        (Brush)FindResource("SecondaryHueMidBrush"));
                    SaveModelSystemButton.Style = (Style)FindResource("MaterialDesignFloatingActionMiniDarkButton");
                });
                if (Session.IsSaving())
                {
                    return;
                }
                MainWindow.SetStatusText("Saving...");
                await Task.Run(() =>
                {
                    if (Session.SaveWait())
                    {
                        try
                        {
                            if (!Session.Save(ref error))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(MainWindow.Us, "Failed to save.\r\n" + error, "Unable to Save",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
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
            GoToModule(ModelSystem.GetModelFor(mss));
        }

        /// <summary>
        /// </summary>
        /// <param name="mss"></param>
        private void GoToModule(ModelSystemStructureModel mss)
        {
            GoToModule(GetModelFor(mss));
        }

        /// <summary>
        /// </summary>
        /// <param name="displayModel"></param>
        private void GoToModule(ModelSystemStructureDisplayModel displayModel)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentlySelected.Clear();
                ModelSystemTreeViewDisplay.ExpandToRoot(displayModel);
                displayModel.IsSelected = true;
            });
        }

        /// <summary>
        /// </summary>
        private void GotoSelectedParameterModule()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                GoToModule((ModelSystemStructure)currentParameter.BelongsTo);
            }
        }

        private static void ExpandParameterDocumentation(object sender)
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

        /// <summary>
        /// </summary>
        public void CopyCurrentModule()
        {
            var selected = ActiveModelSystemView.SelectedModule;
            if (CurrentlySelected.Count == 1)
            {
                selected?.CopyModule();
            }
            else
            {
                ModelSystemStructureDisplayModel.CopyModules(CurrentlySelected);
            }
        }

        /// <summary>
        /// </summary>
        public void PasteCurrentModule()
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
                    Dispatcher.Invoke(() =>
                    {
                        ParameterDisplay.ItemsSource = source;
                        ParameterFilterBox.Display = ParameterDisplay;
                        ParameterFilterBox.Filter = FilterParameters;
                        var type = CurrentlySelected.Count == 1 ? CurrentlySelected[0].Type : null;
                        if (type != null)
                        {
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
                            //SelectedName.Text = CurrentlySelected.Count > 1 ? "Multiple Selected" : "None Selected";
                            //SelectedNamespace.Text = string.Empty;
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
                //SelectedName.Text = "None Selected";
                //SelectedNamespace.Text = string.Empty;
                SelectedDescription.Text = "No description available.";
                SelectedDescription.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private List<ParameterModel> GetActiveParameters()
        {
            return CurrentlySelected.Count switch
            {
                0 => null,
                1 => CurrentlySelected[0].GetParameters().ToList(),
                _ => GetParameterIntersection()
            };
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private List<ParameterModel> GetParameterIntersection()
        {
            var allParameters = CurrentlySelected.Select(m => m.GetParameters());
            return CurrentlySelected.SelectMany(m => m.GetParameters()
                    .Where(p => allParameters.All(list => list.Any(q => p.Name == q.Name && p.Type == q.Type))))
                .ToList();
        }

        /// <summary>
        ///     Displays documentation for the currently selected module.
        /// </summary>
        public void ShowDocumentation()
        {
            if (ActiveModelSystemView.SelectedModule is ModelSystemStructureDisplayModel selectedModule)
            {
                MainWindow.Us.LaunchHelpWindow(selectedModule.BaseModel);
            }
        }

        /// <summary>
        ///     Renames the selected module
        /// </summary>
        public async Task RenameSelectedModule()
        {
            var selected = ActiveModelSystemView.SelectedModule?.BaseModel;

            if (CurrentlySelected.Count == 0)
            {
                return;
            }
            else if (CurrentlySelected.Count == 1)
            {
                var dialog = new StringRequestDialog(RootDialogHost, CurrentlySelected.Count == 1 ? "Rename Module" : "Rename Modules", (value) => !String.IsNullOrWhiteSpace(value), selected.Name);
                _ = await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    string error = null;
                    Session.ExecuteCombinedCommands(
                        "Rename ModelSystem",
                        () =>
                        {
                            if (selected.SetName(dialog.UserInput.Trim(), ref error))
                            {
                                CanSaveModelSystem = true;
                            }
                            else
                            {
                                throw new Exception(error);
                            }
                        });
                }
            }
        }

        /// <summary>
        ///     Renames the selected module's description
        /// </summary>
        public async Task RenameDescription()
        {
            var selected = ActiveModelSystemView.SelectedModule?.BaseModel;
            if (CurrentlySelected.Count == 0)
            {
                return;
            }
            else if (CurrentlySelected.Count == 1)
            {
                var dialog = new StringRequestDialog(RootDialogHost, CurrentlySelected.Count == 1 ? "Change Description" : "Change Descriptions", (value) => true, selected.Description);
                _ = await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    string error = null;
                    Session.ExecuteCombinedCommands(
                        "Rename ModelSystem",
                        () =>
                        {
                            if (selected.SetDescription(dialog.UserInput.Trim(), ref error))
                            {
                                CanSaveModelSystem = true;
                            }
                            else
                            {
                                throw new Exception(error);
                            }
                        });
                }
            }
        }

        /// <summary>
        ///     Brings the specified module into view
        /// </summary>
        /// <param name="selected"></param>
        internal void BringSelectedIntoView(ModelSystemStructureDisplayModel selected)
        {
            var ancestry = DisplayRoot.BuildChainTo(selected);
            if (ancestry is not null)
            {
                var currentContainer = TreeViewDisplay.ModuleDisplay.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                for (var i = 1; i < ancestry.Count; i++)
                {
                    currentContainer =
                            currentContainer.ItemContainerGenerator.ContainerFromItem(ancestry[i]) as TreeViewItem;
                    // Make sure that we found the next element before letting it continue
                    if (currentContainer is null)
                    {
                        return;
                    }
                }
                currentContainer?.BringIntoView();
            }
        }

        /// <summary>
        /// </summary>
        internal void RemoveSelectedModules()
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
                                if (!first.IsCollection && parent.IsCollection)
                                {
                                    MoveFocusNext(parent.Children.IndexOf(first.BaseModel) >= parent.Children.Count - 1);
                                }
                            });
                            /* Re order the children from parent node */
                            /* Remove the module from selected items */
                            //CurrentlySelected.Remove(selected);
                            UpdateParameters();
                            Keyboard.Focus(ModelSystemDisplayContent);
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
                            else if (selected.Parent?.IsCollection == true)
                            {
                                var index = 0;
                                for (var i = 0; i < selected.Parent.Children.Count; i++)
                                {
                                    var sibling = selected.Parent.Children[i];
                                    if (sibling == selected)
                                    {
                                        selected.Parent.Children.RemoveAt(i);
                                        i--;
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

        private void AssignLinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            ShowLinkedParameterDialog(true);
        }

        private void RemoveLinkedParameters_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromLinkedParameter();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetParameter_Click(object sender, RoutedEventArgs e)
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                string error = null;
                if (!currentParameter.ResetToDefault(ref error))
                {
                    MessageBox.Show(GetWindow(), error, "Unable to reset parameter", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                else
                {
                    CanSaveModelSystem = true;
                }
            }
        }

        /// <summary>
        /// </summary>
        private void CopyParameterName()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                Clipboard.SetText(currentParameter.Name);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="hidden"></param>
        private void SetCurrentParameterHidden(bool hidden)
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                string error = null;
                currentParameter.SetHidden(hidden, ref error);
                CanSaveModelSystem = true;
            }
        }

        private void PasteExcelClipboard()
        {
            if (Clipboard.ContainsText())
            {
                static string[] GetEntries(string originalText)
                {
                    return originalText.Contains(Environment.NewLine) ?
                        originalText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                        : originalText.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                }
                // Get the number of records from the text in the clipboard
                var text = GetEntries(Clipboard.GetText().Trim());
                var currentParameterDisplay = GetCurrentParameterDisplay();
                var startingIndex = currentParameterDisplay.SelectedIndex;

                // Make sure there is enough room

                if(startingIndex + text.Length > currentParameterDisplay.Items.Count)
                {
                    MessageBox.Show(GetWindow(), "There is not enough space to paste all of the entries.");
                    return;
                }
                // If we have enough spaces, store to all of the values
                Session.ExecuteCombinedCommands("Paste List", () =>
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (!SetParameterValue(currentParameterDisplay.Items[startingIndex + i] as ParameterDisplayModel, text[i]))
                        {
                            // if there was an issue, abort (an error message has already been sent)
                            return;
                        }
                    }
                });
            }
            else
            {
                MessageBox.Show(GetWindow(), "There was no text data in the clipboard to copy.");
            }
        }

        /// <summary>
        /// </summary>
        private async Task RenameParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var dialog = new StringRequestDialog(RootDialogHost, "Rename Parameter", (value) => !String.IsNullOrWhiteSpace(value), currentParameter.Name);
                await dialog.ShowAsync();
                if (dialog.DidComplete)
                {
                    string error = null;
                    if (!currentParameter.SetName(dialog.UserInput.Trim(), ref error))
                    {
                        MessageBox.Show(GetWindow(), error, "Unable to Set Parameter Name", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    else
                    {
                        RefreshParameters();
                        UpdateQuickParameters();
                        CanSaveModelSystem = true;
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        private void ResetParameterName()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                string error = null;
                currentParameter.RevertNameToDefault(ref error);
                UpdateParameters();
                CanSaveModelSystem = true;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="currentRoot"></param>
        /// <param name="directory"></param>
        /// <returns></returns>
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
                    return null;
                }
                previousRoot = currentRoot;
            } while (true);
            directory = GetInputDirectory(currentRoot, out var inputParameter);
            return inputParameter;
        }

        /// <summary>
        /// </summary>
        /// <param name="openWith"></param>
        /// <param name="openDirectory"></param>
        private void OpenParameterFileLocation(bool openWith, bool openDirectory)
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var inputParameter =
                    GetInputParameter((ActiveModelSystemView.SelectedModule ?? DisplayRoot).BaseModel,
                        out var inputDirectory);
                if (inputParameter is not null)
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
                            toRun.StartInfo.UseShellExecute = true;
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

        /// <summary>
        /// </summary>
        private void SelectDirectoryForCurrentParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var _ = GetInputParameter(
                    Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure),
                    out var inputDirectory);
                if (inputDirectory is not null)
                {
                    var directoryName = MainWindow.OpenDirectory();
                    if (directoryName is null)
                    {
                        return;
                    }

                    TransformToRelativePath(inputDirectory, ref directoryName);
                    SetParameterValue(currentParameter, directoryName);
                }
            }
        }

        /// <summary>
        /// </summary>
        private void SelectFileForCurrentParameter()
        {
            var context = GetCurrentParameterDisplayModelContext() ?? SoftActiveParameterDisplay;
            if (context is not null)
            {
                var currentParameter = context;
                var _ = GetInputParameter(
                    Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure),
                    out var inputDirectory);
                if (inputDirectory is not null)
                {
                    var fileName = MainWindow.OpenFile("Select File",
                        new[] { new KeyValuePair<string, string>("All Files", "*") }, true);
                    if (fileName is null)
                    {
                        return;
                    }
                    TransformToRelativePath(inputDirectory, ref fileName);
                    SetParameterValue(currentParameter, fileName);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="inputDirectory"></param>
        /// <param name="fileName"></param>
        private void TransformToRelativePath(string inputDirectory, ref string fileName)
        {
            var runtimeInputDirectory =
                Path.GetFullPath(
                    Path.Combine(Session.Configuration.ProjectDirectory, "AProject", "RunDirectory", inputDirectory)
                ) + Path.DirectorySeparatorChar;
            if (fileName.StartsWith(runtimeInputDirectory))
            {
                fileName = fileName[runtimeInputDirectory.Length..];
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="root"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private static string GetInputDirectory(ModelSystemStructureModel root, out ParameterModel parameter)
        {
            var inputDir = root.Type.GetProperty("InputBaseDirectory");
            var attributes = inputDir.GetCustomAttributes(typeof(ParameterAttribute), true);
            if (attributes != null && attributes.Length > 0)
            {
                var parameterName = ((ParameterAttribute)attributes[0]).Name;
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

        /// <summary>
        /// </summary>
        /// <param name="inputDirectory"></param>
        /// <param name="parameterValue"></param>
        /// <param name="isInputParameter"></param>
        /// <returns></returns>
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

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string RemoveRelativeDirectories(string path)
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

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            SelectFileForCurrentParameter();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            SelectDirectoryForCurrentParameter();
        }

        /// <summary>
        /// </summary>
        public void UpdateQuickParameters()
        {
            if (QuickParameterDisplay2 != null)
            {
                QuickParameterListView.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel
                    .GetQuickParameters()
                    .OrderBy(n => n.Name));
                QuickParameterFilterBox.Display = QuickParameterListView;
                QuickParameterFilterBox.Filter = FilterParameters;
                QuickParameterFilterBox.RefreshFilter();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RenameParameter_Click(object sender, RoutedEventArgs e)
        {
            await RenameParameter();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetParameterName_Click(object sender, RoutedEventArgs e)
        {
            ResetParameterName();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideParameter_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentParameterHidden(true);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowParameter_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentParameterHidden(false);
        }

        private void PasteSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            PasteExcelClipboard();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            ShowLinkedParameterDialog();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunModelSystem_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentlySelectedParameters();
            ExecuteRun();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            OnPreviewKeyDown(e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemInformation_EnableModuleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (((Label)sender).Tag is ModelSystemStructureDisplayModel module)
            {
                var error = string.Empty;
                module.SetDisabled(!module.IsDisabled, ref error);
                //DisabledModulesList.InvalidateArrange();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (((Border)sender).Tag is ModelSystemStructureDisplayModel module)
            {
                DisabledModules.Remove(module);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToModule_Click(object sender, RoutedEventArgs e)
        {
            GotoSelectedParameterModule();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventargs"></param>
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
        /// </summary>
        /// <param name="view"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private static int GetNewIndex(ListView view, KeyEventArgs e)
        {
            var shift = e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift);
            return e.Key switch
            {
                Key.Down => Math.Min(view.SelectedIndex + 1, view.Items.Count - 1),
                Key.Enter or Key.Tab => shift
                    ? Math.Max(view.SelectedIndex - 1, 0)
                    : Math.Min(view.SelectedIndex + 1, view.Items.Count - 1),
                Key.Up => Math.Max(view.SelectedIndex - 1, 0),
                _ => view.SelectedIndex
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="selected"></param>
        private static void SelectParameterChildControl(UIElement selected)
        {
            var textbox = selected.FindChild<TextBox>("StandardParameterTemplateTextBox");
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

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void B_OnGotFocus(object sender, RoutedEventArgs e)
        {
            SelectParameterChildControl(sender as UIElement);
        }

        /// <summary>
        /// </summary>
        /// <param name="display"></param>
        /// <param name="e"></param>
        private static void ProcessParameterDisplayKeyDown(ListView display, KeyEventArgs e)
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

        /// <summary>
        /// </summary>
        /// <param name="view"></param>
        /// <param name="e"></param>
        private void ProcessOnPreviewKeyboardForParameter(ListView view, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var item = ActiveModelSystemView.SelectedModule;
                if (e.Key == Key.Up)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        view.SelectedIndex = 0;
                        SelectParameterChildControl(
                            (UIElement)view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex));
                    }), DispatcherPriority.Input);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        view.SelectedIndex = 0;
                        SelectParameterChildControl(
                            (UIElement)view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex));
                    }));
                    e.Handled = true;
                }
                else if (e.Key == Key.T)
                {
                    GetCurrentParameterDisplayModelContext().QuickParameter =
                        !GetCurrentParameterDisplayModelContext().QuickParameter;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Tab || e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter)
            {
                var parameter = GetCurrentParameterDisplayModelContext();
                if (e.Source is TextBox tb)
                {
                    if (!SetParameterValue(parameter, tb?.Text))
                    {
                        tb.Text = parameter.Value;
                    }
                }
                else if (e.Source is ComboBox cb)
                {
                    if (!SetParameterValue(GetCurrentParameterDisplayModelContext(), cb?.Text))
                    {
                        cb.Text = parameter.Value;
                    }
                }
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
            var context = GetCurrentParameterDisplay();
            //test for null but should not be possible
            if (context != null)
            {
                ProcessOnPreviewKeyboardForParameter(context, e);
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private ListView GetCurrentParameterDisplay()
        {
            if (QuickParameterListView.IsKeyboardFocusWithin)
            {
                return QuickParameterListView;
            }
            if (ParameterDisplay.IsKeyboardFocusWithin)
            {
                return ParameterDisplay;
            }
            return null;
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
                CanSaveModelSystem = _session.HasChanged;
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
            Process.Start(new ProcessStartInfo() { FileName = path, UseShellExecute = true });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReloadModelSystemToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadModelSystem();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ReloadModelSystem()
        {
            string error = null;
            if (!Session.ReloadModelSystem(ref error))
            {
                MessageBox.Show(GetWindow(), error, "Unable to revert", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            EnumerateDisabled(DisplayRoot);
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as TextBox).DataContext as ParameterDisplayModel != null)
            {
                if (!SetParameterValue((sender as TextBox).DataContext as ParameterDisplayModel,
                    (sender as TextBox).Text))
                {
                    (sender as TextBox).Text = ((sender as TextBox).DataContext as ParameterDisplayModel).Value;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((sender as TextBox).DataContext as ParameterDisplayModel != null)
            {
                if (!SetParameterValue((sender as TextBox).DataContext as ParameterDisplayModel,
                    (sender as TextBox).Text))
                {
                    (sender as TextBox).Text = ((sender as TextBox).DataContext as ParameterDisplayModel).Value;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SetParameterValue(ParameterDisplayModel parameter, string value)
        {
            if (!parameter.SetValue(value, out var error))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    MessageBox.Show(GetWindow(),
                        "We were unable to set the parameter '" + parameter.Name ?? "" + "' with the value '" + value +
                        "'.\r\n" + error, "Unable to Set Parameter",
                        MessageBoxButton.OK, MessageBoxImage.Error)));
                return false;
            }
            return true;
        }


        /// <summary>
        /// </summary>
        /// <param name="column"></param>
        /// <param name="display"></param>
        /// <param name="fromWidth"></param>
        /// <param name="toWidth"></param>
        /// <param name="postAnimateAction"></param>
        /// <param name="durationMs"></param>
        private void AnimateGridColumnWidth(ColumnDefinition column, double fromWidth,
            double toWidth, Action postAnimateAction = null, int durationMs = -1)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs >= 0 ? durationMs : 200));
            var animation = new DoubleAnimation
            {
                //animation.EasingFunction = ease;
                Duration = duration,
                From = fromWidth,
                To = toWidth
            };
            Storyboard.SetTarget(animation, column);
            Storyboard.SetTargetName(animation, column.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(ColumnDefinition.MaxWidthProperty));
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            animation.Completed += delegate
            {
                OnPropertyChanged(nameof(QuickParameterToolBarForeground));
                OnPropertyChanged(nameof(ModuleParameterToolBarForeground));
                postAnimateAction?.Invoke();
            };
            storyboard.Begin(this);
        }


        /// <summary>
        /// </summary>
        /// <param name="element"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private void AnimateOpacity(FrameworkElement element, double from, double to, UIElement focusAfter = null)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(200));
            var animation = new DoubleAnimation
            {
                //animation.EasingFunction = ease;
                Duration = duration,
                From = from,
                To = to
            };
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetName(animation, element.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            animation.Completed += delegate
            {
                if (element.Opacity == 0.0)
                {
                    element.Visibility = Visibility.Collapsed;
                }
                else
                {
                    element.Visibility = Visibility.Visible;

                    if (focusAfter != null)
                    {
                        focusAfter.Focus();
                        Keyboard.Focus(focusAfter);
                    }
                }
            };
            storyboard.Begin(this);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { UpdateQuickParameters(); }));
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { UpdateQuickParameters(); }));
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
        }


        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MDisplay_MouseMove(object sender, MouseEventArgs e2)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="modules"></param>
        private void EnumerateUnassignedRequiredModules(ModelSystemStructureDisplayModel model, List<ModelSystemStructureDisplayModel> modules)
        {
            if (model.BaseModel.Type == null && model.BaseModel.IsOptional == false && !model.IsCollection)
            {
                modules.Add(model);
            }
            if (model.Children != null)
            {
                foreach (var c in model.Children)
                {
                    EnumerateUnassignedRequiredModules(c, modules);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
        }
    }


    /// <summary>
    /// </summary>
    public class ModelSystemEditingSessionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        public ModelSystemEditingSessionChangedEventArgs(ModelSystemEditingSession session)
        {
            Session = session;
        }

        public ModelSystemEditingSession Session { get; }
    }

    /// <summary>
    /// </summary>
    public class SelectedModuleParameterContextChangedEventArgs : EventArgs
    {
        /// <summary>
        /// </summary>
        /// <param name="session"></param>
        public SelectedModuleParameterContextChangedEventArgs(ModelSystemEditingSession session)
        {
            Session = session;
        }

        public ModelSystemEditingSession Session { get; }
    }
}
