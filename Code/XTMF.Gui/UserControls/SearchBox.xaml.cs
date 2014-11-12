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
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for SearchBox.xaml
    /// </summary>
    public partial class SearchBox : UserControl
    {
        protected string _Filter;

        public SearchBox()
        {
            InitializeComponent();
            this.Search.PreviewKeyDown += new KeyEventHandler( Search_KeyDown );
        }

        public event Action<string> TextChanged;

        public string Filter
        {
            get
            {
                return _Filter;
            }

            set
            {
                _Filter = value;
                this.Search.Text = value;
            }
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            Keyboard.Focus( this.Search );
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                // If we see an escape and we have a filter, erase the filter and handel the event
                if ( e.Key == Key.Escape )
                {
                    if ( !String.IsNullOrWhiteSpace( _Filter ) )
                    {
                        this.Filter = String.Empty;
                        e.Handled = true;
                    }
                }
            }
            base.OnKeyDown( e );
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            this.OnKeyDown( e );
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var changed = this.TextChanged;
            this.Filter = this.Search.Text;
            if ( changed != null )
            {
                changed( this.Filter );
            }
        }
    }
}