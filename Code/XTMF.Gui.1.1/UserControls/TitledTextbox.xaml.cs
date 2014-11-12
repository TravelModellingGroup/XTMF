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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for TitledTextbox.xaml
    /// </summary>
    public partial class TitledTextbox : UserControl, INotifyPropertyChanged
    {
        private string _Header;

        public TitledTextbox()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<object> TextChanged;

        public string HeaderText
        {
            get
            {
                return this._Header;
            }

            set
            {
                this._Header = value;
                this.NotifyChanged( "HeaderText" );
            }
        }

        public string HintText
        {
            get
            {
                return this.InputTextBox.HintText;
            }

            set
            {
                this.InputTextBox.HintText = value;
            }
        }

        public string Text
        {
            get
            {
                return this.InputTextBox.Text;
            }

            set
            {
                this.InputTextBox.Text = value;
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if ( e.Handled == false )
            {
                e.Handled = true;
                Keyboard.Focus( this.InputTextBox );
            }
            base.OnGotFocus( e );
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.NotifyChanged( "Text" );
            if ( this.TextChanged != null )
            {
                this.TextChanged( this );
            }
        }

        private void NotifyChanged(string propertyName)
        {
            if ( this.PropertyChanged != null )
            {
                this.PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }
    }
}