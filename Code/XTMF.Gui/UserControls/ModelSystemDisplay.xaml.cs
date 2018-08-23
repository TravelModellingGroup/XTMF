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
using System.Collections.Specialized;
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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Controllers;
using XTMF.Gui.Helpers;
using XTMF.Gui.Interfaces;
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
                new PropertyMetadata(360.0));

        private static int FilterNumber;



        internal readonly List<ModelSystemStructureDisplayModel> CurrentlySelected =
            new List<ModelSystemStructureDisplayModel>();

        private readonly BindingList<LinkedParameterDisplayModel> RecentLinkedParameters =
            new BindingList<LinkedParameterDisplayModel>();

        private bool _canSaveModelSystem;

        private bool _disableMultipleSelectOnce;

        private bool _loadedOnce;

        private Semaphore _saveSemaphor;

        private ParameterDisplayModel _selectedParameterDisplayModel;

        private ModelSystemEditingSession _session;

        private ModelSystemStructureDisplayModel DisplayRoot;

        private readonly LinkedParameterDisplay LinkedParameterDisplayOverlay;

        private object SaveLock = new object();

        private readonly ModelSystemRegionViewDisplay _regionViewDisplay;

        private readonly ModelSystemTreeViewDisplay _treeViewDisplay;

        private IModelSystemView _activeModelSystemView;

        public event EventHandler<ModelSystemEditingSessionChangedEventArgs> ModelSystemEditingSessionChanged;

        public event EventHandler<SelectedModuleParameterContextChangedEventArgs> SelectedModuleParameterContextChanged;

        public string StatusBarModuleCountText
        {
            get
            {
                var s = $"{(_treeViewDisplay != null ? this._treeViewDisplay.ModuleDisplay.Items.Count : 0)} modules";
                return s;
            }
        }

        public IModelSystemView ActiveModelSystemView
        {
            get => this._activeModelSystemView;
            set => this._activeModelSystemView = value;

        }

        /// <summary>
        /// 
        /// </summary>
        public ModelSystemTreeViewDisplay TreeViewDisplay
        {
            get { return this._treeViewDisplay; }
        }

        /// <summary>
        /// 
        /// </summary>
        public Brush QuickParameterToolBarForeground => QuickParameterDisplay2 == null ? (SolidColorBrush)TryFindResource("SecondaryAccentBrush") : (
            QuickParameterDisplay2.IsEnabled
                ? new SolidColorBrush()
                {
                    Color = ((SolidColorBrush)TryFindResource("SecondaryAccentBrush")).Color,

                }
                : new SolidColorBrush()
                {
                    Color = ((SolidColorBrush)TryFindResource("MaterialDesignBody")).Color,
                });

        public Brush ModuleParameterToolBarForeground => ModuleParameterDisplay == null ? (SolidColorBrush)TryFindResource("SecondaryAccentBrush") : (
            ModuleParameterDisplay.IsEnabled
            ? new SolidColorBrush()
            {
                Color = ((SolidColorBrush)TryFindResource("SecondaryAccentBrush")).Color,

            }
            : new SolidColorBrush()
            {
                Color = ((SolidColorBrush)TryFindResource("MaterialDesignBody")).Color,
            });


        /// <summary>
        /// 
        /// </summary>
        public ModelSystemDisplay()
        {
            _saveSemaphor = new Semaphore(1, 1);
            DataContext = this;
            InitializeComponent();
            //AllowMultiSelection(ModuleDisplay);
            Loaded += ModelSystemDisplay_Loaded;

            DisabledModules = new ObservableCollection<ModelSystemStructureDisplayModel>();
            //DisabledModulesList.ItemsSource = DisabledModules;
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
                    GoToModule((ModelSystemStructure)module);
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

            //initialize sub displays for the model system
            this._regionViewDisplay = new ModelSystemRegionViewDisplay(this);
            this._treeViewDisplay = new ModelSystemTreeViewDisplay(this);


            this.ActiveModelSystemView = this._treeViewDisplay;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            // This needs to be executed via the dispatcher to avoid an issue with AvalonDock

            UpdateQuickParameters();
            this.ToggleModuleParameterDisplay(0);
            


        }


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
                    _session.Saved += _Session_Saved;
                }

                _session = value;
                this.ModelSystemEditingSessionChanged?.Invoke(this, new ModelSystemEditingSessionChangedEventArgs(_session));
                if (value != null)
                {
                    value.ProjectWasExternallySaved += ProjectWasExternalSaved;
                }

                CanRunModelSystem = _session.ProjectEditingSession != null;
            }
        }

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
                    if(current.IsMetaModule)
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
            var value = Session.CloseWillTerminate && CanSaveModelSystem;
            if (value)
            {
                if (MessageBox.Show("The model system has not been saved, closing this window will discard the changes!",
                                         "Are you sure?", MessageBoxButton.OKCancel,
                                         MessageBoxImage.Question,
                                         MessageBoxResult.Cancel) == MessageBoxResult.OK)
                {
                    string error = null;
                    if (!Session.Close(ref error))
                    {
                        MessageBox.Show(error, "Failed to close the model system.", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    return true;
                }
                return false;
            }
            else
            {
                string error = null;
                if (!Session.Close(ref error))
                {
                    MessageBox.Show(error, "Failed to close the model system.", MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                }
                return true;
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
        private void _Session_Saved(object sender, EventArgs e)
        {
            CanSaveModelSystem = false;
        }

        private void ProjectWasExternalSaved(object sender, EventArgs e)
        {
            // If the project was saved we need to reload in the new model system model
            Dispatcher.Invoke(() => { ModelSystem = _session.ModelSystemModel; });
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public UIElement GetCurrentlySelectedControl()
        {
            return GetCurrentlySelectedControl(DisplayRoot,
                ActiveModelSystemView.SelectedModule);
        }

        private void Run_RuntimeError(ErrorWithPath error)
        {
            Dispatcher.Invoke(() =>
            {
                //ModuleValidationErrorListView.Items.Clear();
                //ModuleRuntimeErrorListView.Items.Add(new ValidationErrorDisplayModel(DisplayRoot, error.Message,
                //    error.Path));
                //ParameterTabControl.SelectedIndex = 2;
                //ModuleRuntimeValidationErrorListView.UpdateLayout();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorList"></param>
        private void Run_RuntimeValidationError(List<ErrorWithPath> errorList)
        {
            Dispatcher.Invoke(() => { });
            /*ModuleValidationErrorListView.Items.Clear();
            foreach (var error in errorList)
            {
                ModuleRuntimeValidationErrorListView.Items.Add(
                    new ValidationErrorDisplayModel(DisplayRoot, error.Message, error.Path));
            }

            //ParameterTabControl.SelectedIndex = 2;
            ModuleRuntimeValidationErrorListView.UpdateLayout();
        }); */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorList"></param>
        private void Run_ValidationError(List<ErrorWithPath> errorList)
        {
            Dispatcher.Invoke(() => { });
            /*ModuleValidationErrorListView.Items.Clear();
            foreach (var error in errorList)
            {
                ModuleValidationErrorListView.Items.Add(
                    new ValidationErrorDisplayModel(DisplayRoot, error.Message, error.Path));
            }

            //ParameterTabControl.SelectedIndex = 2;
            ModuleValidationErrorListView.UpdateLayout();
        }); */
        }


        /// <summary>
        /// 
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
                ? this._treeViewDisplay.ModuleDisplay.ItemContainerGenerator.ContainerFromItem(current)
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
        /// 
        /// </summary>
        public string DisabledModulesCountText
        {
            get
            {
                if (DisabledModules != null)
                {
                    return $"{DisabledModules.Count}";


                }

                return "0";


            }
        }

        /// <summary>
        /// Initiates a property changed update for the DisabledModules count (text)
        /// </summary>
        public void UpdateDisabledModules()
        {
            this.OnPropertyChanged(this.DisabledModulesCountText);



        }


        /// <summary>
        /// 
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
            var tvi = (TreeViewItem)sender;
            e.Handled = true;
            FilterBox.RefreshFilter();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            FilterBox.Focus();
        }

        public Window GetWindow()
        {
            var current = this as DependencyObject;
            while (current != null && !(current is Window))
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current as Window;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ToggleQuickParameterDisplay()
        {
            var column = ContentDisplayGrid.ColumnDefinitions[2];
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.AnimateGridColumnWidth(column, QuickParameterDisplay2, column.ActualWidth, column.ActualWidth > 0 ? 0 : 400);
                
            }));

        }

        /// <summary>
        /// 
        /// </summary>
        public void ToggleModuleParameterDisplay(int duration = -1)
        {
            var column = ContentDisplayGrid.ColumnDefinitions[4];
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.AnimateGridColumnWidth(column, ModuleParameterDisplay, column.ActualWidth, column.ActualWidth > 0 ? 0 : 400,duration);
            }));

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecentLinkedParameter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject selected)
            {
                //var currentMenu = ParameterTabControl.SelectedItem == QuickParameterTab
                //    ? QuickParameterRecentLinkedParameters
                //    : ParameterRecentLinkedParameters;
                /*var currentMenu = new TabItem();
                if (currentMenu.ItemContainerGenerator.ItemFromContainer(selected) is LinkedParameterDisplayModel
                    selectedLinkedParameter)
                {
                    AddCurrentParameterToLinkedParameter(selectedLinkedParameter.LinkedParameter);
                    RecentLinkedParameters.RemoveAt(RecentLinkedParameters.IndexOf(selectedLinkedParameter));
                    RecentLinkedParameters.Insert(0, selectedLinkedParameter);
                } */
            }
        }

        /// <summary>
        ///     Shows the Linked Parameter dialog
        /// </summary>
        /// <param name="assign"></param>
        internal void ShowLinkedParameterDialog(bool assign = false)
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

        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <returns></returns>
        private ParameterDisplayModel GetCurrentParameterDisplayModelContext()
        {
            if (ParameterDisplay.SelectedItem is ParameterDisplayModel currentParameter)
            {
                return (ParameterDisplayModel)ParameterDisplay.SelectedItem;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 
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

                UpdateQuickParameterEquivalent(currentParameter);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        internal void SelectReplacement()
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
                                }
                            });
                        CanSaveModelSystem = true;
                        RefreshParameters();
                    }
                }
            }
        }

        public void RefreshParameters()
        {
            UpdateParameters();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
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
                        us.QuickParameterListView.ContextMenu.DataContext = us;

                        //TODO ITEMS
                        //set the treeview to use regular model system items
                        us.ActiveModelSystemView.ViewItemsControl.ItemsSource = displayModel;

                        us.ModelSystemName = newModelSystem.Name;
                        us.ActiveModelSystemView.ViewItemsControl.InvalidateVisual();

                        //TODO MAYBE
                        us._treeViewDisplay.ModuleDisplay.Items.MoveCurrentToFirst();
                        us.FilterBox.Display = us.ActiveModelSystemView?.ViewItemsControl;
                        us.UpdateModuleCount();


                        //us.ParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;
                        //us.QuickParameterRecentLinkedParameters.ItemsSource = us.RecentLinkedParameters;
                    });
                });
            }
            else
            {
                //POSSIBLY TODO
                //us.ModuleDisplay.DataContext = null;
                us.ModelSystemName = "No model loaded";
                us.FilterBox.Display = null;
                us.ParameterLinkedParameterMenuItem.ItemsSource = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateModuleCount()
        {
            Dispatcher.BeginInvoke(new Action(() => { StatusBarModuleCountTextBlock.Text = $"{this._treeViewDisplay.ModuleDisplay.Items.Count} Modules"; }));


        }


        /// <summary>
        /// 
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
        /// 
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

            return s;
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
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
                            if (QuickParameterDisplay2.IsEnabled)
                            {
                                QuickParameterDialogHost.IsOpen = true;
                            }
                            else if (ModuleParameterDisplay.IsEnabled)
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
                                //SetMetaModuleStateForSelected(false);
                            }
                            else if (EditorController.IsShiftDown())
                            {
                                //SetMetaModuleStateForSelected(true);
                            }
                            else
                            {
                                SelectReplacement();
                            }

                            e.Handled = true;
                            break;
                        case Key.R:
                            //ParameterTabControl.SelectedIndex = 2;
                            //Mo.Focus();
                            // Keyboard.Focus(ParameterFilterBox);
                            e.Handled = true;
                            break;
                        case Key.P:
                            //ParameterTabControl.SelectedIndex = 1;
                            //ModuleParameterTab.Focus();
                            ModuleParameterDisplay.Focus();
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
                            if (ModuleParameterDisplay.IsKeyboardFocusWithin)
                            {
                                SelectDirectoryForCurrentParameter();
                            }

                            //TODO
                            //if (ModuleDisplay.IsKeyboardFocusWithin)
                            //{
                            //    ToggleDisableModule();
                            //}

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
                            if ((ModelSystemDisplayContent.Content as UserControl).IsKeyboardFocusWithin)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Us.PreviewKeyDown -= UsOnPreviewKeyDown;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
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
                    var run = Session.Run(runName, ref error, runQuestion == MessageBoxResult.Yes, !dialog.IsQueueRun, false);
                    if (run != null)
                    {
                        //ModuleValidationErrorListView.Items.Clear();
                        //ModuleRuntimeValidationErrorListView.Items.Clear();
                        //ModuleRuntimeErrorListView.Items.Clear();
                        //pass this as launchedFrom display in case model system run encounters an error
                        if (isDelayed)
                        {
                            MainWindow.Us.AddDelayedRunToSchedulerWindow(MainWindow.Us.CreateDelayedRunWindow(Session, run, runName, delayedStartTime, this, MainWindow.Us.SchedulerWindow)
                                 , delayedStartTime);
                        }
                        else
                        {
                            MainWindow.Us.AddRunToSchedulerWindow(MainWindow.Us.CreateRunWindow(Session, run, runName, !dialog.IsQueueRun, this, MainWindow.Us.SchedulerWindow));
                        }
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
            QuickParameterListView.ItemsSource = ParameterDisplayModel.CreateParameters(Session.ModelSystemModel
                .GetQuickParameters()
                .OrderBy(n => n.Name));
            Dispatcher.BeginInvoke(new Action(() =>
            {
                //ParameterTabControl.SelectedIndex = 0;
                this.ToggleQuickParameterDisplay();
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


        /// <summary>
        /// 
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

        public void SaveCurrentlySelectedParameters(ListView parameterDisplay)
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
                                //MoveCurrentModule(-1);
                            }
                            else
                            {
                                //MoveFocusNextModule(true);
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
                            else
                            {
                                //MoveFocusNextModule(false);
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
                                ToggleQuickParameterDisplay();
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="up"></param>
        internal void MoveFocusNext(bool up)
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
                        (Brush)FindResource("MaterialDesignPaper"));
                    ButtonProgressAssist.SetIndicatorForeground(SaveModelSystemButton,
                        (Brush)FindResource("SecondaryAccentBrush"));
                    SaveModelSystemButton.Style = (Style)FindResource("MaterialDesignFloatingActionMiniDarkButton");


                });
                MainWindow.SetStatusText("Saving...");
                Task.Run(async () =>
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
            GoToModule(ModelSystem.GetModelFor(mss));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mss"></param>
        private void GoToModule(ModelSystemStructureModel mss)
        {
            GoToModule(GetModelFor(mss));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="displayModel"></param>
        private void GoToModule(ModelSystemStructureDisplayModel displayModel)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentlySelected.Clear();
                this._treeViewDisplay.ExpandToRoot(displayModel);
                displayModel.IsSelected = true;
            });

        }

        /// <summary>
        /// 
        /// </summary>
        private void GotoSelectedParameterModule()
        {
            if (ParameterDisplay.SelectedItem is ParameterDisplayModel currentParameter)
            {
                GoToModule((ModelSystemStructure)currentParameter.BelongsTo);
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
            var selected = ActiveModelSystemView.SelectedModule as ModelSystemStructureDisplayModel;
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
                            //SelectedName.Text = CurrentlySelected.Count == 1
                            //    ? CurrentlySelected[0].Name
                            //   : "Multiple Modules Selected";
                            //SelectedNamespace.Text = type.FullName;
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





        /// <summary>
        /// Displays documentation for the currently selected module.
        /// </summary>
        public void ShowDocumentation()
        {
            if (ActiveModelSystemView.SelectedModule is ModelSystemStructureDisplayModel selectedModule)
            {
                MainWindow.Us.LaunchHelpWindow(selectedModule.BaseModel);
            }
        }



        private void Rename_Clicked(object sender, RoutedEventArgs e)
        {
            RenameSelectedModule();
        }

        private void Description_Clicked(object sender, RoutedEventArgs e)
        {
            RenameDescription();
        }



        /// <summary>
        /// Renames the selected module
        /// </summary>
        public void RenameSelectedModule()
        {
            var selected = ActiveModelSystemView.SelectedModule?.BaseModel;
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

        /// <summary>
        /// Renames the selected module's description
        /// </summary>
        public void RenameDescription()
        {
            var selected = ActiveModelSystemView.SelectedModule?.BaseModel;
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



        /// <summary>
        /// Brings the specified module into view
        /// </summary>
        /// <param name="selected"></param>
        internal void BringSelectedIntoView(ModelSystemStructureDisplayModel selected)
        {
            var ansestry = DisplayRoot.BuildChainTo(selected);
            if (ansestry != null)
            {
                var currentContainer = this._treeViewDisplay.ModuleDisplay.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
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


        /// <summary>
        /// 
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

        /// <summary>
        /// 
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
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void CopyParameterName()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                Clipboard.SetText(currentParameter.Name);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hidden"></param>
        private void SetCurrentParameterHidden(bool hidden)
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                string error = null;
                currentParameter.SetHidden(hidden, ref error);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void RenameParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var selectedContainer =
                    (UIElement)ParameterDisplay.ItemContainerGenerator.ContainerFromItem(currentParameter);
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

        /// <summary>
        /// 
        /// </summary>
        private void ResetParameterName()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private ParameterDisplayModel GetSelectedParameterDisplayModel()
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="openWith"></param>
        /// <param name="openDirectory"></param>
        private void OpenParameterFileLocation(bool openWith, bool openDirectory)
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var inputParameter =
                    GetInputParameter(ActiveModelSystemView.SelectedModule.BaseModel,
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

        /// <summary>
        /// 
        /// </summary>
        private void SelectDirectoryForCurrentParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
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

        /// <summary>
        /// 
        /// </summary>
        private void SelectFileForCurrentParameter()
        {
            if (GetCurrentParameterDisplayModelContext() is ParameterDisplayModel currentParameter)
            {
                var _ = GetInputParameter(
                    Session.GetModelSystemStructureModel(currentParameter.BelongsTo as ModelSystemStructure),
                    out var inputDirectory);
                if (inputDirectory != null)
                {
                    var fileName = MainWindow.OpenFile("Select File",
                        new[] { new KeyValuePair<string, string>("All Files", "*") }, true);
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
            /*  if (e.Source == ParameterTabControl)
              {
                  if (ParameterTabControl.SelectedItem == QuickParameterTab)
                  {
                      UpdateQuickParameters();
                  }
              } */
        }

        public void UpdateQuickParameters()
        {
            //DisplayRoot.
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
            //ToggleDisableModule();
        }


        private void ParameterDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //ParameterWidth = ParameterDisplay.ActualWidth - 24;
        }

        private void QuickParameterDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //ParameterWidth = QuickParameterDisplay2.ActualWidth - 24;
        }

        private void ValidationErrorDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //ParameterWidth = ModuleValidationErrorListView.ActualWidth - 24;
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
            if (((ListView)sender).SelectedItem is ParameterDisplayModel s)
            {
                _selectedParameterDisplayModel = s;
            }
        }

        private void QuickParameterDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ListView)sender).SelectedItem is ParameterDisplayModel s)
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
        /// 
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
        /// MouseDown Listener for the ValidationList 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ValidationListModuleNameMouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as ValidationErrorListControl;
            if (label?.Tag is ModelSystemStructureDisplayModel model)
            {
                //ExpandToRoot(model);
                model.IsSelected = true;
            }
        }

        /// <summary>
        /// PreviewKeyDown Listener for this control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="keyEventArgs"></param>
        private void UsOnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.F5 && IsKeyboardFocusWithin && !LinkedParameterDisplayOverlay.IsVisible)
            {
                SaveCurrentlySelectedParameters();
                ExecuteRun();
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

        /// <summary>
        /// 
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
                    _disableMultipleSelectOnce = true;
                    //ModuleDisplayNavigateUp(item);
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
                    _disableMultipleSelectOnce = true;
                    // ModuleDisplayNavigateDown(item);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        view.SelectedIndex = 0;
                        SelectParameterChildControl(
                            (UIElement)view.ItemContainerGenerator.ContainerFromIndex(view.SelectedIndex));
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
            ProcessOnPreviewKeyboardForParameter(QuickParameterListView, e);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (((sender as TextBox).DataContext as ParameterDisplayModel) != null)
            {
                ((sender as TextBox).DataContext as ParameterDisplayModel).Value = (sender as TextBox).Text;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((sender as TextBox).DataContext as ParameterDisplayModel) != null)
            {
                ((sender as TextBox).DataContext as ParameterDisplayModel).Value = (sender as TextBox).Text;
            }
        }


        /// <summary>
        /// Selected Listener for the RegionView button in the toolbar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegionViewListBoxItem_Selected(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
           {
               ModelSystemDisplayContent.Content = this._regionViewDisplay;
           }));

        }

        /// <summary>
        /// Selected listener for the TreeView button in the toolbar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewListBoxItem_Selected(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ModelSystemDisplayContent.Content = this._treeViewDisplay;
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParameterTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*if (this.ParameterTabControl.SelectedItem == QuickParameterTab)
            {
                this.UpdateQuickParameters();
            } */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="column"></param>
        /// <param name="fromWidth"></param>
        /// <param name="toWidth"></param>
        private void AnimateGridColumnWidth(ColumnDefinition column, FrameworkElement display, double fromWidth, double toWidth, int durationMs = -1)
        {

            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs >= 0 ? durationMs : 300));

            DoubleAnimation animation = new DoubleAnimation();
            //animation.EasingFunction = ease;
            animation.Duration = duration;


            animation.From = fromWidth;
            animation.To = toWidth;
            Storyboard.SetTarget(animation, column);
            Storyboard.SetTargetName(animation, column.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(ColumnDefinition.MaxWidthProperty));

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            animation.Completed += delegate (object sender, EventArgs args)
            {
                display.IsEnabled = !display.IsEnabled;
                OnPropertyChanged("QuickParameterToolBarForeground");
                OnPropertyChanged("ModuleParameterToolBarForeground");
            };


            storyboard.Begin(this);

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterToolbarToggle_OnClick(object sender, RoutedEventArgs e)
        {

            this.ToggleQuickParameterDisplay();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParametersToolbarToggle_OnClick(object sender, RoutedEventArgs e)
        {

            this.ToggleModuleParameterDisplay();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterDisplayClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.ToggleQuickParameterDisplay();
        }

        private void ModuleParameterDisplayClose_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.ToggleModuleParameterDisplay();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterSearchButton_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.ToggleQuickParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ToggleQuickParameterDisplaySearch()
        {
            if (QuickParameterDisplaySearch.Opacity == 0.0)
            {
                this.AnimateOpacity(QuickParameterDisplaySearch, 0, 1.0, QuickParameterFilterBox);
                this.AnimateOpacity(QuickParameterDisplayHeader, 1.0, 0.0);
            }
            else
            {
                this.AnimateOpacity(QuickParameterDisplaySearch, 1.0, 0.0);
                this.AnimateOpacity(QuickParameterDisplayHeader, 0.0, 1.0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void ToggleModuleParameterDisplaySearch()
        {
            if (ModuleParameterDisplaySearch.Opacity == 0.0)
            {
                this.AnimateOpacity(ModuleParameterDisplaySearch, 0, 1.0, ParameterFilterBox);
                this.AnimateOpacity(ModuleParameterDisplayHeader, 1.0, 0.0);

            }
            else
            {
                this.AnimateOpacity(ModuleParameterDisplaySearch, 1.0, 0.0);
                this.AnimateOpacity(ModuleParameterDisplayHeader, 0.0, 1.0);

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private void AnimateOpacity(FrameworkElement element, double from, double to, UIElement focusAfter = null)
        {
            Duration duration = new Duration(TimeSpan.FromMilliseconds(200));

            DoubleAnimation animation = new DoubleAnimation();
            //animation.EasingFunction = ease;
            animation.Duration = duration;


            animation.From = from;
            animation.To = to;
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetName(animation, element.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(FrameworkElement.OpacityProperty));

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            animation.Completed += delegate (object sender, EventArgs args)
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
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterDisplaySearchBackButton_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleQuickParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterDisplaySearchBackButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.ToggleModuleParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterSearchButton_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.ToggleModuleParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { UpdateQuickParameters(); }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => { UpdateQuickParameters(); }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickParameterDisplay2_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (QuickParameterDisplaySearch.Opacity > 0 && e.Key == Key.Escape)
            {
                this.ToggleQuickParameterDisplaySearch();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterDisplay_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ModuleParameterDisplaySearch.Opacity > 0 && e.Key == Key.Escape)
            {
                this.ToggleModuleParameterDisplaySearch();
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ModelSystemEditingSessionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        public ModelSystemEditingSessionChangedEventArgs(ModelSystemEditingSession session)
        {
            Session = session;
        }

        public ModelSystemEditingSession Session { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SelectedModuleParameterContextChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        public SelectedModuleParameterContextChangedEventArgs(ModelSystemEditingSession session)
        {
            Session = session;
        }

        public ModelSystemEditingSession Session { get; }
    }
}