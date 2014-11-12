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
    /// Interaction logic for DeleteProjectPage.xaml
    /// </summary>
    public partial class DeleteModelSystemPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.SelectModelSystemPage, XTMFPage.EditModelSystem, XTMFPage.DeleteModelSystemPage };
        private IModelSystem CurrentModelSystem;
        private SingleWindowGUI XTMF;

        public DeleteModelSystemPage(SingleWindowGUI xmtfWindow)
        {
            this.XTMF = xmtfWindow;
            InitializeComponent();
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            var temp = data as IModelSystem;
            if ( temp != null )
            {
                this.CurrentModelSystem = temp;
                ProjectNameLabel.Content = this.CurrentModelSystem.Name;
            }
        }

        private void CancelButton_Clicked(object obj)
        {
            this.CurrentModelSystem = null;
            this.XTMF.Navigate( XTMFPage.EditModelSystem );
        }

        private void DeleteButton_Clicked(object obj)
        {
            this.XTMF.Delete( this.CurrentModelSystem );
            this.CurrentModelSystem = null;
            this.XTMF.Navigate( XTMFPage.SelectModelSystemPage );
        }
    }
}