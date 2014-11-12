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
using XTMF.Gui.UserControls;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for LinkedParameterEditor.xaml
    /// </summary>
    public partial class LinkedParameterEditor : UserControl
    {
        public static readonly DependencyProperty EditModeProperty = DependencyProperty.Register( "EditMode", typeof( bool )
            , typeof( LinkedParameterEditor ), new FrameworkPropertyMetadata( true, FrameworkPropertyMetadataOptions.AffectsRender, OnEditModeChanged ) );

        public static readonly DependencyProperty LinkedParameterProperty = DependencyProperty.Register( "LinkedParameters", typeof( List<ILinkedParameter> )
            , typeof( LinkedParameterEditor ), new PropertyMetadata( OnLinkedParametersChanged ) );

        public static readonly DependencyProperty SelectedLinkedParameterProperty = DependencyProperty.Register( "SelectedLinkedParameter", typeof( ILinkedParameter )
            , typeof( LinkedParameterEditor ), new PropertyMetadata( OnSelectedLinkedParameterChanged ) );

        /// <summary>
        /// The context menu that is attached to the LP Selector control
        /// </summary>
        private ModelSystemContextMenu LPContextMenu;

        public LinkedParameterEditor()
        {
            this.DataContext = this;
            InitializeComponent();
            LPContextMenu = new ModelSystemContextMenu();
            LPContextMenu.RenamePressed += new Action<string>( LPContextMenu_RenamePressed );
            LPContextMenu.RemovePressed += new Action( LPContextMenu_RemovePressed );
        }

        /// <summary>
        /// Gets notified when the user requests that a new linked parameter be added.
        /// </summary>
        public event Action<string> NewLinkedParameterRequested;

        /// <summary>
        /// Gets notified when the user requests that the given linked parameter be removed
        /// </summary>
        public event Action<ILinkedParameter> RemoveLinkedParameterRequested;

        /// <summary>
        /// Gets notified when the user requests that the given linked parameter should be renamed
        /// </summary>
        public event Action<ILinkedParameter, string> RenameLinkedParameterRequested;

        public bool EditMode
        {
            get { return (bool)GetValue( EditModeProperty ); }
            set { SetValue( EditModeProperty, value ); }
        }

        public List<ILinkedParameter> LinkedParameters
        {
            get { return (List<ILinkedParameter>)GetValue( LinkedParameterProperty ); }
            set { SetValue( LinkedParameterProperty, value ); }
        }

        public Func<ILinkedParameter, IModuleParameter, bool> RemoveLinkedParameterParameter
        {
            get { return this.LinkedParameterDisplay.RemoveLinkedParameterParameter; }
            set { this.LinkedParameterDisplay.RemoveLinkedParameterParameter = value; }
        }

        public ILinkedParameter SelectedLinkedParameter
        {
            get { return (ILinkedParameter)GetValue( SelectedLinkedParameterProperty ); }
            set { SetValue( SelectedLinkedParameterProperty, value ); }
        }

        public Func<ILinkedParameter, string, bool> SetLinkedParameterValue
        {
            get { return this.LinkedParameterDisplay.SetLinkedParameterValue; }
            set { this.LinkedParameterDisplay.SetLinkedParameterValue = value; }
        }

        internal void Refresh()
        {
            this.LinkedParameterDisplay.Refresh();
        }

        private static void OnEditModeChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as LinkedParameterEditor;
            bool editMode = (bool)e.NewValue;
            us.NewLPButton.IsEnabled = editMode;
            us.RemoveLPButton.IsEnabled = editMode;
            us.LinkedParameterDisplay.EditMode = editMode;
            us.LPContextMenu.AllowRemove = editMode;
        }

        private static void OnLinkedParametersChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as LinkedParameterEditor;
            var linkedParameters = e.NewValue as List<ILinkedParameter>;
            us.LoadLinkedParameters( linkedParameters );
            us.SelectedLinkedParameter = null;
        }

        private static void OnSelectedLinkedParameterChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as LinkedParameterEditor;
            var newLP = e.NewValue as ILinkedParameter;
            us.RemoveLPButton.IsEnabled = us.EditMode & ( newLP != null );
            us.LinkedParameterDisplay.LinkedParameter = newLP;
            if ( newLP != null )
            {
                us.LPContextMenu.SetCurrentName( newLP.Name );
            }
        }

        private void Invoke(Action<ILinkedParameter> invokeMe)
        {
            if ( invokeMe != null )
            {
                invokeMe( this.SelectedLinkedParameter );
            }
        }

        private void Invoke(Action<ILinkedParameter, string> invokeMe, string param)
        {
            if ( ( invokeMe != null ) & ( this.SelectedLinkedParameter != null ) )
            {
                invokeMe( this.SelectedLinkedParameter, param );
            }
        }

        private void LinkedParameterSelector_ItemFocused(object linkedParameter)
        {
            this.SelectedLinkedParameter = linkedParameter as ILinkedParameter;
        }

        private void LoadLinkedParameters(List<ILinkedParameter> linkedParameters)
        {
            this.LinkedParameterSelector.Clear();
            if ( linkedParameters == null )
            {
                return;
            }
            foreach ( var lp in linkedParameters )
            {
                this.LinkedParameterSelector.Add( lp.Name, String.Empty, lp, LPContextMenu );
            }
        }

        private void LPContextMenu_RemovePressed()
        {
            Invoke( this.RemoveLinkedParameterRequested );
        }

        private void LPContextMenu_RenamePressed(string name)
        {
            Invoke( this.RenameLinkedParameterRequested, name );
        }

        private void NewLPButton_Clicked(object obj)
        {
            var ev = this.NewLinkedParameterRequested;
            if ( ev != null )
            {
                ev( "New Linked Parameter" );
            }
        }

        private void RemoveLPButton_Clicked(object obj)
        {
            Invoke( this.RemoveLinkedParameterRequested );
        }
    }
}