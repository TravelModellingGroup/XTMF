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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for BorderIconButton.xaml
    /// </summary>
    public partial class BorderIconButton : UserControl, INotifyPropertyChanged
    {

       
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register("Header", typeof(string), typeof(BorderIconButton),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnHeaderChanged, OnCoerceChanged));

       
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(BorderIconButton),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged, OnCoerceChanged));

        public static DependencyProperty SelectedProperty = DependencyProperty.Register("Selected", typeof(bool), typeof(BorderIconButton),
            new FrameworkPropertyMetadata(false, OnSelectedChanged));

        private static Color DefaultControlBackgroundColour;

        private static Color DefaultFocusColour;

        private static Color DefaultSelectionBlue;

        private static Brush FocusBrush;

        private ImageSource _Icon;

        private double _baseWidth;

        private bool _isConstructing = true;

        private bool _mouseDownInside;

        private Point _mouseDownPoint;

        static BorderIconButton()
        {
           // BorderBrushProperty.OverrideMetadata(typeof(BorderIconButton), new FrameworkPropertyMetadata(Brushes.White, BorderBrushChanged));
           // BorderThicknessProperty.OverrideMetadata(typeof(BorderIconButton), new FrameworkPropertyMetadata(new Thickness(1), BorderThicknessChanged));
            try
            {
                //DefaultSelectionBlue = (Color)Application.Current.TryFindResource("SelectionBlue");
                //DefaultControlBackgroundColour = (Color)Application.Current.TryFindResource("ControlBackgroundColour");
                //DefaultFocusColour = (Color)Application.Current.TryFindResource("FocusColour");
                //FocusBrush = new SolidColorBrush(DefaultFocusColour);
            }
            catch
            {
            }
        }

        private static void BorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var us = d as BorderIconButton;
         //   us.BorderOutline.BorderThickness = (Thickness)e.NewValue;
        }

        private static void BorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var us = d as BorderIconButton;
       //     us.BorderOutline.BorderBrush = e.NewValue as Brush;
        }

        public BorderIconButton()
        {
            Selected = false;
            //HighlightColour = DefaultSelectionBlue;
           // FocusColour = DefaultFocusColour;
            InitializeComponent();
            IsEnabledChanged += BorderIconButton_IsEnabledChanged;
            _baseWidth = 200;
            _isConstructing = false;
            Loaded += BorderIconButton_Loaded;
            Unloaded += BorderIconButton_Unloaded;

            BorderThickness = new Thickness(0);
        }

        void BorderIconButton_Loaded(object sender, RoutedEventArgs e)
        {
            // when we unload make sure our background has been reset
            var brush = BorderOutline.Background as SolidColorBrush;
            if (brush != null && !brush.IsFrozen)
            {
              //  brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            //    brush.Color = ShadowColour;
            }
        }

        void BorderIconButton_Unloaded(object sender, RoutedEventArgs e)
        {
            // when we unload make sure our background has been reset
            var brush = BorderOutline.Background as SolidColorBrush;
            if (brush != null && !brush.IsFrozen)
            {
                //brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                //brush.Color = ShadowColour;
            }
        }

        public event Action<object> Clicked;
        public event Action<object> DoubleClicked;

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<object> RightClicked;

        public bool AllowDrag
        {
            get;
            set;
        }

        

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

      

        public ImageSource Icon
        {
            get { return _Icon; }

            set
            {
                _Icon = value;
                NotifyChanged("Icon");
            }
        }

        public bool MouseInside { get; set; }

        public bool Selected
        {
            get { return (bool)GetValue(SelectedProperty); }
            set { SetValue(SelectedProperty, value); }
        }

      

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public void DoContractAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(_baseWidth, 0, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
        //    BeginAnimation(WidthProperty, fadeInAnimation);
        }

        public void DoExpandAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(0, _baseWidth, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
         //   BeginAnimation(WidthProperty, fadeInAnimation);
        }

        public void DoFadeInAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(0, 1, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
         //   BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        public void DoFadeOutAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(1, 0, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
        //    BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        public void FadeInContent()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(0, 1, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
       //     ContentStackPanel.BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        public void FadeOutContent()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(1, 0, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
        //    ContentStackPanel.BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        public void FlashAnimation(int times = 3)
        {
           // var blink = new ColorAnimation(ShadowColour, HighlightColour, new Duration(new TimeSpan(0, 0, 0, 0, 750)));
            //blink.AutoReverse = true;
            //blink.RepeatBehavior = new RepeatBehavior(times);
          //  BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, blink);
        }


        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (!MouseInside)
            {
                if (!Selected)
                {
                   // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    //ColorAnimation fadeInAnimation = new ColorAnimation(FocusColour, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
                    //BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeInAnimation);
                }
            }
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            if (!MouseInside)
            {
                if (!Selected)
                {
                   // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                   // ColorAnimation fadeInAnimation = new ColorAnimation(ShadowColour, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
                  //  BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeInAnimation);
                }
            }
            base.OnLostFocus(e);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            ColorAnimation fadeInAnimation;
            if (!IsHitTestVisible) return;
           // fadeInAnimation = new ColorAnimation(HighlightColour, new Duration(new TimeSpan(0, 0, 0, 0, 100)));
           // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
            //BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeInAnimation);
            e.Handled = true;
            MouseInside = true;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            ColorAnimation fadeOutAnimation;
            if (!IsHitTestVisible) return;
            _mouseDownInside = false;
           // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
          
          //  BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeOutAnimation);
            e.Handled = true;
            MouseInside = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!_mouseDownInside)
            {
                _mouseDownInside = true;
                _mouseDownPoint = e.GetPosition(this);
            }
           // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
           // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(FocusColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            if (DoubleClicked != null)
            {
                e.Handled = true;
                DoubleClicked(this);
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (IsEnabled && !e.Handled)
            {
                if (e.ClickCount == 1)
                {
                    if (_mouseDownInside && Clicked != null)
                    {
                        e.Handled = true;
                        Clicked(this);
                    }
                }
            }
            _mouseDownInside = false;
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            CheckDrag(e);
            base.OnMouseMove(e);
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            if (RightClicked != null)
            {
                _mouseDownInside = true;
              //  BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
              //  BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(FocusColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
            }
            base.OnMouseRightButtonDown(e);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (IsEnabled && !e.Handled)
            {
                if (e.ClickCount == 1)
                {
                   // BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    if (IsFocused || Selected)
                    {
                     //   BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(FocusColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
                    }
                    else
                    {
                 //       BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(HighlightColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
                    }
                    if (_mouseDownInside && RightClicked != null)
                    {
                        RightClicked(this);
                        e.Handled = true;
                    }
                    _mouseDownInside = false;
                }
            }
            base.OnMouseRightButtonUp(e);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
        }

        private static object OnCoerceChanged(DependencyObject source, object e)
        {
            return e;
        }

        private static void OnHeaderChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if (string.IsNullOrWhiteSpace(us.Text))
            {
                us.ToolTip = e.NewValue;
            }
        }

        private static void OnSelectedChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if (us.BorderOutline != null && us.BorderOutline.Background != null)
            {
                if (e.OldValue != e.NewValue)
                {
                    //us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    if ((bool)e.NewValue)
                    {
                     //   us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(us.FocusColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
                    }
                    else
                    {
                    //    us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(us.ShadowColour, new Duration(new TimeSpan(0, 0, 0, 0, 75))));
                    }
                }
            }
        }

        private static void OnHighlightColourChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            var value = (Color)e.NewValue;
            if (!us._isConstructing && us.BorderOutline != null && us.BorderOutline.Background != null)
            {
                var brush = (us.BorderOutline.Background as SolidColorBrush);
                if (brush != null)
                {
                    if (us.Selected || us.IsFocused)
                    {
                     //   var oldColour = brush.Color;
                 //       if (oldColour != value)
                  //      {
                        //    brush.Color = value;
                      //      us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                   //         ColorAnimation fadeInAnimation = new ColorAnimation(oldColour, !us.IsFocused && !us.MouseInside ? value : us.HighlightColour, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
                   //     //    us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeInAnimation);
                 //       }
                    }
                }
                else
                {
                  //  us.BorderOutline.Background = new SolidColorBrush(value);
                }
            }
        }

        private static void OnShadowColourChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            var value = (Color)e.NewValue;
            if (!us._isConstructing && us.BorderOutline != null && us.BorderOutline.Background != null)
            {
                var brush = (us.BorderOutline.Background as SolidColorBrush);
                if (brush != null)
                {
                    var oldColour = brush.Color;
                    if (oldColour != value)
                    {
                      //  brush.Color = value;
                       // us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                   //     ColorAnimation fadeInAnimation = new ColorAnimation(oldColour, !us.IsFocused && !us.MouseInside ? value : us.HighlightColour, new Duration(new TimeSpan(0, 0, 0, 0, 250)));
                      //  us.BorderOutline.Background.BeginAnimation(SolidColorBrush.ColorProperty, fadeInAnimation);
                    }
                }
                else
                {
                 //   us.BorderOutline.Background = new SolidColorBrush(value);
                }
            }
        }

        private static void OnTextChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if (string.IsNullOrWhiteSpace(us.Text))
            {
                us.ToolTip = us.Header;
            }
            else
            {
                us.ToolTip = e.NewValue;
            }
        }

        private void BorderIconButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue != (bool)e.OldValue)
            {
                if ((bool)e.NewValue)
                {
                    FadeInContent();
                }
                else
                {
                    FadeOutContent();
                }
            }
        }

        private void CheckDrag(MouseEventArgs e)
        {
            if (AllowDrag)
            {
                if (_mouseDownInside && e.LeftButton == MouseButtonState.Pressed)
                {
                    BorderOutline.BorderBrush = Brushes.Crimson;
                    var ret = DragDrop.DoDragDrop(this, new DataObject(this), DragDropEffects.Move);
                    BorderOutline.BorderBrush = Brushes.White;
                }
            }
        }

        private void NotifyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}