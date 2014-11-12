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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for SaveProjectPage.xaml
    /// </summary>
    public partial class SaveProjectPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.SaveProjectPage };
        private SingleWindowGUI XTMF;

        public SaveProjectPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.Loaded += new RoutedEventHandler( SaveProjectPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            this.ProjectNameLabel.Content = XTMF.CurrentProject.Name;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Enter )
                {
                    e.Handled = true;
                    this.SaveButton_Clicked( this.SaveButton );
                }
            }
            base.OnKeyUp( e );
        }

        private void DontSaveButton_Clicked(object obj)
        {
            XTMF.CheckToSave = false;
            XTMF.Navigate( XTMFPage.NumberOfPages );
        }

        private void SaveButton_Clicked(object obj)
        {
            string error = null;
            XTMF.CheckToSave = false;
            this.XTMF.CurrentProject.Save( ref error );
            XTMF.Navigate( XTMFPage.NumberOfPages );
        }

        private void SaveProjectPage_Loaded(object sender, RoutedEventArgs e)
        {
            if ( this.XTMF.XTMF.Configuration.AutoSave )
            {
                this.SaveButton_Clicked( this.SaveButton );
            }
        }
    }
}