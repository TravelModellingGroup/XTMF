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
using TMG;

namespace Tasha.Common;

/// <summary>
/// The implementation of the IZone interface
/// </summary>
public sealed class Zone : IZone
{
    public Zone()
    {
    }

    /// <summary>
    /// Creates a new zone to represent the roaming zone
    /// </summary>
    /// <param name="zoneID"></param>
    public Zone(int zoneID)
    {
        ZoneNumber = zoneID;
    }

    /// <summary>
    /// Create a new Zone
    /// </summary>
    /// <param name="zoneID">The ID for this zone</param>
    /// <param name="data">The data from the cache</param>
    public Zone(int zoneID, float[] data)
    {
        ZoneNumber = zoneID;
        PlanningDistrict = (int)data[(int)ZoneCacheColumns.PlanningDistrict];
        Population = (int)data[(int)ZoneCacheColumns.Population];
        WorkGeneral = data[(int)ZoneCacheColumns.WorkGeneral];
        WorkManufacturing = data[(int)ZoneCacheColumns.WorkManufacturing];
        WorkProfessional = data[(int)ZoneCacheColumns.WorkProfessional];
        WorkRetail = data[(int)ZoneCacheColumns.WorkRetail];
        WorkUnknown = data[(int)ZoneCacheColumns.WorkUnknown];
        Employment = data[(int)ZoneCacheColumns.Employment];
        ManufacturingEmployment = data[(int)ZoneCacheColumns.EmploymentManufacturing];
        ProfessionalEmployment = data[(int)ZoneCacheColumns.EmploymentProfessional];
        RetailEmployment = data[(int)ZoneCacheColumns.EmploymentRetail];
        UnknownEmployment = data[(int)ZoneCacheColumns.EmploymentUnknown];
        GeneralEmployment = data[(int)ZoneCacheColumns.EmploymentGeneral];
        X = data[(int)ZoneCacheColumns.X];
        Y = data[(int)ZoneCacheColumns.Y];
        InternalDistance = data[(int)ZoneCacheColumns.InternalDistance];
        RetailActivityLevel = data[(int)ZoneCacheColumns.RetailActivityLevel];
        OtherActivityLevel = data[(int)ZoneCacheColumns.OtherActivityLevel];
        WorkActivityLevel = data[(int)ZoneCacheColumns.WorkActivityLevel];
        ParkingCost = data[(int)ZoneCacheColumns.ParkingCost];
        IntrazonalDensity = data[(int)ZoneCacheColumns.InternalDensity];
        ArterialRoadRatio = data[(int)ZoneCacheColumns.ArtirialPercentage];
    }

    /// <summary>
    /// The order of the columns for the Zone file
    /// </summary>
    private enum ZoneCacheColumns
    {
        //zones.csv
        PlanningDistrict,

        Population,
        WorkGeneral,
        WorkManufacturing,
        WorkProfessional,
        WorkRetail,
        WorkUnknown,
        Employment,
        EmploymentGeneral,
        EmploymentManufacturing,
        EmploymentProfessional,
        EmploymentRetail,
        EmploymentUnknown,

        // Zone-Coordinates
        X,

        Y,
        InternalDistance,

        //fin_activity
        RetailActivityLevel,

        OtherActivityLevel,
        WorkActivityLevel,

        //fin_zone
        InternalDensity,

        ParkingCost,
        ArtirialPercentage
    }

    /// <summary>
    /// ratio of arterial road km to total road km
    /// </summary>
    public float ArterialRoadRatio { get; set; }

    /// <summary>
    /// total zonal employment
    /// </summary>
    public float Employment { get; set; }

    /// <summary>
    /// employment - general office / clerical
    /// </summary>
    public float GeneralEmployment { get; set; }

    /// <summary>
    /// The area that this zone represents
    /// </summary>
    public float InternalArea { get; set; }

    /// <summary>
    /// Average distance within this zone
    /// </summary>
    public float InternalDistance { get; set; }

    /// <summary>
    /// intersection density (# intersections/total road km)
    /// </summary>
    public float IntrazonalDensity { get; set; }

    /// <summary>
    /// employment - manufacturing/construction/trades
    /// </summary>
    public float ManufacturingEmployment { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float OtherActivityLevel { get; set; }

    /// <summary>
    /// How much does parking cost within this zone
    /// </summary>
    public float ParkingCost { get; set; }

    /// <summary>
    /// The planning district the zone is in
    /// </summary>
    public int PlanningDistrict { get; set; }

    /// <summary>
    /// total zonal population for the zone
    /// </summary>
    public int Population { get; set; }

    /// <summary>
    /// employment - professional management technical
    /// </summary>
    public float ProfessionalEmployment { get; set; }

    public int RegionNumber
    {
        get;
        set;
    }

    /// <summary>
    ///
    /// </summary>
    public float RetailActivityLevel { get; set; }

    /// <summary>
    /// employment - retail sales and service
    /// </summary>
    public float RetailEmployment { get; set; }

    public float TotalEmployment
    {
        get
        {
            return GeneralEmployment + ManufacturingEmployment + ProfessionalEmployment + RetailEmployment;
        }

        set
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// employment - We don't know exactly what type it is
    /// </summary>
    public float UnknownEmployment { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkActivityLevel { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkGeneral { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkManufacturing { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkProfessional { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkRetail { get; set; }

    /// <summary>
    ///
    /// </summary>
    public float WorkUnknown { get; set; }

    /// <summary>
    /// X Position of this zone
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y Position of this Zone
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// The ID for the zone
    /// </summary>
    public int ZoneNumber { get; }

    /// <summary>
    /// Returns the distance in meters
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public float GetDistance(IZone x)
    {
        return Math.Abs( x.X - X ) + Math.Abs( x.Y - Y );
    }

    public override int GetHashCode()
    {
        return ZoneNumber;
    }

    public IZone GetWorkBasedLocation(Activity activity, ITashaPerson person)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return ZoneNumber.ToString();
    }
}