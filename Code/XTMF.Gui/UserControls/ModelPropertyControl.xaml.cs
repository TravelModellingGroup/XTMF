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
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for ModelPropertyPage.xaml
    /// </summary>
    public partial class ModelPropertyControl : UserControl
    {
        public static readonly DependencyProperty ElementContextMenuProperty = DependencyProperty.Register( "ElementContextMenu", typeof( ContextMenu )
            , typeof( ModelPropertyControl ) );

        public bool ChangesMade = false;

        private List<ILinkedParameter> _LinkedParameters;

        private IModuleParameters _Parameters;

        private ObservableCollection<ModelParameterProxy> ParameterList;

        public ModelPropertyControl()
        {
            this.ParameterList = new ObservableCollection<ModelParameterProxy>();
            InitializeComponent();
            this.PropertyGrid.Background = Brushes.Transparent;
            this.PropertyGrid.ItemsSource = this.ParameterList;
            this.PropertyGrid.BeginningEdit += new EventHandler<DataGridBeginningEditEventArgs>( PropertyGrid_BeginningEdit );
            this.PropertyGrid.CellEditEnding += new EventHandler<DataGridCellEditEndingEventArgs>( PropertyGrid_CellEditEnding );
            this.PropertyGrid.SizeChanged += new SizeChangedEventHandler( PropertyGrid_SizeChanged );
            this.PropertyGrid.ClipToBounds = true;
            this.PropertyGrid.CanUserSortColumns = false;
            this.PropertyGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
            this.PropertyGrid.SelectionMode = DataGridSelectionMode.Single;
            this.PropertyGrid.SelectionUnit = DataGridSelectionUnit.Cell;
            this.PropertyGrid.CanUserDeleteRows = false;
            this.PropertyGrid.CanUserAddRows = false;
            this.PropertyGrid.FontSize = 16;
        }

        public event Action ParameterChanged;

        public Action<IModuleParameter, string> ControlWrite { get; set; }

        public bool DisableSameParameterSelect { get; set; }

        public ContextMenu ElementContextMenu
        {
            get { return (ContextMenu)GetValue( ElementContextMenuProperty ); }
            set { SetValue( ElementContextMenuProperty, value ); }
        }

        public bool IsEditing { get; set; }

        public List<ILinkedParameter> LinkedParameters
        {
            get
            {
                return _LinkedParameters;
            }

            set
            {
                _LinkedParameters = value;
                var temp = this.Parameters;
                this.Parameters = null;
                this.Parameters = temp;
            }
        }

        public IModuleParameters Parameters
        {
            get { return _Parameters; }

            set
            {
                bool sameAsBefore = SameAsPrevious( value );
                this.Dispatcher.Invoke( new Action( delegate()
                    {
                        int previouslySelectedX = -1, previouslySelectedY = -1;
                        if ( this._Parameters != null )
                        {
                            if ( this.ChangesMade )
                            {
                                if ( this.ControlWrite == null )
                                {
                                    this.Save();
                                }
                                this.ChangesMade = false;
                            }
                            if ( sameAsBefore )
                            {
                                var list = this.PropertyGrid.SelectedCells;
                                if ( list != null && list.Count > 0 )
                                {
                                    var currentCell = list[0];
                                    previouslySelectedX = currentCell.Column.DisplayIndex;
                                    previouslySelectedY = this.PropertyGrid.Items.IndexOf( currentCell.Item );
                                }
                            }
                            this.ParameterList.Clear();
                        }
                        this._Parameters = value;
                        this.Dispatcher.BeginInvoke( new Action( delegate()
                            {
                                if ( this._Parameters != null )
                                {
                                    for ( int i = 0; i < value.Parameters.Count; i++ )
                                    {
                                        if ( this.IsEditing || !value.Parameters[i].SystemParameter )
                                        {
                                            this.ParameterList.Add( new ModelParameterProxy( value.Parameters[i], this.LinkedParameters ) );
                                        }
                                    }
                                }
                                // resize the columns
                                ResizeColumns();
                                // select focus if we are using the same type of module
                                if ( sameAsBefore && !DisableSameParameterSelect )
                                {
                                    if ( previouslySelectedX >= 0 && previouslySelectedY >= 0 )
                                    {
                                        Keyboard.Focus( this.PropertyGrid );
                                        this.PropertyGrid.CurrentColumn = this.PropertyGrid.ColumnFromDisplayIndex( previouslySelectedX );
                                        var item = this.PropertyGrid.Items[previouslySelectedY];
                                        var cellInfo = new DataGridCellInfo( item, this.PropertyGrid.CurrentColumn );
                                        this.PropertyGrid.CurrentCell = cellInfo;
                                        this.PropertyGrid.SelectedCells.Clear();
                                        this.PropertyGrid.SelectedCells.Add( cellInfo );
                                    }
                                }
                                else
                                {
                                    this.DisableSameParameterSelect = false;
                                }
                            } ) );
                    } ) );
            }
        }

        public DataGridRow GetRow(int index)
        {
            DataGridRow row = (DataGridRow)this.PropertyGrid.ItemContainerGenerator.ContainerFromIndex( index );
            if ( row == null )
            {
                // may be virtualized, bring into view and try again
                PropertyGrid.ScrollIntoView( PropertyGrid.Items[index] );
                row = (DataGridRow)PropertyGrid.ItemContainerGenerator.ContainerFromIndex( index );
            }
            return row;
        }

        public void Save()
        {
            int numberOfParameters = this.ParameterList.Count;
            for ( int i = 0; i < numberOfParameters; i++ )
            {
                var realParam = this.Parameters.Parameters.First( (param) => ( param == this.ParameterList[i].RealParameter ) );
                realParam.QuickParameter = this.ParameterList[i].Quick;
                if ( this.ParameterList[i].Type == typeof( Int32 ) )
                {
                    realParam.Value = Int32.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Int64 ) )
                {
                    realParam.Value = Int64.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Double ) )
                {
                    realParam.Value = Double.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Single ) )
                {
                    realParam.Value = Single.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( String ) )
                {
                    realParam.Value = this.ParameterList[i].Value;
                }
                else if ( this.ParameterList[i].Type == typeof( DateTime ) )
                {
                    realParam.Value = DateTime.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Char ) )
                {
                    realParam.Value = Char.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Boolean ) )
                {
                    realParam.Value = Boolean.Parse( this.ParameterList[i].Value );
                }
                else if ( this.ParameterList[i].Type == typeof( Time ) )
                {
                    Time t;
                    Time.TryParse( this.ParameterList[i].Value, out t );
                    realParam.Value = t;
                }
                else
                {
                    throw new XTMFRuntimeException( "Unknown Type" );
                }
            }
            this.ChangesMade = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( e.Handled == false )
            {
                if ( e.Key == Key.Escape )
                {
                    if ( this.IsEditing )
                    {
                        e.Handled = true;
                    }
                }
            }
            base.OnKeyDown( e );
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel( e );
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged( sizeInfo );
            ResizeColumns();
        }

        private void DataGridCell_MouseEnter(object sender, MouseEventArgs e)
        {
            var cell = sender as DataGridCell;
            if ( cell != null )
            {
            }
        }

        private void DataGridCell_MouseLeave(object sender, MouseEventArgs e)
        {
            var cell = sender as DataGridCell;
            if ( cell != null )
            {
            }
        }

        private void DataGridCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            if ( cell.IsEditing )
            {
                this.PropertyGrid.CommitEdit();
            }
        }

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void PropertyGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
        }

        private void PropertyGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if ( e.EditAction == DataGridEditAction.Commit )
            {
                int index = 0;
                var proxy = ( e.Row.Item as ModelParameterProxy );
                int numberOfParameters = this.ParameterList.Count;
                for ( ; index < numberOfParameters; index++ )
                {
                    if ( this.ParameterList[index] == proxy )
                    {
                        break;
                    }
                }
                string currentText = null;
                bool quickSave = e.EditingElement is CheckBox;
                var textbox = e.EditingElement as TextBox;
                if ( textbox != null )
                {
                    currentText = textbox.Text = textbox.Text.Replace( "\b", String.Empty );
                }
                if ( index == numberOfParameters || ( !quickSave && textbox != null
                    && !this.ParameterList[index].Validate( currentText ) ) )
                {
                    // Since the data is invalid make sure to revert the changes
                    e.Cancel = true;
                    return;
                }
                else
                {
                    this.ChangesMade = true;
                    if ( quickSave )
                    {
                        if ( this.ControlWrite != null )
                        {
                            this.ParameterList[index].RealParameter.QuickParameter = ( e.EditingElement as CheckBox ).IsChecked == true;
                        }
                        else
                        {
                            this.ParameterList[index].Quick = ( e.EditingElement as CheckBox ).IsChecked == true;
                        }
                    }
                    else if ( textbox != null )
                    {
                        if ( this.ControlWrite != null )
                        {
                            this.ControlWrite( this.ParameterList[index].RealParameter, currentText );
                        }
                        else
                        {
                            this.ParameterList[index].Value = currentText;
                        }
                    }
                    this.ResizeColumns();
                    if ( this.ParameterChanged != null )
                    {
                        this.ParameterChanged();
                    }
                }
            }
        }

        private void PropertyGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ResizeColumns();
        }

        private void ResizeColumns()
        {
            this.Dispatcher.BeginInvoke( new Action( delegate()
                {
                    this.PropertyGrid.Columns[2].MinWidth = 200;
                    this.PropertyGrid.Columns[3].Width = new DataGridLength( 1, DataGridLengthUnitType.Star );
                    this.PropertyGrid.Columns[3].MinWidth = this.PropertyGrid.Columns[3].ActualWidth;
                    this.PropertyGrid.Columns[3].Width = DataGridLength.Auto;
                } ) );
        }

        private bool SameAsPrevious(IModuleParameters value)
        {
            if ( value == null ) return false;
            if ( _Parameters == null || _Parameters.BelongsTo == null ) return false;
            if ( value.BelongsTo == null ) return false;
            return value.BelongsTo.Type != null && ( value.BelongsTo.Type == _Parameters.BelongsTo.Type );
        }
    }
}