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

/// <summary>
/// This class provides a bunch of functional programming ideas
/// to different types of collections
/// </summary>

public static class FunctionalProgramming
{
    public static void Apply<T>(this IList<T> us, Func<T, T> function)
    {
        int length = us.Count;
        for ( int i = 0; i < length; i++ )
        {
            T variable = us[i];
            us[i] = function( variable );
        }
    }

    public static void Apply<T>(this ICollection<T> us, Func<T, T> function)
    {
        T[] list = us.ToArray();
        for ( int i = 0; i < us.Count; i++ )
        {
            us.Remove( list[i] );
            list[i] = function( list[i] );
            us.Add( list[i] );
        }
    }

    public static void Apply<T>(this T[] us, Func<T, T> function)
    {
        for ( int i = 0; i < us.Length; i++ )
        {
            us[i] = function( us[i] );
        }
    }

    /// <summary>
    /// For two equal sized lists, runs a function on both and stores the result in the first
    /// </summary>
    /// <typeparam name="T">The first type</typeparam>
    /// <typeparam name="K">The second type</typeparam>
    /// <param name="us">The array we are going to store in</param>
    /// <param name="other">The other array we are using in our function</param>
    /// <param name="function">The function to run to find our result</param>
    public static void CoApply<T, K>(this IList<T> us, IList<K> other, Func<T, K, T> function)
    {
        int length = us.Count;
        for ( int i = 0; i < length; i++ )
        {
            T variable = us[i];
            us[i] = function( variable, other[i] );
        }
    }

    /// <summary>
    /// For two equal sized lists, runs a function on both and stores the result in the first
    /// </summary>
    /// <typeparam name="T">The first type</typeparam>
    /// <typeparam name="K">The second type</typeparam>
    /// <param name="us">The array we are going to store in</param>
    /// <param name="other">The other array we are using in our function</param>
    /// <param name="function">The function to run to find our result</param>
    public static void CoDo<T, K>(this IList<T> us, IList<K> other, Action<T, K> function)
    {
        int length = us.Count;
        for ( int i = 0; i < length; i++ )
        {
            function( us[i], other[i] );
        }
    }

    public static IList<T> Do<T, K>(this IList<T> us, IList<K> other, Action<T, K> function)
    {
        int length = us.Count;
        for ( int i = 0; i < length; i++ )
        {
            function( us[i], other[i] );
        }
        return us;
    }

    public static void Do<T>(this ICollection<T> us, Action<T> action)
    {
        foreach ( var v in us )
        {
            action( v );
        }
    }

    public static ICollection<T> Filter<T>(this ICollection<T> us, Predicate<T> test)
    {
        List<T> list = new List<T>( us.Count );
        foreach ( var v in us )
        {
            if ( test( v ) ) list.Add( v );
        }
        return list;
    }

    public static IList<T> Sort<T, K>(this IList<T> us, Func<T, K> SelectKey, Func<K, K, bool> IsHigher)
    {
        int size = us.Count;
        for ( int i = 0; i < size; i++ )
        {
            for ( int j = 1; j < size - i; j++ )
            {
                if ( IsHigher( SelectKey( us[j - 1] ), SelectKey( us[j] ) ) )
                {
                    T temp = us[j];
                    us[j] = us[j - 1];
                    us[j - 1] = temp;
                }
            }
        }
        return us;
    }
}