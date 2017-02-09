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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for ModelSystemSelector.xaml
    /// </summary>
    public partial class Selector : UserControl
    {
        private Action<object> ClickedAction;

        private Color ControlBackground = (Color)Application.Current.FindResource( "ControlBackgroundColour" );

        private List<object> DisplayedItems = new List<object>( 10 );

        private int FocusedSelectedModule;

        private List<object> Items = new List<object>( 10 );

        private List<BorderIconButton> ModelSystemsButtons = new List<BorderIconButton>( 10 );

        private Action<object> RightClickedAction;

        private List<string> Searchable = new List<string>( 10 );

        private Color SelectionBlue = (Color)Application.Current.FindResource( "SelectionBlue" );

        private BitmapImage SettingsImage = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/Settings.png" ) );

        public Selector()
        {
            FocusedSelectedModule = -1;
            ClickedAction = newModelSystem_Clicked;
            RightClickedAction = newModelSystem_RightClicked;
            var selectDelegate = new KeyEventHandler( SearchBox_PreviewKeyDown );
            InitializeComponent();
            SearchBox.PreviewKeyDown += selectDelegate;
            ModelSystemPanel.PreviewKeyDown += selectDelegate;
        }

        public event Action<object> ItemFocused;

        public event Action<BorderIconButton, object> ItemRightClicked;

        public event Action<object> ItemSelected;

        public string NoItemsText { get { return NothingFound.Text; } set { NothingFound.Text = value; } }

        public Orientation Orientation
        {
            get
            {
                return ModelSystemPanel.Orientation;
            }

            set
            {
                Orientation o = value;
                if ( o == Orientation.Horizontal )
                {
                    Containment.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    Containment.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
                else
                {
                    Containment.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    Containment.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                }
                ModelSystemPanel.Orientation = value;
            }
        }

        public void Add(string name, string description, object data)
        {
            Add( name, description, data, null, ControlBackground );
        }

        public void Add(string name, string description, object data, ContextMenu menu)
        {
            FocusedSelectedModule = -1;
            BorderIconButton newModelSystem = new BorderIconButton();
            newModelSystem.HorizontalAlignment = HorizontalAlignment.Left;
            newModelSystem.VerticalAlignment = VerticalAlignment.Center;
            newModelSystem.Header = name;
            newModelSystem.Margin = new Thickness( 5 );
            newModelSystem.Width = 250;
            newModelSystem.Text = description;
            newModelSystem.HighlightColour = SelectionBlue;
            newModelSystem.Icon = SettingsImage;
            newModelSystem.Clicked += ClickedAction;
            newModelSystem.RightClicked += RightClickedAction;
            newModelSystem.ContextMenu = menu;
            Items.Add( data );
            DisplayedItems.Add( data );
            Searchable.Add( String.Concat( ( name == null ? String.Empty : name.ToLower() ), " ", ( description == null ? String.Empty : description.ToLower() ) ) );
            ModelSystemsButtons.Add( newModelSystem );
            ModelSystemPanel.Children.Add( newModelSystem );
            NothingFound.Visibility = Visibility.Collapsed;
        }

        public void Add(string name, string description, object data, ContextMenu menu, Color colour)
        {
            FocusedSelectedModule = -1;
            BorderIconButton newModelSystem = new BorderIconButton();
            newModelSystem.HorizontalAlignment = HorizontalAlignment.Left;
            newModelSystem.VerticalAlignment = VerticalAlignment.Center;
            newModelSystem.Header = name;
            newModelSystem.Margin = new Thickness( 5 );
            newModelSystem.Width = 250;
            newModelSystem.Text = description;
            newModelSystem.HighlightColour = SelectionBlue;
            newModelSystem.Icon = SettingsImage;
            newModelSystem.Clicked += ClickedAction;
            newModelSystem.RightClicked += RightClickedAction;
            newModelSystem.ContextMenu = menu;
            newModelSystem.ShadowColour = colour;
            Items.Add( data );
            DisplayedItems.Add( data );
            Searchable.Add( String.Concat( ( name == null ? String.Empty : name.ToLower() ), " ", ( description == null ? String.Empty : description.ToLower() ) ) );
            ModelSystemsButtons.Add( newModelSystem );
            ModelSystemPanel.Children.Add( newModelSystem );
            NothingFound.Visibility = Visibility.Collapsed;
        }

        public void Clear()
        {
            Items.Clear();
            ModelSystemsButtons.Clear();
            ModelSystemPanel.Children.Clear();
            DisplayedItems.Clear();
            Searchable.Clear();
            SearchBox.Filter = String.Empty;
            NothingFound.Visibility = Visibility.Visible;
        }

        public void TextChanged(string text)
        {
            Dispatcher.BeginInvoke( new Action( delegate
            {
                    FocusedSelectedModule = -1;
                    SetModuleFocus();
                    ApplyFilter( text );
                } ) );
        }

        internal void ClearFilter()
        {
            SearchBox.Filter = String.Empty;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ( !e.Handled )
            {
                if ( e.Key == Key.Down )
                {
                    MoveModuleFocus( 1 );
                    e.Handled = true;
                }
                else if ( e.Key == Key.Up )
                {
                    MoveModuleFocus( -1 );
                    e.Handled = true;
                }
                else if ( e.Key == Key.Enter )
                {
                    SelectFocusedModule();
                    e.Handled = true;
                }
            }
            base.OnKeyDown( e );
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            OnKeyDown( e );
            base.OnPreviewKeyDown( e );
        }

        private void ApplyFilter(string filterText)
        {
            var numberOfModelSystems = Items.Count;
            filterText = filterText.ToLower();
            DisplayedItems.Clear();
            ModelSystemPanel.Children.Clear();
            // Check to see if there is no filter
            if ( String.IsNullOrEmpty( filterText ) )
            {
                // if so just add everything
                for ( int i = 0; i < numberOfModelSystems; i++ )
                {
                    DisplayedItems.Add( Items[i] );
                    ModelSystemPanel.Children.Add( ModelSystemsButtons[i] );
                }
            }
            else
            {
                // if there is a filter then go through everything and only include the things that
                // contain the text of the filter
                if ( numberOfModelSystems > 500 )
                {
                    // go through them all in parallel then add them in order
                    /*var result = this.Searchable.AsParallel().AsOrdered()
                        .Select( (str, index) => new { Index = index } )
                        .Where( ind => this.Searchable[ind.Index].Contains( filterText ) );
                    */
                    List<int> final = new List<int>();
                    var finalLock = new object();
                    Parallel.For( 0, numberOfModelSystems, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        delegate {
                            return new List<int>();
                        },
                        delegate(int i, ParallelLoopState unused, List<int> results)
                        {
                            if ( Searchable[i].Contains( filterText ) )
                            {
                                results.Add( i );
                            }
                            return results;
                        },
                        delegate(List<int> results)
                        {
                            lock ( finalLock )
                            {
                                final.AddRange( results );
                            }
                        } );
                    final.Sort();
                    //var buttons = new System.ComponentModel.BindingList<BorderIconButton>();
                    foreach ( var i in final )
                    {
                        DisplayedItems.Add( Items[i] );
                        ModelSystemPanel.Children.Add( ModelSystemsButtons[i] );
                    }
                }
                else
                {
                    for ( int i = 0; i < numberOfModelSystems; i++ )
                    {
                        if ( Searchable[i].Contains( filterText ) )
                        {
                            DisplayedItems.Add( Items[i] );
                            ModelSystemPanel.Children.Add( ModelSystemsButtons[i] );
                        }
                    }
                }
            }
            if ( DisplayedItems.Count == 0 )
            {
                NothingFound.Visibility = Visibility.Visible;
            }
            else
            {
                NothingFound.Visibility = Visibility.Collapsed;
            }
        }

        private int FindIndex(object obj)
        {
            var numberOfModelSystems = Items.Count;
            for ( int i = 0; i < numberOfModelSystems; i++ )
            {
                if ( ModelSystemPanel.Children[i] == obj )
                {
                    return i;
                }
            }
            return -1;
        }

        private void MoveModuleFocus(int increment)
        {
            FocusedSelectedModule += increment;
            if ( FocusedSelectedModule < 0 )
            {
                FocusedSelectedModule = -1;
            }
            if ( FocusedSelectedModule >= ModelSystemPanel.Children.Count )
            {
                FocusedSelectedModule = ModelSystemPanel.Children.Count - 1;
            }
            SetModuleFocus();
        }

        private void newModelSystem_Clicked(object obj)
        {
            int index = FindIndex( obj );
            FocusedSelectedModule = index;
            SetModuleFocus();
            var e = ItemSelected;
            if ( e != null )
            {
                if ( index != -1 )
                {
                    e( DisplayedItems[index] );
                }
            }
        }

        private void newModelSystem_RightClicked(object obj)
        {
            int index = FindIndex( obj );
            FocusedSelectedModule = index;
            SetModuleFocus();
            // open the button's menu if it exists
            var button = obj as BorderIconButton;
            if ( button != null )
            {
                var menu = button.ContextMenu;
                if ( menu != null )
                {
                    menu.PlacementTarget = button;
                    menu.IsOpen = true;
                    var e = ItemRightClicked;
                    if ( e != null )
                    {
                        e( button, DisplayedItems[index] );
                    }
                }
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown( e );
        }

        private void SelectFocusedModule()
        {
            var panelChildren = ModelSystemPanel.Children;
            if ( panelChildren != null && FocusedSelectedModule >= 0 && FocusedSelectedModule < panelChildren.Count )
            {
                ClickedAction( panelChildren[FocusedSelectedModule] );
                FocusedSelectedModule = -1;
            }
        }

        private void SetModuleFocus()
        {
            int count = 0;
            foreach ( var child in ModelSystemPanel.Children )
            {
                var button = child as BorderIconButton;
                if ( button != null )
                {
                    var selected = ( count == FocusedSelectedModule );
                    button.Selected = selected;
                    if ( selected )
                    {
                        button.BringIntoView();
                    }
                    count++;
                }
            }
            var e = ItemFocused;
            if ( e != null )
            {
                e( FocusedSelectedModule < 0 ? null : DisplayedItems[FocusedSelectedModule] );
            }
        }
    }
}