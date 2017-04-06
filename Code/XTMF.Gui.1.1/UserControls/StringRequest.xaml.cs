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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for StringRequest.xaml
    /// </summary>
    public partial class StringRequest : Window
    {
        private Func<string, bool> Validation;

        /// <summary>
        /// The default width of this control
        /// </summary>
        internal const double DefaultWidth = 300.0;

        /// <summary>
        /// The default hight of this control
        /// </summary>
        internal const double DefaultHeight = 75.0;

        public StringRequest()
        {
            InitializeComponent();
            AnswerBox.PreviewKeyDown += AnswerBox_PreviewKeyDown;


            if (Owner == null)
            {
                var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
                Owner = window;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            AnswerBox.Focus();
        }



        public StringRequest(string question, Func<string, bool> validation)
            : this()
        {
            QuestionLabel.Text = question;
            Validation = validation;
            if (validation != null)
            {
                ValidationLabel.Visibility = validation(Answer) ? Visibility.Hidden : Visibility.Visible;
            }

            var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            Owner = window;
        }



        private void AnswerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CloseSuccessfully();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Visibility = Visibility.Hidden;

                    Close();
                }
            }
        }

        private void CloseSuccessfully()
        {
            var ev = Validation;
            if (ev != null)
            {
                if (!ev(Answer))
                {
                    DialogResult = false;
                    return;
                }
            }
            DialogResult = true;
            //this.Visibility = Visibility.Hidden;
            Close();
        }

        public string Answer { get; private set; }

       

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSuccessfully();
        }


    }
}
