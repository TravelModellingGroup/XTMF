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
using System.Windows.Media;
using XTMF.Gui.UserControls;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for XTMFHeader.xaml
    /// </summary>
    public partial class XTMFHeader : UserControl
    {
        public XTMFHeader()
        {
            InitializeComponent();
        }

        public bool CurrentPageIsStart
        {
            set
            {
                //this.HomeButton.IsEnabled = !value;
            }
        }

        public SingleWindowGUI MainWindow { get; set; }

        public void NewPathSet()
        {
            var path = MainWindow.CurrentPath;
            this.PathStack.Children.Clear();
            if ( path != null )
            {
                bool first = true;
                foreach ( var p in path )
                {
                    if ( !first )
                    {
                        this.PathStack.Children.Add( new Label() { Content = "/", Foreground = Brushes.White } );
                    }
                    Border pl;
                    this.PathStack.Children.Add( pl = new Border() { Child = new PathLabel( p ) { Text = MainWindow.PageNames[(int)p] }, BorderBrush = Brushes.White, BorderThickness = new Thickness( 1 ), Margin = new Thickness( 2 ) } );
                    pl.MouseLeftButtonDown += new MouseButtonEventHandler( pl_MouseLeftButtonUp );
                    first = false;
                }
            }
            else
            {
                this.PathStack.Children.Add( new Label() { Content = "No Path available!", Foreground = Brushes.White, FontSize = 16 } );
            }
        }

        private void pl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ( sender is Border )
            {
                var plabel = sender as Border;
                this.MainWindow.Navigate( ( plabel.Child as PathLabel ).Page );
            }
        }
    }
}