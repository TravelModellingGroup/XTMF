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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ProjectSettings.xaml
    /// </summary>
    public partial class ProjectSettingsPage : UserControl, INotifyPropertyChanged, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.ProjectSettingsPage };

        private IProject _CurrentProject;

        private ImageSource CancelIcon, RemoveIcon;

        private IModelSystemStructure CurrentModelSystem;

        private ModelSystemContextMenu MSContextMenu;

        private ModelSystem SaveAsMS;

        private Color SelectBlue, WarningRed;

        private SingleWindowGUI XTMF;

        public ProjectSettingsPage(SingleWindowGUI xtmfWindow)
        {
            InitializeComponent();
            this.XTMF = xtmfWindow;
            this.MSContextMenu = new ModelSystemContextMenu( true );
            this.MSContextMenu.RenamePressed += new Action<string>( MSContextMenu_RenamePressed );
            this.MSContextMenu.RemovePressed += new Action( MSContextMenu_RemovePressed );
            this.MSContextMenu.SaveAsModelSystemPressed += new Action( MSContextMenu_SaveAsModelSystemPressed );
            this.Loaded += new RoutedEventHandler( ProjectSettingsPage_Loaded );
            this.CancelIcon = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/112_ArrowReturnLeft_Blue_24x24_72.png" ) );
            this.RemoveIcon = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/delete.png" ) );
            this.SelectBlue = (Color)Application.Current.Resources["SelectionBlue"];
            this.WarningRed = (Color)Application.Current.Resources["WarningRed"];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        private IProject CurrentProject
        {
            get
            {
                return this._CurrentProject;
            }

            set
            {
                this._CurrentProject = value;

                if ( this._CurrentProject == null )
                {
                    this.ProjectNameLabel.Content = "No project is Loaded!";
                }
                else
                {
                    this.ProjectNameLabel.Content = this.CurrentProject.Name;
                }
                this.SetModelSystems();
                this.NotifyChanged( "CurrentProject" );
            }
        }

        public void SetActive(object data)
        {
            this.ModelSystemSelector.ClearFilter();
            if ( data is QuestionResult )
            {
                var res = data as QuestionResult;
                if ( res.Success )
                {
                    if ( res.Data is IModelSystemStructure )
                    {
                        var selectedModule = res.Data as IModelSystemStructure;
                        selectedModule.Name = res.Result;
                        var index = this._CurrentProject.ModelSystemStructure.IndexOf( selectedModule );
                        string error = null;
                        if ( !this._CurrentProject.Save( ref error ) )
                        {
                            MessageBox.Show( "Unable to save Project!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                        }
                        this.SetModelSystems();
                    }
                    else if ( res.Data is IModelSystem )
                    {
                        IModelSystem baseMS = res.Data as IModelSystem;
                        string error = null;
                        var lp = this.CurrentProject.LinkedParameters[this.CurrentProject.ModelSystemStructure.IndexOf( this.CurrentModelSystem )];
                        var ms = this.XTMF.CreateCopy( this.CurrentModelSystem, lp, baseMS.Name, this.CurrentModelSystem.Description );
                        if ( !ms.Save( ref error ) )
                        {
                            MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                        }
                    }
                }
            }
            else if ( data is BooleanQuestionResult )
            {
                var res = data as BooleanQuestionResult;
                if ( res.Success && res.Result )
                {
                    string error = null;
                    this._CurrentProject.ModelSystemStructure.Remove( res.Data as IModelSystemStructure );
                    if ( !this._CurrentProject.Save( ref error ) )
                    {
                        MessageBox.Show( "Unable to save project!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                    }
                    this.SetModelSystems();
                }
            }
            this.SaveAsMS = null;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Escape )
                {
                    e.Handled = true;
                    this.XTMF.Navigate( XTMFPage.ProjectSelectPage );
                }
            }
            base.OnKeyDown( e );
        }

        private void DeleteProjectButton_Clicked(object obj)
        {
            this.XTMF.Navigate( XTMFPage.DeleteProjectPage );
        }

        private void ModelSystemSelected(object obj)
        {
            var structure = obj as IModelSystemStructure;
            if ( structure != null )
            {
                this.XTMF.Navigate( XTMFPage.ModelSystemSettingsPage, structure );
            }
        }

        private void ModelSystemSelector_ItemRightClicked(BorderIconButton buttonPressed, object data)
        {
            this.CurrentModelSystem = data as IModelSystemStructure;
        }

        private void MSContextMenu_RemovePressed()
        {
            if ( this.CurrentModelSystem != null )
            {
                BooleanQuestionData question = new BooleanQuestionData();
                var module = this.CurrentModelSystem;
                question.Question = "Are you sure that you want to remove the model system '" + module.Name + "'?";
                question.OnSuccess = XTMFPage.ProjectSettingsPage;
                question.OnCancel = XTMFPage.ProjectSettingsPage;
                question.OnCancelData = null;
                question.OnSuccessData = module;
                question.Path = this.Path;
                question.Yes = new BooleanQuestionButton()
                {
                    Header = "Delete Model System",
                    Description = "Delete " + module.Name,
                    Icon = this.RemoveIcon,
                    Highlight = this.WarningRed
                };
                question.No = new BooleanQuestionButton()
                {
                    Header = "Cancel",
                    Description = "Do not delete the Model System",
                    Icon = this.CancelIcon,
                    Highlight = SelectBlue
                };
                this.XTMF.Navigate( XTMFPage.BooleanQuestionPage, question );
            }
        }

        private void MSContextMenu_RenamePressed(string name)
        {
            if ( this.CurrentModelSystem != null )
            {
                QuestionData question = new QuestionData();
                question.Question = "What would you like to rename the model system?";
                var module = this.CurrentModelSystem;
                question.DefaultText = module.Name;
                question.OnSuccess = XTMFPage.ProjectSettingsPage;
                question.OnCancel = XTMFPage.ProjectSettingsPage;
                question.OnCancelData = null;
                question.OnSuccessData = module;
                question.Path = this.Path;
                question.Hint = "The model systems's new name";
                question.Validate = ValidateModuleName;
                this.XTMF.Navigate( XTMFPage.QuestionPage, question );
            }
        }

        private void MSContextMenu_SaveAsModelSystemPressed()
        {
            if ( this.CurrentModelSystem != null )
            {
                this.SaveAsMS = new ModelSystem( this.XTMF.XTMF.Configuration );
                QuestionData question = new QuestionData();
                question.Path = this.Path;
                question.OnSuccess = XTMFPage.ProjectSettingsPage;
                question.OnSuccessData = this.SaveAsMS;
                question.OnCancel = XTMFPage.ProjectSettingsPage;
                question.OnCancelData = null;
                question.Validate = ValidateModelSystemName;
                question.Question = "Select a unique name for the Model System";
                question.Hint = "Model System";
                question.DefaultText = this.CurrentModelSystem.Name;
                this.XTMF.Navigate( XTMFPage.QuestionPage, question );
            }
        }

        private void NewModelSystemButton_Clicked(object obj)
        {
            this.XTMF.Navigate( XTMFPage.SelectModelSystemPage, this );
        }

        private void NotifyChanged(string propertyName)
        {
            if ( this.PropertyChanged != null )
            {
                this.PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }

        private void OpenProjectFolderButton_Clicked(object obj)
        {
            var directoryName = System.IO.Path.Combine( this.XTMF.XTMF.Configuration.ProjectDirectory, this.CurrentProject.Name );
            if ( this.CurrentProject != null && System.IO.Directory.Exists( directoryName ) )
            {
                System.Diagnostics.Process.Start( directoryName );
            }
        }

        private void ProjectSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.CurrentProject = this.XTMF.CurrentProject;
        }

        private void SetModelSystems()
        {
            this.ModelSystemSelector.Clear();
            if ( this._CurrentProject != null )
            {
                foreach ( var ms in this._CurrentProject.ModelSystemStructure )
                {
                    this.ModelSystemSelector.Add( ms.Name, ms.Description, ms, this.MSContextMenu );
                }
            }
        }

        private string ValidateModelSystemName(string name)
        {
            if ( name == null || name == String.Empty )
            {
                return "The Model System must have a name.";
            }
            this.SaveAsMS.Name = name;
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

        private void ViewRunsButton_Clicked(object obj)
        {
            this.XTMF.Navigate( XTMFPage.ViewProjectRunsPage );
        }
    }
}