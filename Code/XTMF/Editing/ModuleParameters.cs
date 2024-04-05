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

namespace XTMF
{
    public class ModuleParameters : IModuleParameters
    {
        public ModuleParameters() => Parameters = new List<IModuleParameter>(5);

        public ModuleParameters(IList<IModuleParameter> givenParameters) => Parameters = givenParameters;

        public IModelSystemStructure BelongsTo { get; set; }

        public IList<IModuleParameter> Parameters { get; private set; }

        public void Add(ParameterAttribute param, Type t) => Parameters.Add(new ModuleParameter(param, t));

        public IModuleParameters Clone()
        {
            ModuleParameters copy = [];
            foreach (var p in Parameters)
            {
                copy.Parameters.Add(p.Clone());
            }
            return copy;
        }

        public IEnumerator<IModuleParameter> GetEnumerator() => Parameters.GetEnumerator();

        public void Save()
        {
            throw new NotImplementedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Parameters.GetEnumerator();
        }
    }
}