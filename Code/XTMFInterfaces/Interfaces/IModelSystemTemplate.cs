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
namespace XTMF
{
    /// <summary>
    /// Describes how an IProject will execute.
    /// </summary>
    /// <remarks>Use this to create a system like Tasha or GTAModel</remarks>
    public interface IModelSystemTemplate : ISelfContainedModule
    {
        /// <summary>
        /// The base directory for input
        /// (Should be a relative path so that XMTF can put it inside of the project)
        /// </summary>
        string InputBaseDirectory { get; set; }

        /// <summary>
        /// The base directory for output
        /// (Should be a relative path so that XMTF can put it inside of the project)
        /// </summary>
        string OutputBaseDirectory { get; set; }

        /// <summary>
        /// Setting this flag will request that the model system template will exit prematurely
        /// </summary>
        /// <returns>True if this model system template supports exiting (and will exit), false otherwise.</returns>
        bool ExitRequest();
    }
}