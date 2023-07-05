/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for StringRequestDialog.xaml
    /// </summary>
    public partial class StringRequestDialog : UserControl
    {

        private readonly Func<string, bool> _validation;

        public string QuestionText { get; set; }

        public bool DidComplete { get; set; }

        public string UserInput { get; set; }

        private DialogSession _dialogSession;

        private DialogHost _host;

        public StringRequestDialog(DialogHost host, string question, Func<string, bool> validation, string startingText)
        {
            _host = host;
            DataContext = this;
            _validation = validation;
            QuestionText = question;
            DidComplete = false;
            UserInput = startingText;
            InitializeComponent();
            // We clear the focus here to ensure that extra keys strokes before we gain keyboard focus don't go through.
            Keyboard.ClearFocus();
            if (validation != null)
            {
                Binding b = BindingOperations.GetBinding(StringInputTextBox, TextBox.TextProperty);
                if (b != null)
                {
                    foreach (var rule in b.ValidationRules)
                    {
                        if (rule is ValidateString r)
                        {
                            r.ValidationRule = validation;
                        }
                    }
                }
            }
        }

        private bool _firstKeyboardFocus = true;

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            Keyboard.Focus(StringInputTextBox);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            if (_firstKeyboardFocus)
            {
                StringInputTextBox.Select(0, StringInputTextBox.Text.Length);
            }
            Keyboard.Focus(StringInputTextBox);
        }

        private void OpenedEventHandler(object sender, DialogOpenedEventArgs eventargs)
        {
            this._dialogSession = eventargs.Session;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<object> ShowAsync(bool allowClickToClose = true)
        {
            _host.CloseOnClickAway = allowClickToClose;
            return await _host.ShowDialog(this, OpenedEventHandler, ClosingEventHandler);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StringRequestDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DidComplete = true;
                e.Handled = true;
                this._dialogSession.Close(false);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel(e);
            }
        }

        private void OnClose_Clicked(object sender, MouseButtonEventArgs e)
        {
            Cancel(e);
        }

        private void Cancel(RoutedEventArgs e)
        {
            DidComplete = false;
            e.Handled = true;
            try
            {
                this._dialogSession.Close(false);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DidComplete = true;
            e.Handled = true;
            this._dialogSession.Close(false);
        }
    }

    public class ValidateString : ValidationRule
    {

        public Func<string, bool> ValidationRule { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (ValidationRule == null)
            {
                return string.IsNullOrWhiteSpace((value ?? "").ToString())
                    ? new ValidationResult(false, "Field is required.")
                    : ValidationResult.ValidResult;
            }
            else
            {
                return ValidationRule((string)value)
                    ? ValidationResult.ValidResult
                    : new ValidationResult(false, "Invalid Value");
            }
        }
    }
    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return string.IsNullOrWhiteSpace((value ?? "").ToString())
                ? new ValidationResult(false, "Field is required.")
                : ValidationResult.ValidResult;
        }
    }
}
