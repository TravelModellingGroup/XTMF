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

namespace XTMF.Gui;

/// <summary>
/// Interaction logic for BorderIconButton.xaml
/// </summary>
public partial class BorderIconButton : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register("Header", typeof(string), typeof(BorderIconButton),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnHeaderChanged, OnCoerceChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(BorderIconButton),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged, OnCoerceChanged));

    public static DependencyProperty SelectedProperty = DependencyProperty.Register("Selected", typeof(bool), typeof(BorderIconButton));

    private ImageSource _Icon;

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

    public BorderIconButton()
    {
        Selected = false;
        InitializeComponent();
        IsEnabledChanged += BorderIconButton_IsEnabledChanged;
        BorderThickness = new Thickness(0);
    }

    public event Action<object> Clicked;

    public event Action<object> DoubleClicked;

    public event PropertyChangedEventHandler PropertyChanged;

    public event Action<object> RightClicked;

    public bool AllowDrag { get; set; }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set { SetValue(HeaderProperty, value); }
    }

    public ImageSource Icon
    {
        get => _Icon;
        set
        {
            _Icon = value;
            NotifyChanged(nameof(Icon));
        }
    }

    public bool MouseInside { get; set; }

    public bool Selected
    {
        get => (bool)GetValue(SelectedProperty);
        set { SetValue(SelectedProperty, value); }
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set { SetValue(TextProperty, value); }
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        e.Handled = true;
        MouseInside = true;
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (IsHitTestVisible)
        {
            _mouseDownInside = false;
            e.Handled = true;
            MouseInside = false;
        }
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

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) => base.OnRenderSizeChanged(sizeInfo);

    private static object OnCoerceChanged(DependencyObject source, object e) => e;

    private static void OnHeaderChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        var us = source as BorderIconButton;
        if (string.IsNullOrWhiteSpace(us.Text))
        {
            us.ToolTip = e.NewValue;
        }
    }

    private static void OnTextChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        var us = source as BorderIconButton;
        us.ToolTip = string.IsNullOrWhiteSpace(us.Text) ?
            us.ToolTip = us.Header :
            us.ToolTip = e.NewValue;
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}