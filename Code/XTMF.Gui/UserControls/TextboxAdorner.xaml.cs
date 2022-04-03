/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    ///     Interaction logic for TextboxAdorner.xaml
    /// </summary>
    public partial class TextboxAdorner
    {
        private static readonly Brush Background;

        private readonly Border Border = new Border();

        private readonly Action<string> _giveResult;

        private readonly Grid Grid = new Grid();

        private readonly TextBlock TextBlock = new TextBlock();

        private readonly TextBox _textbox = new TextBox
        {
            Width = 400,
            Height = 25
        };

        private bool AlreadySaved;

        private IInputElement PreviousFocus;

        static TextboxAdorner() => Background = (Brush)Application.Current.TryFindResource("PrimaryHueDarkBrush");

        public TextboxAdorner(UIElement adornedElement) :
            base(adornedElement)
        {
        }

        public TextboxAdorner(string question, Action<string> giveResult, UIElement attachedTo, string initialValue = "", bool selectText = false)
            : base(attachedTo)
        {
            Opacity = 0.97;
            Border.BorderBrush = (Brush)Application.Current.TryFindResource("PrimaryHueLightBrush");
            Border.Background = Background;
            Border.BorderThickness = new Thickness(1.0);
            Border.Width = 400;
            Border.Height = 52;
            Grid.RowDefinitions.Add(new RowDefinition {Height = new GridLength(25)});
            Grid.RowDefinitions.Add(new RowDefinition {Height = new GridLength(25)});
            Grid.Margin = new Thickness(2.0);
            Border.Child = Grid;
            TextBlock.Text = question;
            TextBlock.Foreground = (Brush)Application.Current.TryFindResource("PrimaryHueDarkForegroundBrush");
            TextBlock.Background = (Brush)Application.Current.TryFindResource("PrimaryHueDarkBrush");
            TextBlock.FontSize = 14.0;
            
            if (initialValue == null)
            {
                initialValue = string.Empty;
            }
            _textbox.Text = initialValue;
            _textbox.CaretIndex = initialValue.Length;
            _textbox.Foreground = (Brush)Application.Current.TryFindResource("PrimaryHueMidForegroundBrush");
            _textbox.Background = (Brush)Application.Current.TryFindResource("PrimaryHueDarkBrush");
            _textbox.CaretBrush = (Brush)Application.Current.TryFindResource("SecondaryHueLightForegroundBrush");

            _textbox.SelectAll();
            Grid.Children.Add(TextBlock);
            Grid.Children.Add(_textbox);
            Grid.SetRow(TextBlock, 0);
            Grid.SetRow(_textbox, 1);
            AddVisualChild(Border);
            _giveResult = giveResult;
            _textbox.LostFocus += Textbox_LostFocus;
            Loaded += MainLoaded;

            if (selectText)
            {
                _textbox.SelectionStart = 0;
                _textbox.SelectionLength = _textbox.Text.Length;
            }
        }

        bool _canceled = false;

        protected override int VisualChildrenCount => 1;

        private void MainLoaded(object sender, RoutedEventArgs e)
        {
            PreviousFocus = Keyboard.FocusedElement;
            Keyboard.Focus(_textbox);
        }

        private void Textbox_LostFocus(object sender, RoutedEventArgs e) => ExitAdorner();

        private void ExitAdorner()
        {
            if (VisualParent != null)
            {
                AdornerLayer.GetAdornerLayer(VisualParent as UIElement).Remove(this);
            }
            Keyboard.Focus(PreviousFocus);
            if (!_canceled && !AlreadySaved)
            {
                _giveResult?.Invoke(_textbox.Text);
                AlreadySaved = true;
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            _textbox.Focus();
            Keyboard.Focus(_textbox);
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            return Border;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Border.Measure(constraint);
            return Border.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Border.Arrange(new Rect(new Point(0, 0), finalSize));
            return new Size(Border.ActualWidth, Border.ActualHeight);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled == false)
            {
                if (e.Key == Key.Escape)
                {
                    _canceled = true;
                    ExitAdorner();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    _canceled = false;
                    ExitAdorner();
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }
    }
}