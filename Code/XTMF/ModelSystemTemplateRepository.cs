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

/// <summary>
/// Provides an implementation of an IModelSystemRepository
/// </summary>
public class ModelSystemTemplateRepository : IModelSystemTemplateRepository
{
    public List<Type> ModelSystemTemplates { get; } = [];

    public void Add(Type type)
    {
        ModelSystemTemplates.Add(type);
        ModelSystemTemplates.Sort((first, second) =>
       {
           return first.FullName.CompareTo(second.FullName);
       });
    }

    public IEnumerator<Type> GetEnumerator() => ModelSystemTemplates.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ModelSystemTemplates.GetEnumerator();

    public void Unload(Type type) => ModelSystemTemplates.Remove(type);
}