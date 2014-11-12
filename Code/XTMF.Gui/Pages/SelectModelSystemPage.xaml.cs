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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for AddModelSystemPage.xaml
    /// </summary>
    public partial class SelectModelSystemPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _PathEdit = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.SelectModelSystemPage };

        private static XTMFPage[] _PathNonEdit = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.ProjectSettingsPage,
            XTMFPage.SelectModelSystemPage };

        private double BaseAddButtonHeight;
        private ImageSource CancelIcon, RemoveIcon;
        private IModelSystem CurrentModelSystem;
        private bool EditMode;
        private ModelSystemContextMenu MSContextMenu;
        private Color SelectBlue, WarningRed;
        private SingleWindowGUI XTMF;

        public SelectModelSystemPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.MSContextMenu = new ModelSystemContextMenu();
            this.MSContextMenu.RemovePressed += new Action( MSContextMenu_RemovePressed );
            this.MSContextMenu.RenamePressed += new Action<string>( MSContextMenu_RenamePressed );
            this.BaseAddButtonHeight = this.AddNewMSButton.Height;
            this.CancelIcon = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/112_ArrowReturnLeft_Blue_24x24_72.png" ) );
            this.RemoveIcon = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/delete.png" ) );
            this.SelectBlue = (Color)Application.Current.Resources["SelectionBlue"];
            this.WarningRed = (Color)Application.Current.Resources["WarningRed"];
        }

        public XTMFPage[] Path
        {
            get { return this.EditMode ? _PathEdit : _PathNonEdit; }
        }

        public void SetActive(object data)
        {
            // we don't need to read the restrictions because we know that we are looking for model system templates
            bool editMode;
            this.Selector.Clear();
            if ( data is StartingPage )
            {
                editMode = this.EditMode = true;
            }
            else if ( data is ProjectSettingsPage )
            {
                editMode = this.EditMode = false;
            }
            else
            {
                editMode = this.EditMode;
            }
            this.DescriptionLabel.Content = editMode ? "Select which Model System to Edit" : "Select which Model System to Add";
            this.AddNewMSButton.Visibility = editMode ? Visibility.Visible : System.Windows.Visibility.Hidden;
            this.AddNewMSButton.Height = editMode ? this.BaseAddButtonHeight : 0;
            if ( data is QuestionResult )
            {
                var res = data as QuestionResult;
                var ms = this.CurrentModelSystem;
                var dataString = res.Data as string;
                string error = null;
                if ( dataString == "Rename" )
                {
                    // Rename the model system then save it
                    this.XTMF.Rename( ms, res.Result );
                    if ( !ms.Save( ref error ) )
                    {
                        MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                    }
                }
            }
            else if ( data is BooleanQuestionResult )
            {
                var res = data as BooleanQuestionResult;
                if ( res.Success && res.Result )
                {
                    this.XTMF.Delete( res.Data as IModelSystem );
                }
            }
            foreach ( var obj in XTMF.XTMF.ModelSystemController.ModelSystems )
            {
                var ms = obj as IModelSystem;
                this.Selector.Add( ms.Name, ms.Description, ms, this.MSContextMenu );
            }
            this.CurrentModelSystem = null;
        }

        private void AddModelSystemToCurrentProject(IModelSystem pickedMS)
        {
            this.XTMF.CheckToSave = true;
            string error = null;
            if ( !this.XTMF.XTMF.ProjectController.AddModelSystemToProject( this.XTMF.CurrentProject, pickedMS, ref error ) )
            {
                MessageBox.Show( "Unable to add " + pickedMS.Name + " to project " + this.XTMF.CurrentProject.Name + "\r\n" + error,
                    "Unable to add to project", MessageBoxButton.OK, MessageBoxImage.Error );
            }
            this.XTMF.Navigate( XTMFPage.ProjectSettingsPage );
        }

        private void AddNewMSButton_Clicked(object obj)
        {
            this.CurrentModelSystem = new ModelSystem( this.XTMF.XTMF.Configuration );
            // ok it is time to go and learn what model system they would like to make
            QuestionData descriptionQuestion = new QuestionData()
            {
                OnSuccess = XTMFPage.EditModelSystem,
                OnSuccessData = this.CurrentModelSystem,
                OnCancel = XTMFPage.SelectModelSystemPage,
                Question = "How would you describe the Model System?",
                Path = this.Path,
                Hint = "Description"
            };

            QuestionData question = new QuestionData()
            {
                OnSuccess = XTMFPage.QuestionPage,
                OnSuccessData = descriptionQuestion,
                OnCancel = XTMFPage.SelectModelSystemPage,
                Hint = "Model System Name",
                Question = "Select a unique name for the new Model System.",
                Path = this.Path,
                Validate = this.ValidModelSystemNameAndStore
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void EditModelSystemPage(IModelSystem pickedMS)
        {
            this.XTMF.Navigate( XTMFPage.EditModelSystem, pickedMS );
        }

        private void ModelSystem_Clicked(object obj)
        {
            IModelSystem pickedMS = obj as IModelSystem;
            if ( pickedMS != null )
            {
                if ( this.EditMode )
                {
                    // Do nothing for now, soon we will continue with this data
                    EditModelSystemPage( pickedMS );
                }
                else
                {
                    AddModelSystemToCurrentProject( pickedMS );
                }
            }
        }

        private void MSContextMenu_RemovePressed()
        {
            if ( this.CurrentModelSystem != null )
            {
                BooleanQuestionData question = new BooleanQuestionData();
                var modelSystem = this.CurrentModelSystem;
                question.Question = "Are you sure that you want to remove the model system '" + modelSystem.Name + "'?";
                question.OnSuccess = XTMFPage.SelectModelSystemPage;
                question.OnCancel = XTMFPage.SelectModelSystemPage;
                question.OnCancelData = null;
                question.OnSuccessData = modelSystem;
                question.Path = this.Path;
                question.Yes = new BooleanQuestionButton()
                {
                    Header = "Delete Model System",
                    Description = "Delete " + modelSystem.Name,
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
                var ms = this.CurrentModelSystem;
                QuestionData question = new QuestionData()
                {
                    DefaultText = ms.Name,
                    Validate = ValidModelSystemName,
                    Question = "What would you like to call this model system?",
                    Path = this.Path,
                    Hint = "Model System Name",
                    OnSuccess = XTMFPage.SelectModelSystemPage,
                    OnSuccessData = "Rename",
                    OnCancel = XTMFPage.SelectModelSystemPage,
                };
                this.XTMF.Navigate( XTMFPage.QuestionPage, question );
            }
        }

        private void Selector_ItemRightClicked(BorderIconButton button, object data)
        {
            this.CurrentModelSystem = data as IModelSystem;
        }

        private string ValidModelSystemName(string name)
        {
            if ( String.IsNullOrWhiteSpace( name ) )
            {
                return "A model system must have a name.";
            }
            return this.XTMF.UniqueModelSystemName( name );
        }

        private string ValidModelSystemNameAndStore(string name)
        {
            if ( String.IsNullOrWhiteSpace( name ) )
            {
                return "A model system must have a name.";
            }
            if ( this.CurrentModelSystem != null )
            {
                this.CurrentModelSystem.Name = name;
            }
            return this.XTMF.UniqueModelSystemName( name );
        }
    }
}