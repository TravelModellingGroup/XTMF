﻿/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace XTMF.Gui;

/// <summary>
///     Interaction logic for FilterBox.xaml
/// </summary>
public partial class FilterBox : UserControl
{
    public static readonly DependencyProperty FilterWatermarkProperty = DependencyProperty.Register(
        "FilterWatermark", typeof(string), typeof(FilterBox),
        new FrameworkPropertyMetadata("Search...", FrameworkPropertyMetadataOptions.AffectsRender,
            OnFilterWatermarkChanged));

    private string _currentBoxText = string.Empty;

    private ItemsControl _display;

    private Func<object, string, bool> _filter;
    private ICollectionView _itemsSource;

    private Action Refresh;

    public FilterBox()
    {
        UseItemSourceFilter = true;
        InitializeComponent();
    }

    public string FilterWatermark
    {
        get => GetValue(FilterWatermarkProperty) as string;
        set => SetValue(FilterWatermarkProperty, value);
    }

    public void RetriveFocus()
    {
        Dispatcher.InvokeAsync(() =>
        {
            Box.Focus();
            Keyboard.Focus(Box);
        }, DispatcherPriority.Background);
    }

    public bool UseItemSourceFilter { get; set; }

    public Func<object, string, bool> Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            if (_display != null && _itemsSource != null)
            {
                if (UseItemSourceFilter)
                {
                    if (!_itemsSource.CanFilter)
                    {
                        throw new NotSupportedException("The FilterBox is unable to filter data  of type " +
                                                        _itemsSource.SourceCollection.GetType().FullName);
                    }

                    _itemsSource.Filter = o => _filter(o, _currentBoxText);
                }
            }
        }
    }

    public ItemsControl Display
    {
        get => _display;
        set
        {
            _display = value;
            Box.Text = "";
            _itemsSource = CollectionViewSource.GetDefaultView(value.ItemsSource);
            _itemsSource.Refresh();
            if (_filter != null)
            {
                Filter = _filter;
            }

            Refresh = () =>
            {
                if (UseItemSourceFilter)
                {
                   
                    _itemsSource.Refresh();

                }
                else
                {
                    var items = _itemsSource.GetEnumerator();
                    using var differ = _itemsSource.DeferRefresh();
                    while (items.MoveNext())
                    {
                        Filter(items.Current, Box.Text);
                    }
                }
            };
        }
    }

    private static void OnFilterWatermarkChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
    }

    public event EventHandler EnterPressed;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        try
        {
            Box.Focus();
        }
        catch
        {
        }

    }

    private bool HandleEnterPress()
    {
        var ev = EnterPressed;
        if (ev != null)
        {
            ev(this, new EventArgs());
            return true;
        }
        return false;
    }

    private bool ClearFilter()
    {
        if (!string.IsNullOrWhiteSpace(Box.Text))
        {
            Box.Text = string.Empty;
            return true;
        }

        return false;
    }

    private void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        ClearFilter();
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentBoxText = Box.Text;
        RefreshFilter();
        ClearFilterButton.Visibility =
            !string.IsNullOrWhiteSpace(Box.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 
    /// </summary>
    internal void RefreshFilter()
    {
        if (Refresh != null)
        {
            Dispatcher.BeginInvoke(Refresh, DispatcherPriority.Input);
        }
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled == false)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = ClearFilter();
                    break;
                case Key.Enter:
                    e.Handled = HandleEnterPress();
                    break;
                case Key.Down:
                    var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                    var keyboardFocus = Keyboard.FocusedElement as UIElement;
                    keyboardFocus?.MoveFocus(tRequest);
                    e.Handled = true;
                    break;
            }
        }
    }
}