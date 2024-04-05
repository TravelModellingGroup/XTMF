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
using System.Text;

namespace TMG.GTAModel.DataUtility;

public class NumberList : IList<int>
{
    protected int[] Values;

    public int Count
    {
        get { return Values.Length; }
    }

    public bool IsReadOnly
    {
        get { return false; }
    }

    public int this[int index]
    {
        get
        {
            return Values[index];
        }

        set
        {
            Values[index] = value;
        }
    }

    public static bool TryParse(string input, out NumberList data)
    {
        string error = null;
        return TryParse( ref error, input, out data );
    }

    public static bool TryParse(ref string error, string input, out NumberList data)
    {
        data = null;
        var values = new List<int>();
        var i = 0;
        BurnWhiteSpace( ref i, input );
        var length = input.Length;
        while ( i < length )
        {
            var number = 0;
            var c = input[i];
            do
            {
                if ( c == '\n' | c == '\r' )
                {
                    error = "Unexpected newline while trying to read in a number string!";
                    return false;
                }
                if ( c < '0' | c > '9' )
                {
                    error = "We found a(n) '" + c + "' while trying to read a number!";
                    return false;
                }
                number = number * 10 + ( c - '0' );
            } while ( ++i < length && ( ( c = input[i] ) != '\t' & c != ' ' & c != ',' ) );
            BurnWhiteSpace( ref i, input );
            values.Add( number );
        }
        data = [];
        data.Values = [.. values];
        return true;
    }

    public void Add(int item)
    {
        throw new NotSupportedException();
    }

    public void Clear()
    {
        throw new NotSupportedException();
    }

    public bool Contains(int item)
    {
        return IndexOf( item ) != -1;
    }

    public void CopyTo(int[] array, int arrayIndex)
    {
        Array.Copy( Values, 0, array, arrayIndex, Values.Length );
    }

    public IEnumerator<int> GetEnumerator()
    {
        return ( (ICollection<int>)Values ).GetEnumerator();
    }

    public int IndexOf(int item)
    {
        for ( var i = 0; i < Values.Length; i++ )
        {
            if ( item == Values[i] )
            {
                return i;
            }
        }
        return -1;
    }

    public void Insert(int index, int item)
    {
        throw new NotSupportedException();
    }

    public bool Remove(int item)
    {
        throw new NotSupportedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotSupportedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Values.GetEnumerator();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        for ( var i = 0; i < Values.Length; i++ )
        {
            builder.Append( Values[i] );
            builder.Append( ',' );
        }
        return builder.ToString( 0, builder.Length - 1 );
    }

    private static void BurnWhiteSpace(ref int i, string input)
    {
        while ( i < input.Length && WhiteSpace( input[i] ) )
        {
            i++;
        }
    }

    private static bool WhiteSpace(char p)
    {
        switch ( p )
        {
            case '\t':
            case ' ':
            case ',':
                return true;

            default:
                return false;
        }
    }
}