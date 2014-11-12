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
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XTMF.Commands.Editing;
using XTMF.Gui.UserControls;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for UnifiedModelSystemInterface.xaml
    /// </summary>
    public partial class UnifiedModelSystemInterface : UserControl
    {
        public IConfiguration Config;
        protected UMSIContextMenu ModuleContextMenu;
        protected List<IModuleParameter> QuickParameters;
        private static Color AddingYellow;
        private static Color ControlBackgroundColour;
        private static Color FocusColour;
        private static Color HighlightColour;
        private static Color InformationGreen;
        private static BitmapSource ListIcon;
        private static BitmapSource ModuleIcon;
        private static Color WarningRed;

        private bool _EditMode = true;

        private IModelSystem _ModelSystem;

        private IProject _Project;

        private Stack<XTMF.Commands.ICommand> CommandStack = new Stack<XTMF.Commands.ICommand>();

        /// <summary>
        /// The modules panel entry that is currently selected, -1 means none.
        /// </summary>
        private int FocusedSelectedModule;

        private int ProjectModelSystemNumber;

        private Action<IModelSystemStructure> UpdateRootAction;

        private IList<Type> ViableModules;

        static UnifiedModelSystemInterface()
        {
            try
            {
                try
                {
                    ModuleIcon = CreateBitmapCache("pack://application:,,,/XTMF.Gui;component/Resources/Settings.png");
                    ModuleIcon.Freeze();
                    ListIcon = CreateBitmapCache("pack://application:,,,/XTMF.Gui;component/Resources/Plus.png");
                    ListIcon.Freeze();
                }
                catch
                {
                }
                HighlightColour = (Color)Application.Current.TryFindResource("SelectionBlue");
                FocusColour = (Color)Application.Current.TryFindResource("FocusColour");
                ControlBackgroundColour = (Color)Application.Current.TryFindResource("ControlBackgroundColour");
                AddingYellow = (Color)Application.Current.TryFindResource("AddingYellow");
                InformationGreen = (Color)Application.Current.TryFindResource("InformationGreen");
                WarningRed = (Color)Application.Current.TryFindResource("WarningRed");
            }
            catch
            {
            }
        }

        public UnifiedModelSystemInterface()
        {
            InitializeComponent();
            CreateContextMenues();
            this.ModuleContextMenu.RenamePressed += new Action<string>(ModuleContextMenu_RenamePressed);
            this.ModuleContextMenu.RemovePressed += new Action(ModuleContextMenu_RemovePressed);
            this.ModuleContextMenu.InsertRequested += new Action<IModelSystemStructure>(ModuleContextMenu_InsertRequested);
            var keyHandeler = new KeyEventHandler(Search_KeyDown);
            this.ModuleTab.PreviewKeyDown += keyHandeler;
            this.UpdateRootAction = new Action<IModelSystemStructure>(UpdateRoot);
            this.QuickParameterControl.ControlWrite = this.ParameterEditor.ControlWrite = new Action<IModuleParameter, string>(ParameterUpdate);
            this.LinkedParameterEditor.NewLinkedParameterRequested += new Action<string>(LinkedParameterEditor_NewLinkedParameterRequested);
            this.LinkedParameterEditor.RemoveLinkedParameterRequested += new Action<ILinkedParameter>(LinkedParameterEditor_RemoveLinkedParameterRequested);
            this.LinkedParameterEditor.RenameLinkedParameterRequested += new Action<ILinkedParameter, string>(LinkedParameterEditor_RenameLinkedParameterRequested);
            this.LinkedParameterEditor.SetLinkedParameterValue = new Func<ILinkedParameter, string, bool>(LinkedParameterEditor_SetLinkedParameterValue);
            this.LinkedParameterEditor.RemoveLinkedParameterParameter = new Func<ILinkedParameter, IModuleParameter, bool>(LinkedParameterEditor_RemoveLinkedParameterParameter);
        }

        public event Action<ILinkedParameter> RenameLinkedParamater;

        public event Action<IModelSystemStructure> RenameRequested;

        public bool EditMode
        {
            get
            {
                return this._EditMode;
            }
            set
            {
                this._EditMode = value;
                this.ModelSystemDisplay.EditMode = value;
                this.ParameterEditor.IsEditing = value;
                this.QuickParameterControl.IsEditing = value;
                this.ModuleContextMenu.EditMode = value;
                this.ModuleTab.IsEnabled = value;
                this.LinkedParameterTab.IsEnabled = true;
                this.LinkedParameterEditor.EditMode = value;
            }
        }

        public IModelSystem ModelSystem
        {
            get
            {
                return this._ModelSystem;
            }

            set
            {
                this.ModelSystemDisplay.SelectedModule = null;
                this._ModelSystem = value;
                this.CommandStack.Clear();
                this.Root = value.ModelSystemStructure;
                this.ParameterEditor.ModelSystemStructure = null;
                this.EditMode = true;
                this.SetupModelSystem();
                this.RefreshLinkedParameters();
            }
        }

        public IModuleRepository ModuleRepository { get; set; }

        public IModelSystemStructure Root
        {
            get;
            set;
        }

        public bool ShowingParameters { get; set; }

        public bool AddCommand(XTMF.Commands.ICommand command)
        {
            string error = null;
            if(command.Do(ref error))
            {
                this.CommandStack.Push(command);
                return true;
            }
            return false;
        }

        public bool AddCommand(XTMF.Commands.ICommand command, ref string error)
        {
            if(command.Do(ref error))
            {
                this.CommandStack.Push(command);
                return true;
            }
            return false;
        }

        public void DocumentationRequested(object sender, MouseEventArgs e)
        {
            var selected = this.ModelSystemDisplay.SelectedModule;
            if(selected != null)
            {
                new DocumentationWindow() { ModuleType = selected.Type }.Show();
            }
        }

        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
               new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return f;
        }

        public void LoadProject(IProject project, int currentModelSystem, bool editMode = false)
        {
            this.ModelSystemDisplay.SelectedModule = null;
            this.ParameterEditor.ModelSystemStructure = null;
            this._ModelSystem = null;
            this._Project = project;
            this.ProjectModelSystemNumber = currentModelSystem;
            this.CommandStack.Clear();
            this.Root = project.ModelSystemStructure[currentModelSystem];
            this.EditMode = editMode;
            this.SetupModelSystem();
            this.RefreshLinkedParameters();
        }

        public void propertyControl_ParameterChanged()
        {
            // We don't need to save this anymore because we have an action inside to do it for us
            var selected = this.ModelSystemDisplay.SelectedModule;
            this.Save();
            if(selected != null)
            {
                this.ParameterEditor.ModelSystemStructure = selected;
                this.LinkedParameterEditor.Refresh();
            }
            else
            {
                this.ParameterEditor.ModelSystemStructure = null;
            }
            //this.QuickParameterControl.Save();
            this.SetupQuickParameters();
        }

        public void Undo()
        {
            if(this.CommandStack.Count > 0)
            {
                string error = null;
                var command = this.CommandStack.Pop();
                command.Undo(ref error);
                this.SetupModelSystem();
                this.ParameterEditor.ModelSystemStructure = this.ParameterEditor.ModelSystemStructure;
                this.QuickParameterControl.Parameters = this.QuickParameterControl.Parameters;
                this.RefreshLinkedParameters();
            }
        }

        internal void LoadRoot(IModelSystemStructure root)
        {
            this.EditMode = false;
            this._ModelSystem = null;
            this._Project = null;
            this.CommandStack.Clear();
            this.Root = root;
            this.SetupModelSystem();
            this.ParameterEditor.ModelSystemStructure = null;
        }

        internal void Refresh(IModelSystemStructure element)
        {
            this.ModelSystemDisplay.Refresh();
            this.RefreshLinkedParameters();
            this.ModuleNameDisplay.Text = this.ModelSystemDisplay.SelectedModule != null ?
                this.ModelSystemDisplay.SelectedModule.Name : "No Module Selected";
            this.Save();
        }

        internal void RefreshLinkedParameters()
        {
            this.LinkedParameterEditor.LinkedParameters = null;
            var lp = GetLinkParameters();
            this.LinkedParameterEditor.LinkedParameters = lp;
            this.ParameterEditor.LinkedParameters = lp;
            this.ParameterEditor.RefreshParameters();
            this.Save();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(this.OurTabPanel.SelectedItem == ModuleTab)
            {
                if(e.Key == Key.Down)
                {
                    this.MoveModuleFocus(1);
                    e.Handled = true;
                }
                else if(e.Key == Key.Up)
                {
                    this.MoveModuleFocus(-1);
                    e.Handled = true;
                }
                else if(e.Key == Key.Enter)
                {
                    this.SelectFocusedModule();
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if(e.Handled == false)
            {
                if(e.Key == Key.Escape)
                {
                    if(this.ShowingParameters)
                    {
                        e.Handled = true;
                    }
                }
            }
            base.OnKeyUp(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if(e.Handled == false)
            {
                if(!this.OurTabPanel.IsKeyboardFocusWithin)
                {
                    var selected = this.ModelSystemDisplay.SelectedModule;
                    if(e.Key == Key.F2)
                    {
                        if(selected != null)
                        {
                            e.Handled = true;
                            this.RenameSelection_Clicked(null);
                        }
                    }
                    else if(e.Key == Key.F1)
                    {
                        if(selected != null)
                        {
                            if(selected.IsCollection == false)
                            {
                                new DocumentationWindow() { Module = selected }.Show();
                                e.Handled = true;
                            }
                        }
                    }
                    else if(e.Key == Key.Space)
                    {
                        this.ModelSystemDisplay.ToggleExpandSelected();
                    }
                }

                if(e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift))
                {
                    if(e.Key == Key.F2)
                    {
                        e.Handled = true;
                        this.RenameSelection_Clicked(null);
                    }
                    else if(e.Key == Key.F1)
                    {
                        var selected = this.ModelSystemDisplay.SelectedModule;
                        if(selected != null && selected.IsCollection == false)
                        {
                            new DocumentationWindow() { Module = selected }.Show();
                            e.Handled = true;
                        }
                    }
                    else if(e.Key == Key.Down)
                    {
                        this.MoveCurrentlySelectedItemDown();
                        e.Handled = true;
                    }
                    else if(e.Key == Key.Up)
                    {
                        this.MoveCurrentlySelectedItemUp();
                        e.Handled = true;
                    }
                }
            }
            base.OnPreviewKeyDown(e);
        }

        private static CachedBitmap CreateBitmapCache(string uri)
        {
            BitmapImage temp = new BitmapImage();
            temp.BeginInit();
            temp.UriSource = new Uri(uri);
            temp.DecodePixelWidth = 32;
            temp.DecodePixelHeight = 32;
            temp.EndInit();
            return new CachedBitmap(temp, BitmapCreateOptions.None, BitmapCacheOption.Default);
        }

        private void AddQuickParameters(IModelSystemStructure current, List<IModuleParameter> param)
        {
            if(current == null) return;
            if(!current.IsCollection)
            {
                var parameterSet = current.Parameters;
                if(parameterSet != null)
                {
                    var parameters = parameterSet.Parameters;
                    for(int i = 0; i < parameters.Count; i++)
                    {
                        var parameter = parameters[i];
                        if(parameter.QuickParameter)
                        {
                            // check to see if we are editing, if not
                            // we only accept non system parameters
                            if(this.EditMode || !parameter.SystemParameter)
                            {
                                param.Add(parameter);
                            }
                        }
                    }
                }
            }
            var children = current.Children;
            if(children != null)
            {
                for(int i = 0; i < children.Count; i++)
                {
                    AddQuickParameters(children[i], param);
                }
            }
        }

        private IList<Type> ApplyFilter(IList<Type> mutableList, string filter)
        {
            if(String.IsNullOrWhiteSpace(filter))
            {
                return mutableList;
            }
            int length = mutableList.Count;
            filter = filter.ToLower();
            // no i++ since we do that only if we do not remove an item
            for(int i = 0; i < length;)
            {
                bool removed = false;
                if(!mutableList[i].FullName.ToLower().Contains(filter))
                {
                    removed = true;
                    mutableList.RemoveAt(i);
                    length--;
                }
                if(!removed)
                {
                    i++;
                }
            }
            return mutableList;
        }

        private void CreateContextMenues()
        {
            this.ModuleContextMenu = new UMSIContextMenu();
            this.QuickParameterControl.OpenFileRequested += this.lpContextMenu_OpenFileRequested;
            this.QuickParameterControl.OpenFileWithRequested += this.lpContextMenu_OpenFileWithRequested;
            this.QuickParameterControl.OpenFileLocationRequested += this.lpContextMenu_OpenFileLocationRequested;
            this.QuickParameterControl.SelectFileRequested += this.lpContextMenu_SelectFileRequested;
            this.QuickParameterControl.AddToLinkedParameterRequested += this.lpContextMenu_AddToLinkedParameterRequested;

            this.ParameterEditor.OpenFileRequested += this.lpContextMenu_OpenFileRequested;
            this.ParameterEditor.OpenFileWithRequested += this.lpContextMenu_OpenFileWithRequested;
            this.ParameterEditor.OpenFileLocationRequested += this.lpContextMenu_OpenFileLocationRequested;
            this.ParameterEditor.SelectFileRequested += this.lpContextMenu_SelectFileRequested;
            this.ParameterEditor.AddToLinkedParameterRequested += this.lpContextMenu_AddToLinkedParameterRequested;
            this.ModelSystemDisplay.ContextMenu = this.ModuleContextMenu;
        }

        private string GetInputDirectory(IModelSystemStructure root)
        {
            var inputDir = root.Type.GetProperty("InputBaseDirectory");
            var attributes = inputDir.GetCustomAttributes(typeof(ParameterAttribute), true);
            if(attributes != null && attributes.Length > 0)
            {
                var parameterName = ((ParameterAttribute)attributes[0]).Name;
                var parameters = root.Parameters.Parameters;
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].Name == parameterName)
                    {
                        return parameters[i].Value.ToString();
                    }
                }
            }
            return null;
        }

        private List<ILinkedParameter> GetLinkParameters()
        {
            if(this._Project != null)
            {
                if(this._Project.LinkedParameters == null) return null;
                return this._Project.LinkedParameters[this._Project.ModelSystemStructure.IndexOf(this.Root)];
            }
            else
            {
                return this._ModelSystem.LinkedParameters;
            }
        }

        private IModuleParameter GetParameterFromName(string parameterName)
        {
            var parametersSet = this.ModelSystemDisplay.SelectedModule.Parameters;
            if(parametersSet == null) return null;
            var parameters = parametersSet.Parameters;
            for(int i = 0; i < parameters.Count; i++)
            {
                if(parameters[i].Name == parameterName)
                {
                    return parameters[i];
                }
            }
            return null;
        }

        private IModelSystemStructure GetParent(IModelSystemStructure toFind)
        {
            return ModelSystemStructure.GetParent(this.Root, toFind);
        }

        private string GetRelativePath(string inputDirectory, string parameterValue)
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
            return RemoveRelativeDirectories(System.IO.Path.Combine(this.Config.ProjectDirectory, "AProject",
            "RunDirectory", inputDirectory, parameterValue));
        }

        private IModelSystemStructure GetRoot(IModelSystemStructure rootFor)
        {
            return ModelSystemStructure.CheckForRootModule(this.Root, rootFor, ModelSystemStructure.GetRootRequirement(rootFor.Type));
        }

        private bool HasChildren(IModelSystemStructure mss)
        {
            return mss.Children != null && mss.Children.Count > 0;
        }

        private void LinkedParameterEditor_NewLinkedParameterRequested(string obj)
        {
            if(this._ModelSystem != null)
            {
                this.AddCommand(new CreateNewModelSystemLinkedParameter(this._ModelSystem, obj, String.Empty));
            }
            else
            {
                this.AddCommand(new CreateNewProjectLinkedParameter(this._Project, this.ProjectModelSystemNumber, obj, String.Empty));
            }
            this.RefreshLinkedParameters();
        }

        private bool LinkedParameterEditor_RemoveLinkedParameterParameter(ILinkedParameter lp, IModuleParameter param)
        {
            if(!this.AddCommand(new RemoveParameterFromLinkedParameter(lp, param)))
            {
                return false;
            }
            this.ParameterEditor.RefreshParameters();
            this.Save();
            return true;
        }

        private void LinkedParameterEditor_RemoveLinkedParameterRequested(ILinkedParameter lp)
        {
            if(this._ModelSystem != null)
            {
                this.AddCommand(new DeleteLinkedParameter(this._ModelSystem, lp));
            }
            else
            {
                this.AddCommand(new DeleteProjectLinkedParameter(this._Project, this.ProjectModelSystemNumber, lp));
            }
            this.RefreshLinkedParameters();
        }

        private void LinkedParameterEditor_RenameLinkedParameterRequested(ILinkedParameter parameter, string newName)
        {
            string error = null;
            if(parameter.Name != newName)
            {
                if(!this.AddCommand(new RenameLinkedParameter(parameter, newName), ref error))
                {
                    MessageBox.Show(error, "Unable to change name", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                this.RefreshLinkedParameters();
            }
        }

        private bool LinkedParameterEditor_SetLinkedParameterValue(ILinkedParameter lp, string newValue)
        {
            if(!this.AddCommand(new SetLinkedParameterValue(lp, newValue)))
            {
                return false;
            }
            this.QuickParameterControl_ParameterChanged();
            this.ParameterEditor.RefreshParameters();
            return true;
        }

        private void lpContextMenu_AddToLinkedParameterRequested(ILinkedParameter linkedParameter, IModuleParameter parameter)
        {
            string error = null;
            if(parameter == null)
            {
                MessageBox.Show("No parameter was selected!", "Unable To Add", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if(linkedParameter == null)
            {
                MessageBox.Show("No linked parameter parameter was selected!", "Unable To Add", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if(!this.AddCommand(new AddParameterToLinkedParameters(this.GetLinkParameters(), linkedParameter, parameter), ref error))
            {
                MessageBox.Show(error, "Unable To Add", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            this.ParameterEditor.RefreshParameters();
            // this will actually refresh everything
            this.QuickParameterControl_ParameterChanged();
        }

        private void lpContextMenu_OpenFileLocationRequested(IModuleParameter parameter)
        {
            OpenFile(parameter, false, true);
        }

        private void lpContextMenu_OpenFileRequested(IModuleParameter parameter)
        {
            OpenFile(parameter, false, false);
        }

        private void lpContextMenu_OpenFileWithRequested(IModuleParameter parameter)
        {
            OpenFile(parameter, true, false);
        }

        private void lpContextMenu_SelectFileRequested(IModuleParameter parameter)
        {
            var module = parameter.BelongsTo;
            var currentRoot = this.GetRoot(module);
            var inputDirectory = GetInputDirectory(currentRoot);
            if(inputDirectory != null)
            {
                string fileName = this.OpenFile();
                if(fileName == null)
                {
                    return;
                }
                TransformToRelativePath(inputDirectory, ref fileName);
                if(this.AddCommand(new Commands.Editing.ParameterChangeCommand(parameter,
                    fileName, this.GetLinkParameters())))
                {
                    this.ParameterEditor.RefreshParameters();
                    this.QuickParameterControl.RefreshParameters();
                    this.Save();
                }
                else
                {
                    MessageBox.Show("Unable to set file name'" + fileName + "'!", "Unable set file name",
                       MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ModelSystemDisplay_ModuleSelected(object arg1, IModelSystemStructure module)
        {
            this.SetupParameters(module);
        }

        private void ModuleContextMenu_InsertRequested(IModelSystemStructure toInsert)
        {
            this.FocusedSelectedModule = -1;
            this.SetModuleFocus();
            var parent = this.GetParent(this.ModelSystemDisplay.SelectedModule);
            var clone = toInsert.Clone();
            if(parent == null)
            {
                if(this.ModelSystem != null)
                {
                    this.AddCommand(new SetModelSystemRootCommand(UpdateRootAction, this.ModelSystem, clone));
                }
                else
                {
                    this.AddCommand(new SetProjectRootCommand(UpdateRootAction, this._Project, this.ProjectModelSystemNumber, clone));
                }
                this.ModelSystemDisplay.RootModule = toInsert;
                this.RefreshLinkedParameters();
                this.SetupParameters(clone);
                this.SetupModelSystem();
            }
            else
            {
                var selected = this.ModelSystemDisplay.SelectedModule;
                if(selected.IsCollection)
                {
                    if(toInsert.IsCollection)
                    {
                        if(clone.Children == null) return;
                        foreach(var child in clone.Children)
                        {
                            this.AddCommand(new AddToModuleListCommand(selected, child));
                        }
                        this.ModelSystemDisplay.SelectedModule = selected.Children[selected.Children.Count - 1];
                    }
                    else
                    {
                        this.AddCommand(new AddToModuleListCommand(selected, toInsert.Clone()));
                        this.ModelSystemDisplay.SelectedModule = selected.Children[selected.Children.Count - 1];
                    }
                    this.SetupModelSystem();
                }
                else
                {
                    // process being inserted into a non list where the parent is a list
                    if(parent.IsCollection)
                    {
                        var collection = parent.Children;
                        var collectionLength = collection.Count;
                        for(int i = 0; i < collectionLength; i++)
                        {
                            if(collection[i] == this.ModelSystemDisplay.SelectedModule)
                            {
                                this.AddCommand(new ModuleSwapCommand(this.GetLinkParameters(), parent, i, clone));
                                this.ModelSystemDisplay.SelectedModule = clone;
                                this.RefreshLinkedParameters();
                                this.SetupModelSystem();
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Process being inserted into a non list where the parent is also not a list.
                        var collection = parent.Children;
                        if(collection == null)
                        {
                            AddCommand(new SetModelSystemRootCommand(UpdateRootAction, ModelSystem, clone));
                        }
                        else
                        {
                            var collectionLength = collection.Count;
                            for(int i = 0; i < collectionLength; i++)
                            {
                                if(collection[i] == this.ModelSystemDisplay.SelectedModule)
                                {
                                    this.AddCommand(new ModuleSwapCommand(this.GetLinkParameters(), parent, i, clone));
                                    this.ModelSystemDisplay.SelectedModule = clone;
                                    this.RefreshLinkedParameters();
                                    break;
                                }
                            }
                        }
                        this.SetupParameters(clone);
                        this.SetupModelSystem();
                    }
                }
            }
            this.Save();
        }

        private void ModuleContextMenu_RemovePressed()
        {
            this.RemoveButton_Clicked(null);
            this.Save();
        }

        private void ModuleContextMenu_RenamePressed(string name)
        {
            var selectedModule = this.ModelSystemDisplay.SelectedModule;
            string error = null;
            if(selectedModule.Name != name)
            {
                if(this.AddCommand(new XTMF.Commands.Editing.ModuleRenameCommand(selectedModule, name), ref error))
                {
                    this.ModelSystemDisplay.Refresh();
                }
                this.Save();
            }
        }

        private void MoveCurrentlySelectedItemDown()
        {
            this.ModelSystemDisplay.MoveSelectedDown();
        }

        private void MoveCurrentlySelectedItemUp()
        {
            this.ModelSystemDisplay.MoveSelectedUp();
        }

        private void MoveModuleFocus(int increment)
        {
            if(this.ViableModules != null)
            {
                this.FocusedSelectedModule += increment;
                if(this.FocusedSelectedModule < 0)
                {
                    this.FocusedSelectedModule = -1;
                }
                if(this.FocusedSelectedModule >= this.ViableModules.Count)
                {
                    this.FocusedSelectedModule = this.ViableModules.Count - 1;
                }
                SetModuleFocus();
            }
        }

        private void OpenFile(IModuleParameter parameter, bool openWith, bool openDirectory)
        {
            var module = parameter.BelongsTo;
            var parameterValue = parameter.Value.ToString();
            var currentRoot = this.GetRoot(module);
            var inputDirectory = GetInputDirectory(currentRoot);
            if(inputDirectory != null)
            {
                var fileName = this.GetRelativePath(inputDirectory, parameterValue);
                if(openDirectory)
                {
                    openWith = false;
                    fileName = System.IO.Path.GetDirectoryName(fileName);
                }
                try
                {
                    Process toRun = new Process();
                    if(openWith)
                    {
                        toRun.StartInfo.FileName = "Rundll32.exe";
                        toRun.StartInfo.Arguments = "Shell32.dll,OpenAs_RunDLL " + fileName;
                    }
                    else
                    {
                        toRun.StartInfo.FileName = fileName;
                    }
                    toRun.Start();
                }
                catch
                {
                    MessageBox.Show("Unable to load file '" + fileName + "'!", "Unable to open",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Unable to find input directory.", "Unable to open",
                 MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ParameterUpdate(IModuleParameter parameter, string newValue)
        {
            List<ILinkedParameter> linkedParameters = null;
            if(_Project != null)
            {
                if(this._Project.LinkedParameters != null)
                {
                    linkedParameters = this._Project.LinkedParameters[this._Project.ModelSystemStructure.IndexOf(this.Root)];
                }
            }
            else
            {
                linkedParameters = this.ModelSystem.LinkedParameters;
            }
            this.AddCommand(new ParameterChangeCommand(parameter, newValue, linkedParameters));
        }

        private void QuickParameterControl_ParameterChanged()
        {
            var selected = this.ModelSystemDisplay.SelectedModule;
            if(selected != null)
            {
                this.ParameterEditor.ModelSystemStructure = null;
                this.ParameterEditor.ModelSystemStructure = selected;
                this.LinkedParameterEditor.Refresh();
            }
            this.SetupQuickParameters();
            this.Save();
        }

        private void RemoveButton_Clicked(object obj)
        {
            var selected = this.ModelSystemDisplay.SelectedModule;
            if(selected == null)
            {
                return;
            }
            if(!selected.IsCollection)
            {
                if(selected == this.Root)
                {
                    this.ModelSystemDisplay.ForceRefresh = true;
                }
                var parent = this.GetParent(selected);
                if(parent != null && parent.IsCollection)
                {
                    this.AddCommand(new RemoveFromModuleListCommand(this.GetLinkParameters(), parent,
                        selected));
                }
                else
                {
                    this.AddCommand(new ModuleTypeChangeCommand(this.GetLinkParameters(),
                        selected, null));
                }
                this.RefreshLinkedParameters();
                // re-setup our model system
                this.SetupModelSystem();
            }
            else
            {
                var members = selected.Children;
                if(members != null)
                {
                    for(int i = members.Count - 1; i >= 0; i--)
                    {
                        this.AddCommand(new RemoveFromModuleListCommand(this.GetLinkParameters(),
                            selected, members[i]));
                        this.RefreshLinkedParameters();
                    }
                    this.SetupModelSystem();
                }
            }
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

        private void RenameSelection_Clicked(object obj)
        {
            var selected = this.ModelSystemDisplay.SelectedModule;
            if(selected == null)
            {
                return;
            }
            var listenners = this.RenameRequested;
            if(listenners != null)
            {
                listenners(selected);
            }
        }

        private void ReplacementSelected(object selectedItem)
        {
            var selected = this.ModelSystemDisplay.SelectedModule;
            int index = ValidModulePanel.Children.IndexOf(selectedItem as UIElement);
            if(index == -1) return;
            if(selected.IsCollection)
            {
                var newStruct = selected.CreateCollectionMember(this.ViableModules[index]);
                this.AddCommand(new AddToModuleListCommand(selected, newStruct));
            }
            else
            {
                if(selected == this.Root)
                {
                    this.ModelSystemDisplay.ForceRefresh = true;
                }
                this.AddCommand(new ModuleTypeChangeCommand(this.GetLinkParameters(), selected,
                    this.ViableModules[index]));
                this.RefreshLinkedParameters();
            }
            this.SetupParameters(this.ModelSystemDisplay.SelectedModule);
            this.SetupModelSystem();
            this.Save();
        }

        private void Save()
        {
            string error = null;
            if(this.ModelSystem != null)
            {
                if(!this.ModelSystem.Save(ref error))
                {
                    MessageBox.Show("Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            if(this._Project != null)
            {
                if(!this._Project.Save(ref error))
                {
                    MessageBox.Show("Unable to save project!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            this.OnKeyDown(e);
        }

        private void Search_TextChanged(string filter)
        {
            this.SetupViableModules(this.ModelSystemDisplay.SelectedModule, filter);
        }

        private void SelectFocusedModule()
        {
            var panelChildren = this.ValidModulePanel.Children;
            if(panelChildren != null && this.FocusedSelectedModule >= 0 && this.FocusedSelectedModule < panelChildren.Count)
            {
                this.ReplacementSelected(panelChildren[this.FocusedSelectedModule]);
            }
        }

        private void SetModuleFocus()
        {
            int count = 0;
            var children = this.ValidModulePanel.Children;
            for(int i = 0; i < children.Count; i++)
            {
                var button = children[i] as BorderIconButton;
                if(button != null)
                {
                    var selected = (count == this.FocusedSelectedModule);
                    if(button.Selected != selected)
                    {
                        button.Selected = selected;
                    }
                    if(selected)
                    {
                        button.BringIntoView();
                    }
                    count++;
                }
            }
        }

        private void SetupModelSystem()
        {
            var selected = ModelSystemDisplay.SelectedModule;
            if(this.ModelSystemDisplay.RootModule != this.Root)
            {
                selected = this.ModelSystemDisplay.RootModule = this.Root;
                this.ModelSystemDisplay.SelectedModule = selected;
            }
            this.ShowingParameters = false;
            SetupQuickParameters();
            this.ParameterEditor.ModelSystemStructure = selected;
            this.SetupViableModules(selected);
            this.ModelSystemDisplay.Refresh();
        }

        private void SetupParameters(IModelSystemStructure module)
        {
            this.ShowingParameters = true;
            if(module.Type != null || (!this.EditMode))
            {
                this.ModuleNameDisplay.Text = module.Name;
                this.ModuleNameSpaceDisplay.Text = module.Type == null ? String.Empty : module.Type.FullName;
                this.OurTabPanel.SelectedIndex = this.OurTabPanel.Items.IndexOf(this.SettingsTab);
            }
            else
            {
                this.ModuleNameDisplay.Text = "No Module Selected";
                this.ModuleNameSpaceDisplay.Text = String.Empty;
                this.OurTabPanel.SelectedIndex = this.OurTabPanel.Items.IndexOf(this.ModuleTab);
            }
            new Task(delegate ()
            {
                SetupViableModules(module);
                this.Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    if(this.ParameterEditor.ModelSystemStructure != module)
                    {
                        this.ParameterEditor.ModelSystemStructure = module;
                        this.ParameterEditor.Opacity = 0f;
                    }
                }));
            }).Start();
        }

        private void SetupQuickParameters()
        {
            List<IModuleParameter> param = new List<IModuleParameter>(10);
            this.AddQuickParameters(this.Root, param);
            this.QuickParameters = param;
            this.Dispatcher.Invoke(new Action(delegate ()
            {
                this.QuickParameterControl.Parameters = null;
                this.QuickParameterControl.Parameters = this.QuickParameters;
            }));
        }

        private void SetupViableModules(IList<Type> modules)
        {
            this.ValidModulePanel.Children.Clear();
            if(this.ViableModules != null)
            {
                this.ViableModules.Clear();
            }
            this.ShowingParameters = false;
            var l = (modules as List<Type>);
            if(l != null)
            {
                l.Sort(new Comparison<Type>(delegate (Type one, Type two)
                {
                    int value = one.Name.CompareTo(two.Name);
                    if(value != 0) return value;
                    return one.FullName.CompareTo(two.FullName);
                }));
            }
            this.ViableModules = modules;
            var clicked = new Action<object>(ReplacementSelected);
            var rightClicked = new Action<object>(ViableModuleDocumentation);
            if(modules != null)
            {
                var thickness = new Thickness(5, 5, 5, 5);
                // Setup the possible ones
                foreach(var module in modules)
                {
                    BorderIconButton moduleButton = new BorderIconButton();
                    moduleButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                    moduleButton.Header = module.Name;
                    moduleButton.Margin = thickness;
                    moduleButton.Text = module.FullName;
                    moduleButton.HighlightColour = HighlightColour;
                    moduleButton.Icon = ModuleIcon;
                    if(module == this.ModelSystemDisplay.SelectedModule.Type)
                    {
                        moduleButton.ShadowColour = HighlightColour;
                    }
                    moduleButton.Clicked += clicked;
                    moduleButton.RightClicked += rightClicked;
                    this.ValidModulePanel.Children.Add(moduleButton);
                }
            }
        }

        private void SetupViableModules(IModelSystemStructure module, string filter = null)
        {
            this.Dispatcher.BeginInvoke(new Action(delegate ()
            {
                if(filter == null)
                {
                    filter = this.Search.Filter = null;
                }
                this.FocusedSelectedModule = -1;
                this.SetModuleFocus();
                if(module == null) return;
                this.ValidModulePanel.Children.Clear();
                this.SetupViableModules(this.ApplyFilter(module.GetPossibleModules(this.Root), filter));
            }));
        }

        private void TransformToRelativePath(string inputDirectory, ref string fileName)
        {
            var runtimeInputDirectory =
                System.IO.Path.GetFullPath(
                System.IO.Path.Combine(this.Config.ProjectDirectory, "AProject", "RunDirectory", inputDirectory)
                ) + System.IO.Path.DirectorySeparatorChar;
            if(fileName.StartsWith(runtimeInputDirectory))
            {
                fileName = fileName.Substring(runtimeInputDirectory.Length);
            }
        }

        private void UpdateRoot(IModelSystemStructure newRoot)
        {
            this.Root = newRoot;
        }

        private void ViableModuleDocumentation(object selectedItem)
        {
            this.FocusedSelectedModule = -1;
            int index = ValidModulePanel.Children.IndexOf(selectedItem as UIElement);
            if(index == -1) return;
            new DocumentationWindow() { ModuleType = this.ViableModules[index] }.Show();
        }

        private void ModelSystemDisplay_ChildMoved(object control, IModelSystemStructure parentToAlter, int startingPlace, int destinationPlace)
        {
            // implement the move
            // make sure that it is actually moving
            if(startingPlace == destinationPlace) return;

            // if it is figure out in what direction
            if(startingPlace < destinationPlace)
            {
                // if we are moving down then move things up
                var temp = parentToAlter.Children[startingPlace];
                for(int i = startingPlace; i < destinationPlace; i++)
                {
                    parentToAlter.Children[i] = parentToAlter.Children[i + 1];
                }
                parentToAlter.Children[destinationPlace] = temp;
            }
            else
            {
                var temp = parentToAlter.Children[startingPlace];
                for(int i = startingPlace; i > destinationPlace; i--)
                {
                    parentToAlter.Children[i] = parentToAlter.Children[i - 1];
                }
                parentToAlter.Children[destinationPlace] = temp;
            }
            this.Save();
            this.SetupModelSystem();
        }
    }
}