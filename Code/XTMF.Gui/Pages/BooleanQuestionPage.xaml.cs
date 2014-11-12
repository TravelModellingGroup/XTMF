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

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for QuestionPage.xaml
    /// </summary>
    public partial class BooleanQuestionPage : UserControl, IXTMFPage
    {
        private BooleanQuestionData Data;
        private SingleWindowGUI XTMF;

        public BooleanQuestionPage(SingleWindowGUI xtmf)
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
                new BooleanQuestionResult() { Data = this.Data.OnCancelData, Result = false, Success = false } );
        }

        public void Confirm_Clicked(object o)
        {
            this.XTMF.Navigate( this.Data.OnSuccess,
                new BooleanQuestionResult() { Data = this.Data.OnSuccessData, Result = true, Success = true } );
        }

        public void SetActive(object data)
        {
            var questionData = data as BooleanQuestionData;
            if ( questionData == null )
            {
                if ( data is BooleanQuestionResult )
                {
                    var res = data as BooleanQuestionResult;
                    if ( res.Data is BooleanQuestionData )
                    {
                        questionData = res.Data as BooleanQuestionData;
                    }
                }
            }
            if ( questionData == null ) return;

            SetupButton( this.SelectButton, questionData.Yes );
            SetupButton( this.CancelButton, questionData.No );
            this.Data = questionData;
            this.QuestionTextBox.Content = questionData.Question;
            int length;
            XTMFPage[] ourPath = new XTMFPage[( length = questionData.Path.Length ) + 1];
            for ( int i = 0; i < length; i++ )
            {
                ourPath[i] = questionData.Path[i];
            }
            ourPath[length] = XTMFPage.QuestionPage;
            this.Path = ourPath;
            this.SelectButton.IsEnabled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Escape )
                {
                    e.Handled = true;
                    this.Cancel_Clicked( this.CancelButton );
                }
            }
            base.OnKeyUp( e );
        }

        private static void SetupButton(BorderIconButton button, BooleanQuestionButton booleanQuestionButton)
        {
            button.Icon = booleanQuestionButton.Icon;
            button.Header = booleanQuestionButton.Header;
            button.Text = booleanQuestionButton.Description;
            button.HighlightColour = booleanQuestionButton.Highlight;
        }

        private void QuestionPage_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}