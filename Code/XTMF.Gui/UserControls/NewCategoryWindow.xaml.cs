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
using System.Windows.Media;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for NewCategoryWindow.xaml
    /// </summary>
    public partial class NewCategoryWindow : Window
    {
        public Func<string, string> ValidateName;

        public NewCategoryWindow()
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler( NewCategoryWindow_Loaded );
        }

        public Pages.ViewRunsPage.Category Category { get; set; }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( !e.Handled )
            {
                if ( e.Key == Key.Enter )
                {
                    e.Handled = true;
                    if ( Validate() )
                    {
                        this.BorderIconButton_Clicked( null );
                    }
                }
                else if ( e.Key == Key.Escape )
                {
                    this.Close();
                }
            }
            base.OnKeyUp( e );
        }

        private void BorderIconButton_Clicked(object obj)
        {
            this.DialogResult = true;
            this.Category = new Pages.ViewRunsPage.Category()
            {
                Name = this.NameTextBox.Text,
                Colour = ( this.ColourShower.Fill as SolidColorBrush ).Color,
                RunNames = new string[0],
                Loaded = new bool[0]
            };
            this.Close();
        }

        private void ColourSelector_ColourSelected(Color obj)
        {
            this.ColourShower.Fill = new SolidColorBrush( obj );
        }

        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.CreateButton.IsEnabled = Validate();
        }

        private void NewCategoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Validate();
        }

        private bool Validate()
        {
            return ( this.NameErrorLabel.Content = this.ValidateName( this.NameTextBox.Text ) ) == null;
        }
    }
}