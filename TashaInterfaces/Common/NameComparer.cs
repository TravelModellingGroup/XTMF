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
namespace Tasha.Common
{
    public abstract class NameComparer : INamedType
    {
        public static bool operator !=(NameComparer a, NameComparer b)
        {
            return !( a == b );
        }

        public static bool operator ==(NameComparer a, NameComparer b)
        {
            return a.Name == b.Name;
        }

        public override bool Equals(object obj)
        {
            if ( obj is INamedType )
            {
                return ( (INamedType)obj ).Name == this.Name;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #region INamedObject Members

        public abstract string Name { get; }

        #endregion INamedObject Members
    }
}