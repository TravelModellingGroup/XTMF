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
namespace TMG.GTAModel;

public sealed class Person : IPerson
{
    public int Age
    {
        get;
        internal set;
    }

    public bool DriversLicense
    {
        get;
        internal set;
    }

    public int EmploymentStatus
    {
        get;
        internal set;
    }

    public float ExpansionFactor
    {
        get;
        internal set;
    }

    public IHousehold Household
    {
        get;
        internal set;
    }

    public int Occupation
    {
        get;
        internal set;
    }

    public IZone SchoolZone
    {
        get;
        set;
    }

    public int StudentStatus
    {
        get;
        internal set;
    }

    public IZone WorkZone
    {
        get;
        set;
    }
}