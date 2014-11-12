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
using System.Linq;
using System.Text;
using System.IO;
using TMG;
using XTMF;

namespace James.UTDM
{
    class Person : IPerson
    {
        public IHousehold Household
        {
            get;
            internal set;
        }

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

        public IZone WorkZone
        {
            get;
            set;
        }

        public IZone SchoolZone
        {
            get;
            set;
        }

        public int EmploymentStatus
        {
            get;
            internal set;
        }

        public int StudentStatus
        {
            get;
            internal set;
        }

        public int Occupation
        {
            get;
            internal set;
        }


        public float ExpansionFactor
        {
            get;
            internal set;
        }


        public void Save(StringBuilder writer)
        {
            writer.Append(this.Age);
            writer.Append(',');
            writer.Append(this.Household != null ? this.Household.Cars : 0);
            writer.Append(',');
            writer.Append(this.SchoolZone != null ? this.SchoolZone.ZoneNumber : -1);
            writer.Append(',');
            writer.Append(this.WorkZone != null ? this.WorkZone.ZoneNumber : -1);
            writer.Append(',');
            writer.Append((int)this.EmploymentStatus);
            writer.Append(',');
            writer.Append((int)this.StudentStatus);
            writer.Append(',');
            writer.Append((int)this.Occupation);
            writer.Append(',');
            writer.Append(this.DriversLicense ? 1 : 0);
            writer.Append(',');
            writer.Append(this.ExpansionFactor);
        }
        public void Save(StreamWriter writer)
        {
            writer.Write(this.Age);
            writer.Write(',');
            writer.Write(this.Household != null ? this.Household.Cars : 0);
            writer.Write(',');
            writer.Write(this.SchoolZone != null ? this.SchoolZone.ZoneNumber : -1);
            writer.Write(',');
            writer.Write(this.WorkZone != null ? this.WorkZone.ZoneNumber : -1);
            writer.Write(',');
            writer.Write((int)this.EmploymentStatus);
            writer.Write(',');
            writer.Write((int)this.StudentStatus);
            writer.Write(',');
            writer.Write((int)this.Occupation);
            writer.Write(',');
            writer.Write(this.DriversLicense ? 1 : 0);
            writer.Write(',');
            writer.Write(this.ExpansionFactor);
        }
    }
}
