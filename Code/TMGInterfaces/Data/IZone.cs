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
namespace TMG
{
    public interface IZone
    {
        /// <summary>
        /// ratio of arterial road km to total road km
        /// </summary>
        float ArterialRoadRatio { get; set; }

        /// <summary>
        /// total zonal employment
        /// </summary>
        float Employment { get; set; }

        /// <summary>
        /// employment - general office / clerical
        /// </summary>
        float GeneralEmployment { get; set; }

        /// <summary>
        /// The area that this zone covers
        /// </summary>
        float InternalArea { get; set; }

        /// <summary>
        /// Average distance within this zone
        /// </summary>
        float InternalDistance { get; set; }

        /// <summary>
        /// intersection density (# intersections/total road km)
        /// </summary>
        float IntrazonalDensity { get; set; }

        /// <summary>
        /// employment - manufacturing/construction/trades
        /// </summary>
        float ManufacturingEmployment { get; set; }

        /// <summary>
        ///
        /// </summary>
        float OtherActivityLevel { get; set; }

        /// <summary>
        /// How much does parking cost within this zone
        /// </summary>
        float ParkingCost { get; set; }

        /// <summary>
        /// The planning district the zone is in
        /// </summary>
        int PlanningDistrict { get; set; }

        /// <summary>
        /// total zonal population for the zone
        /// </summary>
        int Population { get; set; }

        /// <summary>
        /// employment - professional management technical
        /// </summary>
        float ProfessionalEmployment { get; set; }

        /// <summary>
        /// The region that this zone is in
        /// </summary>
        int RegionNumber { get; set; }

        /// <summary>
        ///
        /// </summary>
        float RetailActivityLevel { get; set; }

        /// <summary>
        /// employment - retail sales and service
        /// </summary>
        float RetailEmployment { get; set; }

        /// <summary>
        ///
        /// </summary>
        float TotalEmployment
        {
            get;
            set;
        }

        /// <summary>
        /// employment - We don't know exactly what type it is
        /// </summary>
        float UnknownEmployment { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkActivityLevel { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkGeneral { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkManufacturing { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkProfessional { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkRetail { get; set; }

        /// <summary>
        ///
        /// </summary>
        float WorkUnknown { get; set; }

        /// <summary>
        /// X Position of this zone
        /// </summary>
        float X { get; set; }

        /// <summary>
        /// Y Position of this Zone
        /// </summary>
        float Y { get; set; }

        /// <summary>
        /// The ID for the zone
        /// </summary>
        int ZoneNumber { get; }
    }
}