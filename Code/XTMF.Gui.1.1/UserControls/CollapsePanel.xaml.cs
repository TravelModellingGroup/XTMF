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

        private bool _collapsing;

        public CollapsePanel()
        {
            InitializeComponent();
        }

        public double InternalHeight
        {
            get
            {
                return _internalHeight;
            }

            set
            {
                InnerContentContainer.Height = _internalHeight = value;
            }
        }

        public void Add(UIElement element)
        {
            lock ( this )
            {
                InnerContents.Children.Add( element );
            }
        }

        public void Remove(UIElement element)
        {
            lock ( this )
            {
                InnerContents.Children.Remove( element );
            }
        }

        protected override void OnCollapsed()
        {
            if ( !_collapsing )
            {
                _collapsing = true;
                IsExpanded = true;
                double startingHeight = _internalHeight;
                var hide = new DoubleAnimation( 0, new Duration( TimeSpan.FromMilliseconds( 150 ) ) );
                hide.Completed += delegate
                {
                    var shrink = new DoubleAnimation( 0, new Duration( TimeSpan.FromMilliseconds( 225 ) ) );
                    shrink.RemoveRequested += AnimationStopped;
                    shrink.Completed += AnimationStopped;
                    InnerContentContainer.BeginAnimation( HeightProperty, shrink );
                };
                hide.RemoveRequested += AnimationStopped;
                InnerContentContainer.BeginAnimation( OpacityProperty, hide );
            }
        }

        protected override void OnExpanded()
        {
            if ( !_collapsing )
            {
                InnerContentContainer.Opacity = 0;
                DoubleAnimation expand = new DoubleAnimation( 0, _internalHeight, new Duration( TimeSpan.FromMilliseconds( 225 ) ) );
                expand.Completed += delegate
                {
                    DoubleAnimation show = new DoubleAnimation( 1, new Duration( TimeSpan.FromMilliseconds( 150 ) ) );
                    InnerContentContainer.BeginAnimation( OpacityProperty, show );
                };
                InnerContentContainer.BeginAnimation( HeightProperty, expand );
                base.OnExpanded();
            }
        }

        private void AnimationStopped(object sending, EventArgs e)
        {
            IsExpanded = false;
            _collapsing = false;
            InnerContentContainer.Opacity = 1;
            base.OnCollapsed();
        }
    }
}