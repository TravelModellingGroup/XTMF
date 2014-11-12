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
using System.Windows.Media;
using System.Windows.Media.Animation;
using XTMF.Gui.UserControls;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for ModelSystemDisplay.xaml
    /// </summary>
    public partial class ModelSystemDisplay : UserControl
    {
        public static readonly DependencyProperty RootModuleProperty = DependencyProperty.Register( "RootModule", typeof( IModelSystemStructure )
            , typeof( ModelSystemDisplay ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnRootModuleChanged ) );

        public static readonly DependencyProperty SelectedModuleProperty = DependencyProperty.Register( "SelectedModule", typeof( IModelSystemStructure )
            , typeof( ModelSystemDisplay ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedModuleChanges ) );

        public static readonly DependencyProperty EditModeProperty = DependencyProperty.Register( "EditMode", typeof( bool )
            , typeof( ModelSystemDisplay ), new FrameworkPropertyMetadata( false, FrameworkPropertyMetadataOptions.AffectsRender, OnEditModeChanged ) );

        public ModelSystemDisplay()
        {
            InitializeComponent();
        }

        public event Action<object, IModelSystemStructure> ModuleSelected;
        public event Action<object, IModelSystemStructure, int, int> ChildMoved;

        public ModelSystemDisplayStructure DisplayRoot
        {
            get;
            set;
        }

        public IModelSystemStructure RootModule
        {
            get
            {
                return (IModelSystemStructure)this.GetValue( RootModuleProperty );
            }
            set
            {
                this.SetValue( RootModuleProperty, value );
            }
        }

        public IModelSystemStructure SelectedModule
        {
            get
            {
                return (IModelSystemStructure)this.GetValue( SelectedModuleProperty );
            }
            set
            {
                this.SetValue( SelectedModuleProperty, value );
            }
        }

        public bool EditMode
        {
            get
            {
                return (bool)this.GetValue( EditModeProperty );
            }
            set
            {
                this.SetValue( EditModeProperty, value );
            }
        }

        struct DepthFirstCombination
        {
            internal ModelSystemDisplayStructure DisplayStructure;
            internal int CurrentPosition;
        }

        public void MoveSelectedDown()
        {
            var toFind = this.SelectedModule;
            var stack = new Stack<DepthFirstCombination>();
            stack.Push( new DepthFirstCombination()
            {
                DisplayStructure = this.DisplayRoot,
                CurrentPosition = 0
            } );
            while ( stack.Count > 0 )
            {
                var current = stack.Pop();
                var children = current.DisplayStructure.Children;
                // if this is the right one, success
                if ( current.DisplayStructure.Structure == toFind )
                {
                    //second phase goes here
                    if ( children != null && ( current.DisplayStructure.IsExpanded &
                        current.CurrentPosition < children.Length ) )
                    {
                        children[current.CurrentPosition].IsSelected = true;
                        return;
                    }
                    while ( stack.Count > 0 )
                    {
                        current = stack.Pop();
                        children = current.DisplayStructure.Children;
                        if ( current.CurrentPosition < children.Length )
                        {
                            children[current.CurrentPosition].IsSelected = true;
                            return;
                        }
                    }
                    return;
                }
                if ( children != null )
                {
                    if ( current.CurrentPosition < children.Length )
                    {
                        current.CurrentPosition++;
                        stack.Push( current );
                        stack.Push( new DepthFirstCombination()
                        {
                            DisplayStructure = children[current.CurrentPosition - 1],
                            CurrentPosition = 0
                        } );
                    }
                }
            }
        }

        public void MoveSelectedUp()
        {
            var toFind = this.SelectedModule;
            var stack = new Stack<DepthFirstCombination>();
            stack.Push( new DepthFirstCombination()
            {
                DisplayStructure = this.DisplayRoot,
                CurrentPosition = ( this.DisplayRoot.Children == null ? -1 : this.DisplayRoot.Children.Length - 1 )
            } );
            while ( stack.Count > 0 )
            {
                var current = stack.Pop();
                var children = current.DisplayStructure.Children;
                // if this is the right one, success
                if ( current.DisplayStructure.Structure == toFind )
                {
                    //second phase goes here
                    if ( stack.Count > 0 )
                    {
                        current = stack.Pop();
                        if ( !current.DisplayStructure.IsExpanded )
                        {
                            current.DisplayStructure.IsSelected = true;
                        }
                        else
                        {
                            children = current.DisplayStructure.Children;
                            if ( current.CurrentPosition >= 0 )
                            {
                                var child = children[current.CurrentPosition];
                                while ( true )
                                {
                                    // we need to find the "bottom most child"
                                    if ( child.IsExpanded )
                                    {
                                        child = child.Children[child.Children.Length - 1];
                                    }
                                    else
                                    {
                                        child.IsSelected = true;
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                current.DisplayStructure.IsSelected = true;
                            }
                        }
                    }
                    return;
                }
                if ( children != null )
                {
                    if ( current.CurrentPosition >= 0 )
                    {
                        current.CurrentPosition--;
                        stack.Push( current );
                        stack.Push( new DepthFirstCombination()
                        {
                            DisplayStructure = children[current.CurrentPosition + 1],
                            CurrentPosition = ( children[current.CurrentPosition + 1].Children == null ? -1 :
                                                    children[current.CurrentPosition + 1].Children.Length - 1 )
                        } );
                    }
                }
            }
        }

        public bool ForceRefresh { get; set; }


        public void Refresh()
        {
            if ( this.DisplayRoot == null || this.DisplayRoot.Structure != this.RootModule )
            {
                this.DisplayRoot = new ModelSystemDisplayStructure( this.RootModule );
                this.DisplayTree.ItemsSource = new ModelSystemDisplayStructure[] { this.DisplayRoot };
            }
            else
            {
                this.DisplayRoot.RefreshAll();
                if ( ForceRefresh )
                {
                    this.DisplayTree.Items.Refresh();
                    this.ForceRefresh = false;
                }
            }
        }

        private static void OnRootModuleChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ModelSystemDisplay;
            us.DisplayRoot = new ModelSystemDisplayStructure( us.RootModule );
            us.DisplayTree.ItemsSource = new ModelSystemDisplayStructure[] { us.DisplayRoot };
        }

        private static void OnEditModeChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = (ModelSystemDisplay)source;
            ( (UMSIContextMenu)us.ContextMenu ).EditMode = (bool)e.NewValue;
        }

        private static void OnSelectedModuleChanges(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = (ModelSystemDisplay)source;
            var d = us.ModuleSelected;
            var selected = e.NewValue as IModelSystemStructure;
            if ( selected == null )
            {
                return;
            }
            if ( us.DisplayTree.SelectedItem != selected )
            {
                var displayElement = FindElement( us.DisplayRoot, selected );
                if ( displayElement != null )
                {
                    displayElement.IsSelected = true;
                }
            }
            if ( d != null )
            {
                d( source, e.NewValue as IModelSystemStructure );
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if ( !e.Handled )
            {
                e.Handled = true;
                var display = ( e.NewValue as ModelSystemDisplayStructure );
                if ( display == null )
                {
                    this.SelectedModule = this.RootModule;
                }
                else
                {
                    this.SelectedModule = display.Structure;
                }
            }
        }

        internal bool IsExpanded(IModelSystemStructure modelSystemStructure)
        {
            var element = FindElement( this.DisplayRoot, modelSystemStructure );
            if ( element != null )
            {
                return element.IsExpanded;
            }
            return false;
        }

        private static ModelSystemDisplayStructure FindElement(ModelSystemDisplayStructure root, IModelSystemStructure toFind)
        {
            var stack = new Stack<ModelSystemDisplayStructure>();
            stack.Push( root );
            while ( stack.Count > 0 )
            {
                var current = stack.Pop();
                // if this is the right one, success
                if ( current.Structure == toFind )
                {
                    return current;
                }
                var children = current.Children;
                if ( children != null )
                {
                    // if not, add our children to the stack and continue on
                    for ( int i = 0; i < children.Length; i++ )
                    {
                        stack.Push( children[i] );
                    }
                }
            }
            return null;
        }

        internal void ToggleExpandSelected()
        {
            var element = this.DisplayTree.SelectedItem as ModelSystemDisplayStructure;
            if ( element == null )
            {
                element = this.DisplayRoot;
            }
            if ( element != null )
            {
                element.IsExpanded = !element.IsExpanded;
            }
        }

        private void DisplayTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition( this.DisplayTree );
            var hitItem = this.DisplayTree.InputHitTest( position ) as DependencyObject;
            if ( hitItem != null && hitItem != this.DisplayTree )
            {
                var item = FindAncestor<TreeViewItem>( hitItem );
                // we might have been clicked without having a treeview item selected
                if ( item == null ) return;
                var structure = item.Header as ModelSystemDisplayStructure;
                this.SelectedModule = structure.Structure;
                if ( structure.Structure != null )
                {
                    if ( structure.Structure.IsCollection )
                    {
                        ( (UMSIContextMenu)this.ContextMenu ).SetData(
                            this.RootModule,
                            ModelSystemStructure.GetParent( this.RootModule, structure.Structure ),
                            structure.Structure );
                    }
                    else
                    {
                        ( (UMSIContextMenu)this.ContextMenu ).SetData(
                            ModelSystemStructure.CheckForRootModule( this.RootModule, structure.Structure, ModelSystemStructure.GetRootRequirement( structure.Structure.Type ) ),
                            ModelSystemStructure.GetParent( this.RootModule, structure.Structure ),
                            structure.Structure );
                    }
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject dependencyObject)
                where T : class
        {
            DependencyObject target = dependencyObject;
            do
            {
                target = VisualTreeHelper.GetParent( target );
            }
            while ( target != null && !( target is T ) );
            return target as T;
        }

        private void DisplayTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if ( e.LeftButton == MouseButtonState.Pressed )
            {
                var mousePos = e.GetPosition( null );
                var diff = this.MouseDownPoint - mousePos;
                if ( Math.Abs( diff.X ) > 1
                    || Math.Abs( diff.Y ) > 1 )
                {
                    if ( this.EditMode )
                    {
                        var treeView = sender as TreeView;
                        var treeViewItem =
                            FindAncestor<TreeViewItem>( (DependencyObject)e.OriginalSource );
                        if ( treeView == null || treeViewItem == null )
                            return;
                        var folderViewModel = treeView.SelectedItem as ModelSystemDisplayStructure;
                        if ( folderViewModel == null )
                            return;
                        var dragData = new DataObject( folderViewModel );
                        this.DraggedItem = treeViewItem;
                        DragDrop.DoDragDrop( treeViewItem, dragData, DragDropEffects.Move );
                    }
                }

            }
        }

        Point MouseDownPoint;
        TreeViewItem DraggedItem;
        private void DisplayTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.MouseDownPoint = e.GetPosition( null );
        }

        private void DisplayTree_DragEnter(object sender, DragEventArgs e)
        {
            if ( !e.Data.GetDataPresent( typeof( ModelSystemDisplayStructure ) ) )
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DisplayTree_Drop(object sender, DragEventArgs e)
        {
            if ( e.Data.GetDataPresent( typeof( ModelSystemDisplayStructure ) ) )
            {
                var originModelSystemStructure = e.Data.GetData( typeof( ModelSystemDisplayStructure ) ) as ModelSystemDisplayStructure;
                var destination = FindAncestor<TreeViewItem>( (DependencyObject)e.OriginalSource );
                if ( destination == null ) return;
                var destinationModelSystemStructure = destination.Header as ModelSystemDisplayStructure;
                if ( destinationModelSystemStructure == null || originModelSystemStructure == null )
                    return;
                DoubleAnimation animation = new DoubleAnimation( 0, new Duration( TimeSpan.FromMilliseconds( 250 ) ) );
                var appear = new DoubleAnimation( 1.0, new Duration( TimeSpan.FromMilliseconds( 250 ) ) );
                animation.Completed += (o, data) =>
                {
                    this.Dispatcher.BeginInvoke( new Action( () =>
                    {
                        // here is the magic, first make sure that they both have the same parent and are in a collection
                        var firstParent = this.GetParent( this.DisplayRoot, originModelSystemStructure );
                        var secondParent = this.GetParent( this.DisplayRoot, destinationModelSystemStructure );
                        if ( firstParent == secondParent && firstParent != null && firstParent.Structure.IsCollection )
                        {
                            // now the question is should it go above or below?
                            var relativeToDestination = e.GetPosition( destination );
                            bool goBelow = relativeToDestination.Y >= ( destination.ActualHeight / 2 );
                            var indexOfOrigin = firstParent.IndexOf( originModelSystemStructure );
                            var indexOfDestination = firstParent.IndexOf( destinationModelSystemStructure );
                            var callBack = this.ChildMoved;
                            if ( callBack != null )
                            {
                                if ( goBelow )
                                {
                                    var numberOfChildren = firstParent.Children.Length;
                                    callBack( this, firstParent.Structure, indexOfOrigin, indexOfDestination + 1 >= numberOfChildren ?
                                        indexOfDestination : indexOfDestination + 1 );
                                }
                                else
                                {
                                    callBack( this, firstParent.Structure, indexOfOrigin, indexOfDestination );
                                }
                            }
                            // now we can refresh the parent structure
                            firstParent.RefreshAll();
                            this.DisplayTree.Items.Refresh();
                        }
                    } ) );
                    ( this.DraggedItem as FrameworkElement ).BeginAnimation( FrameworkElement.OpacityProperty, appear );
                };
                ( this.DraggedItem as FrameworkElement ).BeginAnimation( FrameworkElement.OpacityProperty, animation );
            }
        }

        private ModelSystemDisplayStructure GetParent(ModelSystemDisplayStructure current, ModelSystemDisplayStructure lookingFor)
        {
            var children = current.Children;
            if ( children == null )
            {
                return null;
            }
            for ( int i = 0; i < children.Length; i++ )
            {
                if ( children[i] == lookingFor )
                {
                    return current;
                }
            }
            for ( int i = 0; i < children.Length; i++ )
            {
                var res = GetParent( children[i], lookingFor );
                if ( res != null ) return res;
            }
            return null;
        }
    }
}