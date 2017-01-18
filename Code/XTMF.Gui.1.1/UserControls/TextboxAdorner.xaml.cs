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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for TextboxAdorner.xaml
    /// </summary>
    public partial class TextboxAdorner : Adorner
    {
        Border Border = new Border();

        Grid Grid = new Grid();
        TextBlock TextBlock = new TextBlock();

        public TextBox Textbox = new TextBox()
        {
            Width = 400,
            Height = 25
        };

        private static Brush Background;

        static TextboxAdorner()
        {
            Background = (Brush)Application.Current.TryFindResource("ControlBackgroundBrush");
        }

        public TextboxAdorner(UIElement adornedElement) :
        base(adornedElement)
        {
                
            }


        Action<string> GiveResult;

        public TextboxAdorner(string question, Action<string> giveResult, UIElement attachedTo, string initialValue = "") : base(attachedTo)
        {
            this.Opacity = 0.9;
            Border.BorderBrush = Brushes.White;
            Border.Background = Background;
            Border.BorderThickness = new Thickness(2.0);
            Border.Width = 400;
            Border.Height = 52;
            Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(25) });
            Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(25) });
            Grid.Margin = new Thickness(2.0);
            Border.Child = this.Grid;
            TextBlock.Text = question;
            TextBlock.Foreground = Brushes.White;
            TextBlock.FontSize = 14.0;
            if(initialValue == null)
            {
                initialValue = String.Empty;
            }
            Textbox.Text = initialValue;
            Textbox.CaretIndex = initialValue.Length;
            Grid.Children.Add(TextBlock);
            Grid.Children.Add(Textbox);
            Grid.SetRow(TextBlock, 0);
            Grid.SetRow(Textbox, 1);
            AddVisualChild(Border);
            GiveResult = giveResult;
            Textbox.LostFocus += Textbox_LostFocus;
            Loaded += MainLoaded;
        }

        private IInputElement PreviousFocus;

        private void MainLoaded(object sender, RoutedEventArgs e)
        {
            PreviousFocus = Keyboard.FocusedElement;
            Keyboard.Focus(Textbox);
        }

        private void Textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            ExitAdorner(true);
        }

        private void ExitAdorner(bool save = false)
        {
            if(VisualParent != null)
            {
                AdornerLayer.GetAdornerLayer(VisualParent as UIElement).Remove(this);
            }
            Keyboard.Focus(PreviousFocus);
            if(save)
            {
                GiveResult?.Invoke(Textbox.Text);
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            Textbox.Focus();
            Keyboard.Focus(Textbox);
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            if(index != 0) throw new ArgumentOutOfRangeException();
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
            if(e.Handled == false)
            {
                if(e.Key == Key.Escape)
                {
                    ExitAdorner();
                    e.Handled = true;
                }
                else if(e.Key == Key.Enter)
                {
                    ExitAdorner(true);
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }
    }
}
