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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XTMF.Editing
{
    /// <summary>
    /// Provides support for a rolling stack
    /// </summary>
    public sealed class EditingStack : ICollection<XTMFCommand>
    {
        public EditingStack(int capacity)
        {
            Capacity = capacity;
            Data = new XTMFCommand[capacity];
            IsReadOnly = false;
        }
        /// <summary>
        /// The backing data for the stack
        /// </summary>
        private XTMFCommand[] Data;

        public int Capacity { get; private set; }

        public int Count { get; private set; }

        public bool IsReadOnly { get; private set; }

        private int Head = -1;

        private object DataLock = new object();

        /// <summary>
        /// Add a new command onto the stack
        /// </summary>
        /// <param name="item"></param>
        public void Add(XTMFCommand item)
        {
            lock (DataLock)
            {
                // since this is a circle, there is no issue
                Head = (Head + 1) % Capacity;
                Count++;
                Data[Head] = item;
                if(Count > Capacity)
                {
                    Count = Capacity;
                }
            }
        }

        /// <summary>
        /// Get the top element off of the stack
        /// </summary>
        /// <returns>The top element</returns>
        public XTMFCommand Pop()
        {
            if (TryPop(out XTMFCommand result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Attempt to pop the top element off of the stack
        /// </summary>
        /// <param name="command">The command that was popped off the stack, null if it failed.</param>
        /// <returns>If the pop was successful</returns>
        public bool TryPop(out XTMFCommand command)
        {
            lock (DataLock)
            {
                if(Count > 0)
                {
                    Count--;
                    command = Data[Head];
                    Head = (Head - 1) % Capacity;
                    return true;
                }
                command = null;
                return false;
            }
        }

        public void Clear()
        {
            lock (DataLock)
            {
                Array.Clear(Data, 0, Data.Length);
                Count = 0;
            }
        }

        public bool Contains(XTMFCommand item)
        {
            lock (DataLock)
            {
                for(int i = 0; i < Count; i++)
                {
                    var headoffset = (Head - i);
                    int index = headoffset < 0 ? Capacity + headoffset : headoffset;
                    if (Data[index] == item)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void CopyTo(XTMFCommand[] array, int arrayIndex)
        {
            if(array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            lock (DataLock)
            {
                if(array.Length - arrayIndex < Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                }
                for(int i = 0; i < Count; i++)
                {
                    var headoffset = (Head - i);
                    int index = headoffset < 0 ? Capacity + headoffset : headoffset;
                    array[arrayIndex++] = Data[index];
                }
            }
        }

        public IEnumerator<XTMFCommand> GetEnumerator()
        {
            lock (DataLock)
            {
                for(int i = 0; i < Count; i++)
                {
                    var headoffset = (Head - i);
                    int index = headoffset < 0 ? Capacity + headoffset : headoffset;
                    yield return Data[index];
                }
            }
        }

        public bool Remove(XTMFCommand item)
        {
            throw new NotSupportedException("Removing an item is not supported for a stack.");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
