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
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTypeSelect.xaml
    /// </summary>
    public partial class ModuleTypeSelect : Window
    {
        private ModelSystemStructureModel SelectedModule;
        private ModelSystemEditingSession ModelSystemSession;

        public class Model : INotifyPropertyChanged
        {
            internal Type type;

            public Model(Type type)
            {
                this.type = type;
            }

            public string Name { get { return type.Name; } }

            public string Text { get { return type.FullName; } }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ModuleTypeSelect()
        {
            InitializeComponent();
        }

        private bool CheckAgainstFilter(object o, string text)
        {
            var model = o as Model;
            if ( string.IsNullOrWhiteSpace( text ) ) return true;
            if ( model == null ) return false;
            return model.Name.IndexOf( text, StringComparison.CurrentCultureIgnoreCase ) >= 0 || model.Text.IndexOf( text, StringComparison.CurrentCultureIgnoreCase ) >= 0;
        }

        public ModuleTypeSelect(ModelSystemEditingSession session, ModelSystemStructureModel selectedModule)
            : this()
        {
            ModelSystemSession = session;
            SelectedModule = selectedModule;
            BuildRequirements( session );
            FilterBox.Filter = CheckAgainstFilter;
            FilterBox.Display = Display;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated( e );
            FilterBox.Focus();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus( e );
            if ( e.OriginalSource == this )
            {
                FilterBox.Focus();
            }
        }

        private List<Model> AvailableModules;

        public Type SelectedType { get; private set; }


        /// <summary>
        /// Figure out what types we are going to be restricted by
        /// </summary>
        private void BuildRequirements(ModelSystemEditingSession session)
        {
            List<Type> available = session.GetValidModules( SelectedModule );
            Display.ItemsSource = AvailableModules = Convert( available );
        }

        private List<Model> Convert(List<Type> before)
        {
            var ret = before.Select( o => new Model( o ) ).ToList();
            ret.Sort( (a, b) => a.Name.CompareTo( b.Name ) );
            return ret;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp( e );
            if ( e.Handled == false )
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
                else if(e.Key == Key.Enter)
                {
                    e.Handled = true;
                    Select();
                }
            }
        }

        private void BorderIconButton_Clicked(object obj)
        {
            Select();
        }

        private void Select()
        {
            var index = Display.SelectedItem;
            if (index == null) return;
            SelectedType = (index as Model).type;
            DialogResult = true;
            Close();
        }
    }
}
