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

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register( "ErrorMessage", typeof(string), typeof(ErrorWindow),
        new FrameworkPropertyMetadata( OnErrorMessageChanged ) );

        public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register( "Exception", typeof(Exception), typeof(ErrorWindow),
                new FrameworkPropertyMetadata( OnErrorChanged ) );

        public ErrorWindow()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        public string ErrorMessage
        {
            get
            {
                return this.GetValue( ErrorMessageProperty ) as string;
            }

            set
            {
                this.SetValue( ErrorMessageProperty, value as string );
            }
        }

        public Exception Exception
        {
            get
            {
                return this.GetValue( ErrorProperty ) as Exception;
            }

            set
            {
                this.SetValue( ErrorProperty, value as Exception );
            }
        }

        public void Continue(object bob)
        {
            this.Close();
        }

        public void Copy(object bob)
        {
            var error = GetTopRootException( this.Exception );
            if ( error == null )
            {
                SetToClipboard( ErrorMessage );
            }
            else
            {
                SetToClipboard( error.Message + "\r\n" + error.StackTrace );
            }
        }

        private static System.Exception GetTopRootException(System.Exception value)
        {
            if ( value == null ) return null;
            var agg = value as AggregateException;
            if ( agg != null )
            {
                return GetTopRootException( agg.InnerException );
            }
            return value;
        }

        private static void OnErrorChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var window = source as ErrorWindow;
            var value = e.NewValue as Exception;
            value = GetTopRootException( value );
            if ( value != null )
            {
                if ( value is OutOfMemoryException )
                {
                    window.MessageBox.Text = "The system ran out of memory, please check the amount of memory available on your system.  If there is still insufficient memory for this model system please contact your model system designer.";
                    window.StackTraceBox.Text = value.StackTrace;
                }
                else
                {
                    window.MessageBox.Text = "Runtime Exception:\r\n" + value.Message;
                    window.StackTraceBox.Text = value.StackTrace;
                }
            }
            else
            {
                window.MessageBox.Text = "No error found!";
                window.StackTraceBox.Text = "No error found!";
            }
        }

        private static void OnErrorMessageChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var window = source as ErrorWindow;
            var value = e.NewValue as string;
            if ( value != null )
            {
                window.MessageBox.Text = value;
                window.StackTraceBox.Text = "No Stack Trace";
            }
            else
            {
                window.MessageBox.Text = "No error found!";
                window.StackTraceBox.Text = "No error Trace";
            }
        }

        private void SetToClipboard(string str)
        {
            Clipboard.SetText( str );
        }
    }
}