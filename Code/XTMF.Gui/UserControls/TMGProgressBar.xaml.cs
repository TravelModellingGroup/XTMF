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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XTMF.Gui;

/// <summary>
/// Interaction logic for TMGProgressBar.xaml
/// </summary>
public partial class TMGProgressBar : Control
{
    private bool _IsIndeterminate;

    private int _Maximum;

    private int _Minimum;

    private float _Value;

    private LinearGradientBrush BottomBrush = new();

    private SolidColorBrush ForgroundBrush;

    private Pen OutlinePen = new() { Brush = Brushes.Gray };

    private LinearGradientBrush OverlayBrush;

    private DateTime StartTime = DateTime.Now;

    private LinearGradientBrush TopBrush = new();

    public TMGProgressBar()
    {
        Finished = false;
        InitializeComponent();
        ForgroundBrush = new SolidColorBrush();
        OverlayBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        OverlayBrush.GradientStops.Add( new GradientStop( Colors.Transparent, -1 ) );
        OverlayBrush.GradientStops.Add( new GradientStop( Colors.Transparent, 0.1 ) );
        OverlayBrush.GradientStops.Add( new GradientStop( Color.FromArgb( 50, 255, 255, 255 ), 0.2 ) );
        OverlayBrush.GradientStops.Add( new GradientStop( Color.FromArgb( 50, 255, 255, 255 ), 0.2 ) );
        OverlayBrush.GradientStops.Add( new GradientStop( Colors.Transparent, 0.1 ) );
        OverlayBrush.GradientStops.Add( new GradientStop( Colors.Transparent, 2 ) );
        Color transparent = Color.FromArgb( 40, 255, 255, 255 );
        TopBrush.StartPoint = BottomBrush.StartPoint = new Point( 0.5, 0 );
        TopBrush.EndPoint = BottomBrush.EndPoint = new Point( 0.5, 1 );
        TopBrush.GradientStops.Add( new GradientStop( transparent, 0 ) );
        TopBrush.GradientStops.Add( new GradientStop( Colors.Transparent, 1 ) );
        BottomBrush.GradientStops.Add( new GradientStop( Colors.Transparent, 0 ) );
        BottomBrush.GradientStops.Add( new GradientStop( transparent, 1 ) );
        Value = 25;
    }

    public bool Finished { get; set; }
    

    public bool IsIndeterminate
    {
        get => _IsIndeterminate;
        set { _IsIndeterminate = value; InvalidateVisual(); }
    }

    public int Maximum { get => _Maximum; set { _Maximum = value; InvalidateVisual(); } }

    public int Minimum { get => _Minimum; set { _Minimum = value; InvalidateVisual(); } }

    public float Value { get => _Value; set { _Value = value; InvalidateVisual(); } }

    public void SetForgroundColor(Color colour) => ForgroundBrush.Color = colour;

    RectangleGeometry RectGeo = new();

    RectangleGeometry ShadowGeo = new();

    protected override void OnRender(DrawingContext drawingContext)
    {
        float total = _Maximum - _Minimum;
        float percent = ( _Value / total );
        var rect = new Rect( 0, 0, ActualWidth, ActualHeight );
        var forgroundRect = new Rect( 0, 0, ActualWidth * percent, ActualHeight );
        var shadowRect = new Rect( 0, 0, ActualWidth * percent + 1, ActualHeight );
        var typeFace = new Typeface( FontFamily, FontStyle, FontWeight, FontStretch );
        var percentText = ( percent ).ToString( "0.##%" );
#pragma warning disable CS0618 // Type or member is obsolete
        var PercentText = new FormattedText( percentText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeFace, FontSize, Brushes.White );
        var ShadowText = new FormattedText( percentText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeFace, FontSize, Brushes.Black );
#pragma warning restore CS0618 // Type or member is obsolete
        var textX = ( ActualWidth - PercentText.Width ) / 2;
        var textY = ( ActualHeight - PercentText.Height ) / 2;
        var now = DateTime.Now - StartTime;
        double firstStop;
        double secondStop;
        double totalTime = 1 * 1000;
        firstStop = !Finished ? 3 * ((now.TotalMilliseconds % totalTime) / totalTime) - 1 : 0.5 - 0.08;
        secondStop = firstStop + 0.08;
        RectGeo.Rect = rect;
        ShadowGeo.Rect = shadowRect;
        OverlayBrush.GradientStops[1].Offset = ( OverlayBrush.GradientStops[2].Offset = firstStop ) - 0.3;
        OverlayBrush.GradientStops[4].Offset = ( OverlayBrush.GradientStops[3].Offset = secondStop ) + 0.3;
        drawingContext.PushClip( RectGeo );
        drawingContext.DrawRectangle( Background, null, rect );
        drawingContext.DrawRectangle( Brushes.DarkSlateGray, null, shadowRect );
        drawingContext.DrawRectangle( ForgroundBrush, null, forgroundRect );
        drawingContext.PushClip( ShadowGeo );
        drawingContext.DrawRectangle( OverlayBrush, null, rect );
        drawingContext.Pop();
        drawingContext.DrawRectangle( TopBrush, null, new Rect( 0, 0, ActualWidth, ActualHeight / 2 ) );
        drawingContext.DrawRectangle( BottomBrush, null, new Rect( 0, ActualHeight - ( ActualHeight / 2 ), ActualWidth, ActualHeight / 2 ) );
        drawingContext.DrawText( ShadowText, new Point( textX + 1, textY + 1 ) );
        drawingContext.DrawText( PercentText, new Point( textX, textY ) );
        drawingContext.DrawRectangle( null, OutlinePen, rect );
        drawingContext.Pop();
    }

    protected override void ParentLayoutInvalidated(UIElement child)
    {
        InvalidateVisual();
        base.ParentLayoutInvalidated( child );
    }
}