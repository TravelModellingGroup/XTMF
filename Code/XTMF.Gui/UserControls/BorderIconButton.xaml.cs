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
        public static readonly DependencyProperty FocusColourProperty = DependencyProperty.Register( "FocusColour", typeof( Color ), typeof( BorderIconButton ) );

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register( "Header", typeof( string ), typeof( BorderIconButton ),
            new FrameworkPropertyMetadata( String.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnHeaderChanged, OnCoerceChanged ) );

        public static readonly DependencyProperty HighlightColourProperty = DependencyProperty.Register( "HighlightColour", typeof( Color ), typeof( BorderIconButton ) );

        public static readonly DependencyProperty ShadowColourProperty = DependencyProperty.Register( "ShadowColour", typeof( Color ), typeof( BorderIconButton ),
            new FrameworkPropertyMetadata( Color.FromRgb( 0x30, 0x30, 0x30 ), FrameworkPropertyMetadataOptions.AffectsRender, OnShadowColourChanged ) );

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register( "Text", typeof( string ), typeof( BorderIconButton ),
            new FrameworkPropertyMetadata( String.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged, OnCoerceChanged ) );

        public static DependencyProperty SelectedProperty = DependencyProperty.Register( "Selected", typeof( bool ), typeof( BorderIconButton ),
            new FrameworkPropertyMetadata( false, OnSelectedChanged ) );

        private static Color DefaultControlBackgroundColour;

        private static Color DefaultFocusColour;

        private static Color DefaultSelectionBlue;

        private static Brush FocusBrush;

        private ImageSource _Icon;

        private double BaseWidth;

        private bool IsConstructing = true;

        private bool MouseDownInside = false;

        private Point MouseDownPoint;

        static BorderIconButton()
        {
            try
            {
                DefaultSelectionBlue = (Color)Application.Current.TryFindResource( "SelectionBlue" );
                DefaultControlBackgroundColour = (Color)Application.Current.TryFindResource( "ControlBackgroundColour" );
                DefaultFocusColour = (Color)Application.Current.TryFindResource( "FocusColour" );
                FocusBrush = new SolidColorBrush( DefaultFocusColour );
            }
            catch
            {
            }
        }

        public BorderIconButton()
        {
            this.Selected = false;
            this.HighlightColour = DefaultSelectionBlue;
            this.FocusColour = DefaultFocusColour;
            this.Focusable = true;
            InitializeComponent();
            this.IsEnabledChanged += new DependencyPropertyChangedEventHandler( BorderIconButton_IsEnabledChanged );
            this.BaseWidth = 200;
            this.IsConstructing = false;
            this.Loaded += BorderIconButton_Loaded;
            this.Unloaded += BorderIconButton_Unloaded;
        }

        void BorderIconButton_Loaded(object sender, RoutedEventArgs e)
        {
            // when we unload make sure our background has been reset
            var brush = this.BorderOutline.Background as SolidColorBrush;
            if ( brush != null && !brush.IsFrozen )
            {
                brush.BeginAnimation( SolidColorBrush.ColorProperty, null );
                brush.Color = this.ShadowColour;
            }
        }

        void BorderIconButton_Unloaded(object sender, RoutedEventArgs e)
        {
            // when we unload make sure our background has been reset
            var brush = this.BorderOutline.Background as SolidColorBrush;
            if ( brush != null && !brush.IsFrozen )
            {
                brush.BeginAnimation( SolidColorBrush.ColorProperty, null );
                brush.Color = this.ShadowColour;
            }
        }

        public event Action<object> Clicked;

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<object> RightClicked;

        public bool AllowDrag
        {
            get;
            set;
        }

        public Color FocusColour
        {
            get { return (Color)GetValue( FocusColourProperty ); }
            set { SetValue( FocusColourProperty, value ); }
        }

        public string Header
        {
            get { return (string)GetValue( HeaderProperty ); }
            set { SetValue( HeaderProperty, value ); }
        }

        public Color HighlightColour
        {
            get { return (Color)GetValue( HighlightColourProperty ); }
            set { SetValue( HighlightColourProperty, value ); }
        }

        public ImageSource Icon
        {
            get { return _Icon; }

            set
            {
                this._Icon = value;
                this.NotifyChanged( "Icon" );
            }
        }

        public bool MouseInside { get; set; }

        public bool Selected
        {
            get { return (bool)this.GetValue( SelectedProperty ); }
            set { this.SetValue( SelectedProperty, value ); }
        }

        public Color ShadowColour
        {
            get { return (Color)GetValue( ShadowColourProperty ); }

            set
            {
                SetValue( ShadowColourProperty, value );
            }
        }

        public string Text
        {
            get { return (string)GetValue( TextProperty ); }
            set { SetValue( TextProperty, value ); }
        }

        public void DoContractAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( this.BaseWidth, 0, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.BeginAnimation( UserControl.WidthProperty, fadeInAnimation );
        }

        public void DoExpandAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( 0, this.BaseWidth, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.BeginAnimation( UserControl.WidthProperty, fadeInAnimation );
        }

        public void DoFadeInAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( 0, 1, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.BeginAnimation( UserControl.OpacityProperty, fadeInAnimation );
        }

        public void DoFadeOutAnimation()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( 1, 0, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.BeginAnimation( UserControl.OpacityProperty, fadeInAnimation );
        }

        public void FadeInContent()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( 0, 1, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.ContentStackPanel.BeginAnimation( UserControl.OpacityProperty, fadeInAnimation );
        }

        public void FadeOutContent()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation( 1, 0, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            this.ContentStackPanel.BeginAnimation( UserControl.OpacityProperty, fadeInAnimation );
        }

        public void FlashAnimation(int times = 3)
        {
            var blink = new ColorAnimation( this.ShadowColour, this.HighlightColour, new Duration( new TimeSpan( 0, 0, 0, 0, 750 ) ) );
            blink.AutoReverse = true;
            blink.RepeatBehavior = new RepeatBehavior( times );
            this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, blink );
        }
       

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if ( !this.MouseInside )
            {
                if ( !this.Selected )
                {
                    ColorAnimation fadeInAnimation = new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
                    this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, fadeInAnimation );
                }
            }
            base.OnGotFocus( e );
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            if ( !this.MouseInside )
            {
                if ( !this.Selected )
                {
                    ColorAnimation fadeInAnimation = new ColorAnimation( this.ShadowColour, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
                    this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, fadeInAnimation );
                }
            }
            base.OnLostFocus( e );
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            ColorAnimation fadeInAnimation;
            if ( !this.IsHitTestVisible ) return;
            fadeInAnimation = new ColorAnimation( this.HighlightColour, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );

            this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, fadeInAnimation );
            e.Handled = true;
            this.MouseInside = true;
            base.OnMouseEnter( e );
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            ColorAnimation fadeInAnimation;
            if ( !this.IsHitTestVisible ) return;
            this.MouseDownInside = false;
            if ( this.IsFocused || this.Selected )
            {
                fadeInAnimation = new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            }
            else
            {
                fadeInAnimation = new ColorAnimation( this.ShadowColour, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            }
            this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, fadeInAnimation );
            e.Handled = true;
            this.MouseInside = false;
            base.OnMouseLeave( e );
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if ( !this.MouseDownInside )
            {
                this.MouseDownInside = true;
                this.MouseDownPoint = e.GetPosition( this );
            }
            this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
            base.OnMouseLeftButtonDown( e );
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if ( this.IsEnabled && !e.Handled )
            {
                if ( e.ClickCount == 1 )
                {
                    e.Handled = true;
                    if ( MouseDownInside && this.Clicked != null )
                    {
                        this.Clicked( this );
                    }
                    if ( this.IsFocused || this.Selected )
                    {
                        this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                    else
                    {
                        this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.HighlightColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                }
            }
            this.MouseDownInside = false;
            base.OnMouseLeftButtonUp( e );
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            CheckDrag( e );
            base.OnMouseMove( e );
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            if ( this.RightClicked != null )
            {
                this.MouseDownInside = true;
                this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
            }
            base.OnMouseRightButtonDown( e );
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if ( this.IsEnabled && !e.Handled )
            {
                if ( e.ClickCount == 1 )
                {
                    if ( this.IsFocused || this.Selected )
                    {
                        this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                    else
                    {
                        this.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( this.HighlightColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                    if ( MouseDownInside && this.RightClicked != null )
                    {
                        this.RightClicked( this );
                        e.Handled = true;
                    }
                    this.MouseDownInside = false;
                }
            }
            base.OnMouseRightButtonUp( e );
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged( sizeInfo );
        }

        private static object OnCoerceChanged(DependencyObject source, object e)
        {
            return e;
        }

        private static void OnHeaderChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if ( String.IsNullOrWhiteSpace( us.Text ) )
            {
                us.ToolTip = e.NewValue;
            }
        }

        private static void OnSelectedChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if ( us.BorderOutline != null && us.BorderOutline.Background != null )
            {
                if ( e.OldValue != e.NewValue )
                {
                    if ( (bool)e.NewValue )
                    {
                        us.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( us.FocusColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                    else
                    {
                        us.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, new ColorAnimation( us.ShadowColour, new Duration( new TimeSpan( 0, 0, 0, 0, 75 ) ) ) );
                    }
                }
            }
        }

        private static void OnShadowColourChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            var value = (Color)e.NewValue;
            if ( !us.IsConstructing && us.BorderOutline != null && us.BorderOutline.Background != null
                && !us.IsFocused && !us.MouseInside )
            {
                var brush = ( us.BorderOutline.Background as SolidColorBrush );
                if ( brush != null )
                {
                    if ( brush.Color != value )
                    {
                        ColorAnimation fadeInAnimation = new ColorAnimation( value, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
                        us.BorderOutline.Background.BeginAnimation( SolidColorBrush.ColorProperty, fadeInAnimation );
                    }
                }
                else
                {
                    us.BorderOutline.Background = new SolidColorBrush( value );
                }
            }
        }

        private static void OnTextChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as BorderIconButton;
            if ( String.IsNullOrWhiteSpace( us.Text ) )
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
            if ( (bool)e.NewValue != (bool)e.OldValue )
            {
                if ( (bool)e.NewValue == true )
                {
                    this.FadeInContent();
                }
                else
                {
                    this.FadeOutContent();
                }
            }
        }

        private void CheckDrag(MouseEventArgs e)
        {
            if ( this.AllowDrag )
            {
                if ( MouseDownInside && e.LeftButton == MouseButtonState.Pressed )
                {
                    this.BorderOutline.BorderBrush = Brushes.Crimson;
                    var ret = DragDrop.DoDragDrop( this, new DataObject( this ), DragDropEffects.Move );
                    this.BorderOutline.BorderBrush = Brushes.White;
                }
            }
        }

        private void NotifyChanged(string propertyName)
        {
            if ( this.PropertyChanged != null )
            {
                this.PropertyChanged( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }
    }
}