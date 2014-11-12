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
using System.Windows.Controls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.SettingsPage };
        private SingleWindowGUI XTMF;

        public SettingsPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            string value;
            if ( this.XTMF.XTMF.Configuration.AdditionalSettings.TryGetValue( "UseGlass", out value ) )
            {
                bool glass = false;
                bool.TryParse( value, out glass );
                this.GlassBox.IsChecked = glass;
            }
            if ( this.XTMF.XTMF.Configuration.AdditionalSettings.TryGetValue( "EditProjects", out value ) )
            {
                bool editProject;
                bool.TryParse( value, out editProject );
                this.EditProjectBox.IsChecked = editProject;
            }
            this.ProjectDirectoryBox.Text = this.XTMF.XTMF.Configuration.ProjectDirectory;
            this.AutoSaveBox.IsChecked = this.XTMF.XTMF.Configuration.AutoSave;
            Validate();
        }

        private void ProjectDirectoryBox_TextChanged(object obj)
        {
            Validate();
        }

        private void SaveButton_Clicked(object obj)
        {
            string error = null;
            if ( !this.XTMF.XTMF.Configuration.SetProjectDirectory( this.ProjectDirectoryBox.Text, ref error ) )
            {
                return;
            }
            this.XTMF.XTMF.Configuration.AutoSave = ( this.AutoSaveBox.IsChecked == true );
            this.XTMF.XTMF.Configuration.AdditionalSettings["UseGlass"] = ( this.GlassBox.IsChecked == true ).ToString();
            this.XTMF.XTMF.Configuration.AdditionalSettings["EditProjects"] = ( this.EditProjectBox.IsChecked == true ).ToString();
            this.XTMF.XTMF.Configuration.Save();
            this.XTMF.Window_Loaded( null, null );
            this.XTMF.Navigate( XTMFPage.StartPage );
        }

        private void Validate()
        {
            string error = null;
            bool ableToSave = this.XTMF.XTMF.Configuration.ValidateProjectDirectory( this.ProjectDirectoryBox.Text, ref error );
            this.ErrorLabel.Content = error;
            this.SaveButton.IsEnabled = ableToSave;
        }
    }
}