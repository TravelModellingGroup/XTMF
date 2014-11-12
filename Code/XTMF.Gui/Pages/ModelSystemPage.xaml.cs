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
using System.Windows.Input;
using XTMF.Commands.Editing;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ModelSystemPage.xaml
    /// </summary>
    public partial class ModelSystemPage : UserControl, INotifyPropertyChanged, IXTMFPage
    {
        // Using a DependencyProperty as the backing store for CurrentProject.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentProjectProperty =
            DependencyProperty.Register( "CurrentModule", typeof( IModelSystemStructure ), typeof( ModelSystemPage ), new UIPropertyMetadata( null ) );

        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage,
            XTMFPage.ProjectSettingsPage, XTMFPage.ModelSystemSettingsPage };

        private ModelSystemPageContext Context;

        private ILinkedParameter LastLinkedParameterRequest;

        private List<int> ModuleIndirection = new List<int>();

        private IModelSystem NewMSHolder;

        private Stack<IModelSystemStructure> ParentNodes = new Stack<IModelSystemStructure>();

        private List<IModuleParameter> QuickParameters = new List<IModuleParameter>();

        private SingleWindowGUI XTMF;

        public ModelSystemPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.ModelSystemInterface.Config = xtmf.XTMF.Configuration;
            this.Loaded += new RoutedEventHandler( ModelSystemPage_Loaded );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IModelSystemStructure CurrentModule
        {
            get { return (IModelSystemStructure)GetValue( CurrentProjectProperty ); }

            set
            {
                SetValue( CurrentProjectProperty, value );
                if ( value == null )
                {
                    this.ModuleNameLabel.Text = "No Module Loaded!";
                }
                else
                {
                    this.ModuleNameLabel.Text = value.Name;
                }
                this.ModuleNameLabel.ToolTip = this.ModuleNameLabel.Text;
                this.NotifyChanged( "CurrentModule" );
            }
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            if ( data is IModelSystemStructure )
            {
                // If we are here then we were called from the parent level
                this.CurrentModule = data as IModelSystemStructure;
                this.Context = new ModelSystemPageContext();
                var project = this.XTMF.CurrentProject;
                this.Context.Project = project;
                this.Context.ModelSystem = data as IModelSystemStructure;
                this.Context.CurrentNode = data as IModelSystemStructure;
                this.Delete.IsEnabled = true;
                this.ParentNodes.Clear();
                var index = project.ModelSystemStructure.IndexOf( CurrentModule );
                if ( index >= 0 )
                {
                    bool editMode = false;
                    string value;
                    if ( this.XTMF.XTMF.Configuration.AdditionalSettings.TryGetValue( "EditProjects", out value ) )
                    {
                        bool.TryParse( value, out editMode );
                    }
                    this.ModelSystemInterface.LoadProject( project, index, editMode );
                    this.ColourKey.EditMode = editMode;
                }
                this.Run.IsEnabled = true;
            }
            else if ( data is ModelSystemPageContext )
            {
                var context = data as ModelSystemPageContext;
                this.Context = context;
                this.CurrentModule = context.CurrentNode;
                this.Delete.IsEnabled = context.CurrentNode == context.ModelSystem;
            }
            else if ( data is QuestionResult )
            {
                var result = data as QuestionResult;
                if ( result.Success == true )
                {
                    if ( result.Data == null || result.Data is IModelSystemStructure )
                    {
                        var module = result.Data as IModelSystemStructure;
                        if ( module != null )
                        {
                            this.ModelSystemInterface.AddCommand( new ModuleRenameCommand( module, result.Result ) );
                            this.ModuleNameLabel.Text = this.CurrentModule.Name;
                            this.ModelSystemInterface.Refresh( module );
                        }
                        else
                        {
                            this.ModelSystemInterface.AddCommand( new ModuleRenameCommand( CurrentModule, result.Result ) );
                            this.ModuleNameLabel.Text = this.CurrentModule.Name;
                            this.ModelSystemInterface.Refresh( this.CurrentModule );
                        }
                        this.ModuleNameLabel.ToolTip = this.ModuleNameLabel.Text;
                        this.XTMF.CheckToSave = true;
                        this.XTMF.CurrentProject.HasChanged = true;
                        this.XTMF.Reload = true;
                    }
                    else if ( result.Data is IModelSystem )
                    {
                        IModelSystem baseMS = result.Data as IModelSystem;
                        string error = null;
                        var lp = this.Context.Project.LinkedParameters[this.Context.Project.ModelSystemStructure.IndexOf( this.Context.ModelSystem )];
                        var ms = this.XTMF.CreateCopy( this.Context.ModelSystem, lp, baseMS.Name, this.Context.ModelSystem.Description );
                        if ( !ms.Save( ref error ) )
                        {
                            MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                        }
                    }
                    if ( result.Data is string )
                    {
                        var ms = this.ModelSystemInterface.ModelSystem;
                        var dataString = result.Data as string;
                        string error = null;
                        if ( dataString == "Rename LinkedParameter" )
                        {
                            this.ModelSystemInterface.AddCommand( new XTMF.Commands.Editing.RenameLinkedParameter( this.LastLinkedParameterRequest, result.Result ), ref error );
                            this.ModelSystemInterface.RefreshLinkedParameters();
                        }
                    }
                    return;
                }
            }
            this.ModuleIndirection.Clear();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( !e.Handled )
            {
                if ( e.Key == Key.Z && e.KeyboardDevice.Modifiers == ModifierKeys.Control )
                {
                    this.ModelSystemInterface.Undo();
                    this.UndoButton.FlashAnimation( 1 );
                    e.Handled = true;
                }
            }
            base.OnKeyUp( e );
        }

        private void Back_Clicked(object obj)
        {
            if ( this.ParentNodes.Count > 0 )
            {
                this.Context.CurrentNode = this.ParentNodes.Pop();
                this.XTMF.Navigate( XTMFPage.ModelSystemSettingsPage, this.Context );
            }
            else
            {
                this.XTMF.Navigate( XTMFPage.ProjectSettingsPage );
            }
        }

        private void Delete_Clicked(object obj)
        {
            var project = this.XTMF.CurrentProject;
            if ( project != null )
            {
                this.XTMF.Navigate( XTMFPage.RemoveModelSystem, this.CurrentModule );
            }
        }

        private void ModelSystemInterface_RenameRequested(IModelSystemStructure obj)
        {
            this.Rename_Clicked( obj );
        }

        private void ModelSystemPage_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus( this );
        }

        private void ModelSystemViewer_RenameLinkedParamater(ILinkedParameter obj)
        {
            LastLinkedParameterRequest = obj;
            QuestionData question = new QuestionData()
            {
                DefaultText = obj.Name,
                Validate = ValidateSubmodelName,
                Question = "What would you like to call this linked parameter?",
                Path = this.Path,
                Hint = "Linked Parameter Name",
                OnSuccess = XTMFPage.ModelSystemSettingsPage,
                OnSuccessData = "Rename LinkedParameter",
                OnCancel = XTMFPage.ModelSystemSettingsPage,
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void NotifyChanged(string parameter)
        {
            var handle = PropertyChanged;
            if ( handle != null )
            {
                handle( this, new PropertyChangedEventArgs( parameter ) );
            }
        }

        private void RebuildQuickParameters()
        {
            this.QuickParameters.Clear();
            this.RebuildQuickParameters( this.CurrentModule );
        }

        private void RebuildQuickParameters(IModelSystemStructure current)
        {
            var parameters = current.Parameters;
            if ( parameters != null )
            {
                foreach ( var par in parameters )
                {
                    if ( par.QuickParameter )
                    {
                        this.QuickParameters.Add( par );
                    }
                }
            }
            IList<IModelSystemStructure> desc = current.Children;
            if ( desc == null ) return;
            foreach ( var child in desc )
            {
                this.RebuildQuickParameters( child );
            }
        }

        private void Rename_Clicked(object obj)
        {
            QuestionData question = new QuestionData();
            question.Question = "What would you like to rename the module?";
            var module = obj as IModelSystemStructure;
            if ( module != null )
            {
                question.DefaultText = module.Name;
            }
            else
            {
                question.DefaultText = this.CurrentModule.Name;
            }
            question.OnSuccess = XTMFPage.ModelSystemSettingsPage;
            question.OnCancel = XTMFPage.ModelSystemSettingsPage;
            question.OnCancelData = null;
            question.OnSuccessData = module;
            question.Path = this.Path;
            question.Hint = "The module's new name";
            question.Validate = ValidateModuleName;
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void Run_Clicked(object obj)
        {
            QuestionData question = new QuestionData();
            question.Path = this.Path;
            question.OnSuccess = XTMFPage.RunModelSystemPage;
            question.OnSuccessData = this.Context.ModelSystem;
            question.OnCancel = XTMFPage.ModelSystemSettingsPage;
            question.OnCancelData = this.Context;
            question.Validate = ValidateRunName;
            question.Question = "Select a unique name for the run";
            question.Hint = "Run Name";
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void SaveMS_Clicked(object obj)
        {
            this.NewMSHolder = new ModelSystem( this.XTMF.XTMF.Configuration );
            QuestionData question = new QuestionData();
            question.Path = this.Path;
            question.OnSuccess = XTMFPage.ModelSystemSettingsPage;
            question.OnSuccessData = this.NewMSHolder;
            question.OnCancel = XTMFPage.ModelSystemSettingsPage;
            question.OnCancelData = this.Context;
            question.Validate = ValidateModelSystemName;
            question.Question = "Select a unique name for the Model System";
            question.Hint = "Model System";
            question.DefaultText = this.ModuleNameLabel.Text;
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void SetModelSystems()
        {
        }

        private void UndoButton_Clicked(object obj)
        {
            this.ModelSystemInterface.Undo();
        }

        private string ValidateModelSystemName(string name)
        {
            if ( name == null || name == String.Empty )
            {
                return "The Model System must have a name.";
            }
            this.NewMSHolder.Name = name;
            return this.XTMF.UniqueModelSystemName( name );
        }

        private string ValidateModuleName(string name)
        {
            if ( name == null || name == String.Empty )
            {
                return "You must select a name for the module!";
            }
            return null;
        }

        private string ValidateRunName(string name)
        {
            if ( name == null || name == String.Empty )
            {
                return "You must select a name for your run.";
            }

            var invalidFileNameCharacters = System.IO.Path.GetInvalidFileNameChars();
            foreach ( var c in invalidFileNameCharacters )
            {
                if ( name.Contains( c ) )
                {
                    return String.Format( "The run name may not contain the character {0}.", c );
                }
            }

            var path = System.IO.Path.Combine( this.XTMF.XTMF.Configuration.ProjectDirectory, this.Context.Project.Name, name );
            if ( System.IO.Directory.Exists( path ) )
            {
                return "This name has already been taken!";
            }
            return null;
        }

        private string ValidateSubmodelName(string name)
        {
            if ( String.IsNullOrWhiteSpace( name ) )
            {
                return "A model system must have a name.";
            }
            return null;
        }

        private class ModelSystemPageContext
        {
            public IModelSystemStructure CurrentNode;
            public IModelSystemStructure ModelSystem;
            public IProject Project;
        }
    }
}