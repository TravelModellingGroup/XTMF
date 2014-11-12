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
    /// Used by IModel's to describe what this model is for
    /// </summary>
    [AttributeUsage( AttributeTargets.Class )]
    public class ModuleInformationAttribute : Attribute
    {
        /// <summary>
        ///
        /// </summary>
        public ModuleInformationAttribute()
        {
        }

        /// <summary>
        /// What this model does, or should do
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The location of the image to use for this module.
        /// Leave blank to use the default icon.
        /// </summary>
        public string IconURI { get; set; }

        /// <summary>
        /// The name of this module
        /// </summary>
        public string Name { get; set; }
    }
}