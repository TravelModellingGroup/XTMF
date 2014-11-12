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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for ViewRunsPage.xaml
    /// </summary>
    public partial class ViewRunsPage : UserControl, IXTMFPage
    {
        protected Category[] Categories;

        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.ProjectSettingsPage
        , XTMFPage.ViewProjectRunsPage};

        private ImageSource Icon;
        private CategorySelectMenu Menu;
        private string ProjectDirectory;
        private BorderIconButton SelectedButton;
        private string SelectedRunName;
        private SingleWindowGUI XTMF;

        public ViewRunsPage()
        {
            InitializeComponent();
            this.Menu = new CategorySelectMenu();
        }

        public ViewRunsPage(SingleWindowGUI xtmf)
            : this()
        {
            this.XTMF = xtmf;
            this.Menu.CategorySelected += new Action<Category>( Menu_CategorySelected );
            this.Menu.NewCategory += new Action<Category>( Menu_NewCategory );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            var currentProject = this.XTMF.CurrentProject;
            if ( currentProject == null )
            {
                MessageBox.Show( "There was no project selected!" );
                Environment.Exit( 1 );
            }
            try
            {
                var projectDirectory = System.IO.Path.Combine( this.XTMF.XTMF.Configuration.ProjectDirectory, currentProject.Name );
                this.ProjectDirectory = projectDirectory;
                this.Categories = LoadCategories( projectDirectory );
                var array = LoadAndStoreAllRuns( currentProject, Categories );
                this.SetupPastRuns( projectDirectory, array );
                this.Menu.Categoies = Categories;
            }
            catch ( Exception e )
            {
                MessageBox.Show( e.Message + "\r\n" + e.StackTrace );
                Environment.Exit( 1 );
            }
        }

        private void AddSelectedRun(Category obj)
        {
            if ( obj.RunNames == null )
            {
                obj.RunNames = new string[1];
                obj.Loaded = new bool[1];
            }
            else
            {
                var temp = new string[obj.RunNames.Length + 1];
                Array.Copy( obj.RunNames, temp, obj.RunNames.Length );
                obj.RunNames = temp;
                var tempb = new bool[obj.Loaded.Length + 1];
                Array.Copy( obj.Loaded, tempb, obj.Loaded.Length );
                obj.Loaded = tempb;
            }
            obj.RunNames[obj.RunNames.Length - 1] = System.IO.Path.GetFileName( System.IO.Path.GetDirectoryName( this.SelectedRunName ) );
            obj.Loaded[obj.Loaded.Length - 1] = true;
        }

        private Category FindCategory(Category[] categories, string name)
        {
            for ( int i = 0; i < categories.Length; i++ )
            {
                if ( categories[i].RunNames == null ) continue;
                for ( int j = 0; j < categories[i].RunNames.Length; j++ )
                {
                    if ( categories[i].RunNames[j] == name )
                    {
                        categories[i].Loaded[j] = true;
                        return categories[i];
                    }
                }
            }
            return null;
        }

        private KeyValuePair<string, Category>[] LoadAndStoreAllRuns(IProject currentProject, Category[] categories)
        {
            var projectDirectory = System.IO.Path.Combine( this.XTMF.XTMF.Configuration.ProjectDirectory, currentProject.Name );
            KeyValuePair<string, Category>[] array = null;
            try
            {
                var subDirectories = Directory.GetDirectories( projectDirectory );
                ConcurrentBag<KeyValuePair<string, Category>> runs = new ConcurrentBag<KeyValuePair<string, Category>>();
                if ( this.Icon == null )
                {
                    this.Icon = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/base_cog_32.png" ) );
                }
                Parallel.For( 0, subDirectories.Length, delegate(int i)
                {
                    try
                    {
                        if ( File.Exists( System.IO.Path.Combine( subDirectories[i], "RunParameters.xml" ) ) )
                        {
                            var name = System.IO.Path.GetFileName( subDirectories[i] );
                            runs.Add( new KeyValuePair<string, Category>( name, FindCategory( categories, name ) ) );
                        }
                    }
                    catch ( IOException )
                    {
                    }
                } );
                array = runs.ToArray();
                this.Sort( array );
            }
            catch ( AggregateException e )
            {
                MessageBox.Show( "We experienced an error in the XTMF code\r\n" + e.Message + "\r\n" + e.StackTrace );
            }
            catch ( IOException )
            {
            }
            return array;
        }

        private Category[] LoadCategories(string projectDirectory)
        {
            try
            {
                List<Category> cats = new List<Category>();
                // it will throw an exception if we do not have any RunOrganization data yet
                XmlDocument doc = new XmlDocument();
                doc.Load( System.IO.Path.Combine( projectDirectory, "RunOrganization.xml" ) );
                var root = doc["Root"];
                if ( root != null )
                {
                    // if there are no entries, then we have no categories
                    if ( !root.HasChildNodes || root.ChildNodes == null || root.ChildNodes.Count == 0 )
                    {
                        return new Category[0];
                    }
                    // otherwise go through the children
                    foreach ( XmlNode categoryChild in root.ChildNodes )
                    {
                        if ( categoryChild.Name == "Category" )
                        {
                            Category c = new Category();
                            c.Name = categoryChild.Attributes["Name"].InnerText;
                            c.Colour = Color.FromRgb( byte.Parse( categoryChild.Attributes["R"].InnerText ),
                                byte.Parse( categoryChild.Attributes["G"].InnerText ),
                                byte.Parse( categoryChild.Attributes["B"].InnerText ) );
                            cats.Add( c );
                            if ( categoryChild.HasChildNodes )
                            {
                                List<string> listedNames = new List<string>();
                                foreach ( XmlNode child in categoryChild.ChildNodes )
                                {
                                    if ( child.Name == "Run" )
                                    {
                                        listedNames.Add( child.Attributes["Name"].InnerText );
                                    }
                                }
                                c.RunNames = listedNames.ToArray();
                                c.Loaded = new bool[c.RunNames.Length];
                            }
                        }
                    }
                }
                return cats.ToArray();
            }
            catch
            {
                return new Category[0];
            }
        }

        private void Menu_CategorySelected(ViewRunsPage.Category obj)
        {
            this.RemoveSelectedRun();
            if ( obj == null )
            {
                this.SelectedButton.ShadowColour = (Color)Application.Current.TryFindResource( "ControlBackgroundColour" );
            }
            else
            {
                this.SelectedButton.ShadowColour = obj.Colour;
                this.AddSelectedRun( obj );
            }
            this.SaveCategories( this.ProjectDirectory, this.Categories );
        }

        private void Menu_NewCategory(ViewRunsPage.Category obj)
        {
            Category[] newList = null;
            if ( Categories == null )
            {
                newList = new Category[1];
                newList[0] = obj;
            }
            else
            {
                newList = new Category[this.Categories.Length + 1];
                Array.Copy( this.Categories, newList, this.Categories.Length );
                newList[this.Categories.Length] = obj;
            }
            this.Categories = newList;
            this.SaveCategories( this.ProjectDirectory, this.Categories );
            this.Menu.Categoies = this.Categories;
        }

        private void PastRuns_ItemRightClicked(BorderIconButton button, object obj)
        {
            this.SelectedButton = button;
            this.SelectedRunName = obj as string;
        }

        private void PastRuns_ItemSelected(object obj)
        {
            this.XTMF.Navigate( XTMFPage.ViewProjectRunPage, obj );
        }

        private void RemoveSelectedRun()
        {
            if ( this.Categories == null ) return;
            var selectedName = System.IO.Path.GetFileName( System.IO.Path.GetDirectoryName( this.SelectedRunName ) );
            for ( int i = 0; i < this.Categories.Length; i++ )
            {
                if ( this.Categories[i].RunNames == null ) continue;
                for ( int j = 0; j < this.Categories[i].RunNames.Length; j++ )
                {
                    if ( this.Categories[i].RunNames[j] == selectedName )
                    {
                        var temp = new string[this.Categories[i].RunNames.Length - 1];
                        Array.Copy( this.Categories[i].RunNames, temp, j );
                        Array.Copy( this.Categories[i].RunNames, j + 1, temp, j, this.Categories[i].RunNames.Length - j - 1 );
                        this.Categories[i].RunNames = temp;
                        var tempb = new bool[this.Categories[i].Loaded.Length - 1];
                        Array.Copy( this.Categories[i].Loaded, tempb, j );
                        Array.Copy( this.Categories[i].Loaded, j + 1, tempb, j, this.Categories[i].Loaded.Length - j - 1 );
                        this.Categories[i].Loaded = tempb;
                    }
                }
            }
        }

        private void SaveCategories(string projectDirectory, Category[] categories)
        {
            try
            {
                using ( XmlWriter writer = XmlTextWriter.Create( System.IO.Path.Combine( projectDirectory, "RunOrganization.xml" ),
                    new XmlWriterSettings() { Indent = true, Encoding = Encoding.Unicode } ) )
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement( "Root" );
                    for ( int i = 0; i < categories.Length; i++ )
                    {
                        if ( categories[i].RunNames == null || categories[i].RunNames.Length == 0 ) continue;
                        writer.WriteStartElement( "Category" );
                        writer.WriteAttributeString( "Name", categories[i].Name );
                        writer.WriteAttributeString( "R", categories[i].Colour.R.ToString() );
                        writer.WriteAttributeString( "G", categories[i].Colour.G.ToString() );
                        writer.WriteAttributeString( "B", categories[i].Colour.B.ToString() );
                        for ( int j = 0; j < categories[i].RunNames.Length; j++ )
                        {
                            // don't save if there is no record of this run
                            if ( !categories[i].Loaded[j] ) continue;
                            writer.WriteStartElement( "Run" );
                            writer.WriteAttributeString( "Name", categories[i].RunNames[j] );
                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            catch ( IOException )
            {
            }
        }

        private void SetupPastRuns(string projectDirectory, KeyValuePair<string, Category>[] array)
        {
            this.PastRuns.Clear();
            var backgroundColour = (Color)Application.Current.TryFindResource( "ControlBackgroundColour" );
            for ( int i = 0; i < array.Length; i++ )
            {
                if ( array[i].Value != null )
                {
                    this.PastRuns.Add( array[i].Key, "View this run", System.IO.Path.GetFullPath( System.IO.Path.Combine( projectDirectory, array[i].Key, "RunParameters.xml" ) ), this.Menu, array[i].Value.Colour );
                }
                else
                {
                    this.PastRuns.Add( array[i].Key, "View this run", System.IO.Path.GetFullPath( System.IO.Path.Combine( projectDirectory, array[i].Key, "RunParameters.xml" ) ), this.Menu, backgroundColour );
                }
            }
        }

        private void Sort(KeyValuePair<string, Category>[] array)
        {
            for ( int i = 0; i < array.Length; i++ )
            {
                for ( int j = 0; j < array.Length - i - 1; j++ )
                {
                    if ( array[j + 1].Key == null
                        || array[j].Key.CompareTo( array[j + 1].Key ) > 0 )
                    {
                        var temp = array[j + 1];
                        array[j + 1] = array[j];
                        array[j] = temp;
                    }
                }
            }
        }

        public class Category
        {
            internal Color Colour;
            internal bool[] Loaded;
            internal string Name;
            internal string[] RunNames;
        }
    }
}