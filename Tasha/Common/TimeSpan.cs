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
using XTMF;

namespace Tasha.Common
{
    public struct TashaTimeSpan
    {
        public Time End;
        public Time Start;

        public TashaTimeSpan(Time s, Time e)
        {
            Start = s;
            End = e;
        }

        public bool InTimeSpan(Time time)
        {
            // Don't take out the && since both of these are actually a method call
            return ( ( time >= Start ) && ( time <= End ) );
        }
    }
}