/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Reflection;

namespace XTMF;

/// <summary>
/// This class provides the ability to work jointly with fields and properties disregarding
/// their differences.
/// </summary>
public abstract class UnifiedFieldType
{
    public abstract string Name { get; }

    public abstract object[] GetAttributes();

    public abstract bool IsPublic { get; }

    public static IEnumerable<UnifiedFieldType> GetMembers(Type t)
    {
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(m => (UnifiedFieldType)new FieldType(m));
        var properties = t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(m => (UnifiedFieldType)new PropertyType(m));
        return fields.Union(properties);
    }

    private class FieldType : UnifiedFieldType
    {
        private FieldInfo _Member;

        public FieldType(FieldInfo field) => _Member = field;

        public override string Name => _Member.Name;

        public override bool IsPublic => _Member.IsPublic;

        public override object[] GetAttributes() => _Member.GetCustomAttributes(true);
    }


    private class PropertyType : UnifiedFieldType
    {
        private PropertyInfo _Member;

        public PropertyType(PropertyInfo field) => _Member = field;

        public override string Name => _Member.Name;

        public override bool IsPublic => 
            (_Member.GetMethod == null || _Member.GetMethod.IsPublic) &&
            (_Member.SetMethod == null || _Member.SetMethod.IsPublic);

        public override object[] GetAttributes()
        {
            return _Member.GetCustomAttributes(true);
        }
    }
}
