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
using System.Windows.Controls;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for PathLabel.xaml
    /// </summary>
    public partial class PathLabel : UserControl
    {
        public XTMFPage Page;

        public PathLabel(XTMFPage page)
        {
            this.Page = page;
            InitializeComponent();
        }

        public event Action<object> Clicked;

        public object Text
        {
            get
            {
                return this.PathText.Content;
            }

            set
            {
                this.PathText.Content = value;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if ( this.IsEnabled && !e.Handled )
            {
                if ( e.ClickCount == 1 )
                {
                    e.Handled = true;
                    if ( this.Clicked != null )
                    {
                        this.Clicked( this );
                    }
                }
            }
            base.OnMouseLeftButtonUp( e );
        }
    }
}