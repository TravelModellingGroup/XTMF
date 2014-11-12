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
using System.ComponentModel;
using System.Windows.Media;

namespace XTMF.Gui
{
    public class ModelSystemDisplayStructure : INotifyPropertyChanged
    {
        public IModelSystemStructure Structure;
        private ModelSystemDisplayStructure[] _Children;

        private string _Description;

        private bool _IsSelected;

        private string _Name;

        private Type Type;

        public ModelSystemDisplayStructure(IModelSystemStructure mss)
        {
            this.Structure = mss;
            this._Name = mss.Name;
            this._Description = mss.Description;
            this.Type = mss.Type;
            BuildChildren();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ModelSystemDisplayStructure[] Children
        {
            get
            {
                return _Children;
            }
        }

        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                this._Description = value;
                this.Changed( "Description" );
            }
        }

        public bool IsSelected
        {
            get
            {
                return _IsSelected;
            }

            set
            {
                this._IsSelected = value;
                this.Changed( "IsSelected" );
            }
        }

        private bool _IsExpanded;
        public bool IsExpanded
        {
            get
            {
                return _IsExpanded;
            }
            set
            {
                if ( this._IsExpanded != value )
                {
                    this._IsExpanded = value;
                    this.Changed( "IsExpanded" );
                }
            }
        }

        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                this._Name = value;
                this.Changed( "Name" );
            }
        }

        public Color ShadowColour
        {
            get
            {
                if ( this.Structure.IsCollection == true )
                {
                    return Color.FromRgb( 140, 140, 0 );
                }
                else
                {
                    if ( this.Structure.Type != null )
                    {
                        return Color.FromRgb( 0x30, 0x30, 0x30 );
                    }
                    return this.Structure.Required ?
                        Color.FromRgb( 140, 0, 0 ) : Color.FromRgb( 0, 140, 0 );
                }
            }
        }

        private void Changed(string propertyName)
        {
            var e = this.PropertyChanged;
            if ( e != null )
            {
                e( this, new PropertyChangedEventArgs( propertyName ) );
            }
        }

        internal void RefreshAll()
        {
            this.RefreshAllProperties();
            if ( this._Children != null )
            {
                for ( int i = 0; i < this._Children.Length; i++ )
                {
                    _Children[i].RefreshAll();
                }
            }
        }

        internal void RefreshAllProperties()
        {
            this.Name = this.Structure.Name;
            this.Description = this.Structure.Description;
            if ( this.Structure.IsCollection )
            {
                BuildChildren();
                this.Changed( "Children" );
            }
            else
            {
                if ( this.Type != this.Structure.Type )
                {
                    bool typeChanged = this.Type == this.Structure.Type;
                    this.Type = this.Structure.Type;
                    this.Changed( "Type" );
                    this.Changed( "ShadowColour" );
                    BuildChildren();
                    this.Changed( "Children" );
                }
                else
                {
                    if ( BuildChildren() )
                    {
                        this.Changed( "Children" );
                    }
                }
            }
        }

        private bool BuildChildren()
        {
            var anyChanges = false;
            var children = this.Structure.Children;
            if ( children != null )
            {
                var oldChildren = this._Children;
                if ( oldChildren == null || oldChildren.Length != children.Count )
                {
                    var newChildren = new ModelSystemDisplayStructure[children.Count];
                    if ( oldChildren == null )
                    {
                        for ( int i = 0; i < newChildren.Length; i++ )
                        {
                            newChildren[i] = new ModelSystemDisplayStructure( children[i] );
                        }
                        anyChanges = true;
                    }
                    else
                    {
                        int index;
                        for ( int i = 0; i < children.Count; i++ )
                        {
                            var child = children[i];
                            if ( ( index = IndexOf( oldChildren, children[i] ) ) >= 0 )
                            {
                                newChildren[i] = oldChildren[index];
                                if ( index != i )
                                {
                                    anyChanges = true;
                                }
                            }
                            else
                            {
                                newChildren[i] = new ModelSystemDisplayStructure( children[i] );
                                anyChanges = true;
                            }
                        }
                    }
                    this._Children = newChildren;
                }
                else
                {
                    int index;
                    for ( int i = 0; i < children.Count; i++ )
                    {
                        var child = children[i];
                        index = IndexOf( oldChildren, children[i] );
                        if ( index != i )
                        {
                            anyChanges = true;
                            break;
                        }
                    }
                    if ( anyChanges )
                    {
                        var newChildren = new ModelSystemDisplayStructure[children.Count];
                        for ( int i = 0; i < children.Count; i++ )
                        {
                            var child = children[i];
                            if ( ( index = IndexOf( oldChildren, children[i] ) ) >= 0 )
                            {
                                newChildren[i] = oldChildren[index];
                                if ( index != i )
                                {
                                    anyChanges = true;
                                }
                            }
                            else
                            {
                                newChildren[i] = new ModelSystemDisplayStructure( children[i] );
                                anyChanges = true;
                            }
                        }
                        this._Children = newChildren;
                    }
                }
            }
            return anyChanges;
        }

        internal int IndexOf(ModelSystemDisplayStructure[] oldChildren, IModelSystemStructure modelSystemStructure)
        {
            for ( int i = 0; i < oldChildren.Length; i++ )
            {
                if ( oldChildren[i].Structure == modelSystemStructure )
                {
                    return i;
                }
            }
            return -1;
        }

        internal int IndexOf(ModelSystemDisplayStructure modelSystemStructure)
        {
            var children = this._Children;
            if ( children == null )
            {
                return -1;
            }
            for ( int i = 0; i < children.Length; i++ )
            {
                if ( children[i] == modelSystemStructure )
                {
                    return i;
                }
            }
            return -1;
        }

        internal void Move(int startingPlace, int destinationPlace)
        {
            // make sure that it is actually moving
            if ( startingPlace == destinationPlace ) return;

            // if it is figure out in what direction
            if ( startingPlace < destinationPlace )
            {
                // if we are moving down then move things up
                var temp = _Children[startingPlace];
                for ( int i = startingPlace; i < destinationPlace; i++ )
                {
                    _Children[i] = _Children[i + 1];
                }
                _Children[destinationPlace] = temp;
            }
            else
            {
                var temp = _Children[startingPlace];
                for ( int i = destinationPlace; i < startingPlace; i++ )
                {
                    _Children[i + 1] = _Children[i];
                }
                _Children[destinationPlace] = temp;
            }
            this.Changed( "Children" );
        }
    }
}