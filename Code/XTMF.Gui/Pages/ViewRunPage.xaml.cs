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
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ViewRunPage.xaml
    /// </summary>
    public partial class ViewRunPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.ProjectSettingsPage
        , XTMFPage.ViewProjectRunsPage, XTMFPage.ViewProjectRunPage};

        private IModelSystem NewMSHolder;
        private string RunDirectory;
        private SingleWindowGUI XTMF;

        public ViewRunPage()
        {
            InitializeComponent();
        }

        public ViewRunPage(SingleWindowGUI xtmf)
            : this()
        {
            this.XTMF = xtmf;
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            var result = data as QuestionResult;
            if ( result == null && data is string )
            {
                this.RunDirectory = System.IO.Path.GetDirectoryName( (string)data );
                this.ModelInterface.LoadRoot( ModelSystemStructure.Load( (string)data, this.XTMF.XTMF.Configuration ) );
                this.ModelInterface.EditMode = false;
            }
            else if ( result != null && result.Data is IModelSystem )
            {
                // save here
                IModelSystem ms = result.Data as IModelSystem;
                ms.ModelSystemStructure = this.ModelInterface.Root.Clone();
                string error = null;
                if ( !ms.Save( ref error ) )
                {
                    MessageBox.Show( "Unable to save Model System!\r\n" + error, "Unable to save!", MessageBoxButton.OK, MessageBoxImage.Error );
                }
                this.XTMF.XTMF.Configuration.ModelSystemRepository.Add( ms );
            }
        }

        private void DeleteRunButton_Clicked(object obj)
        {
            this.XTMF.Navigate( XTMFPage.DeleteRunPage, this.RunDirectory );
        }

        private void OpenOutputButton_Clicked(object obj)
        {
            try
            {
                if ( System.IO.Directory.Exists( this.RunDirectory ) )
                {
                    System.Diagnostics.Process.Start( this.RunDirectory );
                }
                else
                {
                    MessageBox.Show( this.RunDirectory + " does not exist!" );
                }
            }
            catch
            {
                MessageBox.Show( this.RunDirectory + " does not exist!" );
            }
        }

        private void ReRunButton_Clicked(object obj)
        {
            QuestionData question = new QuestionData();
            question.Path = this.Path;
            question.OnSuccess = XTMFPage.RunModelSystemPage;
            question.OnSuccessData = this.ModelInterface.Root;
            question.OnCancel = XTMFPage.ViewProjectRunPage;
            question.OnCancelData = null;
            question.Validate = ValidateRunName;
            question.Question = "Select a unique name for the run";
            question.Hint = "Run Name";
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private void SaveButton_Clicked(object obj)
        {
            this.NewMSHolder = new ModelSystem( this.XTMF.XTMF.Configuration );
            QuestionData question = new QuestionData();
            question.Path = this.Path;
            question.OnSuccess = XTMFPage.ViewProjectRunPage;
            question.OnSuccessData = this.NewMSHolder;
            question.OnCancel = XTMFPage.ViewProjectRunPage;
            question.OnCancelData = null;
            question.Validate = ValidateModelSystemName;
            question.Question = "Select a unique name for the Model System";
            question.Hint = "Model System";
            question.DefaultText = System.IO.Path.GetFileName( System.IO.Path.GetDirectoryName( this.RunDirectory ) );
            this.XTMF.Navigate( XTMFPage.QuestionPage, question );
        }

        private string ValidateModelSystemName(string name)
        {
            if ( name == null || name == String.Empty )
            {
                return "The Model System must have a name.";
            }
            this.NewMSHolder.Name = name;
            var modelSystemDirectory = this.XTMF.XTMF.Configuration.ModelSystemDirectory;
            if ( System.IO.File.Exists( System.IO.Path.Combine( modelSystemDirectory, name + ".xml" ) ) )
            {
                return "A Model System with this name already exists!";
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

            var path = System.IO.Path.Combine( this.XTMF.XTMF.Configuration.ProjectDirectory,
                System.IO.Path.GetDirectoryName( this.RunDirectory ), name );
            if ( System.IO.Directory.Exists( path ) )
            {
                return "This name has already been taken!";
            }
            return null;
        }
    }
}