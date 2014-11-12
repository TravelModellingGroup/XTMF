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
namespace TMG
{
    /// <summary>
    /// Defines the possible states for some person's employment status
    /// F Full time
    /// H Home - full time
    /// J Home - part time
    /// O Not employed
    /// P Part time
    /// 9 Unknown
    /// </summary>
    public enum TTSEmploymentStatus
    {
        /// <summary>
        /// Works full time
        /// </summary>
        FullTime = 'F',

        /// <summary>
        /// Works at home, full time
        /// </summary>
        WorkAtHome_FullTime = 'H',

        /// <summary>
        /// Works at home, part time
        /// </summary>
        WorkAtHome_PartTime = 'J',

        /// <summary>
        /// Is not employed
        /// </summary>
        NotEmployed = 'O',

        /// <summary>
        /// Employed Part time
        /// </summary>
        PartTime = 'P',

        /// <summary>
        /// We don't know how they are employed
        /// </summary>
        Unknown = '9',
    }
}