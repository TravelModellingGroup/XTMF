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
using System.Windows.Media.Imaging;

namespace XTMF.Gui.UserControls
{
    public class LinkedParameterContextMenu : ContextMenu
    {
        private bool _EditMode;
        private MenuItem AddTo;
        private MenuItem CopyButton;
        private MenuItem FileSelectorButton;
        private MenuItem FileOpenButton;
        private MenuItem FileOpenWithButton;
        private MenuItem FileOpenLocationButton;
        public Func<List<ILinkedParameter>> GetLinkedParameters { get; set; }
        private List<ILinkedParameter> LinkedParameters;

        public LinkedParameterContextMenu()
        {
            this.EditMode = false;
            AddCopy();
            AddFileButtons();
            AddAddTo();
        }

        public LinkedParameterContextMenu(Func<List<ILinkedParameter>> getLinkedParameters)
            : this()
        {
            this.GetLinkedParameters = getLinkedParameters;
        }

        public event Action<ILinkedParameter, object> AddToLinkedParameterRequested;

        public event Action<object> OpenFileRequested;
        public event Action<object> OpenFileWithRequested;
        public event Action<object> OpenFileLocationRequested;
        public event Action<object> SelectFileRequested;
        public event Action<object> CopyParameterName;

        public bool EditMode
        {
            get
            {
                return _EditMode;
            }

            set
            {
                // make sure there was actually a change
                if ( this.EditMode != value )
                {
                    // then we need to actually process it
                    this._EditMode = value;
                    if ( this.EditMode )
                    {
                        this.AddTo.Visibility = System.Windows.Visibility.Visible;
                        AddLinkedParameters();
                    }
                    else
                    {
                        this.AddTo.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }
            }
        }

        protected override void OnOpened(RoutedEventArgs e)
        {
            if ( this.EditMode )
            {
                this.AddTo.Items.Clear();
                AddLinkedParameters();
            }
            base.OnOpened( e );
        }

        private void AddAddTo()
        {
            AddTo = new MenuItem();
            AddTo.Header = "Add to Linked Parameter";
            AddTo.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/Plus.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.Items.Add( AddTo );
        }

        private void AddCopy()
        {
            this.CopyButton = new MenuItem();
            this.CopyButton.Header = "Copy";
            this.CopyButton.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/CopyHS.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.CopyButton.Click += new RoutedEventHandler( CopyButton_Click );
            this.Items.Add( this.CopyButton );
        }

        private void AddFileButtons()
        {
            this.Items.Add( new Separator() );

            this.FileOpenButton = new MenuItem();
            this.FileOpenButton.Header = "Open File";
            this.FileOpenButton.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/OpenFile.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.FileOpenButton.Click += new RoutedEventHandler( FileOpenButton_Click );
            this.Items.Add( this.FileOpenButton );

            this.FileOpenWithButton = new MenuItem();
            this.FileOpenWithButton.Header = "Open With";
            this.FileOpenWithButton.Click += new RoutedEventHandler( FileOpenWithButton_Click );
            this.Items.Add( this.FileOpenWithButton );

            this.FileOpenLocationButton = new MenuItem();
            this.FileOpenLocationButton.Header = "Open Location";
            this.FileOpenLocationButton.Click += new RoutedEventHandler( FileOpenFileLocationButton_Click );
            this.Items.Add( this.FileOpenLocationButton );

            this.Items.Add( new Separator() );

            this.FileSelectorButton = new MenuItem();
            this.FileSelectorButton.Header = "Select File";
            this.FileSelectorButton.Icon = new Image()
            {
                Source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Resources/Edit.png" ) ),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Width = 20,
                Height = 20
            };
            this.FileSelectorButton.Click += new RoutedEventHandler( FileSelectorButton_Click );
            this.Items.Add( this.FileSelectorButton );
        }

        private void AddLinkedParameters()
        {
            this.AddTo.Items.Clear();
            var source = new BitmapImage( new Uri( "pack://application:,,,/XTMF.Gui;component/Images/Link.png" ) );
            this.LinkedParameters = this.GetLinkedParameters();
            if ( this.LinkedParameters == null || this.LinkedParameters.Count == 0 )
            {
                this.AddTo.IsEnabled = false;
                return;
            }
            this.AddTo.IsEnabled = true;
            var handler = new RoutedEventHandler( LP_Click );
            foreach ( var lp in this.LinkedParameters )
            {
                var option = new MenuItem();
                option.Icon = new Image() { Source = source, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch, VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Width = 20, Height = 20 };
                option.Header = lp.Name;
                option.Click += handler;
                this.AddTo.Items.Add( option );
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.CopyParameterName;
            if ( ev != null )
            {
                ev( this );
            }
        }

        private void FileOpenButton_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.OpenFileRequested;
            if ( ev != null )
            {
                ev( this );
            }
        }

        private void FileOpenWithButton_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.OpenFileWithRequested;
            if ( ev != null )
            {
                ev( this );
            }
        }

        private void FileOpenFileLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.OpenFileLocationRequested;
            if ( ev != null )
            {
                ev( this );
            }
        }

        private void FileSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            var ev = this.SelectFileRequested;
            if ( ev != null )
            {
                ev( this );
            }
        }

        private void LP_Click(object sender, RoutedEventArgs e)
        {
            var ev = AddToLinkedParameterRequested;
            if ( ev != null )
            {
                ev( this.LinkedParameters[this.AddTo.Items.IndexOf( sender )], this );
            }
        }
    }
}