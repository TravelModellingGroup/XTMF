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
using System.IO;
using System.Xml;

namespace XTMF
{
    /// <summary>
    /// Defines a meta structure for the model system
    /// Allowing for recursive definition in a tree format
    /// </summary>
    public interface IModelSystemStructure
    {
        /// <summary>
        /// The children of this element
        /// </summary>
        IList<IModelSystemStructure> Children { get; }

        /// <summary>
        /// What this Element is used for
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Check to see if this ProjectStructure is in fact a collection node.
        /// </summary>
        /// <remarks>
        /// If this is a collection node the Type and Model fields are invalid and instead you
        /// should be using the CollectionMembers property.
        /// </remarks>
        bool IsCollection { get; }

        /// <summary>
        /// The actual model that this represents
        /// </summary>
        IModule Module { get; set; }

        /// <summary>
        /// The name of the element
        /// </summary>
        string Name { get; set; }

        /// <summary>
        ///
        /// </summary>
        IModuleParameters Parameters { get; set; }

        string ParentFieldName { get; set; }

        /// <summary>
        /// The type that we need to fit into
        /// </summary>
        Type ParentFieldType { get; set; }

        /// <summary>
        /// Is it required to have a value set for this
        /// point in the structure to be
        /// able to execute
        /// </summary>
        bool Required { get; set; }

        /// <summary>
        /// The type of the element
        /// </summary>
        Type Type { get; set; }

        DateTime LastModified { get; set; }

        /// <summary>
        /// Notify if the model system structure
        /// should behave like a meta module
        /// </summary>
        bool IsMetaModule { get; set; }

        /// <summary>
        /// Add a new child node
        /// </summary>
        /// <param name="name">The name of the child</param>
        /// <param name="type">The type of the child</param>
        void Add(string name, Type type);

        /// <summary>
        /// Add a new child node
        /// </summary>
        /// <param name="p"></param>
        void Add(IModelSystemStructure p);


        /// <summary>
        /// Make an exact copy of this model system structure
        /// </summary>
        /// <returns>A copy of this model system structure including all children</returns>
        IModelSystemStructure Clone(IModelSystemStructure parent = null);

        /// <summary>
        /// Create a new IProjectStructure that can be used as a
        /// collection member
        /// </summary>
        /// <param name="newType">The type to make it from</param>
        /// <returns>The newly created project structure</returns>
        IModelSystemStructure CreateCollectionMember(Type newType);

        IModelSystemStructure Parent { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="topModule"></param>
        /// <returns></returns>
        List<Type> GetPossibleModules(IModelSystemStructure topModule);

        /// <summary>
        /// Save the model system to disk
        /// </summary>
        void Save(string fileName);

        void Save(Stream stream);

        void Save(XmlWriter writer);

        /// <summary>
        /// Validate the project structure
        /// </summary>
        /// <param name="error">A message returned if there is an error found providing additional information</param>
        /// <param name="parent"></param>
        /// <returns>If the ProjectStructure is valid (able to be created into an executable model system)</returns>
        bool Validate(ref string error, IModelSystemStructure parent = null);


    }
}