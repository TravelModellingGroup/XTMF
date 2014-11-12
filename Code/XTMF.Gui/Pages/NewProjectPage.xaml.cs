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

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for NewProjectPage.xaml
    /// </summary>
    public partial class NewProjectPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.NewProjectPage };
        private SingleWindowGUI XTMFWindow;

        public NewProjectPage(SingleWindowGUI mainWindow)
        {
            InitializeComponent();
            this.XTMFWindow = mainWindow;
            this.Loaded += new RoutedEventHandler( NewProjectPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            this.ProjectNameBox.Text = String.Empty;
            this.ProjectDescriptionBox.Text = String.Empty;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if ( e.Handled == false )
            {
                e.Handled = true;
                Keyboard.Focus( this.ProjectNameBox );
            }
            base.OnGotFocus( e );
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( !e.Handled && e.Key == Key.Enter )
            {
                if ( this.ValidateSettings() )
                {
                    e.Handled = true;
                    this.SaveButton_Clicked( this.SaveButton );
                }
            }
            base.OnKeyUp( e );
        }

        private void CancelButton_Clicked(object obj)
        {
            this.XTMFWindow.Navigate( XTMFPage.ProjectSelectPage );
        }

        private void NewProjectPage_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus( this.ProjectNameBox );
        }

        private void SaveButton_Clicked(object obj)
        {
            IProject project = this.XTMFWindow.CreateProject( this.ProjectNameBox.Text, this.ProjectDescriptionBox.Text );
            if ( project == null )
            {
                this.XTMFWindow.Navigate( XTMFPage.StartPage );
            }
            else
            {
                this.XTMFWindow.Navigate( XTMFPage.ProjectSettingsPage );
            }
        }

        private void TitledTextbox_TextChanged(object obj)
        {
            bool valid = ValidateSettings();
            this.SaveButton.IsEnabled = valid;
        }

        private bool ValidateSettings()
        {
            var valid = this.XTMFWindow.XTMF.ProjectController.ValidateProjectName( this.ProjectNameBox.Text );
            return ( valid & this.ProjectDescriptionBox.Text.Length != 0 );
        }
    }
}