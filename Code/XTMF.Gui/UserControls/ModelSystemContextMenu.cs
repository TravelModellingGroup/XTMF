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
using System.Windows.Media.Imaging;

namespace XTMF.Gui.UserControls
{
    public class ModelSystemContextMenu : ContextMenu
    {
        public static readonly DependencyProperty AllowRemoveProperty = DependencyProperty.Register( "AllowRemove", typeof( bool )
            , typeof( ModelSystemContextMenu ), new FrameworkPropertyMetadata( true, FrameworkPropertyMetadataOptions.AffectsRender, OnAllowRemoveChanged ) );

        bool NewRename = false;
        public UIElement SelectedElement;
        protected MenuItem Remove;
        protected MenuItem Rename;
        protected MenuItem SaveAsModelSystem;
        protected HintedTextBox NameBox = new HintedTextBox() { HintText = "Type Name", MinWidth = 250 };

        public ModelSystemContextMenu(bool saveAsMS = false)
        {
            InitializeItems();
            this.SaveAsModelSystem.Visibility = saveAsMS ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            this.NameBox.PreviewLostKeyboardFocus += OnPreviewLostKeyboardFocus;
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

        public event Action RemovePressed;

        public event Action<string> RenamePressed;

        public event Action SaveAsModelSystemPressed;

        public bool AllowRemove
        {
            get { return (bool)GetValue( AllowRemoveProperty ); }
            set { SetValue( AllowRemoveProperty, value ); }
        }

        private static void OnAllowRemoveChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ModelSystemContextMenu;
            bool allowRemove = (bool)e.NewValue;
            us.Remove.IsEnabled = allowRemove;
        }

        public void SetCurrentName(string name)
        {
            if ( !this.NewRename )
            {
                this.NewRename = true;
                this.SwitchRename();
            }
            this.NameBox.Text = name;
        }

        private void ExecuteEvent(Action toExecute)
        {
            var delegates = toExecute;
            if ( delegates != null )
            {
                delegates();
            }
        }

        private void SwitchRename()
        {
            this.Rename.Header = NameBox;
            this.Rename.LostFocus += new RoutedEventHandler( Rename_Click );
            this.Unloaded += ModelSystemContextMenu_Unloaded;
        }

        private void InitializeItems()
        {
            // Create all of the MenuItem objects to begin with
            Rename = new MenuItem();
            Remove = new MenuItem();
            SaveAsModelSystem = new MenuItem();

            // Now that we have the objects we should go and add the information to all of the menu items
            // 1) Add in the icons
            // 2) Add in text
            this.Rename.Header = "Rename";
            this.Remove.Header = "Remove";
            this.SaveAsModelSystem.Header = "Save As Model System";

            // This will only be called once so we don't need to worry about caching all of these.          
            this.Remove.Icon = new Image() { Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/delete.png" ) ), Width = 16, Height = 16 };
            this.SaveAsModelSystem.Icon = new Image() { Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/saveHS.png" ) ), Width = 16, Height = 16 };
            this.Rename.Icon = new Image() { Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/base_cog_32.png" ) ), Width = 16, Height = 16 };
            // 3) Add event handellers for all of the options
            this.Rename.Click += Rename_Click;
            this.Remove.Click += new RoutedEventHandler( Remove_Click );
            this.SaveAsModelSystem.Click += new RoutedEventHandler( SaveAsModelSystem_Click );

            // Tool tips
            this.Rename.ToolTip = "Rename the selected item";
            this.Remove.ToolTip = "Remove the selected item";
            this.SaveAsModelSystem.ToolTip = "Save this contained model system as a model system.";

            // Add in all of the menu items now that have have all been setup
            this.Items.Add( Rename );
            this.Items.Add( Remove );
            this.Items.Add( this.SaveAsModelSystem );
            // Now that everything has been created update the layout
            this.UpdateLayout();
        }

        void ModelSystemContextMenu_Unloaded(object sender, RoutedEventArgs e)
        {
            this.Rename_Click( this, e );
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEvent( this.RemovePressed );
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.RenamePressed;
            if ( ev != null )
            {
                ev( this.NewRename ? this.NameBox.Text : null );
            }
        }

        private void SaveAsModelSystem_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEvent( this.SaveAsModelSystemPressed );
        }
    }
}