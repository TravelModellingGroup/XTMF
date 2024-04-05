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

namespace XTMF;

public interface IModelSystemTemplateRepository : IEnumerable<Type>
{
    /// <summary>
    /// The model systems currently loaded in this
    /// installation of XTMF
    /// </summary>
    List<Type> ModelSystemTemplates { get; }

    /// <summary>
    /// add a new type of model system to the model system repository
    /// </summary>
    /// <param name="type">The type that implements IModelSystem</param>
    void Add(Type type);

    /// <summary>
    /// Unload a type from the IModelSystem Repository
    /// </summary>
    /// <param name="type">The type to remove</param>
    void Unload(Type type);
}