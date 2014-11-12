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
    /// Interaction logic for DeleteModelSystemPage.xaml
    /// </summary>
    public partial class RemoveModelSystemPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage,
            XTMFPage.ProjectSettingsPage, XTMFPage.ModelSystemSettingsPage, XTMFPage.RemoveModelSystem };

        private IModelSystemStructure CurrentProject;

        private SingleWindowGUI XTMF;

        public RemoveModelSystemPage(SingleWindowGUI XTMF)
        {
            this.XTMF = XTMF;
            InitializeComponent();
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            var temp = data as IModelSystemStructure;
            if ( temp != null )
            {
                this.CurrentProject = temp;
                this.ProjectNameLabel.Content = this.CurrentProject.Name;
            }
        }

        private void CancelButton_Clicked(object obj)
        {
            this.CurrentProject = null;
            this.XTMF.Navigate( XTMFPage.ModelSystemSettingsPage );
        }

        private void DeleteButton_Clicked(object obj)
        {
            int index = this.XTMF.CurrentProject.ModelSystemStructure.IndexOf( CurrentProject );
            if ( index >= 0 )
            {
                this.XTMF.CurrentProject.ModelSystemStructure.RemoveAt( index );
                this.XTMF.CurrentProject.LinkedParameters.RemoveAt( index );
                this.XTMF.CurrentProject.HasChanged = true;
                this.XTMF.CheckToSave = true;
                this.CurrentProject = null;
            }
            this.XTMF.Navigate( XTMFPage.ProjectSettingsPage );
        }
    }
}