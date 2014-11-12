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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XTMF.Gui
{
    public class ModelParameterProxy : INotifyPropertyChanged
    {
        public Type Type;

        internal IModuleParameter RealParameter;

        public ModelParameterProxy(IModuleParameter realParam, List<ILinkedParameter> linkedParameters)
        {
            this.RealParameter = realParam;
            if ( realParam.BelongsTo != null )
            {
                this.ModuleName = realParam.BelongsTo.Name;
            }
            else
            {
                this.ModuleName = null;
            }
            this.Name = realParam.Name;
            if ( realParam.Value == null )
            {
                FixType( realParam );
            }
            else
            {
                this.Type = realParam.Type;
            }
            this.Description = realParam.Description;
            this.Quick = this.TempQuick = realParam.QuickParameter;
            this.ToolTipText = this.Name + " : " + this.ModuleName;
            if ( this.Type == typeof( DateTime ) )
            {
                DateTime v = (DateTime)realParam.Value;
                this.Value = v.ToShortTimeString();
            }
            else
            {
                this.Value = realParam.Value.ToString();
            }
            if ( linkedParameters != null )
            {
                bool found = false;
                // check to see if it is in a linked parameter
                for ( int i = 0; i < linkedParameters.Count; i++ )
                {
                    if ( linkedParameters[i] != null )
                    {
                        for ( int j = 0; j < linkedParameters[i].Parameters.Count; j++ )
                        {
                            if ( linkedParameters[i].Parameters[j] == realParam )
                            {
                                this.LinkedParameter = linkedParameters[i].Name;
                                found = true;
                                break;
                            }
                        }
                        if ( found )
                        {
                            break;
                        }
                    }
                }
            }
            this.TempValue = this.Value;
        }

        public string Description { get; private set; }

        public bool IsInLinkedParameter
        {
            get
            {
                // whitespace/empty is fine
                return this.LinkedParameter != null;
            }
        }

        public string LinkedParameter { get; set; }

        public Visibility LinkedParameterVisibility
        {
            get
            {
                return this.IsInLinkedParameter ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public string ModuleName { get; private set; }

        public string Name { get; private set; }

        public bool Quick { get; set; }

        public Visibility SystemParameterVisibility
        {
            get
            {
                return this.RealParameter.SystemParameter ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool TempQuick { get; set; }

        private string _TempValue;
        public string TempValue
        {
            get
            {
                return _TempValue;
            }
            set
            {
                this._TempValue = value;
                var e = this.PropertyChanged;
                if ( e != null )
                {
                    e( this, new PropertyChangedEventArgs( "TempValue" ) );
                }
            }
        }

        public string ToolTipText { get; private set; }

        private string _Value;
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                this._Value = value;
                var e = this.PropertyChanged;
                if ( e != null )
                {
                    e( this, new PropertyChangedEventArgs( "Value" ) );
                }
            }
        }

        public static bool operator !=(IModuleParameter realParam, ModelParameterProxy us)
        {
            return ( realParam != us.RealParameter );
        }

        public static bool operator ==(IModuleParameter realParam, ModelParameterProxy us)
        {
            return ( realParam == us.RealParameter );
        }

        public override bool Equals(object obj)
        {
            var other = obj as IModuleParameter;
            if ( other != null )
            {
                return ( other == this.RealParameter );
            }
            return base.Equals( obj );
        }

        public override int GetHashCode()
        {
            return this.RealParameter.GetHashCode();
        }

        internal bool Validate(string newData)
        {
            string error = null;
            var res = ArbitraryParameterParser.ArbitraryParameterParse( this.Type, newData, ref error );
            if ( res == null )
            {
                MessageBox.Show( error, "Invalid Data", MessageBoxButton.OK, MessageBoxImage.Error );
                return false;
            }
            return true;
        }

        private void FixType(IModuleParameter realParam)
        {
            var moduleType = realParam.BelongsTo.Type;
            if ( SearchFields( moduleType, realParam.Name ) )
            {
                return;
            }
            SearchProperties( moduleType, realParam.Name );
        }

        private bool SearchFields(System.Type moduleType, string name)
        {
            var fields = moduleType.GetFields();
            foreach ( var field in fields )
            {
                var at = field.GetCustomAttributes( typeof( ParameterAttribute ), true );
                if ( at != null )
                {
                    foreach ( var parameter in at )
                    {
                        var p = parameter as ParameterAttribute;
                        if ( p != null )
                        {
                            if ( p.Name == name )
                            {
                                this.Type = field.FieldType;
                                this.RealParameter.Value = String.Empty;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool SearchProperties(System.Type moduleType, string name)
        {
            var fields = moduleType.GetProperties();
            foreach ( var field in fields )
            {
                var at = field.GetCustomAttributes( typeof( ParameterAttribute ), true );
                if ( at != null )
                {
                    foreach ( var parameter in at )
                    {
                        var p = parameter as ParameterAttribute;
                        if ( p != null )
                        {
                            if ( p.Name == name )
                            {
                                this.Type = field.PropertyType;
                                this.Value = p.DefaultValue.ToString();
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return this.Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Interaction logic for ParameterEditor.xaml
    /// </summary>
    public partial class ParameterEditor : UserControl
    {
        public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register( "IsEditing", typeof( bool )
            , typeof( ParameterEditor ), new FrameworkPropertyMetadata( false, FrameworkPropertyMetadataOptions.AffectsRender, OnEditngChanged ) );

        public static readonly DependencyProperty LinkedParametersProperty = DependencyProperty.Register( "LinkedParameters", typeof( List<ILinkedParameter> )
            , typeof( ParameterEditor ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnLinkedParametersChanged ) );

        public static readonly DependencyProperty ModelSystemStructureProperty = DependencyProperty.Register( "ModelSystemStructure", typeof( IModelSystemStructure )
            , typeof( ParameterEditor ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnModuleChanged ) );

        public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register( "Parameters", typeof( IList<IModuleParameter> )
            , typeof( ParameterEditor ), new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsRender, OnParametersChanged ) );

        public static readonly DependencyProperty ProxyParametersProperty = DependencyProperty.Register( "ProxyParameters", typeof( BindingList<ModelParameterProxy> )
            , typeof( ParameterEditor ) );

        private bool ModuleChangedLast;

        public event Action<IModuleParameter> OpenFileRequested;
        public event Action<IModuleParameter> OpenFileWithRequested;
        public event Action<IModuleParameter> OpenFileLocationRequested;
        public event Action<IModuleParameter> SelectFileRequested;
        public event Action<ILinkedParameter, IModuleParameter> AddToLinkedParameterRequested;

        public ParameterEditor()
        {
            this.DataContext = this;
            InitializeComponent();
            this.LPContextMenu.OpenFileRequested += LPContextMenu_OpenFileRequested;
            this.LPContextMenu.OpenFileWithRequested += LPContextMenu_OpenFileWithRequested;
            this.LPContextMenu.OpenFileLocationRequested += ParameterEditor_OpenFileLocationRequested;
            this.LPContextMenu.SelectFileRequested += ParameterEditor_SelectFileRequested;
            this.LPContextMenu.CopyParameterName += CopyButton_Click;
            this.LPContextMenu.AddToLinkedParameterRequested += LPContextMenu_AddToLinkedParameterRequested;
            this.LPContextMenu.GetLinkedParameters = () => this.LinkedParameters;
        }

        void LPContextMenu_AddToLinkedParameterRequested(ILinkedParameter linkedParameter, object contextMenu)
        {
            var e = this.AddToLinkedParameterRequested;
            if ( e != null )
            {
                e( linkedParameter, this.SelectedItem.RealParameter );
            }
        }

        void LPContextMenu_OpenFileRequested(object obj)
        {
            this.Run( this.OpenFileRequested );
        }

        void LPContextMenu_OpenFileWithRequested(object obj)
        {
            this.Run( this.OpenFileWithRequested );
        }

        void ParameterEditor_OpenFileLocationRequested(object obj)
        {
            this.Run( this.OpenFileLocationRequested );
        }

        void ParameterEditor_SelectFileRequested(object obj)
        {
            this.Run( this.SelectFileRequested );
        }

        private void Run(Action<IModuleParameter> runMe)
        {
            var e = runMe;
            if ( e != null )
            {
                var selectedParameter = GetSelectedItem();
                if ( selectedParameter != null )
                {
                    e( selectedParameter.RealParameter );
                }
            }
        }

        private void CopyButton_Click(object sender)
        {
            var selectedParameter = GetSelectedItem();
            if ( selectedParameter != null )
            {
                Clipboard.SetText( selectedParameter.Name );
            }
        }

        private ModelParameterProxy SelectedItem;

        private ModelParameterProxy GetSelectedItem()
        {
            return this.SelectedItem;
        }

        private void UpdateSelectedItem()
        {
            var numberOfItems = this.ParameterDisplay.Items.Count;
            for ( int i = 0; i < numberOfItems; i++ )
            {
                var element = this.ParameterDisplay.ItemContainerGenerator.ContainerFromIndex( i ) as FrameworkElement;
                var relativePos = Mouse.GetPosition( element );
                if ( element.InputHitTest( relativePos ) != null )
                {
                    this.SelectedItem = ( this.ParameterDisplay.ItemContainerGenerator.ContainerFromIndex( i ) as FrameworkElement ).DataContext as ModelParameterProxy;
                    return;
                }
            }
            var frameworkElement = this.ParameterDisplay.ItemContainerGenerator.ContainerFromIndex( numberOfItems ) as FrameworkElement;
            if ( frameworkElement != null )
            {
                this.SelectedItem = frameworkElement.DataContext as ModelParameterProxy;
            }
        }

        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            this.UpdateSelectedItem();
            base.OnContextMenuOpening( e );
        }

        public event Action ParameterChanged;

        public Action<IModuleParameter, string> ControlWrite { get; set; }

        public bool IsEditing
        {
            get
            {
                return (bool)this.GetValue( IsEditingProperty );
            }

            set
            {
                this.SetValue( IsEditingProperty, value );
            }
        }

        public List<ILinkedParameter> LinkedParameters
        {
            get
            {
                return this.GetValue( LinkedParametersProperty ) as List<ILinkedParameter>;
            }
            set
            {
                this.SetValue( LinkedParametersProperty, value );
            }
        }

        public IModelSystemStructure ModelSystemStructure
        {
            get
            {
                return this.GetValue( ModelSystemStructureProperty ) as IModelSystemStructure;
            }
            set
            {
                this.SetValue( ModelSystemStructureProperty, value );
            }
        }

        public IList<IModuleParameter> Parameters
        {
            get
            {
                return this.GetValue( ParametersProperty ) as List<IModuleParameter>;
            }
            set
            {
                this.SetValue( ParametersProperty, value );
            }
        }

        public BindingList<ModelParameterProxy> ProxyParameters
        {
            get
            {
                return this.GetValue( ProxyParametersProperty ) as BindingList<ModelParameterProxy>;
            }
            private set
            {
                this.SetValue( ProxyParametersProperty, value );
            }
        }

        public void RefreshParameters()
        {
            var parameters = this.GetParameters( this.ModelSystemStructure );
            this.SortParameters( parameters );
            this.ProxyParameters = parameters;
        }

        public void Validate(TextBox textBox)
        {
            var proxies = this.ProxyParameters;
            if ( proxies == null ) return;
            for ( int i = 0; i < proxies.Count; i++ )
            {
                if ( proxies[i].Value != proxies[i].TempValue | proxies[i].Quick != proxies[i].TempQuick )
                {
                    var fixedValue = proxies[i].TempValue.Replace( "\b", "" ).Replace( "\0", "" );
                    proxies[i].TempValue = fixedValue;
                    if ( proxies[i].Validate( fixedValue ) )
                    {
                        this.ControlWrite( proxies[i].RealParameter, fixedValue );
                        var e = this.ParameterChanged;
                        if ( e != null )
                        {
                            e();
                        }
                        if ( proxies[i].IsInLinkedParameter )
                        {
                            for ( int j = 0; j < proxies.Count; j++ )
                            {
                                proxies[j].Value = proxies[j].TempValue = proxies[j].RealParameter.Value.ToString();
                            }
                        }
                    }
                    else
                    {
                        textBox.Text = proxies[i].TempValue = proxies[i].Value;
                    }
                }
            }
        }

        public void ValidateQuickParamters()
        {
            bool any = false;
            if ( this.ProxyParameters == null ) return;
            for ( int i = 0; i < this.ProxyParameters.Count; i++ )
            {
                if ( this.ProxyParameters[i].Quick != this.ProxyParameters[i].TempQuick )
                {
                    this.ProxyParameters[i].RealParameter.QuickParameter = this.ProxyParameters[i].Quick = this.ProxyParameters[i].TempQuick;
                    any = true;
                }
            }
            if ( any )
            {
                var e = this.ParameterChanged;
                if ( e != null )
                {
                    e();
                }
            }
        }

        private static void OnModuleChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ParameterEditor;
            us.ModuleChangedLast = true;
            us.RefreshParameters();
            us.Dispatcher.BeginInvoke( new Action( delegate()
                {
                    DoubleAnimation animation = new DoubleAnimation( 0, 1, new Duration( new TimeSpan( 0, 0, 0, 0, 150 ) ) );
                    us.BeginAnimation( ParameterEditor.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace );
                } ), System.Windows.Threading.DispatcherPriority.Render );
        }

        private static void OnParametersChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var us = source as ParameterEditor;
            us.ModuleChangedLast = false;
            us.RefreshParameters();
        }

        private static void OnEditngChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            // do nothing for now
            var us = source as ParameterEditor;
            if ( us.LPContextMenu != null )
            {
                us.LPContextMenu.EditMode = (bool)e.NewValue;
            }
        }

        private static void OnLinkedParametersChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            // do nothing for now
        }


        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            this.ValidateQuickParamters();
        }

        private BindingList<ModelParameterProxy> GetParameters(IModelSystemStructure mss)
        {
            BindingList<ModelParameterProxy> ret = new BindingList<ModelParameterProxy>();
            if ( this.ModuleChangedLast )
            {
                if ( mss == null || mss.IsCollection || mss.Parameters == null )
                {
                    return ret;
                }
                var parameters = mss.Parameters.Parameters;
                for ( int i = 0; i < parameters.Count; i++ )
                {
                    if ( this.IsEditing | !parameters[i].SystemParameter )
                    {
                        ret.Add( new ModelParameterProxy( parameters[i], this.LinkedParameters ) );
                    }
                }
            }
            else
            {
                var parameters = this.Parameters;
                if ( parameters == null )
                {
                    return ret;
                }
                for ( int i = 0; i < parameters.Count; i++ )
                {
                    ret.Add( new ModelParameterProxy( parameters[i], this.LinkedParameters ) );
                }
            }
            return ret;
        }

        private void SortParameters(BindingList<ModelParameterProxy> param)
        {
            for ( int i = 0; i < param.Count; i++ )
            {
                for ( int j = 0; j < param.Count - i - 1; j++ )
                {
                    if ( param[j].Name.CompareTo( param[j + 1].Name ) > 0 )
                    {
                        var temp = param[j];
                        param[j] = param[j + 1];
                        param[j + 1] = temp;
                    }
                }
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
    }
}