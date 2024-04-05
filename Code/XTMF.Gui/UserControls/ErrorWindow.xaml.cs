/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF.Gui.UserControls;

/// <summary>
///     Interaction logic for ErrorWindow.xaml
/// </summary>
public partial class ErrorWindow : Window
{
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register("ErrorMessage",
        typeof(string), typeof(ErrorWindow),
        new FrameworkPropertyMetadata(OnErrorMessageChanged));

    public static readonly DependencyProperty ErrorStackTraceProperty =
        DependencyProperty.Register("ErrorStackTrace", typeof(string), typeof(ErrorWindow),
            new FrameworkPropertyMetadata(OnErrorStackTraceChanged));

    public ErrorWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty) as string;
        set => SetValue(ErrorMessageProperty, value);
    }

    public string ErrorStackTrace
    {
        get => GetValue(ErrorStackTraceProperty) as string;
        set => SetValue(ErrorStackTraceProperty, value);
    }

    public void Continue(object bob) => Close();

    private static void OnErrorStackTraceChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        (source as ErrorWindow).StackTraceBox.Text = e.NewValue is string value ? value : "No error found!";
    }

    private static void OnErrorMessageChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        (source as ErrorWindow).MessageBox.Text = e.NewValue is string value ? value : "No error found!";
    }

    public void Copy(object bob)
    {
        SetToClipboard(ErrorStackTrace == null ?
            ErrorMessage :
            ErrorMessage + "\r\n" + ErrorStackTrace);
    }

    private void SetToClipboard(string str) => Clipboard.SetText(str);

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        SetToClipboard(ErrorStackTrace == null ?
            ErrorMessage :
            ErrorMessage + "\r\n" + ErrorStackTrace);
    }
}