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
using Microsoft.Win32;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ImportTypeSelectPage.xaml
    /// </summary>
    public partial class ImportTypeSelectPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ImportPage };
        private ImportTypeContext Context;
        private QuestionData Question;
        private SingleWindowGUI XTMF;

        public ImportTypeSelectPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.Loaded += new RoutedEventHandler( ImportTypeSelectPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            if ( !( data is QuestionResult ) )
            {
                if ( this.Context == null )
                {
                    this.Context = new ImportTypeContext();
                }
                this.Context.Files = null;
                this.Context.Result = null;
                this.Context.Type = ImportTypeContext.ImportType.NumberOfImportTypes;
            }
            else
            {
                var res = data as QuestionResult;
                this.Context = res.Data as ImportTypeContext;
                this.Context.Result = res.Result;
            }
        }

        private void CopyFiles(string[] fileNames)
        {
            string copyToDir;
            if ( this.Context.Type == ImportTypeContext.ImportType.ModelSystem )
            {
                copyToDir = this.XTMF.XTMF.Configuration.ModelSystemDirectory;
            }
            else
            {
                copyToDir = this.XTMF.XTMF.Configuration.ProjectDirectory;
            }
            var length = fileNames.Length;
            string result = this.Context.Result;
            int startIndex = result == null ? 0 : 1;
            if ( result != null )
            {
                switch ( result )
                {
                    case "Overwrite":
                        {
                            if ( this.Context.Type == ImportTypeContext.ImportType.ModelSystem )
                            {
                                this.XTMF.LoadModelSystem( fileNames[0], true );
                            }
                            else
                            {
                                this.XTMF.ImportProject( fileNames[0], true );
                            }
                        }
                        break;

                    case "Rename":
                        {
                            var path = System.IO.Path.GetDirectoryName( fileNames[0] );
                            var baseFileName = System.IO.Path.GetFileNameWithoutExtension( fileNames[0] );
                            var extension = System.IO.Path.GetExtension( fileNames[0] );
                            for ( int i = 2; true; i++ )
                            {
                                var renamedForm = System.IO.Path.Combine( copyToDir, String.Format( "{0}({1}){2}", baseFileName, i, extension ) );

                                if ( this.Context.Type == ImportTypeContext.ImportType.ModelSystem )
                                {
                                    if ( !System.IO.File.Exists( renamedForm ) )
                                    {
                                        System.IO.File.Copy( fileNames[0], renamedForm, false );
                                        this.XTMF.LoadModelSystem( renamedForm, false );
                                        break;
                                    }
                                }
                                else if ( this.Context.Type == ImportTypeContext.ImportType.Project )
                                {
                                    this.XTMF.ImportProject( fileNames[0], false );
                                    break;
                                }
                            }
                        }
                        break;

                    case "Cancel":
                        {
                        }
                        break;
                }
            }
            for ( int i = startIndex; i < length; i++ )
            {
                if ( this.Context.Type == ImportTypeContext.ImportType.ModelSystem )
                {
                    string newMsFile = System.IO.Path.Combine( copyToDir, System.IO.Path.GetFileName( fileNames[i] ) );
                    if ( System.IO.File.Exists( newMsFile ) )
                    {
                        this.Context.Files = new string[length - i];
                        Array.Copy( fileNames, i, this.Context.Files, 0, length - i );
                        Question = new QuestionData()
                        {
                            Question = System.IO.Path.GetFileName( fileNames[i] ),
                            OnSuccess = XTMFPage.ImportPage,
                            OnCancel = XTMFPage.ImportPage,
                            OnSuccessData = this.Context,
                            OnCancelData = this.Context,
                            DefaultText = null,
                            Path = null,
                            Validate = null
                        };
                        this.XTMF.Navigate( XTMFPage.FileNamePage, Question );
                        return;
                    }
                    else
                    {
                        System.IO.File.Copy( fileNames[i], newMsFile, true );
                        this.XTMF.LoadModelSystem( newMsFile );
                    }
                }
                else if ( this.Context.Type == ImportTypeContext.ImportType.Project )
                {
                    this.XTMF.ImportProject( fileNames[0], false );
                }
            }

            if ( this.Context.Type == ImportTypeContext.ImportType.ModelSystem )
            {
                MessageBox.Show( "The Model System(s) have been imported successfully", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information );
            }
            else
            {
                MessageBox.Show( "The Project(s) have been imported successfully", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information );
            }

            // if we got here we are finished and can return the state
            this.Context.Type = ImportTypeContext.ImportType.NumberOfImportTypes;
            this.Context.Result = null;
            this.Context.Files = null;
        }

        private void ImportTypeSelectPage_Loaded(object sender, RoutedEventArgs e)
        {
            if ( this.Context == null || this.Context.Type == ImportTypeContext.ImportType.NumberOfImportTypes ) return;
            switch ( this.Context.Type )
            {
                case ImportTypeContext.ImportType.ModelSystem:
                    this.CopyFiles( this.Context.Files );
                    break;

                case ImportTypeContext.ImportType.Project:
                    break;

                case ImportTypeContext.ImportType.ModuleLibrary:
                    break;
            }
        }

        private void ModeSystemsButton_Clicked(object obj)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Multiselect = true;
            open.Filter = "Model System (.xml)|*.xml|All Files (*.*)|*.*";
            switch ( open.ShowDialog( this.XTMF ) )
            {
                case true:
                    {
                        this.Context.Type = ImportTypeContext.ImportType.ModelSystem;
                        CopyFiles( open.FileNames );
                    }
                    break;

                case false:
                case null:
                    break;
            }
        }

        private void ModuleButton_Clicked(object obj)
        {
        }

        private void ProjectsButton_Clicked(object obj)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Multiselect = true;
            open.Filter = "XTMF Project (.xml)|*.xml|All Files (*.*)|*.*";
            switch ( open.ShowDialog( this.XTMF ) )
            {
                case true:
                    {
                        this.Context.Type = ImportTypeContext.ImportType.Project;
                        CopyFiles( open.FileNames );
                    }
                    break;

                case false:
                case null:
                    break;
            }
        }

        private class ImportTypeContext
        {
            public string[] Files;

            public string Result;

            public ImportType Type;

            public enum ImportType { ModelSystem, Project, ModuleLibrary, NumberOfImportTypes }
        }
    }
}