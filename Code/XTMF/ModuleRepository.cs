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
using System.Linq;
using System.Collections.Generic;

namespace XTMF
{
    public class ModuleRepository : IModuleRepository
    {
        private List<Type> _ContainedModules = [];

        public IList<Type> Modules => _ContainedModules;

        public bool AddModule(Type module)
        {
            if ( !_ContainedModules.Contains( module ) )
            {
                _ContainedModules.Add( module );
                _ContainedModules.Sort( delegate(Type first, Type second)
                {
                    return first.Name.CompareTo( second.Name );
                } );
                return true;
            }
            return false;
        }

        public IEnumerator<Type> GetEnumerator() => _ContainedModules.GetEnumerator();

        public Type GetModuleType(string typeName)
        {
            return _ContainedModules.FirstOrDefault(model => model.FullName == typeName);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _ContainedModules.GetEnumerator();
        }

        public void Unload(Type type) => _ContainedModules.Remove(type);
    }
}