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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for StringRequest.xaml
    /// </summary>
    public partial class StringRequest : Window
    {
        private Func<string, bool> Validation;

        public StringRequest()
        {
            InitializeComponent();
            AnswerBox.PreviewKeyDown += AnswerBox_PreviewKeyDown;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated( e );
            AnswerBox.Focus();
        }

        public StringRequest(string question, Func<string, bool> validation)
            : this()
        {
            QuestionLabel.Text = question;
            Validation = validation;
            if (validation != null)
            {
                ValidationLabel.Visibility = validation( Answer ) ? Visibility.Hidden : Visibility.Visible;
            }
        }

        private void AnswerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ( e.Key == Key.Enter )
            {
                CloseSuccessfully();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyUp( e );
            if (e.Handled == false)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            }
        }

        private void CloseSuccessfully()
        {
            var ev = Validation;
            if ( ev != null )
            {
                if ( !ev( Answer ) )
                {
                    DialogResult = false;
                    return;
                }
            }
            DialogResult = true;
            Close();
        }

        public string Answer { get; private set; }

        private void AnswerBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = AnswerBox.Text;
            if ( text != null )
            {
                var ev = Validation;
                if ( text.IndexOf( '\b' ) >= 0 )
                {
                    text = text.Replace( "\b", "" );
                }
                if ( ev == null || ev( text ) )
                {
                    Answer = text;
                    ValidationLabel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ValidationLabel.Visibility = Visibility.Visible;
                }
            }
        }

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSuccessfully();
        }
    }
}
