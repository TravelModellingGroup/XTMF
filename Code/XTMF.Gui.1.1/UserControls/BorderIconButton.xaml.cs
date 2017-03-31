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

        private ImageSource _Icon;

        private double _baseWidth;

        private bool _isConstructing = true;

        private bool _mouseDownInside;

        private Point _mouseDownPoint;

        static BorderIconButton()
        {

            try
            {

            }
            catch
            {
            }
        }

        private static void BorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private static void BorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        public BorderIconButton()
        {
            Selected = false;
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

        }

        void BorderIconButton_Unloaded(object sender, RoutedEventArgs e)
        {

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
        }

        public void DoExpandAnimation()
        {
        }

        public void DoFadeInAnimation()
        {
        }

        public void DoFadeOutAnimation()
        {

        }

        public void FadeInContent()
        {

        }

        public void FadeOutContent()
        {

        }

        public void FlashAnimation(int times = 3)
        {
        }


        protected override void OnGotFocus(RoutedEventArgs e)
        {

        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {

            base.OnLostFocus(e);
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {

            e.Handled = true;
            MouseInside = true;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {

            if (!IsHitTestVisible) return;
            _mouseDownInside = false;

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
                }
            base.OnMouseRightButtonDown(e);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (IsEnabled && !e.Handled)
            {
                if (e.ClickCount == 1)
                {
         
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
          
        }

        private static void OnHighlightColourChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
           
        }

        private static void OnShadowColourChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
           
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

            if (!this.IsEnabled)
            {
                TextContent.Opacity = 0.3;
                HeaderContent.Opacity = 0.3;
                IconImage.Opacity = 0.3;
            }
            else
            {
                TextContent.Opacity = 1.0;
                HeaderContent.Opacity = 1.0;
                IconImage.Opacity = 1.0;
            }
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