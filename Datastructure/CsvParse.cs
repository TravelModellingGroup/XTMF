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
using System.Text;

namespace Datastructure;

internal static class CsvParse
{
    internal static float ParseFixedFloat(string line, int offset, int length)
    {
        var start = offset + length;
        var scientificNotation = false;
        var exponent = 0;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '.' | line[start - 1] == '-' | line[start - 1] == '+' ) )
        {
            if ( start > 2 && ( line[start - 1] == '-' & ( line[start - 2] == 'e' | line[start - 2] == 'E' ) ) )
            {
                scientificNotation = true;
                exponent = -ParseInt( line, start, offset + length );
                // subtract
                length -= ( offset + length ) - start + 2;
                start -= 2;
            }
            else if ( start > 2 && ( line[start - 1] == '+' & ( line[start - 2] == 'e' | line[start - 2] == 'E' ) ) )
            {
                scientificNotation = true;
                exponent = ParseInt( line, start, offset + length );
                // subtract
                length -= ( offset + length ) - start + 2;
                start -= 2;
            }
            else
            {
                start--;
            }
        }
        if ( scientificNotation )
        {
            return (float)Math.Pow( 10, exponent ) * ParseFloat( line, start, offset + length );
        }
        else
        {
            return ParseFloat( line, start, offset + length );
        }
    }

    internal static float ParseFixedFloat(StringBuilder line, int offset, int length)
    {
        var start = offset + length;
        var scientificNotation = false;
        var exponent = 0;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '.' | line[start - 1] == '-' | line[start - 1] == '+' ) )
        {
            if ( start > 2 && ( line[start - 1] == '-' & ( line[start - 2] == 'e' | line[start - 2] == 'E' ) ) )
            {
                scientificNotation = true;
                exponent = -ParseInt( line, start, offset + length );
                // subtract
                length -= ( offset + length ) - start + 2;
                start -= 2;
            }
            else if ( start > 2 && ( line[start - 1] == '+' & ( line[start - 2] == 'e' | line[start - 2] == 'E' ) ) )
            {
                scientificNotation = true;
                exponent = ParseInt( line, start, offset + length );
                // subtract
                length -= ( offset + length ) - start + 2;
                start -= 2;
            }
            else
            {
                start--;
            }
        }
        if ( scientificNotation )
        {
            return (float)Math.Pow( 10, exponent ) * ParseFloat( line, start, offset + length );
        }
        else
        {
            return ParseFloat( line, start, offset + length );
        }
    }

    internal static float ParseFixedFloat(char[] line, int offset, int length)
    {
        var start = offset + length;
        var scientificNotation = false;
        var exponent = 0;
        while ( start > 0 && ( ( line[start - 1] >= '0' & line[start - 1] <= '9' ) | line[start - 1] == '.' | line[start - 1] == '-' | line[start - 1] == '+' ) )
        {
            if ( start > 2 && ( line[start - 2] == 'e' | line[start - 2] == 'E' ) )
            {
                if ( line[start - 1] == '-' )
                {
                    exponent = -ParseInt( line, start, offset + length );
                }
                else if ( line[start - 1] == '+' )
                {
                    exponent = ParseInt( line, start, offset + length );
                }
                else
                {
                    break;
                }
                scientificNotation = true;
                // subtract
                length = ( offset + length ) - start + 2;
                start -= 2;
            }
            else
            {
                start--;
            }
        }
        if ( scientificNotation )
        {
            return (float)Math.Pow( 10, exponent ) * ParseFloat( line, start, offset + length );
        }
        else
        {
            return ParseFloat( line, start, offset + length );
        }
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
        var ival = 0;
        float fval = 0;
        var multiplyer = 0.1f;
        int i;
        char c;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( i = indexFrom; i < indexTo; i++ )
        {
            if ( ( c = str[i] ) == '.' )
            {
                break;
            }
            // Same as multiplying by 10
            ival = ( ival << 1 ) + ( ival << 3 );
            ival += c - '0';
        }
        for ( i++; i < indexTo; i++ )
        {
            var k = ( str[i] - '0' );
            fval += k * multiplyer;
            multiplyer *= 0.1f;
        }
        fval += ival;
        return neg ? -fval : fval;
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
        var ival = 0;
        float fval = 0;
        var multiplyer = 0.1f;
        int i;
        char c;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( i = indexFrom; i < indexTo; i++ )
        {
            if ( ( c = str[i] ) == '.' )
            {
                break;
            }
            // Same as multiplying by 10
            ival = ( ival << 1 ) + ( ival << 3 );
            ival += c - '0';
        }
        for ( i++; i < indexTo; i++ )
        {
            var k = ( str[i] - '0' );
            fval += k * multiplyer;
            multiplyer *= 0.1f;
        }
        fval += ival;
        return neg ? -fval : fval;
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
        var ival = 0;
        float fval = 0;
        var multiplyer = 0.1f;
        int i;
        char c;
        var neg = str[indexFrom] == '-';
        if ( neg ) indexFrom++;
        for ( i = indexFrom; i < indexTo; i++ )
        {
            if ( ( c = str[i] ) == '.' )
            {
                break;
            }
            // Same as multiplying by 10
            ival = ( ival << 1 ) + ( ival << 3 );
            ival += c - '0';
        }
        for ( i++; i < indexTo; i++ )
        {
            var k = ( str[i] - '0' );
            fval += k * multiplyer;
            multiplyer *= 0.1f;
        }
        fval += ival;
        return neg ? -fval : fval;
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