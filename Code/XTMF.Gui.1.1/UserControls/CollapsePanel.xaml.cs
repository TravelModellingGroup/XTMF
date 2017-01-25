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
using System.Windows.Media.Animation;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for CollapsePanel.xaml
    /// </summary>
    public partial class CollapsePanel : Expander
    {
        private double _internalHeight;

        private bool _collapsing = false;

        public CollapsePanel()
        {
            InitializeComponent();
        }

        public double InternalHeight
        {
            get
            {
                return this._internalHeight;
            }

            set
            {
                this.InnerContentContainer.Height = this._internalHeight = value;
            }
        }

        public void Add(UIElement element)
        {
            lock ( this )
            {
                this.InnerContents.Children.Add( element );
            }
        }

        public void Remove(UIElement element)
        {
            lock ( this )
            {
                this.InnerContents.Children.Remove( element );
            }
        }

        protected override void OnCollapsed()
        {
            if ( !this._collapsing )
            {
                this._collapsing = true;
                this.IsExpanded = true;
                double startingHeight = this._internalHeight;
                var hide = new DoubleAnimation( 0, new Duration( TimeSpan.FromMilliseconds( 150 ) ) );
                hide.Completed += new EventHandler( delegate(object o, EventArgs e)
                    {
                        var shrink = new DoubleAnimation( 0, new Duration( TimeSpan.FromMilliseconds( 225 ) ) );
                        shrink.RemoveRequested += new EventHandler( AnimationStopped );
                        shrink.Completed += new EventHandler( AnimationStopped );
                        this.InnerContentContainer.BeginAnimation( ScrollViewer.HeightProperty, shrink );
                    } );
                hide.RemoveRequested += new EventHandler( AnimationStopped );
                this.InnerContentContainer.BeginAnimation( ScrollViewer.OpacityProperty, hide );
            }
        }

        protected override void OnExpanded()
        {
            if ( !this._collapsing )
            {
                this.InnerContentContainer.Opacity = 0;
                DoubleAnimation expand = new DoubleAnimation( 0, this._internalHeight, new Duration( TimeSpan.FromMilliseconds( 225 ) ) );
                expand.Completed += new EventHandler( delegate(object o, EventArgs e)
                    {
                        DoubleAnimation show = new DoubleAnimation( 1, new Duration( TimeSpan.FromMilliseconds( 150 ) ) );
                        this.InnerContentContainer.BeginAnimation( ScrollViewer.OpacityProperty, show );
                    } );
                this.InnerContentContainer.BeginAnimation( ScrollViewer.HeightProperty, expand );
                base.OnExpanded();
            }
        }

        private void AnimationStopped(object sending, EventArgs e)
        {
            this.IsExpanded = false;
            this._collapsing = false;
            this.InnerContentContainer.Opacity = 1;
            base.OnCollapsed();
        }
    }
}