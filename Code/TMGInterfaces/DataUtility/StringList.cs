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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace TMG.DataUtility;

/// <summary>
/// Allows you to have a parameter that is a list of strings.  Strings are separated by ,'s.  If you have a \ before it, it will treat the comma as
/// part of the current string.
/// </summary>
public sealed class StringList : IList<string>
{
    private readonly string[] _data;

    private StringList(string[] strings)
    {
        _data = strings;
    }

    public static bool TryParse(string input, out StringList stringList)
    {
        List<string> temp = [];
        StringBuilder current = new();
        bool escape = false;
        var length = input.Length;
        for ( int i = 0; i < length; i++ )
        {
            char c = input[i];
            if ( escape )
            {
                current.Append( c );
                escape = false;
            }
            else if ( c == '\\' )
            {
                escape = true;
            }
            else
            {
                if ( c != ',' )
                {
                    current.Append( c );
                }
                else
                {
                    temp.Add( current.ToString() );
                    current.Clear();
                }
            }
        }
        //add the rest
        if ( current.Length > 0 )
        {
            temp.Add( current.ToString() );
        }
        stringList = new StringList([.. temp]);
        return true;
    }

    public static bool TryParse(ref string error, string input, out StringList stringList)
    {
        return TryParse( input, out stringList );
    }

    public int IndexOf(string item)
    {
        var data = _data;
        for ( int i = 0; i < data.Length; i++ )
        {
            if ( data[i] == item )
            {
                return i;
            }
        }
        return -1;
    }

    public void Insert(int index, string item)
    {
        throw new InvalidOperationException();
    }

    public void RemoveAt(int index)
    {
        throw new InvalidOperationException();
    }

    public string this[int index]
    {
        get
        {
            return _data[index];
        }
        set
        {
            _data[index] = value;
        }
    }

    public void Add(string item)
    {
        throw new InvalidOperationException();
    }

    public void Clear()
    {
        Array.Clear(_data, 0, _data.Length);
    }

    public bool Contains(string item)
    {
        return IndexOf( item ) != -1;
    }

    public void CopyTo(string[] array, int arrayIndex)
    {
        if(array.Length - arrayIndex < _data.Length)
        {
            throw new ArgumentException($"{nameof(array)} does not have enough space to store the StringList starting at position {arrayIndex}.");
        }
        Array.Copy(_data, 0, array, arrayIndex, _data.Length);
    }

    public int Count => _data.Length;

    public bool IsReadOnly => false;

    public bool Remove(string item)
    {
        throw new InvalidOperationException();
    }

    public IEnumerator<string> GetEnumerator()
    {
        return ( (IEnumerable<string>)_data ).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    public string[] ToArray()
    {
        return [.. _data];
    }

    public override string ToString()
    {
        StringBuilder builder = new();
        var data = _data;
        for ( int i = 0; i < data.Length; i++ )
        {
            builder.Append( data[i].Replace( ",", "\\," ) );
            builder.Append( ',' );
        }
        if ( builder.Length > 0 )
        {
            return builder.ToString( 0, builder.Length - 1 );
        }
        else
        {
            return String.Empty;
        }
    }
}
