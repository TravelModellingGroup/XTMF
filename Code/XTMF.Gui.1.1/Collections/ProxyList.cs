/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.Collections
{
    internal sealed class ProxyList<T> : IList<T>
    {
        private IList<T> TrueList;

        public ProxyList(IList<T> proxyThis)
        {
            TrueList = proxyThis;
        }

        public T this[int index]
        {
            get
            {
                return TrueList[index];
            }

            set
            {
                TrueList[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return TrueList.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return TrueList.IsReadOnly;
            }
        }

        public void Add(T item)
        {
            TrueList.Add(item);
        }

        public void Clear()
        {
            TrueList.Clear();
        }

        public bool Contains(T item)
        {
            return TrueList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            TrueList.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return TrueList.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            TrueList.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return TrueList.Remove(item);
        }

        public void RemoveAt(int index)
        {
            TrueList.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)(TrueList)).GetEnumerator();
        }
    }
}
