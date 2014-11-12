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
using System.Linq;

namespace TMG.Emme
{
    public struct Link
    {
        public float Capacity;
        public int I;
        public int J;
        public float Lanes;
        public float Length;
        public int LinkType;
        public char[] Modes;
        public bool Modified;
        public float Speed;
        public float VDF;

        public override bool Equals(object obj)
        {
            if ( obj is Link )
            {
                return this == (Link)obj;
            }
            else
            {
                return base.Equals( obj );
            }
        }

        public static bool operator ==(Link first, Link o)
        {
            if ( first.Length != o.Length ) return false;
            if ( first.LinkType != o.LinkType ) return false;
            if ( first.Lanes != o.Lanes ) return false;
            if ( first.VDF != o.VDF ) return false;
            if ( first.Modes.Length != o.Modes.Length ) return false;
            if ( first.Speed != o.Speed ) return false;
            if ( first.Capacity != o.Capacity ) return false;

            for ( int i = 0; i < first.Modes.Length; i++ )
            {
                if ( !o.Modes.Contains( first.Modes[i] ) ) return false;
            }
            for ( int i = 0; i < o.Modes.Length; i++ )
            {
                if ( !first.Modes.Contains( o.Modes[i] ) ) return false;
            }
            return true;
        }

        public static bool operator !=(Link first, Link o)
        {
            return first != o;
        }

        public override int GetHashCode()
        {
            // return the XOR of the 2 links
            return this.I ^ this.J;
        }
    }
}