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
using System.Windows.Input;
using System.Windows.Threading;
using XTMF.Commands.Editing;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for EditModelSystemPage.xaml
    /// </summary>
    public partial class EditModelSystemPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.SelectModelSystemPage, XTMFPage.EditModelSystem };
        private ILinkedParameter LastLinkedParameterRequest;
        private IModelSystemStructure LastRenameStructureRequest;
        private SingleWindowGUI XTMF;

        public EditModelSystemPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.ModelSystemViewer.Config = xtmf.XTMF.Configuration;
            this.Loaded += new RoutedEventHandler( EditModelSystemPage_Loaded );
            this.ModelSystemViewer.ModuleRepository = this.XTMF.XTMF.Configuration.ModelRepository;
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke( DispatcherPriority.Background,
               new DispatcherOperationCallback( ExitFrame ), frame );
            Dispatcher.PushFrame( frame );
        }

        public object ExitFrame(object f)
        {
            ( (DispatcherFrame)f ).Continue = false;
            return null;
        }

        public void SetActive(object data)
        {
            if ( data is IModelSystem )
            {
                DoEvents();
                this.ModelSystemViewer.ModelSystem = data as IModelSystem;
                this.ModelSystemNameLabel.ToolTip = this.ModelSystemNameLabel.Text = ( data as IModelSystem ).Name;
            }
            else if ( data is QuestionResult )
            {
                var res = data as QuestionResult;
                if ( res.Success )
                {
                    if ( res.Data is string )
                    {
                        var ms = this.ModelSystemViewer.ModelSystem;
                        var dataString = res.Data as string;
                        string error = null;
                        if ( dataString == "Rename" )
                        {
                            this.XTMF.Rename( ms, res.Result );
                            this.ModelSystemNameLabel.ToolTip = this.ModelSystemNameLabel.Text = ms.Name;
                            if ( !ms.Save( ref error ) )
                            {
                                MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                            }
                        }
                        else if ( dataString == "Rename Submodel" )
                        {
                            this.ModelSystemViewer.AddCommand( new ModuleRenameCommand( this.LastRenameStructureRequest, res.Result ) );
                            this.ModelSystemViewer.Refresh( this.LastRenameStructureRequest );
                        }
                        else if ( dataString == "Rename LinkedParameter" )
                        {
                            this.ModelSystemViewer.AddCommand( new XTMF.Commands.Editing.RenameLinkedParameter( this.LastLinkedParameterRequest, res.Result ) );
                            this.ModelSystemViewer.RefreshLinkedParameters();
                        }
                        else if ( dataString == "Copy" )
                        {
                            // don't add the name here or it will try to load it
                            var newMS = this.XTMF.CreateCopy( ms.ModelSystemStructure, ms.LinkedParameters, res.Result, ms.Description );
                            if ( !newMS.Save( ref error ) )
                            {
                                MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                            }
                            this.ModelSystemNameLabel.ToolTip = this.ModelSystemNameLabel.Text = newMS.Name;
                            this.ModelSystemViewer.ModelSystem = newMS;
                        }
                    }
                    else
                    {
                        var resMS = res.Data as IModelSystem;
                        var ms = this.XTMF.CreateModelSystem( resMS.Name, resMS.Description, null, null );
                        if ( ms.ModelSystemStructure == null )
                        {
                            ms.ModelSystemStructure = new ModelSystemStructure( this.XTMF.XTMF.Configuration, null, typeof( IModelSystemTemplate ) );
                        }
                        ms.ModelSystemStructure.Name = ms.Name;
                        ms.Description = res.Result; // the last question is actually the description
                        this.ModelSystemViewer.ModelSystem = ms;
                        this.ModelSystemNameLabel.ToolTip = this.ModelSystemNameLabel.Text = ms.Name;
                        string error = null;
                        if ( !ms.Save( ref error ) )
                        {
                            MessageBox.Show( "Unable to save model system!\r\n" + error );
                        }
                    }
                }
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if ( e.Handled == false )
            {
                e.Handled = true;
            }
            base.OnGotFocus( e );
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( !e.Handled )
            {
            }
            base.OnKeyUp( e );
        }

        private void CopyButton_Clicked(object obj)
        {
            var ms = this.ModelSystemViewer.ModelSystem;
            QuestionData question = new QuestionData()
            {
                DefaultText = "Copy of " + ms.Name,
                Validate = ValidModelSystemName,
                Question = "What would you like to call this new model system?",
                Path = this.Path,
                Hint = "Model System Name",
                OnSuccess = XTMFPage.EditModelSystem,
                OnSuccessData = "Copy",
                OnCancel = XTMFPage.EditModelSystem,
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void DeleteButton_Clicked(object obj)
        {
            var ms = this.ModelSystemViewer.ModelSystem;
            this.XTMF.Navigate( XTMFPage.DeleteModelSystemPage, ms );
        }

        private void EditModelSystemPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.ModelSystemViewer.Focus();
        }

        private void ExportButton_Clicked(object obj)
        {
            var ms = this.ModelSystemViewer.ModelSystem;
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.FileName = this.ModelSystemViewer.ModelSystem.Name;
            saveDialog.InitialDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            saveDialog.Filter = "Model System (.xml)|*.xml|All Files (*.*)|*.*";
            saveDialog.FilterIndex = 0;
            saveDialog.ValidateNames = true;
            string error = null;
            if ( saveDialog.ShowDialog() == true )
            {
                ms.Save( saveDialog.FileName, ref error );
            }
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
                OnSuccess = XTMFPage.EditModelSystem,
                OnSuccessData = "Rename LinkedParameter",
                OnCancel = XTMFPage.EditModelSystem,
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void ModelSystemViewer_RenameRequested(IModelSystemStructure obj)
        {
            LastRenameStructureRequest = obj;
            QuestionData question = new QuestionData()
            {
                DefaultText = obj.Name,
                Validate = ValidateSubmodelName,
                Question = "What would you like to call this submodel?",
                Path = this.Path,
                Hint = "Submodel Name",
                OnSuccess = XTMFPage.EditModelSystem,
                OnSuccessData = "Rename Submodel",
                OnCancel = XTMFPage.EditModelSystem,
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void RenameButton_Clicked(object obj)
        {
            var ms = this.ModelSystemViewer.ModelSystem;
            QuestionData question = new QuestionData()
            {
                DefaultText = ms.Name,
                Validate = ValidModelSystemName,
                Question = "What would you like to call this model system?",
                Path = this.Path,
                Hint = "Model System Name",
                OnSuccess = XTMFPage.EditModelSystem,
                OnSuccessData = "Rename",
                OnCancel = XTMFPage.EditModelSystem,
            };
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void UndoButton_Clicked(object obj)
        {
            this.ModelSystemViewer.Undo();
        }

        private string ValidateSubmodelName(string name)
        {
            if ( String.IsNullOrWhiteSpace( name ) )
            {
                return "A model system must have a name.";
            }
            return null;
        }

        private string ValidModelSystemName(string name)
        {
            if ( String.IsNullOrWhiteSpace( name ) )
            {
                return "A model system must have a name.";
            }
            return this.XTMF.UniqueModelSystemName( name );
        }
    }
}