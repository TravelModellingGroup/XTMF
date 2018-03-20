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
    [Serializable]
    public class XTMFRuntimeException : Exception
    {
        /// <summary>
        /// The module that caused the error.
        /// Check for null.
        /// </summary>
        public IModule Module { get; }

        public XTMFRuntimeException()
        {
        }

        [System.Obsolete("Use XTMFRuntimeException(IModule module, string message) instead.")]
        public XTMFRuntimeException(string message)
        {

        }

        public XTMFRuntimeException(IModule module, string message)
            : base(message)
        {
            Module = module;
        }

        public XTMFRuntimeException(IModule module, Exception wrapedException, string message = null)
            : base(String.IsNullOrWhiteSpace(message) ? wrapedException?.Message ?? "No Message" : message, wrapedException)
        {
            Module = module;
        }
    }
}