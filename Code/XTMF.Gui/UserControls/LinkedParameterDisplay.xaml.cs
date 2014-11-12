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
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for LinkedParameterDisplay.xaml
    /// </summary>
    public partial class LinkedParameterDisplay : UserControl
    {
        public static readonly DependencyProperty EditModeProperty = DependencyProperty.Register( "EditMode", typeof( bool )
            , typeof( LinkedParameterDisplay ), new FrameworkPropertyMetadata( true, FrameworkPropertyMetadataOptions.AffectsRender, OnEditModeChanged ) );

        public static readonly DependencyProperty LinkedParameterProperty = DependencyProperty.Register( "LinkedParameter", typeof( ILinkedParameter )
            , typeof( LinkedParameterDisplay ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnLinkedParameterChanged ) );

        public static readonly DependencyProperty LinkedParameterValueProperty = DependencyProperty.Register( "LinkedParameterValue", typeof( string )
            , typeof( LinkedParameterDisplay ), new FrameworkPropertyMetadata( "", FrameworkPropertyMetadataOptions.AffectsRender ) );

        public Func<ILinkedParameter, IModuleParameter, bool> RemoveLinkedParameterParameter;

        public Func<ILinkedParameter, string, bool> SetLinkedParameterValue;

        private bool IsEditingContainedModuleGrid = false;

        private LPValue[] LPData = { new LPValue() { Name = "None", Value = "", Exists = false } };

        private List<LPValue> LPModules = new List<LPValue>();

        public LinkedParameterDisplay()
        {
            this.DataContext = this;
            InitializeComponent();
            this.ContainedModuleGrid.SelectionMode = DataGridSelectionMode.Single;
            this.ContainedModuleGrid.SelectionUnit = DataGridSelectionUnit.Cell;
            this.ContainedModuleGrid.CanUserDeleteRows = false;
            this.ContainedModuleGrid.CanUserAddRows = false;
            this.ContainedModuleGrid.CellEditEnding += this.ContainedModuleGrid_CellEditEnding;
            RefreshGrids();
        }

        public bool EditMode
        {
            get { return (bool)GetValue( EditModeProperty ); }
            set { SetValue( EditModeProperty, value ); }
        }

        public ILinkedParameter LinkedParameter
        {
            get { return (ILinkedParameter)GetValue( LinkedParameterProperty ); }
            set { SetValue( LinkedParameterProperty, value ); }
        }

        internal void Refresh()
        {
            var lp = this.LinkedParameter;
            Refresh( this, lp );
        }

        private static void OnEditModeChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as LinkedParameterDisplay;
            us.Included.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnLinkedParameterChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as LinkedParameterDisplay;
            var lp = e.NewValue as ILinkedParameter;
            Refresh( us, lp );
        }

        private static void Refresh(LinkedParameterDisplay us, ILinkedParameter lp)
        {
            if ( lp == null )
            {
                us.LPData[0].Name = "None";
                us.LPData[0].Value = String.Empty;
                us.LPData[0].Exists = false;
            }
            else
            {
                us.LPData[0].Name = lp.Name;
                us.LPData[0].Value = lp.Value;
                us.LPData[0].Exists = true;
            }
            us.LoadLPModules( lp );
            us.RefreshGrids();
        }

        private void LoadLPModules(ILinkedParameter lp)
        {
            this.LPModules.Clear();
            if ( lp != null && lp.Parameters != null )
            {
                foreach ( var param in lp.Parameters )
                {
                    this.LPModules.Add( new LPValue() { ModuleName = param.BelongsTo.Name, Name = param.Name, Value = param.Value.ToString(), Exists = true } );
                }
            }
        }

        private void RefreshGrids()
        {
            this.NameBlock.DataContext = null;
            this.ValueBox.DataContext = null;
            if ( this.LPData != null )
            {
                this.NameBlock.DataContext = this.LPData;
                if ( this.LPData[0].Exists )
                {
                    this.ValueBox.DataContext = this.LPData;
                    this.ValueBox.IsEnabled = true;
                }
                else
                {
                    this.ValueBox.IsEnabled = false;
                }
            }
            else
            {
                this.ValueBox.IsEnabled = false;
            }
            if ( !IsEditingContainedModuleGrid )
            {
                this.ContainedModuleGrid.ItemsSource = this.LPModules;
                this.ContainedModuleGrid.Items.Refresh();
            }
        }

        private void Validate(TextBox box)
        {
            // time to check if we can actually save the data
            if ( !SetLinkedParameterValue( this.LinkedParameter, box.Text ) )
            {
                MessageBox.Show( "This value is invalid for this linked parameter" );
            }
            else
            {
                this.Refresh();
            }
        }

        new private void LostFocus(StackPanel border)
        {
            if ( border.Background.IsFrozen )
            {
                border.Background = border.Background.CloneCurrentValue();
            }
            if ( !border.IsKeyboardFocusWithin )
            {
                ColorAnimation setFocus = new ColorAnimation( Color.FromRgb( 0x30, 0x30, 0x30 ), new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
                border.Background.BeginAnimation( SolidColorBrush.ColorProperty, setFocus );
            }
        }

        private void ParameterBorder_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = ( sender as StackPanel );
            if ( border == null ) return;
            SetFocus( border );
        }

        private void ParameterBorder_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = ( sender as StackPanel );
            if ( border == null ) return;
            LostFocus( border );
        }

        private void ParameterBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = ( sender as StackPanel );
            if ( border == null ) return;
            SetFocus( border );
        }

        private void ParameterBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = ( sender as StackPanel );
            if ( border == null ) return;
            LostFocus( border );
        }

        private void SetFocus(StackPanel border)
        {
            if ( border.Background.IsFrozen )
            {
                border.Background = border.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation( border.IsKeyboardFocusWithin ? Color.FromRgb( 0x70, 0x70, 0x70 ) : Color.FromRgb( 0x40, 0x40, 0x40 ),
                new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            border.Background.BeginAnimation( SolidColorBrush.ColorProperty, setFocus );
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textbox = ( sender as TextBox );
            if ( textbox == null ) return;
            if ( textbox.Background.IsFrozen )
            {
                textbox.Background = textbox.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation( Color.FromRgb( 0x79, 0x79, 0x79 ), new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            textbox.Background.BeginAnimation( SolidColorBrush.ColorProperty, setFocus );
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as TextBox;
            BindingExpression be = box.GetBindingExpression( TextBox.TextProperty );
            be.UpdateSource();
            var textbox = ( sender as TextBox );
            if ( textbox == null ) return;
            if ( textbox.Background.IsFrozen )
            {
                textbox.Background = textbox.Background.CloneCurrentValue();
            }
            ColorAnimation setFocus = new ColorAnimation( Color.FromRgb( 0x59, 0x59, 0x59 ), new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
            textbox.Background.BeginAnimation( SolidColorBrush.ColorProperty, setFocus );
        }

        private void TextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            bool move = e.Key == Key.Enter | e.Key == Key.Up | e.Key == Key.Down;
            bool up = e.Key == Key.Enter ? ( ( Keyboard.IsKeyDown( Key.LeftShift ) | Keyboard.IsKeyDown( Key.RightShift ) ) )
                : e.Key == Key.Up;
            if ( move )
            {
                // we need to delay this until after the text is changed
                var box = sender as TextBox;
                BindingExpression be = box.GetBindingExpression( TextBox.TextProperty );
                be.UpdateSource();
                // MoveFocus takes a TraversalRequest as its argument.
                TraversalRequest request = new TraversalRequest(
                   up ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next );

                // Gets the element with keyboard focus.
                UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;

                // Change keyboard focus.
                if ( elementWithFocus != null )
                {
                    elementWithFocus.MoveFocus( request );
                }
            }
        }

        private void TextBox_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            // we need to delay this until after the text is changed
            var box = sender as TextBox;
            Validate( box );
        }

        private void ContainedModuleGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if ( e.EditAction == DataGridEditAction.Cancel )
            {
                return;
            }

            var element = e.EditingElement as CheckBox;
            if ( element != null && element.IsChecked == false )
            {
                var row = e.Row;
                var index = this.LPModules.IndexOf( row.Item as LPValue );
                if ( index >= 0 )
                {
                    IsEditingContainedModuleGrid = true;
                    this.RemoveLinkedParameterParameter( this.LinkedParameter, this.LinkedParameter.Parameters[index] );
                    IsEditingContainedModuleGrid = false;
                }
            }
        }

        private void ContainedModuleGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            if ( !cell.IsEditing )
            {
                // enables editing on single click
                if ( !cell.IsFocused )
                {
                    cell.Focus();
                }

                if ( !cell.IsSelected )
                {
                    cell.IsSelected = true;
                }
            }
        }

        private void ContainedModuleGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            if ( cell.IsEditing )
            {
                this.ContainedModuleGrid.CommitEdit( DataGridEditingUnit.Cell, true );
                this.ContainedModuleGrid.CancelEdit();
                this.Refresh();
            }
        }

        private class LPValue
        {
            public bool Exists { get; set; }

            public string ModuleName { get; set; }

            public string Name { get; set; }

            public string Value { get; set; }
        }
    }
}