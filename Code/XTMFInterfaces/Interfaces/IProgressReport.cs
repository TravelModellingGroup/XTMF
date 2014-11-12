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

namespace XTMF
{
    /// <summary>
    /// An IProgressReport helps provide information
    /// to the user about how the system is
    /// </summary>
    public interface IProgressReport
    {
        /// <summary>
        /// A colour attribute for this progress report.
        /// In R,G,B format.
        /// </summary>
        /// <remarks>Typically this is for the colour of a client's progress bar</remarks>
        Tuple<byte, byte, byte> Colour { get; set; }

        /// <summary>
        /// A function that provides the current progress to report.
        /// </summary>
        Func<float> GetProgress { get; }

        /// <summary>
        /// The name of this progress report.
        /// </summary>
        string Name { get; }
    }
}