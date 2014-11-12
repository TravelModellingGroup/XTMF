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
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ProjectPage.xaml
    /// </summary>
    public partial class ProjectPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage };
        private SingleWindowGUI XTMFGUI;

        public ProjectPage(SingleWindowGUI mainWindow)
        {
            XTMFGUI = mainWindow;
            this.XTMFGUI.Projects.ListChanged += new ListChangedEventHandler( Projects_ListChanged );
            InitializeComponent();
            this.Reset();
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void AddNewProject(IProject project)
        {
            this.ProjectSelector.Add( project.Name, project.Description, project );
        }

        public void Clear()
        {
            this.ProjectSelector.Clear();
        }

        public void SetActive(object data)
        {
            this.ProjectSelector.ClearFilter();
            this.ProjectSelector.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Escape )
                {
                    e.Handled = true;
                    this.XTMFGUI.Navigate( XTMFPage.StartPage );
                }
            }
            base.OnKeyDown( e );
        }

        private void NewProject_Clicked(object sender)
        {
            this.XTMFGUI.Navigate( XTMFPage.NewProjectPage );
        }

        private void Project_Selected(object item)
        {
            var project = item as IProject;
            if ( project != null )
            {
                this.XTMFGUI.CurrentProject = project;
                this.XTMFGUI.Navigate( XTMFPage.ProjectSettingsPage, project );
            }
        }

        private void Projects_ListChanged(object sender, ListChangedEventArgs e)
        {
            switch ( e.ListChangedType )
            {
                case ListChangedType.ItemAdded:
                    this.Reset();
                    break;

                case ListChangedType.ItemDeleted:
                case ListChangedType.ItemMoved:
                case ListChangedType.Reset:
                    this.Reset();
                    break;
            }
        }

        private void Reset()
        {
            this.ProjectSelector.Clear();
            foreach ( var project in this.XTMFGUI.XTMF.ProjectController.Projects )
            {
                this.AddNewProject( (IProject)project );
            }
        }
    }
}