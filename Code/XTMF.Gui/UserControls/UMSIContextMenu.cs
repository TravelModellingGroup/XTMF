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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XTMF.Gui.UserControls
{
    public class UMSIContextMenu : ContextMenu
    {
        public IModelSystemStructure SelectedElement;
        HintedTextBox NameBox = new HintedTextBox() { HintText = "Type Name", MinWidth = 200, MaxWidth = 250 };
        protected MenuItem Copy;
        protected IModelSystemStructure CopyBuffer;
        protected MenuItem Cut;
        protected MenuItem Help;
        protected MenuItem Paste;
        protected MenuItem Remove;
        protected MenuItem Rename;
        protected IModelSystemStructure SelectedElementParent;
        private bool _EditMode = true;
        private bool ExitedOnClick = false;
        private bool WasTextAltered;

        public UMSIContextMenu()
        {
            InitializeItems();
            this.Loaded += UMSIContextMenu_Loaded;
            this.Unloaded += UMSIContextMenu_Unloaded;
            this.NameBox.PreviewLostKeyboardFocus += OnPreviewLostKeyboardFocus;
            this.NameBox.GotKeyboardFocus += NameBox_GotKeyboardFocus;
        }

        void NameBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            this.WasTextAltered = true;
        }

        void UMSIContextMenu_Loaded(object sender, RoutedEventArgs e)
        {
            this.ExitedOnClick = false;
            this.WasTextAltered = false;
        }

        private void OnPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // This will stop the menu items from taking over the keyboard control from the rename textbox. 
            // Since it is the only textbox this will work
            if ( this.IsKeyboardFocusWithin )
            {
                e.Handled = true;
            }
        }

        void UMSIContextMenu_Unloaded(object sender, RoutedEventArgs e)
        {
            if ( !this.ExitedOnClick & this.WasTextAltered )
            {
                this.Rename_Click( sender, e );
            }
        }

        public event Action<IModelSystemStructure> InsertRequested;

        public event Action RemovePressed;

        public event Action<string> RenamePressed;

        public bool EditMode
        {
            get
            {
                return _EditMode;
            }
            set
            {
                this._EditMode = value;
                var enable = this._EditMode;
                this.Copy.IsEnabled = enable;
                this.Cut.IsEnabled = enable;
                this.Paste.IsEnabled = enable;
                this.Remove.IsEnabled = enable;
            }
        }

        public void SetData(IModelSystemStructure root,
            IModelSystemStructure parentStructure, IModelSystemStructure selectedElement)
        {
            this.SelectedElement = selectedElement;
            this.Update( root, parentStructure, selectedElement );
        }

        public void Update(IModelSystemStructure root, IModelSystemStructure parent,
            IModelSystemStructure selectedElement)
        {
            this.NameBox.Text = selectedElement.Name;
            this.Help.IsEnabled = !selectedElement.IsCollection;
            if ( !this.EditMode )
            {
                return;
            }
            this.SelectedElementParent = parent;
            this.SelectedElement = selectedElement;
            if ( selectedElement.IsCollection )
            {
                if ( CopyBuffer != null )
                {
                    this.Paste.IsEnabled = this.IsAssignable( root, parent, selectedElement );
                }
                else
                {
                    this.Paste.IsEnabled = false;
                }
                this.Copy.IsEnabled = this.Cut.IsEnabled = selectedElement.Children != null && selectedElement.Children.Count > 0;
            }
            else
            {
                if ( CopyBuffer != null )
                {
                    if ( CopyBuffer.Type != null )
                    {
                        this.Paste.IsEnabled = this.IsAssignable( root, parent, selectedElement );
                    }
                    else
                    {
                        this.Paste.IsEnabled = false;
                    }
                }
                else
                {
                    this.Paste.IsEnabled = false;
                }
                this.Copy.IsEnabled = this.Cut.IsEnabled = selectedElement.Type != null;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            this.ExitedOnClick = true;
            if ( this.SelectedElement != null )
            {
                this.CopyBuffer = this.SelectedElement.Clone();
            }
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if ( this.SelectedElement != null )
            {
                this.CopyBuffer = SelectedElement.Clone();
                this.ExitedOnClick = true;
                if ( this.RemovePressed != null )
                {
                    this.RemovePressed();
                }
            }
        }

        private void Documenation_Click(object sender, RoutedEventArgs e)
        {
            if ( this.SelectedElement != null )
            {
                new DocumentationWindow() { Module = this.SelectedElement }.Show();
            }
        }

        private void InitializeItems()
        {
            // Create all of the MenuItem objects to begin with
            Rename = new MenuItem();
            Remove = new MenuItem();
            Copy = new MenuItem();
            Cut = new MenuItem();
            Paste = new MenuItem();
            Help = new MenuItem();

            // Now that we have the objects we should go and add the information to all of the menu items
            // 1) Add in the icons
            // 2) Add in text
            this.Rename.Header = this.NameBox;
            this.Remove.Header = "Remove";
            this.Copy.Header = "Copy";
            this.Cut.Header = "Cut";
            this.Paste.Header = "Paste";
            this.Help.Header = "Help";

            // This will only be called once so we don't need to worry about caching all of these.
            this.Rename.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/base_cog_32.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Remove.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/delete.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Copy.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/CopyHS.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Cut.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/CutHS.png" ) )
                ,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Paste.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/PasteHS.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Help.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/Help.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };

            // 3) Add event handlers for all of the options
            this.Rename.Click += new RoutedEventHandler( Rename_Click );
            this.Remove.Click += new RoutedEventHandler( Remove_Click );
            this.Copy.Click += new RoutedEventHandler( Copy_Click );
            this.Cut.Click += new RoutedEventHandler( Cut_Click );
            this.Paste.Click += new RoutedEventHandler( Paste_Click );
            this.Help.Click += new RoutedEventHandler( Documenation_Click );

            // Tool tips
            this.Rename.ToolTip = "Rename the selected item";
            this.Remove.ToolTip = "Remove the selected item";
            this.Copy.ToolTip = "Make a copy of the selected item and its descendants";
            this.Cut.ToolTip = "Make a copy and dispose of the selected item.";
            this.Paste.ToolTip = "Insert the copied item";
            this.Help.ToolTip = "View the documentation for this module.";

            // Add in all of the menu items now that have have all been setup
            this.Items.Add( Rename );
            this.Items.Add( Remove );
            this.Items.Add( Copy );
            this.Items.Add( Cut );
            this.Items.Add( Paste );
            this.Items.Add( Help );
            // Now that everything has been created update the layout
            this.UpdateLayout();
        }

        private bool IsAssignable(IModelSystemStructure rootStructure, IModelSystemStructure parentStructure, IModelSystemStructure selectedElement)
        {
            // This will update what module we are using for the root as per the Re-rootable extension for XTMF
            try
            {
                var parent = parentStructure == null ? typeof( IModelSystemTemplate ) : parentStructure.Type;
                if ( this.CopyBuffer.IsCollection )
                {
                    // Make sure that we are doing collection to collection and that they are of the right types
                    if ( !selectedElement.IsCollection || !selectedElement.ParentFieldType.IsAssignableFrom( this.CopyBuffer.ParentFieldType ) )
                    {
                        return false;
                    }
                    // now make sure that every new element is alright with the parent and root
                    var parentType = selectedElement.ParentFieldType;
                    var arguements = parentType.IsArray ? parentType.GetElementType() : parentType.GetGenericArguments()[0];
                    foreach ( var member in this.CopyBuffer.Children )
                    {
                        var t = member.Type;
                        if ( arguements.IsAssignableFrom( t ) && ( parent == null || ModelSystemStructure.CheckForParent( parent, t ) ) && ModelSystemStructure.CheckForRootModule( rootStructure, selectedElement, t ) != null )
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    var t = this.CopyBuffer.Type;
                    rootStructure = ModelSystemStructure.CheckForRootModule( rootStructure, selectedElement, t );
                    if ( selectedElement.IsCollection )
                    {
                        var parentType = selectedElement.ParentFieldType;

                        var arguements = parentType.IsArray ? parentType.GetElementType() : parentType.GetGenericArguments()[0];
                        if ( arguements.IsAssignableFrom( t ) && ( ModelSystemStructure.CheckForParent( parent, t ) ) && ModelSystemStructure.CheckForRootModule( rootStructure, selectedElement, t ) != null )
                        {
                            return true;
                        }

                    }
                    else
                    {
                        if ( selectedElement.ParentFieldType.IsAssignableFrom( t ) && ( parent == null || ModelSystemStructure.CheckForParent( parent, t ) ) && ModelSystemStructure.CheckForRootModule( rootStructure, selectedElement, t ) != null )
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if ( this.CopyBuffer != null )
            {
                if ( this.InsertRequested != null )
                {
                    this.InsertRequested( this.CopyBuffer );
                }
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            this.ExitedOnClick = true;
            if ( this.RemovePressed != null )
            {
                this.RemovePressed();
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if ( this.RenamePressed != null & this.WasTextAltered )
            {
                this.RenamePressed( this.NameBox.Text );
            }
        }
    }
}