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
        private readonly Border _border = new Border();


        private readonly Action<string> _giveResult;

        private readonly Grid _grid = new Grid();
        private readonly TextBlock _textBlock = new TextBlock();

        private readonly TextBox _textbox = new TextBox
        {
            Width = 400,
            Height = 25
        };

        private bool AlreadySaved;

        private IInputElement PreviousFocus;

        static TextboxAdorner()
        {
            Background = (Brush) Application.Current.TryFindResource("ControlBackgroundBrush");
        }

        public TextboxAdorner(UIElement adornedElement) :
            base(adornedElement)
        {
        }

        public TextboxAdorner(string question, Action<string> giveResult, UIElement attachedTo, string initialValue = "")
            : base(attachedTo)
        {
            Opacity = 0.9;
            _border.BorderBrush = Brushes.White;
            _border.Background = Background;
            _border.BorderThickness = new Thickness(2.0);
            _border.Width = 400;
            _border.Height = 52;
            _grid.RowDefinitions.Add(new RowDefinition {Height = new GridLength(25)});
            _grid.RowDefinitions.Add(new RowDefinition {Height = new GridLength(25)});
            _grid.Margin = new Thickness(2.0);
            _border.Child = _grid;
            _textBlock.Text = question;
            _textBlock.Foreground = Brushes.White;
            _textBlock.FontSize = 14.0;
            if (initialValue == null)
            {
                initialValue = string.Empty;
            }
            _textbox.Text = initialValue;
            _textbox.CaretIndex = initialValue.Length;
            _grid.Children.Add(_textBlock);
            _grid.Children.Add(_textbox);
            Grid.SetRow(_textBlock, 0);
            Grid.SetRow(_textbox, 1);
            AddVisualChild(_border);
            _giveResult = giveResult;
            _textbox.LostFocus += Textbox_LostFocus;
            Loaded += MainLoaded;
        }

        protected override int VisualChildrenCount => 1;

        private void MainLoaded(object sender, RoutedEventArgs e)
        {
            PreviousFocus = Keyboard.FocusedElement;
            Keyboard.Focus(_textbox);
        }

        private void Textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            ExitAdorner(true);
        }

        private void ExitAdorner(bool save = false)
        {
            if (VisualParent != null)
            {
                AdornerLayer.GetAdornerLayer(VisualParent as UIElement).Remove(this);
            }
            Keyboard.Focus(PreviousFocus);
            if (save && !AlreadySaved)
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
            return _border;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _border.Measure(constraint);
            return _border.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _border.Arrange(new Rect(new Point(0, 0), finalSize));
            return new Size(_border.ActualWidth, _border.ActualHeight);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled == false)
            {
                if (e.Key == Key.Escape)
                {
                    ExitAdorner();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    ExitAdorner(true);
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }
    }
}