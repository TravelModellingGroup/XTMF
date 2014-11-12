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
namespace TMG.GTAModel
{
    internal class HOTZone : IZone
    {
        public HOTZone()
        {
        }

        public float ArterialRoadRatio
        {
            get;
            set;
        }

        public float Employment
        {
            get;
            set;
        }

        public float GeneralEmployment
        {
            get;
            set;
        }

        public float InternalArea
        {
            get;
            set;
        }

        public float InternalDistance
        {
            get;
            set;
        }

        public float IntrazonalDensity
        {
            get;
            set;
        }

        public float ManufacturingEmployment
        {
            get;
            set;
        }

        public float OtherActivityLevel
        {
            get;
            set;
        }

        public float ParkingCost
        {
            get;
            set;
        }

        public int PlanningDistrict
        {
            get;
            set;
        }

        public int Population
        {
            get;
            set;
        }

        public float ProfessionalEmployment
        {
            get;
            set;
        }

        public int RegionNumber
        {
            get;
            set;
        }

        public float RetailActivityLevel
        {
            get;
            set;
        }

        public float RetailEmployment
        {
            get;
            set;
        }

        public float TotalEmployment
        {
            get { return Employment; }
            set { this.Employment = value; }
        }

        public float UnknownEmployment
        {
            get;
            set;
        }

        public float WorkActivityLevel
        {
            get;
            set;
        }

        public float WorkGeneral
        {
            get;
            set;
        }

        public float WorkManufacturing
        {
            get;
            set;
        }

        public float WorkProfessional
        {
            get;
            set;
        }

        public float WorkRetail
        {
            get;
            set;
        }

        public float WorkUnknown
        {
            get;
            set;
        }

        public float X
        {
            get;
            set;
        }

        public float Y
        {
            get;
            set;
        }

        public int ZoneNumber
        {
            get;
            set;
        }

        public override string ToString()
        {
            return "Zone Number: " + this.ZoneNumber;
        }
    }
}