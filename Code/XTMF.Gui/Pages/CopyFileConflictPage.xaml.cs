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
    /// Interaction logic for CopyFileConflictPage.xaml
    /// </summary>
    public partial class CopyFileConflictPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.FileNamePage };
        private QuestionData CurrentQuestion;

        private SingleWindowGUI XTMF;

        public CopyFileConflictPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            InitializeComponent();
            this.Loaded += new RoutedEventHandler( CopyFileConflictPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            if ( data is QuestionData )
            {
                var question = data as QuestionData;
                this.FileNameLabel.Content = question.Question;
                this.CurrentQuestion = question;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( !e.Handled )
            {
                if ( e.Key == Key.Escape )
                {
                    e.Handled = true;
                    this.Cancel_Clicked( null );
                }
            }
            base.OnKeyDown( e );
        }

        private void Cancel_Clicked(object obj)
        {
            QuestionResult res = new QuestionResult()
            {
                Data = this.CurrentQuestion.OnCancelData,
                Result = "Cancel",
                Success = true
            };
            this.XTMF.Navigate( this.CurrentQuestion.OnCancel, res );
        }

        private void CopyFileConflictPage_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus( this );
        }

        private void Overwrite_Clicked(object obj)
        {
            QuestionResult res = new QuestionResult()
            {
                Data = this.CurrentQuestion.OnSuccessData,
                Result = "Overwrite",
                Success = true
            };
            this.XTMF.Navigate( this.CurrentQuestion.OnSuccess, res );
        }

        private void Rename_Clicked(object obj)
        {
            QuestionResult res = new QuestionResult()
            {
                Data = this.CurrentQuestion.OnSuccessData,
                Result = "Rename",
                Success = true
            };
            this.XTMF.Navigate( this.CurrentQuestion.OnSuccess, res );
        }
    }
}