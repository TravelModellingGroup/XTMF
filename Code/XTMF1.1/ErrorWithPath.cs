/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using System.Threading.Tasks;

namespace XTMF
{
    /// <summary>
    /// Describes an error with a path to the module that
    /// generated it.
    /// </summary>
    public struct ErrorWithPath
    {
        /// <summary>
        /// The path through the model system structure's children
        /// to get to the offending module.  This is null if the path
        /// is unknowable.  An empty list is the root module.
        /// </summary>
        public IReadOnlyList<int> Path { get; }

        /// <summary>
        /// The error message to pass on.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The stack trace at the point of the error.  This is null in the case
        /// of validation.
        /// </summary>
        public string StackTrace { get; }

        /// <summary>
        /// Creates a new error with the given path.
        /// </summary>
        /// <param name="path">The path to use, a copy will be stored.</param>
        /// <param name="message"></param>
        /// <param name="stackTrace"></param>
        public ErrorWithPath(List<int> path, string message, string stackTrace = null)
        {
            // Make a copy of the path
            Path = path?.ToList();
            Message = message;
            StackTrace = stackTrace;
        }
    }
}
