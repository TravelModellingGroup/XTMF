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

namespace XTMF.Controller
{
    internal class DataAccessView<T> : IBindingList
    {
        private IList<T> Data;

        public DataAccessView(IList<T> data)
        {
            this.Data = data;
        }

        public event ListChangedEventHandler ListChanged;

        public bool AllowEdit
        {
            get { return false; }
        }

        public bool AllowNew
        {
            get { return false; }
        }

        public bool AllowRemove
        {
            get { return false; }
        }

        public int Count
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool IsSorted
        {
            get { return true; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public ListSortDirection SortDirection
        {
            get { return ListSortDirection.Ascending; }
        }

        public PropertyDescriptor SortProperty
        {
            get { return null; }
        }

        public bool SupportsChangeNotification
        {
            get { return true; }
        }

        public bool SupportsSearching
        {
            get { return false; }
        }

        public bool SupportsSorting
        {
            get { return false; }
        }

        public object SyncRoot
        {
            get { return this.Data; }
        }

        public object this[int index]
        {
            get
            {
                return this.Data[index];
            }

            set
            {
                throw new NotSupportedException( "This view is read-only" );
            }
        }

        public int Add(object value)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void AddIndex(PropertyDescriptor property)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public object AddNew()
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void Clear()
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public bool Contains(object value)
        {
            return this.Data.Contains( (T)value );
        }

        public void CopyTo(Array array, int index)
        {
            var length = this.Data.Count;
            if ( array.Length - index < length )
            {
            }
            for ( int i = 0; i < length; i++ )
            {
                array.SetValue( this.Data[i], i + index );
            }
        }

        public int Find(PropertyDescriptor property, object key)
        {
            return -1;
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            return this.Data.GetEnumerator();
        }

        public int IndexOf(object value)
        {
            return this.Data.IndexOf( (T)value );
        }

        public void Insert(int index, object value)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void Remove(object value)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void RemoveIndex(PropertyDescriptor property)
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        public void RemoveSort()
        {
            throw new NotSupportedException( "This view is read-only" );
        }

        internal void ItemWasAdded(T obj)
        {
            var lc = this.ListChanged;
            if ( lc != null )
            {
                lc( this, new ListChangedEventArgs( ListChangedType.ItemAdded, this.Data.IndexOf( obj ) ) );
            }
        }

        internal void ItemWasRemoved(T obj, int index)
        {
            var lc = this.ListChanged;
            if ( lc != null )
            {
                lc( this, new ListChangedEventArgs( ListChangedType.ItemDeleted, index ) );
            }
        }
    }
}