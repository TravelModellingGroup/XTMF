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

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for QuestionPage.xaml
    /// </summary>
    public partial class QuestionPage : UserControl, IXTMFPage
    {
        private QuestionData Data;
        private bool PreviousValid = false;
        private SingleWindowGUI XTMF;

        public QuestionPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.Loaded += new RoutedEventHandler( QuestionPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get;
            set;
        }

        public void Cancel_Clicked(object o)
        {
            this.XTMF.Navigate( this.Data.OnCancel,
                new QuestionResult() { Data = this.Data.OnCancelData, Result = String.Empty, Success = false } );
        }

        public void Confirm_Clicked(object o)
        {
            this.XTMF.Navigate( this.Data.OnSuccess,
                new QuestionResult() { Data = this.Data.OnSuccessData, Result = this.QuestionTextBox.Text, Success = true } );
        }

        public void SetActive(object data)
        {
            var questionData = data as QuestionData;
            if ( questionData == null )
            {
                if ( data is QuestionResult )
                {
                    var res = data as QuestionResult;
                    if ( res.Data is QuestionData )
                    {
                        questionData = res.Data as QuestionData;
                    }
                }
            }
            if ( questionData == null ) return;
            this.PreviousValid = false;
            this.Data = questionData;
            this.QuestionTextBox.Width = double.NaN;
            this.QuestionTextBox.HeaderText = questionData.Question;
            this.QuestionTextBox.HintText = questionData.Hint;
            this.QuestionTextBox.Dispatcher.BeginInvoke(
                new Action( delegate()
                {
                    var width = this.QuestionTextBox.HeaderContent.ActualWidth + 50;
                    this.QuestionTextBox.Width = width + ( 50 - ( width % 50 ) );
                } ), System.Windows.Threading.DispatcherPriority.Background );
            if ( questionData.DefaultText != null )
            {
                this.QuestionTextBox.Text = questionData.DefaultText;
            }
            else
            {
                this.QuestionTextBox.Text = String.Empty;
            }
            int length;
            XTMFPage[] ourPath = new XTMFPage[( length = questionData.Path.Length ) + 1];
            for ( int i = 0; i < length; i++ )
            {
                ourPath[i] = questionData.Path[i];
            }
            ourPath[length] = XTMFPage.QuestionPage;
            this.Path = ourPath;
            if ( this.Data.Validate != null )
            {
                this.SelectButton.IsEnabled = this.PreviousValid = ( ( this.ErrorLabel.Content = this.Data.Validate( this.QuestionTextBox.Text ) ) == null );
            }
            else
            {
                this.SelectButton.IsEnabled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Enter )
                {
                    e.Handled = true;
                    if ( this.Data.Validate == null || ( this.PreviousValid = ( this.Data.Validate( this.QuestionTextBox.Text ) == null ) ) )
                    {
                        this.Confirm_Clicked( this.SelectButton );
                    }
                }
                else if ( e.Key == Key.Escape )
                {
                    e.Handled = true;
                    this.Cancel_Clicked( this.CancelButton );
                }
            }
            base.OnKeyUp( e );
        }

        private void QuestionPage_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus( this.QuestionTextBox );
        }

        private void QuestionTextBox_TextChanged(object obj)
        {
            if ( this.Data.Validate != null )
            {
                var prev = this.PreviousValid;
                this.SelectButton.IsEnabled = this.PreviousValid = ( ( this.ErrorLabel.Content = this.Data.Validate( this.QuestionTextBox.Text ) ) == null );
            }
        }
    }
}