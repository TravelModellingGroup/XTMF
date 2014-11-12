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
using System.Windows.Media;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    public class CategorySelectMenu : ContextMenu
    {
        protected Pages.ViewRunsPage.Category[] _Categoies;
        protected MenuItem[] MenuItems;

        public CategorySelectMenu()
        {
        }

        public event Action<Pages.ViewRunsPage.Category> CategorySelected;

        public event Action<Pages.ViewRunsPage.Category> NewCategory;

        public Pages.ViewRunsPage.Category[] Categoies
        {
            get
            {
                return _Categoies;
            }

            set
            {
                this._Categoies = value;
                this.Rebuild();
            }
        }

        private int FindIndex(object sender)
        {
            for ( int i = 0; i < this.MenuItems.Length; i++ )
            {
                if ( this.MenuItems[i] == sender )
                {
                    return i;
                }
            }
            return -1;
        }

        private void item_Click(object sender, RoutedEventArgs e)
        {
            int index = FindIndex( sender );
            if ( index != -1 )
            {
                if ( index == this.MenuItems.Length - 2 )
                {
                    var window = new NewCategoryWindow();
                    window.Owner = App.Current.MainWindow;
                    window.ValidateName = new Func<string, string>( delegate(string name)
                        {
                            if ( String.IsNullOrWhiteSpace( name ) ) return "Please pick a name";
                            if ( this._Categoies == null ) return null;
                            for ( int i = 0; i < this._Categoies.Length; i++ )
                            {
                                if ( this._Categoies[i].Name == name )
                                {
                                    return "The name \"" + name + "\" already exists!";
                                }
                            }
                            return null;
                        } );
                    if ( window.ShowDialog() == true )
                    {
                        Pages.ViewRunsPage.Category newCat = window.Category;
                        // Create a new category here!
                        var cs = this.NewCategory;
                        if ( cs != null )
                        {
                            cs( newCat );
                        }
                        cs = this.CategorySelected;
                        if ( cs != null )
                        {
                            cs( newCat );
                        }
                    }
                }
                else if ( index == this.MenuItems.Length - 1 )
                {
                    var cs = this.CategorySelected;
                    if ( cs != null )
                    {
                        cs( null );
                    }
                }
                else
                {
                    var cs = this.CategorySelected;
                    if ( cs != null )
                    {
                        cs( this.Categoies[index] );
                    }
                }
            }
        }

        private void Rebuild()
        {
            this.Items.Clear();
            if ( this.Categoies != null )
            {
                this.MenuItems = new MenuItem[this._Categoies.Length + 2];
                for ( int i = 0; i < this._Categoies.Length; i++ )
                {
                    this.MenuItems[i] = new MenuItem();
                    this.MenuItems[i].Header = this._Categoies[i].Name;
                    this.MenuItems[i].Click += new RoutedEventHandler( item_Click );
                    this.MenuItems[i].Icon = new Border()
                    {
                        Child = new Rectangle()
                            {
                                Fill = new SolidColorBrush( this._Categoies[i].Colour ),
                                Width = 16,
                                Height = 16,
                            },
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness( 1 )
                    };
                    this.Items.Add( this.MenuItems[i] );
                }
            }
            else
            {
                this.MenuItems = new MenuItem[1];
            }
            this.MenuItems[this.MenuItems.Length - 2] = new MenuItem() { Header = "Create New" };
            this.MenuItems[this.MenuItems.Length - 2].Click += new RoutedEventHandler( item_Click );
            this.Items.Add( this.MenuItems[this.MenuItems.Length - 2] );
            this.MenuItems[this.MenuItems.Length - 1] = new MenuItem() { Header = "None" };
            this.MenuItems[this.MenuItems.Length - 1].Click += new RoutedEventHandler( item_Click );
            this.Items.Add( this.MenuItems[this.MenuItems.Length - 1] );
            // Now that everything has been created update the layout
            this.UpdateLayout();
        }
    }
}