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

namespace Tasha.Common
{
    public static class ArrayFunctions
    {
        public static IList<T> FindCommonElements<T>(IList<T> listA, IList<T> listB, Func<T, T, bool> equalityComparer)
        {
            IList<T> commonList = new List<T>( listA.Count );

            foreach ( var itemA in listA )
            {
                foreach ( var itemB in listB )
                {
                    if ( equalityComparer( itemA, itemB ) )
                    {
                        commonList.Add( itemA );
                        break;
                    }
                }
            }

            return commonList;
        }

        public static IList<T> FindCommonElements<T>(Func<T, T, bool> equalityComparer, params IList<T>[] lists)
        {
            if ( lists.Count() == 0 )
            {
                return new List<T>();
            }

            IList<T> commonList = new List<T>( lists[0] );

            for ( int i = 1; i < lists.Count(); i++ )
            {
                commonList = FindCommonElements( commonList, lists[i], equalityComparer );
            }

            return commonList;
        }
    }
}