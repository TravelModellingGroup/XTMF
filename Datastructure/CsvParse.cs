/*
    Copyright 2014-2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datastructure;

internal static class CsvParse
{

    internal static float ParseFloat(ReadOnlySpan<char> line)
    {
        if (!float.TryParse(line, out float value))
        {
            _ = float.TryParse(line, CultureInfo.InvariantCulture, out value);
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float ParseFixedFloat(char[] line, int offset, int length)
    {
        var buffer = line.AsSpan(offset, length);
        return ParseFloat(buffer);
    }

    internal static int ParseFixedInt(string line, int offset, int length)
    {
        var start = offset + length;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '-' ) )
        {
            start--;
        }
        return ParseInt( line, start, offset + length );
    }

    internal static int ParseFixedInt(StringBuilder line, int offset, int length)
    {
        var start = offset + length;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '-' ) )
        {
            start--;
        }
        return ParseInt( line, start, offset + length );
    }

    internal static int ParseFixedInt(char[] line, int offset, int length)
    {
        var start = offset + length;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '-' ) )
        {
            start--;
        }
        return ParseInt( line, start, offset + length );
    }

    /// <summary>
    /// Use this to parse an float out of a string
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <param name="indexFrom">Where to start(including)</param>
    /// <param name="indexTo">Where to stop (excluding)</param>
    /// <returns></returns>
    internal static float ParseFloat(string str, int indexFrom, int indexTo)
    {
        var buffer = str.AsSpan(indexFrom, indexTo - indexFrom);
        return ParseFloat(buffer);
    }

    /// <summary>
    /// Use this to parse an float out of a string
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <param name="indexFrom">Where to start(including)</param>
    /// <param name="indexTo">Where to stop (excluding)</param>
    /// <returns></returns>
    internal static float ParseFloat(StringBuilder str, int indexFrom, int indexTo)
    {
        var length = indexTo - indexFrom;
        Span<char> buffer = stackalloc char[length];
        str.CopyTo(indexFrom, buffer, length);
        return ParseFloat(buffer);
    }

    /// <summary>
    /// Use this to parse an float out of a string
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <param name="indexFrom">Where to start(including)</param>
    /// <param name="indexTo">Where to stop (excluding)</param>
    /// <returns></returns>
    internal static float ParseFloat(char[] str, int indexFrom, int indexTo)
    {
        var buffer = str.AsSpan(indexFrom, indexTo - indexFrom);
        return ParseFloat(buffer);
    }

    /// <summary>
    /// Use this to parse an integer out of a string
    /// </summary>
    /// <param name="str">The string's beginning</param>
    /// <param name="indexFrom">Where to start reading from</param>
    /// <param name="indexTo">Where to stop reading</param>
    /// <returns>The integer value</returns>
    internal static int ParseInt(string str, int indexFrom, int indexTo)
    {
        var value = 0;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( var i = indexFrom; i < indexTo; i++ )
        {
            // Same as multiplying by 10
            value = ( value << 1 ) + ( value << 3 );
            value += str[i] - '0';
        }
        return neg ? -value : value;
    }

    /// <summary>
    /// Use this to parse an integer out of a string
    /// </summary>
    /// <param name="str">The string's beginning</param>
    /// <param name="indexFrom">Where to start reading from</param>
    /// <param name="indexTo">Where to stop reading</param>
    /// <returns>The integer value</returns>
    internal static int ParseInt(StringBuilder str, int indexFrom, int indexTo)
    {
        var value = 0;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( var i = indexFrom; i < indexTo; i++ )
        {
            // Same as multiplying by 10
            value = ( value << 1 ) + ( value << 3 );
            value += str[i] - '0';
        }
        return neg ? -value : value;
    }

    /// <summary>
    /// Use this to parse an integer out of a string
    /// </summary>
    /// <param name="str">The string's beginning</param>
    /// <param name="indexFrom">Where to start reading from</param>
    /// <param name="indexTo">Where to stop reading</param>
    /// <returns>The integer value</returns>
    internal static int ParseInt(char[] str, int indexFrom, int indexTo)
    {
        var value = 0;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( var i = indexFrom; i < indexTo; i++ )
        {
            // Same as multiplying by 10
            value = ( value << 1 ) + ( value << 3 );
            value += str[i] - '0';
        }
        return neg ? -value : value;
    }
}