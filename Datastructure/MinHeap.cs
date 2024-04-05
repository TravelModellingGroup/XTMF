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

namespace Datastructure;

public class MinHeap<T> : ICollection<T?> where T : IComparable<T?>
{
    private int Elements;
    private T?[] Data;

    /// <summary>
    /// Crate a new empty min heap
    /// </summary>
    public MinHeap()
    {
        Elements = 0;
        Data = new T[8];
    }

    /// <summary>
    /// Create a new min heap
    /// </summary>
    /// <param name="startingData">The initial data to be used</param>
    public MinHeap(IList<T> startingData)
    {
        Elements = startingData.Count;
        var temp = new T?[Elements];
        for ( var i = 0; i < temp.Length; i++ )
        {
            temp[i] = startingData[i];
        }
        Data = temp;
        Heapify();
    }

    /// <summary>
    /// Add an item to the Min Heap
    /// </summary>
    /// <param name="item">The item to add to the </param>
    public void Add(T? item)
    {
        if ( item is null )
        {
            throw new ArgumentNullException(nameof(item));
        }
        if (Elements >= Data.Length )
        {
            IncreaseSize();
        }
        int index;
        Data[( index = Elements++ )] = item;
        Heapify( index );
    }

    /// <summary>
    /// Remove the min valued item from the heap
    /// </summary>
    /// <returns>A smallest valued object stored in the heap</returns>
    public T? Pop()
    {
        return Remove( 0 );
    }

    /// <summary>
    /// Remove the given item from the heap
    /// </summary>
    /// <param name="item">The item to remove</param>
    /// <returns>If the item was removed.</returns>
    public bool Remove(T? item)
    {
        var data = Data;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i]?.Equals(item) == true)
            {
                Remove(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes an element from a given position in the internal array
    /// </summary>
    /// <param name="elementAt">The position to remove from</param>
    /// <returns>The value at that position in the array</returns>
    private T? Remove(int elementAt)
    {
        var data = Data;
        var ret = data[elementAt];
        var elements = Elements;

        int children;
        while ( ( children = elementAt * 2 + 1 ) < elements )
        {
            if ( children + 1 >= elements )
            {
                data[elementAt] = data[children];
                elementAt = children;
                break;
            }
            else
            {
                if ( (data[children]?.CompareTo( data[children + 1] ) ?? -1) <= 0 )
                {
                    data[elementAt] = data[children];
                    elementAt = children;
                }
                else
                {
                    data[elementAt] = data[children + 1];
                    elementAt = children + 1;
                }
            }
        }
        // move the last element to our current
        var temp = data[elements - 1];
        data[elements - 1] = default;
        data[elementAt] = temp;
        // we now have 1 less element
        Elements--;
        Heapify( elementAt );
        return ret;
    }

    /// <summary>
    /// Sort out the entire heap into a proper min heap structure
    /// </summary>
    private void Heapify()
    {
        var data = Data;
        var start = (Elements - 2 ) / 2;
        var end = Elements - 1;
        while ( start >= 0 )
        {
            SiftDown( data, start, end );
            start--;
        }
    }

    /// <summary>
    /// Rebuild the heap structure for the given element
    /// </summary>
    /// <param name="element">The element position to build the structure for</param>
    private void Heapify(int element)
    {
        var data = Data;
        var end = Elements - 1;
        element = (element - 1) >> 1;
        while ( element > 0 )
        {
            SiftDown( data, element, end );
            element = ( element - 1 ) >> 1;
        }
        if ( element >= 0 )
        {
            SiftDown( data, element, end );
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    private static void SiftDown(T?[] data, int start, int end)
    {
        while ( start * 2 + 1 <= end )
        {
            var child = start * 2 + 1;
            var swap = start;
            if ( (data[swap]?.CompareTo(data[child]) ?? 1) > 0 )
            {
                swap = child;
            }
            if ( child + 1 <= end && (data[swap]?.CompareTo( data[child + 1] ) ?? 1) > 0 )
            {
                swap = child + 1;
            }
            if ( swap != start )
            {
                var temp = data[start];
                data[start] = data[swap];
                data[swap] = temp;
                start = swap;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// Doubles the size of the internal heap representation
    /// </summary>
    private void IncreaseSize()
    {
        var temp = new T[Data.Length * 2];
        Array.Copy(Data, temp, Data.Length );
        Data = temp;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Clear()
    {
        Elements = 0;
        Array.Clear(Data, 0, Data.Length );
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(T? item)
    {
        if(item is null) return false;
        var data = Data;
        for ( var i = 0; i < data.Length; i++ )
        {
            if (data[i]?.Equals(item) == true)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    public void CopyTo(T?[] array, int arrayIndex)
    {
        if ( array.Length - arrayIndex < Elements)
        {
            throw new ArgumentException( "The array has an insufficient length for copying to!", nameof(array));
        }
        Array.Copy(Data, 0, array, arrayIndex, Data.Length );
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => Elements;

    /// <summary>
    /// 
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<T?> GetEnumerator()
    {
        for ( var i = 0; i < Elements; i++ )
        {
            yield return Data[i];
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        for ( var i = 0; i < Elements; i++ )
        {
            yield return Data[i];
        }
    }
}
